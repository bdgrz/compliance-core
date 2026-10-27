using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Settles the withdrawal once the source fact carrying its exact operation was applied.</summary>
[Discriminator("bdgrz.tenant-manager-guard.complete-administrator-withdrawal", 1)]
public sealed record CompleteAdministratorWithdrawal(Uuid CommandId, CompleteAdministratorWithdrawalSourceProof Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid MemberId, Uuid MemberEpisode)
    : IAdministratorTransitionReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
