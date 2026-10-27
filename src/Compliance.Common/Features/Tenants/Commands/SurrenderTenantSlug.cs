using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Commands;

/// <summary>
///     Releases a slug the tenant owns for good on the slug owner. <paramref name="CommandId" /> is the logical identity of the
///     operation; an exact retry replays the original acceptance and changed intent conflicts.
/// </summary>
[Discriminator("bdgrz.tenant-slug.surrender", 1)]
public sealed record SurrenderTenantSlug(Uuid CommandId, Uuid TenantId, string Slug)
    : IRequest, ITenantLifecycleReactionRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
