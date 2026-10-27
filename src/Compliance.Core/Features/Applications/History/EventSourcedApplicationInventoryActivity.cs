using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Applications.Services;
namespace Bdgrz.Compliance.Features.Applications.History;

sealed class EventSourcedApplicationInventoryActivity(IAggregateReader reader,
    IApplicationDirectoryReader directory, LegacySystemInstanceSource legacy)
    : IApplicationInventoryActivity
{
    public async ValueTask<bool> IsDeclaredAsync(Uuid tenantId, Uuid applicationId,
        CancellationToken ct = default)
    {
        var application = await reader.HydrateApplicationAsync(tenantId, applicationId, ct).ConfigureAwait(false);
        return application.IsCreated;
    }

    public async ValueTask<SystemInstanceReferenceState> GetInstanceStateAsync(Uuid tenantId,
        Uuid instanceId, CancellationToken ct = default)
    {
        var source = await reader.HydrateAsync(new DeclaredSystemInstance(tenantId,
            instanceId), ct).ConfigureAwait(false);
        var instance = await directory.GetInstanceAsync(tenantId, instanceId, ct)
            .ConfigureAwait(false);
        if (instance is not null && (instance.TenantId != tenantId ||
                                     instance.SystemInstanceId != instanceId))
            return SystemInstanceReferenceState.Missing;
        if (source.IsCreated)
        {
            // Reference commands wait for the tenant-scoped instance projection, so a
            // just-committed source is a recoverable conflict until visible.
            if (instance is null || instance.Revision < source.Revision)
                return SystemInstanceReferenceState.Pending;
            return instance.ApplicationId == source.ApplicationId
                ? SystemInstanceReferenceState.Declared
                : SystemInstanceReferenceState.Missing;
        }
        if (instance is null)
            return (await legacy.FindPendingAsync(tenantId, instanceId, ct)
                    .ConfigureAwait(false)).Clear
                ? SystemInstanceReferenceState.Missing
                : SystemInstanceReferenceState.Pending;
        // Historical declarations live in application streams; a projected legacy row is
        // authoritative, so no stream is rebuilt for the common case.
        return await legacy.ExistsAsync(tenantId, instance.ApplicationId, instanceId, ct)
            .ConfigureAwait(false)
            ? SystemInstanceReferenceState.Declared
            : SystemInstanceReferenceState.Missing;
    }
}
