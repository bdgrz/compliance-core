using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>Reinstates the member episode a restoration admitted, waiting behind any earlier unfinished withdrawal.</summary>
[Discriminator("bdgrz.tenant-manager-guard.apply-member-restoration", 1)]
public sealed record ApplyMemberRestoration(Uuid CommandId, SourceFactProof<TenantManagerRestorationAccepted> Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid MemberId, Uuid MemberEpisode)
    : IAdministratorTransitionReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
