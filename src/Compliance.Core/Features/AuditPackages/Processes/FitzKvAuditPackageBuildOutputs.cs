using System.Globalization;
using System.Text;
using System.Text.Json;
using Cntryl.Fitz;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Contracts;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
namespace Bdgrz.Compliance.Features.AuditPackages.Processes;

/// <summary>Durable insert-if-absent checkpoint before the successful result receiver can be dispatched.</summary>
sealed class FitzKvAuditPackageBuildOutputs(IKvClient client) : IAuditPackageBuildOutputs
{
    public async Task<AuditPackageBuildOutputRecord?> ReadAsync(Uuid tenantId, Uuid buildId, CancellationToken ct)
    {
        await using var tx = await client.BeginAsync(Route(tenantId, buildId), KvMode.ReadOnly, ct).ConfigureAwait(false);
        var found = await tx.GetAsync(Key(buildId), ct).ConfigureAwait(false);
        if (!found.Found)
            return null;
        var output = Parse(found.Value!.Value.Span);
        if (output.TenantId != tenantId || output.BuildId != buildId)
            throw new ImmutableContentIntegrityException("A build checkpoint differs from its exact tenant or logical build.");
        return output;
    }

    public async Task<AuditPackageBuildOutputRecord> PublishIfAbsentAsync(AuditPackageBuildOutputRecord output,
        CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var tx = await client.BeginAsync(Route(output.TenantId, output.BuildId), KvMode.ReadWrite,
                    ct).ConfigureAwait(false);
                var found = await tx.GetAsync(Key(output.BuildId), ct).ConfigureAwait(false);
                if (found.Found)
                {
                    var existing = Parse(found.Value!.Value.Span);
                    await tx.RollbackAsync(ct).ConfigureAwait(false);
                    if (existing != output)
                        throw new ImmutableContentIntegrityException("A logical build cannot publish a different source or archive.");
                    return existing;
                }
                await tx.InsertAsync(Key(output.BuildId), JsonSerializer.SerializeToUtf8Bytes(output,
                    ComplianceCoreJsonContext.Default.AuditPackageBuildOutputRecord), ct).ConfigureAwait(false);
                await tx.CommitAsync(KvDurability.Sync, ct).ConfigureAwait(false);
                return output;
            }
            catch (KvException exception) when (attempt < 4 && FitzKvProcessRecordStore.IsIsolationConflict(exception))
            {
                // Retry the route-lock transaction; no checkpoint was published.
            }
        }
    }

    static string Route(Uuid tenantId, Uuid buildId) =>
        $"kv://bdgrz/audit-package-build-outputs/{tenantId}-{buildId.ToString("N", CultureInfo.InvariantCulture)[..2]}";
    static byte[] Key(Uuid buildId) => Encoding.UTF8.GetBytes("b/" + buildId);
    static AuditPackageBuildOutputRecord Parse(ReadOnlySpan<byte> bytes) => JsonSerializer.Deserialize(bytes,
        ComplianceCoreJsonContext.Default.AuditPackageBuildOutputRecord)
        ?? throw new ImmutableContentIntegrityException("A successful build checkpoint cannot be empty.");
}
