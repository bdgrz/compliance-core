using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
namespace Bdgrz.Compliance.Features.Tenants.Commands;

/// <summary>
///     Asks the tenant to surrender its confirmed slug. <paramref name="CommandId" /> is the logical identity of the
///     operation; an exact retry replays the original acceptance and changed intent conflicts.
/// </summary>
[Discriminator("bdgrz.tenant-slug-surrender.request", 1)]
public sealed record RequestTenantSlugSurrender(Uuid CommandId, Uuid TenantId, string Slug)
    : IRequest, IPlatformOperatorRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
