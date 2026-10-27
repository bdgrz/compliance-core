using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Applies the original suspend, deprovision or reinstate of a member episode the guard admitted as neutral.</summary>
[Discriminator("bdgrz.tenant-manager-guard.apply-neutral-member-source-mutation", 1)]
public sealed record ApplyNeutralMemberSourceMutation(Uuid CommandId, SourceFactProof<TenantManagerSourceMutationAdmitted> Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid MemberId, Uuid MemberEpisode, string MutationKind)
    : IAdministratorTransitionReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
