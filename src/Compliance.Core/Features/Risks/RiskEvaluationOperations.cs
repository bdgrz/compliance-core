using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Independence;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Risks;
/// <summary>Native first-owner acceptance; exact retries are resolved before mutable source selection.</summary>
public sealed class RiskEvaluationOperations(IAggregateReader reader, RiskEvaluationReader evaluations,
    NativeOperationExecutor operations, IImmutableContentStore content, TimeProvider clock, RiskMethodReader methodSources)
{
    public async ValueTask<Result<RiskAssessmentView>> RecordAssessmentAsync(IRequestContext<RecordRiskAssessment> context,
        RiskGovernanceReader governanceReader, CancellationToken ct)
    {
        var request = context.Request;
        var actor = RiskActor.From(context.Actor, request.TenantId);
        return await ExecuteAsync<RecordRiskAssessment, RiskAssessmentView>(context, request.TenantId, request.ProgramId, request.RiskId,
            request.CommandId, request.ExpectedRevision, nameof(RecordRiskAssessment), actor,
            async (snapshot, at, token) =>
            {
                var found = await RequireRiskAsync(request.TenantId, request.ProgramId, request.RiskId, token).ConfigureAwait(false);
                if (!found.IsSuccess)
                    return found;
                var methods = await methodSources.ReadAsync(request.TenantId, request.ProgramId, token).ConfigureAwait(false);
                if (methods.GetVersion(request.MethodVersion) is not { } method)
                    return Result.Failure(new RequestError(RequestErrorKind.Validation, "The risk method version was not found."));
                var governance = await governanceReader.ReadRiskAsync(request.TenantId, request.ProgramId, request.RiskId, token).ConfigureAwait(false);
                return Outcome(snapshot.Rules.RecordAssessment(request.ProgramId, request.ExpectedRevision, request.CommandId, method,
                    request.Phase, request.Likelihood, request.Impact, request.Rationale, actor.MemberId, actor.Display, at,
                    governance.HasAcceptedControlTreatment(request.RiskId)));
            }, ct).ConfigureAwait(false);
    }
    public async ValueTask<Result> ChooseTreatmentAsync(IRequestContext<ChooseRiskTreatment> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = RiskActor.From(context.Actor, request.TenantId);
        var result = await ExecuteAsync<ChooseRiskTreatment, UnitResult>(context, request.TenantId, request.ProgramId, request.RiskId,
            request.CommandId, request.ExpectedRevision, nameof(ChooseRiskTreatment), actor,
            async (snapshot, at, token) =>
            {
                var found = await RequireRiskAsync(request.TenantId, request.ProgramId, request.RiskId, token).ConfigureAwait(false);
                return !found.IsSuccess ? found : Outcome(snapshot.Rules.ChooseTreatment(request.ProgramId, request.ExpectedRevision,
                    request.Kind, request.Rationale, actor.MemberId, actor.Display, at, request.CommandId, AuthoredIntentDigest.Compute(request)));
            }, ct).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
    }
    public async ValueTask<Result<RiskAcceptanceView>> AcceptAsync(IRequestContext<AcceptRisk> context,
        IAccessGrantPermissionAuthorizer permissions, RiskGovernanceReader governanceReader,
        ISeparationOfDutiesWaiverProofResolver waivers, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result<RiskAcceptanceView>.Failure(new RequestError(RequestErrorKind.Forbidden, "Risk acceptance requires a personal HTTP invocation."));
        var request = context.Request;
        var actor = RiskActor.From(context.Actor, request.TenantId);
        var permission = RbacPermissions.RiskAcceptanceFor(request.ApproverAuthority);
        var held = permission is not null && await permissions.IsAllowedAsync(request.TenantId, actor.UserId, actor.MemberId,
            request.ProgramId, permission, ct).ConfigureAwait(false);
        if (!held)
            return Result<RiskAcceptanceView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The current actor does not hold the requested risk acceptance authority."));
        return await ExecuteAsync<AcceptRisk, RiskAcceptanceView>(context, request.TenantId, request.ProgramId, request.RiskId,
            request.CommandId, request.ExpectedRevision, nameof(AcceptRisk), actor,
            async (snapshot, at, token) =>
            {
                var found = await RequireRiskAsync(request.TenantId, request.ProgramId, request.RiskId, token).ConfigureAwait(false);
                if (!found.IsSuccess)
                    return found;
                if (snapshot.FindAssessment(request.ResidualAssessmentId) is not { Phase: RiskEvaluation.Residual } residual)
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict, "Accept the current residual assessment."));
                var methods = await methodSources.ReadAsync(request.TenantId, request.ProgramId, token).ConfigureAwait(false);
                var method = methods.Find(residual.MethodVersionId) ?? throw new InvalidOperationException("A residual assessment must cite a published method.");
                var governance = await governanceReader.ReadRiskAsync(request.TenantId, request.ProgramId, request.RiskId, token).ConfigureAwait(false);
                Uuid? ownerMemberId = null;
                if (governance.OwnerOf(request.RiskId) is { } owner)
                {
                    var (_, currentMemberId) = await RiskOwnerResolution.ResolveAsync(reader, request.TenantId, owner.PersonId, token).ConfigureAwait(false);
                    ownerMemberId = owner.CorrelatedMemberId == actor.MemberId || currentMemberId == actor.MemberId
                        ? actor.MemberId : currentMemberId ?? owner.CorrelatedMemberId;
                }
                SeparationOfDutiesWaiverProof? waiver = null;
                if (request.SeparationOfDutiesWaiverId is { } waiverId)
                {
                    var scope = new SeparationOfDutiesWaiverProofScope(request.TenantId, waiverId, actor.MemberId, actor.MemberId,
                        SeparationOfDutiesActions.Approve, SeparationOfDutiesRecordTypes.Risk, request.RiskId, request.ResidualAssessmentId, request.ExpectedRevision);
                    var resolved = await waivers.ResolveAsync(scope, at, token).ConfigureAwait(false);
                    if (!resolved.IsSuccess)
                        return Result.Failure(resolved.Error!);
                    waiver = resolved.Value;
                }
                return Outcome(snapshot.Rules.Accept(request.ProgramId, request.ExpectedRevision, request.CommandId, request.ResidualAssessmentId,
                    method, request.ApproverAuthority, held, request.ExpiresAt, request.Rationale, actor.MemberId, actor.Display, at, ownerMemberId, waiver));
            }, ct).ConfigureAwait(false);
    }
    async ValueTask<Result<TOut>> ExecuteAsync<TRequest, TOut>(IRequestContext<TRequest> context, Uuid tenant, Uuid program, Uuid risk,
        Uuid command, long expected, string kind, RiskActor actor, Func<RiskEvaluationReadSnapshot, DateTimeOffset, CancellationToken, ValueTask<Result>> prepare,
        CancellationToken ct) where TRequest : IRequestBase where TOut : notnull
    {
        if (tenant == Uuid.Empty || program == Uuid.Empty || risk == Uuid.Empty || command == Uuid.Empty || expected < 0 || actor.MemberId == Uuid.Empty)
            return Result<TOut>.Failure(new RequestError(RequestErrorKind.Validation, "An evaluation operation requires its typed identity and exact non-negative revision."));
        var provenance = OwnerProvenance.For(new RiskEvaluation(tenant, risk), command, kind,
            ActorReference.ForMember(actor.MemberId, actor.Display), risk.ToString(), unchecked((ulong)expected), AuthoredIntentDigest.Compute(context.Request), clock.GetUtcNow())
            with
        { FirstOwner = new RiskEvaluation(tenant, risk).Stream };
        try
        {
            FrozenRiskEvaluationMutation? frozen = null;
            Bdgrz.Compliance.Features.Proofs.Models.ImmutableContentRef? result = null;
            long sequence = 0;
            ulong physical = 0;
            var accepted = await operations.ExecuteAsync(new NativeOperation<RiskEvaluation>(AcceptedOperationBinding.Of(provenance),
                () => new RiskEvaluation(tenant, risk, command), owner => owner.Acceptance,
                async (owner, token) =>
                {
                    if (owner.Revision != expected || owner.Header is not null && owner.ProgramId != program)
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict, "The evaluation requires its original expected revision and exact program."));
                    var snapshot = await evaluations.ReadAsync(owner, program, token).ConfigureAwait(false);
                    var before = snapshot.ToView();
                    var valid = await prepare(snapshot, provenance.AcceptedAt, token).ConfigureAwait(false);
                    if (!valid.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(valid.Error!);
                    frozen = await RiskEvaluationContent.FreezeAsync(content, before, snapshot.Rules, kind, token).ConfigureAwait(false);
                    var response = frozen.Mutation switch
                    {
                        RiskAssessmentRecordedBody assessment => await NativeContent.FreezeAsync(content, tenant, RiskEvaluationContent.AssessmentResultSchema, assessment.Assessment, token).ConfigureAwait(false),
                        RiskAcceptedBody acceptance => await NativeContent.FreezeAsync(content, tenant, RiskEvaluationContent.AcceptanceResultSchema, acceptance.Acceptance, token).ConfigureAwait(false),
                        _ => await NativeContent.FreezeUnitAsync(content, tenant, token).ConfigureAwait(false),
                    };
                    result = response.Result;
                    sequence = owner.NativeSequence;
                    physical = owner.CommittedStreamPosition;
                    return Result<FirstResponseDescriptor>.Success(response.Response);
                }, (owner, first) => owner.Append(RiskEvaluationContent.Fact(frozen!, sequence + 1, provenance, first, result!), frozen!, sequence, physical)), context, ct).ConfigureAwait(false);
            if (accepted.IsSuccess)
            {
                var descriptor = accepted.Value switch
                {
                    AcceptedAppendResult.Committed committed => committed.Response,
                    AcceptedAppendResult.Replayed replayed => replayed.Response,
                    _ => null,
                };
                var schema = kind == nameof(RecordRiskAssessment) ? RiskEvaluationContent.AssessmentResultSchema :
                    kind == nameof(AcceptRisk) ? RiskEvaluationContent.AcceptanceResultSchema : UnitResult.Schema;
                if (descriptor is not null && (descriptor.HttpStatus != 200 || descriptor.ContentType != ImmutableContentStoreExtensions.JsonMediaType ||
                    descriptor.ResultSchema != schema || descriptor.Body is not StandardResponseBody body || body.Content.Schema != schema))
                    throw new ImmutableContentIntegrityException("The evaluation original response has another exact result schema or HTTP representation.");
            }
            return await NativeContent.ResponseAsync<TOut>(content, tenant, accepted, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<TOut>.Failure(RiskEvaluationReadFailure.Of(error));
        }
    }
    async ValueTask<Result> RequireRiskAsync(Uuid tenant, Uuid program, Uuid risk, CancellationToken ct)
    {
        var draft = await reader.HydrateAsync(new RiskDraft(tenant, risk), ct).ConfigureAwait(false);
        return draft.IsCreated && draft.ProgramId == program ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.NotFound, "The risk was not found."));
    }
    static Result Outcome(Bdgrz.Compliance.Features.Versioning.CommandFailure? failure) => failure is null ? Result.Success : Result.Failure(failure.ToRequestError());
}
