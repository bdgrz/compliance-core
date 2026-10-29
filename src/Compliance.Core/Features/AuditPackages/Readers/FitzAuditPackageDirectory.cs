using Cntryl.Fitz;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using Bdgrz.Compliance.Features.AuditPackages.Projections;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
namespace Bdgrz.Compliance.Features.AuditPackages.Readers;

sealed class FitzAuditPackageDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/audit-package-identity-directory/projection-v1", ProjectorName),
        IAuditPackageDirectoryReader, IAuditPackageDirectoryProjection
{
    public const string ProjectorName = "AuditPackageIdentityDirectoryV1";

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), AuditPackageManifest.Area)), ct);

    public async ValueTask ApplyAsync(AuditPackageCreated fact, CancellationToken ct = default)
    {
        if (fact.TenantId == Uuid.Empty || fact.ProgramId == Uuid.Empty || fact.SubjectId == Uuid.Empty ||
            fact.Revision != 1 || fact.PackageVersion != 1 || fact.OccurredAt == default ||
            fact.Operation.Owner != new AuditPackageManifest(fact.TenantId, fact.SubjectId).Stream ||
            fact.Operation.LogicalId != fact.SubjectId)
            throw new InvalidOperationException("A package identity must come from its exact native creation.");
        var identity = new AuditPackageSummaryView(fact.TenantId, fact.ProgramId, fact.SubjectId,
            string.Empty, "created_identity", fact.OccurredAt);
        var current = await AuditPackageDirectorySchema.Packages.GetAsync(Transaction, fact.SubjectId, ct)
            .ConfigureAwait(false);
        if (current is not null)
        {
            if (current != identity)
                throw new InvalidOperationException("A package identity cannot change its original program or creation ordering.");
            return;
        }
        await AuditPackageDirectorySchema.Packages.InsertAsync(Transaction, identity, ct).ConfigureAwait(false);
    }

    public async ValueTask<Page<AuditPackageSummaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await AuditPackageDirectorySchema.Packages.QueryAsync(tx,
            AuditPackageDirectorySchema.ByProgramCreated.Query().WithPrefix(programId.ToString())
                .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
    }
}
