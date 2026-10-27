using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Removes the administrators-team assignment a withdrawal admitted, after the guard withdrew availability.</summary>
[Discriminator("bdgrz.tenant-manager-guard.apply-administrator-assignment-withdrawal", 1)]
public sealed record ApplyAdministratorAssignmentWithdrawal(Uuid CommandId, SourceFactProof<TenantManagerWithdrawalAccepted> Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid MemberId, Uuid MemberEpisode, Uuid AssignmentId)
    : IAdministratorTransitionReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
