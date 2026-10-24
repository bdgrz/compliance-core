using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Reads one tenant from the directory and stamps the tenant's current revision from its owner stream, so a
///     caller can expect exactly the revision it observed when it changes the slug.
/// </summary>
public sealed class GetTenantHandler(ITenantDirectoryReader directory, IAggregateReader reader)
    : IRequestHandler<GetTenant, TenantView>
{
    public async ValueTask<Result<TenantView>> HandleAsync(IRequestContext<GetTenant> context,
        CancellationToken ct)
    {
        var tenant = await directory.GetAsync(context.Request.TenantId, ct).ConfigureAwait(false);
        if (tenant is null)
            return Result<TenantView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The tenant was not found."));
        var owner = await reader.HydrateAsync(new Tenant(context.Request.TenantId), ct).ConfigureAwait(false);
        return Result<TenantView>.Success(tenant with { Revision = owner.Revision });
    }
}
