using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Tenants.Contracts;
namespace Bdgrz.Compliance.Features.Tenants.Commands;

/// <summary>
///     Invites a person to a tenant as a platform operator. <paramref name="InvitationId" /> is the client-chosen
///     identity of the invitation: it selects the invitation owner, and an exact retry replays the original
///     acceptance while a changed recipient or purpose under the same id conflicts.
/// </summary>
[Discriminator("bdgrz.tenant-member.invite", 1)]
public sealed record InviteTenantMember(Uuid InvitationId, Uuid TenantId, string EmailAddress, string Affiliation,
    bool Administrator = false) : IRequest, ICallable, IPlatformOperatorRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => InvitationId;
}
