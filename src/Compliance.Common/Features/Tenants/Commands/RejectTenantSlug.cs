using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Tenants.Contracts;
namespace Bdgrz.Compliance.Features.Tenants.Commands;

/// <summary>
///     Rejects the tenant's pending slug claim or slug change once the slug owner refused it. <paramref name="CommandId" /> is the logical identity of the
///     operation; an exact retry replays the original acceptance and changed intent conflicts.
/// </summary>
[Discriminator("bdgrz.tenant-slug.reject", 1)]
public sealed record RejectTenantSlug(Uuid CommandId, Uuid TenantId, string Slug)
    : IRequest, ITenantLifecycleReactionRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
