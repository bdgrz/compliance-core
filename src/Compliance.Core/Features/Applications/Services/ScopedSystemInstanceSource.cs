using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Applications.Aggregates;
namespace Bdgrz.Compliance.Features.Applications.Services;

/// <summary>Resolves a scope subject from its instance stream, then its legacy application stream.</summary>
public static class ScopedSystemInstanceSource
{
    public static async ValueTask<ScopedSystemInstance?> FindAsync(IAggregateReader reader,
        IDomainEventReader events, Uuid tenantId, Uuid applicationId, Uuid instanceId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var instance = await reader.HydrateAsync(new DeclaredSystemInstance(tenantId,
            instanceId), ct).ConfigureAwait(false);
        if (instance.IsCreated)
            return instance.ApplicationId == applicationId
                ? ScopedSystemInstance.From(instance)
                : null;
        var legacy = await LegacySystemInstanceSource.ReadDeclarationAsync(events, tenantId,
            applicationId, instanceId, ct).ConfigureAwait(false);
        return legacy is null ? null : ScopedSystemInstance.FromLegacy(legacy);
    }
}
