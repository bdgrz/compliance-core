using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>Suspends or deprovisions the member episode a withdrawal admitted, after the guard withdrew availability.</summary>
[Discriminator("bdgrz.tenant-manager-guard.apply-member-withdrawal", 1)]
public sealed record ApplyMemberWithdrawal(Uuid CommandId, SourceFactProof<TenantManagerWithdrawalAccepted> Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid MemberId, Uuid MemberEpisode, string WithdrawalKind)
    : IAdministratorTransitionReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
