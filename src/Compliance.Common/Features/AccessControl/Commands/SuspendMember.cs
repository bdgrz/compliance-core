using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Suspends a member episode through the tenant manager guard. <paramref name="CommandId" /> is the logical
///     identity of the suspension and an exact retry replays the original acceptance.
///     <paramref name="MemberEpisodeId" /> is the member episode being suspended and
///     <paramref name="ExpectedMemberRevision" /> the member revision the caller observed; a negative revision is
///     unconstrained and reserved for system callers.
/// </summary>
[Discriminator("bdgrz.tenant.member.suspend", 1)]
public sealed record SuspendMember(Uuid CommandId, Uuid TenantId, Uuid UserId, string Reason, Uuid MemberEpisodeId,
    long ExpectedMemberRevision)
    : IRequest<AdministratorTransitionResponse>, IClientRbacMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
