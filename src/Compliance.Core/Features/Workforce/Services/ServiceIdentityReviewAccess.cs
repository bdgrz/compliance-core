using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Services;

public static class ServiceIdentityReviewAccess
{
    public static ValueTask<bool> IsAllowedAsync(IAggregateReader reader, IPermissionAuthorizer permissions,
        Uuid tenantId, ServiceIdentity identity, Uuid userId, CancellationToken ct) => ValueTask.FromResult(false);
}
