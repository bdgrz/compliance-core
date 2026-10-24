using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Revises an open treatment action. Its accountable member must be active and every named evidence request must
///     belong to the program and remain uncancelled. Only unfinished work revises; the command is the logical operation,
///     so a retry replays and a changed intent conflicts.
/// </summary>
public sealed class ReviseRiskTreatmentActionHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<ReviseRiskTreatmentAction>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseRiskTreatmentAction> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ActionId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A revision requires a command id, an action id and a non-negative expected revision."));
        var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId, request.ProgramId,
            request.RiskId, ct).ConfigureAwait(false);
        if (!risk.IsSuccess)
            return risk;
        var actor = RiskActor.From(context.Actor, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new RiskTreatmentAction(request.TenantId, request.ProgramId,
            request.RiskId, request.ActionId, request.CommandId), ct).ConfigureAwait(false);
        var provenance = FindingContent.Provenance(request.CommandId, "ReviseRiskTreatmentAction", snapshot.Stream,
            actorReference, (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? evidenceManifest = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = snapshot.PrepareRevise(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return prepared;
            var invalid = RiskCommands.ValidateAction(request.Title, request.TargetState, request.ExpectedEvidence,
                request.DueOn, request.AccountableMemberId, now);
            if (invalid is not null || request.EvidenceRequestIds?.Any(static id => id == Uuid.Empty) is true)
                return Result.Failure((invalid ?? CommandFailure.InvalidContent(
                    "A treatment action edit requires valid evidence references.")).ToRequestError());
            if (!await FindingCommands.IsActiveMemberAsync(reader, request.TenantId, request.AccountableMemberId, ct)
                    .ConfigureAwait(false))
                return Result.Failure(FindingCommands.InactiveOwner());
            var evidence = RiskCommands.Distinct(request.EvidenceRequestIds);
            if (await RiskCommands.RequireCitableEvidenceAsync(reader, request.TenantId, request.ProgramId, evidence, ct)
                    .ConfigureAwait(false) is { } unciteable)
                return Result.Failure(unciteable);
            evidenceManifest = await FindingContent.WriteAsync<IReadOnlyList<Uuid>>(content, request.TenantId,
                RiskContent.RevisedEvidenceSchema, evidence, ct).ConfigureAwait(false);
            (response, result) = await RiskCommands.FreezeUnitAsync(content, request.TenantId, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<RiskTreatmentAction>(binding,
            () => new RiskTreatmentAction(request.TenantId, request.ProgramId, request.RiskId, request.ActionId,
                request.CommandId),
            action => action.Acceptance,
            action => FindingCommands.Prepared(action.PrepareRevise(request.ExpectedRevision), response),
            (RiskTreatmentAction action, ImmutableContentRef frozen) => action.Revise(provenance, request.Title.Trim(),
                request.TargetState.Trim(), request.ExpectedEvidence.Trim(), request.DueOn,
                request.AccountableMemberId, evidenceManifest!, actorReference, now, frozen, result!)), context, ct)
            .ConfigureAwait(false);
        return await RiskCommands.UnitAsync(content, request.TenantId, outcome, ct).ConfigureAwait(false);
    }
}
