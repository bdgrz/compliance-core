using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Requests a tenant slug change. <paramref name="CommandId" /> is the logical identity of the change and an exact
///     retry replays the original acceptance. <paramref name="ExpectedRevision" /> is the tenant revision the caller
///     observed; a negative value is unconstrained and reserved for system callers.
/// </summary>
[Discriminator("bdgrz.tenant.change-slug", 1)]
public sealed record ChangeTenantSlug(Uuid CommandId, Uuid TenantId, string Slug, long ExpectedRevision)
    : IRequest, ICallable, IPlatformOperatorOrTenantAdminRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
