using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Reinstates a suspended member episode through the tenant manager guard. <paramref name="CommandId" /> is the
///     logical identity of the reinstatement and an exact retry replays the original acceptance.
///     <paramref name="MemberEpisodeId" /> is the suspended episode and <paramref name="ExpectedMemberRevision" /> the
///     member revision the caller observed; a negative revision is unconstrained and reserved for system callers.
/// </summary>
[Discriminator("bdgrz.tenant.member.reinstate", 1)]
public sealed record ReinstateMember(Uuid CommandId, Uuid TenantId, Uuid UserId, Uuid MemberEpisodeId,
    long ExpectedMemberRevision)
    : IRequest<AdministratorTransitionResponse>, IClientRbacMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
