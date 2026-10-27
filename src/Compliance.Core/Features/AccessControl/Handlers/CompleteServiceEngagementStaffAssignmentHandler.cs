using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     Records the actual staff assignment after the irreversible authorization. The revised accepted snapshot and the
///     directory snapshots the assignment relied on are derived from the frozen evidence at the server-frozen decision
///     instant, and the engagement revision advances exactly once. The completion can never precede the authorization
///     and always finishes forward.
/// </summary>
sealed class CompleteServiceEngagementStaffAssignmentHandler(EngagementCompletion completion)
    : IRequestHandler<CompleteServiceEngagementStaffAssignment, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<CompleteServiceEngagementStaffAssignment> context, CancellationToken ct)
    {
        var command = context.Request;
        var loaded = await completion.PrepareAsync(command.Source, command.Intent, command.OperationId,
            command.CommandId, command.Target, command.Generation, command.EngagementId, command.ServiceEpoch,
            command.CommitmentProofManifest, EngagementIndependenceOperationKind.StaffAssignment, ct)
            .ConfigureAwait(false);
        if (loaded.Error is not null)
            return Result<ReceiverOutcome>.Failure(loaded.Error);
        var (owner, operation, intent) = (loaded.Owner!, loaded.Operation!, loaded.Intent!);
        if (intent.StaffMemberId != command.StaffMemberId || intent.EffectiveFrom is not { } effectiveFrom)
            return Result<ReceiverOutcome>.Failure(EngagementAcceptanceVerifier.Permanent(
                "The completion differs from the frozen staff assignment."));
        ServiceEngagementAcceptanceView? current;
        EngagementAcceptanceEvidence evidence;
        try
        {
            current = await completion.ReadAcceptanceAsync(owner, ct).ConfigureAwait(false);
            evidence = await completion.ReadEvidenceAsync(intent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<ReceiverOutcome>.Failure(EngagementAcceptanceVerifier.Transient(
                $"The accepted snapshot cannot be read yet: {exception.Message}"));
        }

        if (current is null || evidence.AssignedStaff is not { } assigned || assigned.StaffMemberId != command.StaffMemberId)
            return Result<ReceiverOutcome>.Failure(EngagementAcceptanceVerifier.Permanent(
                "The staff assignment relies on an accepted engagement and its frozen directory snapshot."));
        var decisionAt = operation.Authorized!.DecisionAt;
        var view = EngagementAcceptanceViews.Assigned(current, assigned, effectiveFrom, intent.Header.OriginalActor,
            decisionAt);
        var assignedContent = await completion.WriteAsync(owner.TenantId, owner.Stream, command.OperationId,
            EngagementAcceptanceEffects.AssignedStaffSchema, assigned, ct).ConfigureAwait(false);
        var partnerContent = await completion.WriteAsync(owner.TenantId, owner.Stream, command.OperationId,
            EngagementAcceptanceEffects.PartnerStaffSchema, evidence.PartnerStaff, ct).ConfigureAwait(false);
        var designationContent = await completion.WriteAsync(owner.TenantId, owner.Stream, command.OperationId,
            EngagementAcceptanceEffects.PartnerDesignationSchema, evidence.PartnerDesignation, ct)
            .ConfigureAwait(false);
        var viewReference = await completion.WriteAsync(owner.TenantId, owner.Stream, command.OperationId,
            EngagementAcceptanceEffects.AcceptanceSchema, view, ct).ConfigureAwait(false);
        var result = await completion.WriteResultAsync(owner.TenantId, owner.Stream, command.OperationId, viewReference, ct)
            .ConfigureAwait(false);
        return await completion.AppendAsync(loaded, command.CommandId,
            nameof(CompleteServiceEngagementStaffAssignment), command.Source, context,
            (fresh, provenance) => fresh.RecordStaffAssignment(provenance, assignedContent, effectiveFrom,
                partnerContent, designationContent, evidence.PartnerDutyContinuityStartRevision,
                intent.Header.OriginalActor, decisionAt, result), ct).ConfigureAwait(false);
    }
}
