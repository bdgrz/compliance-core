using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>Settles the restoration once the source fact carrying its exact operation was applied, and restores availability only when current eligibility holds.</summary>
[Discriminator("bdgrz.tenant-manager-guard.complete-administrator-restoration", 1)]
public sealed record CompleteAdministratorRestoration(Uuid CommandId, CompleteAdministratorRestorationSourceProof Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid MemberId, Uuid MemberEpisode, ImmutableContentRef EligibilityProofs)
    : IAdministratorTransitionReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
