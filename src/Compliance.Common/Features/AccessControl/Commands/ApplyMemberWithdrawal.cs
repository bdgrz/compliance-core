using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>Suspends or deprovisions the member episode a withdrawal admitted, after the guard withdrew availability.</summary>
[Discriminator("bdgrz.tenant-manager-guard.apply-member-withdrawal", 1)]
public sealed record ApplyMemberWithdrawal(Uuid CommandId, SourceFactProof<TenantManagerWithdrawalAccepted> Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid MemberId, Uuid MemberEpisode, string WithdrawalKind)
    : IAdministratorTransitionReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
