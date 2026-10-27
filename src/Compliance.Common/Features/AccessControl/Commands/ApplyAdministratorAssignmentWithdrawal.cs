using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>Removes the administrators-team assignment a withdrawal admitted, after the guard withdrew availability.</summary>
[Discriminator("bdgrz.tenant-manager-guard.apply-administrator-assignment-withdrawal", 1)]
public sealed record ApplyAdministratorAssignmentWithdrawal(Uuid CommandId, SourceFactProof<TenantManagerWithdrawalAccepted> Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid MemberId, Uuid MemberEpisode, Uuid AssignmentId)
    : IAdministratorTransitionReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
