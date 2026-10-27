using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Tenants.Commands;
namespace Bdgrz.Compliance.Features.Tenants.Authorizers;

sealed class ResolveMyTenantSlugAuthorizer : IRequestAuthorizer<ResolveMyTenantSlug>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<ResolveMyTenantSlug> context,
        CancellationToken ct) =>
        ValueTask.FromResult(UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Slug resolution requires a Bdgrz user identity.")));
}
