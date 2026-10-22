using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

public sealed record FindingHistoryMigrationStatus(Uuid TenantId, Uuid ProgramId,
    Uuid RequestedMigrationId, Uuid? ActiveMigrationId, bool IsStarted, bool IsComplete,
    ulong NextSourceOffset, ulong FenceMarkerResourceOffset, ulong ImportedFindingEventCount,
    string SourceDigest, Uuid FenceEventId);

public sealed class FindingHistoryMigrationStatusReader(IAggregateReader reader)
{
    public async ValueTask<FindingHistoryMigrationStatus> ReadAsync(Uuid tenantId,
        Uuid programId, Uuid requestedMigrationId, CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty ||
            requestedMigrationId == Uuid.Empty)
            throw new ArgumentException("A finding migration status needs tenant, program, and migration IDs.");

        var migration = await reader.HydrateAsync(new FindingMigrationAggregate(tenantId, programId), ct)
            .ConfigureAwait(false);
        return new(tenantId, programId, requestedMigrationId,
            migration.IsStarted ? migration.MigrationId : null, migration.IsStarted,
            migration.IsComplete, migration.NextSourceOffset,
            migration.FenceMarkerResourceOffset, migration.ImportedFindingEventCount,
            migration.SourceDigest, migration.FenceEventId);
    }
}
