using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>Applies the original administrators-team removal the guard admitted as neutral.</summary>
[Discriminator("bdgrz.tenant-manager-guard.apply-neutral-administrator-assignment-mutation", 1)]
public sealed record ApplyNeutralAdministratorAssignmentMutation(Uuid CommandId, SourceFactProof<TenantManagerSourceMutationAdmitted> Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid MemberId, Uuid MemberEpisode, Uuid AssignmentId)
    : IAdministratorTransitionReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
