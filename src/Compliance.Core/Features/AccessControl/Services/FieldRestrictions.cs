using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>Applies field-class read permissions before a handler returns, lists, searches, or exports a record.</summary>
public static class FieldRestrictions
{
    public static async ValueTask<FieldRedactor> ForActorAsync(IPermissionAuthorizer permissions,
        Uuid tenantId, Uuid userId, FieldClass fieldClass, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(fieldClass);
        var canRead = await permissions.IsAllowedAsync(tenantId, userId,
                RbacIds.Member(tenantId, userId), fieldClass.ReadPermission, ct)
            .ConfigureAwait(false);
        return new FieldRedactor(fieldClass, canRead);
    }
}
