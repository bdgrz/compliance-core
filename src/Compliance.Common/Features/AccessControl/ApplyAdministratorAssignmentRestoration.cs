using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Assigns the administrators team the restoration or registration admitted, waiting behind any earlier unfinished withdrawal.</summary>
[Discriminator("bdgrz.tenant-manager-guard.apply-administrator-assignment-restoration", 1)]
public sealed record ApplyAdministratorAssignmentRestoration(Uuid CommandId, SourceFactProof<TenantManagerRestorationAccepted> Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid MemberId, Uuid MemberEpisodeId, Uuid AssignmentId, ulong ExpectedAssignmentRevision)
    : IAdministratorTransitionReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
