using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Immutable technical successful-output ledger, scoped to tenant and logical build.</summary>
public interface IAuditPackageBuildOutputs
{
    Task<AuditPackageBuildOutputRecord?> ReadAsync(Uuid tenantId, Uuid buildId, CancellationToken ct);
    Task<AuditPackageBuildOutputRecord> PublishIfAbsentAsync(AuditPackageBuildOutputRecord output, CancellationToken ct);
}
