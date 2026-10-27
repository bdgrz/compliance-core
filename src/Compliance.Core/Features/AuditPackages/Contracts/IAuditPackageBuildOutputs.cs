using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.AuditPackages.Contracts;

/// <summary>Immutable technical successful-output ledger, scoped to tenant and logical build.</summary>
public interface IAuditPackageBuildOutputs
{
    Task<AuditPackageBuildOutputRecord?> ReadAsync(Uuid tenantId, Uuid buildId, CancellationToken ct);
    Task<AuditPackageBuildOutputRecord> PublishIfAbsentAsync(AuditPackageBuildOutputRecord output, CancellationToken ct);
}
