using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Projections;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Independence.Contracts;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Commands;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Controls.Services;

/// <summary>Native first-owner acceptance over exact immutable local authority and the physical preparation cut.</summary>
public sealed partial class ControlOperations(IAggregateReader reader, ControlReader controls,
    NativeOperationExecutor operations, IImmutableContentStore content, TimeProvider clock,
    ISeparationOfDutiesWaiverProofResolver? waivers = null)
{
    public IAggregateReader Sources => reader;
    public TimeProvider Clock => clock;

    public async ValueTask<Result<TOut>> ExecuteAsync<TRequest, TOut>(IRequestContext<TRequest> context,
        Uuid tenantId, Uuid? programId, Uuid controlId, Uuid commandId, string kind, long? expectedRevision,
        Func<ControlReadSnapshot, ControlWriteActor, DateTimeOffset, CancellationToken, ValueTask<Result>> validate,
        Func<ControlRules, ControlWriteActor, DateTimeOffset, Result> apply,
        Func<ControlRules, ControlWriteActor, DateTimeOffset, IControlMutationContent?>? receipt,
        Func<IReadOnlyList<SeparationOfDutiesWaiverProof>>? authorizedWaivers, CancellationToken ct,
        string? authoredDigest = null)
        where TRequest : IRequestBase where TOut : notnull
    {
        if (tenantId == Uuid.Empty || controlId == Uuid.Empty || commandId == Uuid.Empty || programId == Uuid.Empty ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId) || RequestActor.IsSystem(context.Actor))
            return Result<TOut>.Failure(new RequestError(RequestErrorKind.Validation,
                "A Control operation requires exact identities and a personal member actor."));
        var member = RbacIds.Member(tenantId, userId);
        var actor = new ControlWriteActor(member, UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
        var provenance = OwnerProvenance.For(new ControlDraft(tenantId, controlId), commandId, kind,
            ActorReference.ForMember(member, member.ToString()), controlId.ToString(),
            expectedRevision is { } expected ? unchecked((ulong)expected) : null,
            authoredDigest ?? AuthoredIntentDigest.Compute(context.Request), clock.GetUtcNow());
        try
        {
            var preparedSequence = 0L;
            var preparedPhysical = 0UL;
            FrozenControlMutation? frozen = null;
            ControlMutationContent? mutation = null;
            ControlAuthorityContent? beforeAuthority = null;
            ControlAuthorityContent? afterAuthority = null;
            ImmutableContentRef? resultContent = null;
            var accepted = await operations.ExecuteAsync(new NativeOperation<ControlDraft>(
                AcceptedOperationBinding.Of(provenance), () => new ControlDraft(tenantId, controlId, commandId),
                owner => owner.Acceptance,
                async (owner, token) =>
                {
                    var scope = programId ?? owner.ProgramId;
                    var valid = owner.Prepare(scope, expectedRevision, kind == nameof(CreateControlDraft));
                    if (!valid.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(valid.Error!);
                    var snapshot = await controls.ReadAsync(owner, scope, token).ConfigureAwait(false);
                    valid = await validate(snapshot, actor, provenance.AcceptedAt, token).ConfigureAwait(false);
                    if (!valid.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(valid.Error!);
                    var rules = snapshot.Rules;
                    beforeAuthority = owner.Header is { } header ? rules.Authority(header) : null;
                    valid = apply(rules, actor, provenance.AcceptedAt);
                    if (!valid.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(valid.Error!);
                    var changes = rules.PendingChanges.ToArray();
                    mutation = new ControlMutationContent(changes, authorizedWaivers?.Invoke(),
                        changes.Length == 0 ? receipt?.Invoke(rules, actor, provenance.AcceptedAt) : null);
                    frozen = await ControlContent.FreezeAsync(content, rules, mutation, token).ConfigureAwait(false);
                    afterAuthority = rules.Authority(frozen.Header);
                    var response = await ControlResponses.FreezeAsync(content, rules, kind, token).ConfigureAwait(false);
                    resultContent = await content.WriteJsonAsync(tenantId.ToString(), Uuid.CreateVersion4(),
                        "mutation-result-content.v1", new MutationResultContent(response.ResultSchema, response.Body), token)
                        .ConfigureAwait(false);
                    preparedSequence = owner.NativeSequence;
                    preparedPhysical = owner.CommittedStreamPosition;
                    return Result<FirstResponseDescriptor>.Success(response);
                },
                (owner, first) => owner.Append(ControlFactFactory.Create(kind, frozen!.Header,
                    preparedSequence + 1, provenance, frozen.Change, frozen.Authority, first, resultContent!, mutation!),
                    preparedSequence, preparedPhysical, mutation, beforeAuthority, afterAuthority)), context, ct).ConfigureAwait(false);
            if (!accepted.IsSuccess)
                return Result<TOut>.Failure(accepted.Error!);
            var descriptor = accepted.Value switch
            {
                AcceptedAppendResult.Committed committed => committed.Response,
                AcceptedAppendResult.Replayed replayed => replayed.Response,
                _ => null,
            };
            if (descriptor is null)
                return Result<TOut>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The Control logical operation is already bound to another actor, revision or authored intent."));
            var schema = kind switch
            {
                nameof(CreateControlDraft) => ControlResponses.Registration,
                nameof(ProposeControlSuccessor) => ControlResponses.Successor,
                nameof(ProposeControlRetirement) => ControlResponses.Retirement,
                _ => UnitResult.Schema,
            };
            if (descriptor.ResultSchema != schema || descriptor.Body is not StandardResponseBody body || body.Content.Schema != schema)
                throw new ImmutableContentIntegrityException("The Control original response has another result schema.");
            var value = await content.ReadJsonAsync<TOut>(tenantId.ToString(), body.Content, ct).ConfigureAwait(false);
            return Result<TOut>.Success(value);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<TOut>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The Control's immutable content cannot be verified.",
                isTransient: exception is ImmutableContentUnavailableException));
        }
    }

    public async ValueTask<Result> ExecuteUnitAsync<TRequest>(IRequestContext<TRequest> context,
        Uuid tenantId, Uuid? programId, Uuid controlId, Uuid commandId, string kind, long? expectedRevision,
        Func<ControlReadSnapshot, ControlWriteActor, DateTimeOffset, CancellationToken, ValueTask<Result>> validate,
        Func<ControlRules, ControlWriteActor, DateTimeOffset, Result> apply,
        Func<ControlRules, ControlWriteActor, DateTimeOffset, IControlMutationContent?>? receipt,
        Func<IReadOnlyList<SeparationOfDutiesWaiverProof>>? authorizedWaivers, CancellationToken ct) where TRequest : IRequestBase
    {
        var result = await ExecuteAsync<TRequest, UnitResult>(context, tenantId, programId, controlId, commandId, kind,
            expectedRevision, validate, apply, receipt, authorizedWaivers, ct).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
    }

    public async ValueTask<Result<SeparationOfDutiesWaiverProof?>> ResolveWaiverAsync(Uuid tenantId, Uuid? waiverId,
        Uuid memberId, Uuid targetId, Uuid targetVersionId, long revision, string action, DateTimeOffset at,
        CancellationToken ct)
    {
        if (waiverId is null)
            return Result<SeparationOfDutiesWaiverProof?>.Success(null);
        if (waivers is null)
            return Result<SeparationOfDutiesWaiverProof?>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact waiver proof resolver is not available.", true));
        var resolved = await waivers.ResolveAsync(new SeparationOfDutiesWaiverProofScope(tenantId, waiverId.Value,
            memberId, memberId, action, SeparationOfDutiesRecordTypes.Control, targetId, targetVersionId, revision), at, ct)
            .ConfigureAwait(false);
        return resolved.IsSuccess ? Result<SeparationOfDutiesWaiverProof?>.Success(resolved.Value)
            : Result<SeparationOfDutiesWaiverProof?>.Failure(resolved.Error!);
    }

    public static Result Outcome(CommandFailure? failure) => failure is null ? Result.Success : Result.Failure(failure.ToRequestError());
    public static ValueTask<Result> Ready() => ValueTask.FromResult(Result.Success);
}
