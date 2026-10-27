using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
namespace Bdgrz.Compliance.Features.Tenants.Commands;

/// <summary>
///     Records that the slug owner refused the tenant's slug surrender. <paramref name="CommandId" /> is the logical identity of the
///     operation; an exact retry replays the original acceptance and changed intent conflicts.
/// </summary>
[Discriminator("bdgrz.tenant-slug-surrender.reject", 1)]
public sealed record RejectTenantSlugSurrender(Uuid CommandId, Uuid TenantId, string Slug)
    : IRequest, ITenantLifecycleReactionRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
