using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class RiskDraftOperations(NativeOperationExecutor operations, RiskDraftReader drafts,
    IAggregateReader reader, IImmutableContentStore content, TimeProvider clock)
{
    public const string RegistrationSchema = "risk-registration.v1";
    public ValueTask<Result<RiskRegistration>> CreateAsync(IRequestContext<CreateRiskDraft> context, CancellationToken ct) =>
        ExecuteAsync<CreateRiskDraft, RiskRegistration>(context, context.Request.TenantId, context.Request.ProgramId,
            context.Request.RiskId, context.Request.RiskId, nameof(CreateRiskDraft), null, context.Request, ct);
    public async ValueTask<Result> ReviseAsync(IRequestContext<ReviseRiskDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        var result = await ExecuteAsync<ReviseRiskDraft, UnitResult>(context, request.TenantId, request.ProgramId, request.RiskId,
            request.CommandId, nameof(ReviseRiskDraft), request.ExpectedRevision, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
    }
    async ValueTask<Result<TOut>> ExecuteAsync<TRequest, TOut>(IRequestContext<TRequest> context, Uuid tenant,
        Uuid program, Uuid risk, Uuid command, string kind, long? expected, object request, CancellationToken ct)
        where TRequest : IRequestBase where TOut : notnull
    {
        if (tenant == Uuid.Empty || program == Uuid.Empty || risk == Uuid.Empty || command == Uuid.Empty || expected is < 1)
            return Fail<TOut>(RequestErrorKind.Validation, "A risk draft operation requires exact identities and a positive revision.");
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user) || RequestActor.IsSystem(context.Actor))
            return Fail<TOut>(RequestErrorKind.Forbidden, "A risk draft operation requires a personal member actor.");
        if (request is CreateRiskDraft create && risk != RiskDraft.IdFor(tenant, program, create.Identifier))
            return Fail<TOut>(RequestErrorKind.Validation, "The authored risk id must match its normalized tenant/program identifier identity.");
        var snapshot = new RiskDraft(tenant, risk);
        var operation = OwnerProvenance.For(snapshot, command, kind,
            ActorReference.ForMember(RbacIds.Member(tenant, user), UserIdentityClaims.BdgrzDisplay(context.Actor, user)),
            risk.ToString(), expected is { } gate ? unchecked((ulong)gate) : null, AuthoredIntentDigest.Compute(context.Request), clock.GetUtcNow())
            with
        { FirstOwner = snapshot.Stream };
        RiskDraftRevisionView? prepared = null;
        ImmutableContentRef? revisionContent = null;
        ImmutableContentRef? resultContent = null;
        var sequence = 0L;
        var position = 0UL;
        try
        {
            var accepted = await operations.ExecuteAsync(new NativeOperation<RiskDraft>(AcceptedOperationBinding.Of(operation),
                () => new RiskDraft(tenant, risk, command), owner => owner.Acceptance,
                async (owner, token) =>
                {
                    try
                    {
                        RiskDraftRules rules;
                        if (request is CreateRiskDraft creation)
                        {
                            if (owner.IsCreated)
                                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                                    "The risk identity already retains another original creation."));
                            var programSource = await reader.HydrateAsync(new ComplianceProgram(tenant, program), token).ConfigureAwait(false);
                            if (!programSource.IsCreated)
                                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound, "The program was not found."));
                            rules = new RiskDraftRules(tenant, risk);
                            var created = rules.Create(creation, RbacIds.Member(tenant, user), operation.Actor.Display, operation.AcceptedAt);
                            if (!created.IsSuccess)
                                return Result<FirstResponseDescriptor>.Failure(created.Error!);
                        }
                        else
                        {
                            var current = await drafts.ReadAsync(owner, program, token).ConfigureAwait(false);
                            if (!current.IsCreated)
                                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound, "The risk draft was not found."));
                            rules = current.Rules;
                            var revision = (ReviseRiskDraft)request;
                            var failure = rules.Revise(program, revision.ExpectedRevision,
                                new RiskDraftContent(revision.Title, revision.Scenario, revision.PotentialEffect, revision.SourceNote),
                                RbacIds.Member(tenant, user), operation.Actor.Display, operation.AcceptedAt);
                            if (failure is not null)
                                return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
                        }
                        prepared = rules.Current!;
                        revisionContent = await NativeContent.WriteAsync(content, tenant, RiskDraftFactContent.Schema, prepared, token).ConfigureAwait(false);
                        var frozen = request is CreateRiskDraft
                            ? await NativeContent.FreezeAsync(content, tenant, RegistrationSchema,
                                new RiskRegistration(risk, prepared.Identifier, 1), token).ConfigureAwait(false)
                            : await NativeContent.FreezeUnitAsync(content, tenant, token).ConfigureAwait(false);
                        resultContent = frozen.Result;
                        sequence = owner.NativeSequence;
                        position = owner.CommittedStreamPosition;
                        return Result<FirstResponseDescriptor>.Success(frozen.Response);
                    }
                    catch (ImmutableContentIntegrityException failure)
                    {
                        return Result<FirstResponseDescriptor>.Failure(RiskDraftReadFailure.Of(failure));
                    }
                }, (owner, first) => owner.Append(request is CreateRiskDraft
                    ? new RiskDraftCreated(RiskDraftFactContent.Header(prepared!), revisionContent!, sequence + 1, operation, first, resultContent)
                    : new RiskDraftRevised(RiskDraftFactContent.Header(prepared!), revisionContent!, sequence + 1, operation, first, resultContent),
                    prepared!, sequence, position)), context, ct).ConfigureAwait(false);
            if (!accepted.IsSuccess)
                return Result<TOut>.Failure(accepted.Error!);
            var descriptor = accepted.Value switch
            {
                AcceptedAppendResult.Committed committed => committed.Response,
                AcceptedAppendResult.Replayed replayed => replayed.Response,
                _ => null,
            };
            if (descriptor is null)
                return Result<TOut>.Failure(AcceptedAppendResult.ConflictError);
            var schema = request is CreateRiskDraft ? RegistrationSchema : UnitResult.Schema;
            if (descriptor.HttpStatus != 200 || descriptor.ResultSchema != schema || descriptor.Body is not StandardResponseBody body || body.Content.Schema != schema)
                throw new ImmutableContentIntegrityException("The risk draft original response has another status, schema or body kind.");
            return Result<TOut>.Success(await content.ReadJsonAsync<TOut>(tenant.ToString(), body.Content, ct).ConfigureAwait(false));
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<TOut>.Failure(RiskDraftReadFailure.Of(failure));
        }
    }
    static Result<T> Fail<T>(RequestErrorKind kind, string message) where T : notnull => Result<T>.Failure(new RequestError(kind, message));
}
