using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Assigns a governed workforce person as risk owner. The person need not sign in; a correlated member is
///     snapshotted for owner-based separation of duties only. The command is the logical operation of the risk's own
///     governance owner, so an exact retry replays the original acceptance and a changed intent conflicts.
/// </summary>
public sealed class AssignRiskOwnerHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<AssignRiskOwner>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<AssignRiskOwner> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An owner assignment requires a command id and a non-negative expected revision."));
        var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId, request.ProgramId,
            request.RiskId, ct).ConfigureAwait(false);
        if (!risk.IsSuccess)
            return risk;
        var actor = RiskActor.From(context.Actor, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new RiskGovernance(request.TenantId, request.ProgramId,
            request.RiskId, request.CommandId), ct).ConfigureAwait(false);
        var provenance = FindingContent.Provenance(request.CommandId, "AssignRiskOwner", snapshot.Stream,
            actorReference, (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? ownerContent = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = snapshot.PrepareAssignOwner(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return prepared;
            if (request.PersonId == Uuid.Empty || RiskCommands.InvalidText(request.Rationale))
                return Result.Failure(CommandFailure.InvalidContent(
                    "A risk owner requires a workforce person and a rationale of at most 4000 characters.")
                    .ToRequestError());
            var (exists, memberId) = await RiskOwnerResolution.ResolveAsync(reader, request.TenantId,
                request.PersonId, ct).ConfigureAwait(false);
            if (!exists)
                return Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "The risk owner must be a recorded workforce person in this organization."));
            if (snapshot.Owner is { } current)
            {
                var existing = await FindingContent.ReadAsync<RiskOwnerView>(content, request.TenantId,
                    current.OwnerContent, ct).ConfigureAwait(false);
                if (existing.PersonId == request.PersonId && existing.CorrelatedMemberId == memberId)
                    return Result.Failure(CommandFailure.StateConflict("The person already owns the risk.")
                        .ToRequestError());
            }

            ownerContent = await FindingContent.WriteAsync(content, request.TenantId, RiskContent.OwnerSchema,
                new RiskOwnerView(request.PersonId, memberId, request.Rationale.Trim(), actorReference, now), ct)
                .ConfigureAwait(false);
            (response, result) = await RiskCommands.FreezeUnitAsync(content, request.TenantId, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<RiskGovernance>(binding,
            () => new RiskGovernance(request.TenantId, request.ProgramId, request.RiskId, request.CommandId),
            governance => governance.Acceptance,
            governance => FindingCommands.Prepared(governance.PrepareAssignOwner(request.ExpectedRevision), response),
            (RiskGovernance governance, ImmutableContentRef frozen) =>
                governance.AssignOwner(provenance, ownerContent!, frozen, result!)), context, ct).ConfigureAwait(false);
        return await RiskCommands.UnitAsync(content, request.TenantId, outcome, ct).ConfigureAwait(false);
    }
}
