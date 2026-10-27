using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Queries;
namespace Bdgrz.Compliance.Features.Tenants.Authorizers;

sealed class GetTenantProvisioningStatusAuthorizer(IAggregateReader reader)
    : IRequestAuthorizer<GetTenantProvisioningStatus>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<GetTenantProvisioningStatus> context,
        CancellationToken ct)
    {
        if (RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Tenant access requires a Bdgrz user identity."));

        var tenant = await reader.HydrateAsync(new Tenant(context.Request.TenantId), ct).ConfigureAwait(false);
        return tenant.WasRegisteredByCreator(userId)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.NotFound, "The tenant was not found."));
    }
}
