using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Records the actual staff assignment after the irreversible authorization. <see cref="Source" /> is the
///     <see cref="EngagementPracticeCommitmentsAuthorized" /> fact; finalization can never precede it, preserves the
///     frozen decision instant and recovers forward once the authorization committed.
///     <paramref name="CommitmentProofManifest" /> lists the permanent commitment facts of the staff member and the
///     canonical user.
/// </summary>
[Discriminator("bdgrz.service-engagement-independence.complete-service-engagement-staff-assignment", 1)]
public sealed record CompleteServiceEngagementStaffAssignment(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid EngagementId, long ServiceEpoch, ImmutableContentRef CommitmentProofManifest, Uuid StaffMemberId)
    : IRequest<ReceiverOutcome>, IEngagementAcceptanceReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
