using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     The immutable source boundary captured from the committed partitioning marker. The
///     pre-fence domain version is distinct from the marker's physical offset and own logical
///     version: import cursors include every source record strictly before that offset, while the
///     marker and all later records are outside the import.
/// </summary>
sealed record FindingMigrationFence(Uuid TenantId, Uuid ProgramId, Uuid MigrationId,
    Uuid MarkerEventId, ulong PreFenceProgramVersion, ulong MarkerResourceOffset,
    ulong MarkerProgramVersion)
{
    public EventStreamAddress SourceStream => new(TenantId.ToString(),
        FindingAggregate.LegacySourceArea, ProgramId.ToString());

    public static FindingMigrationFence FromMarker(DomainEventRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Event is not RemediationPartitioningStarted marker ||
            record.ResourceOffset == ulong.MaxValue)
            throw InvalidFence();

        var metadata = record.Event.Metadata;
        if (marker.TenantId == Uuid.Empty || marker.ProgramId == Uuid.Empty ||
            marker.MigrationId == Uuid.Empty || marker.PreFenceProgramVersion == ulong.MaxValue ||
            record.Stream != new EventStreamAddress(marker.TenantId.ToString(),
                FindingAggregate.LegacySourceArea, marker.ProgramId.ToString()) ||
            metadata.AggregateId != marker.ProgramId || metadata.EventId == Uuid.Empty ||
            metadata.IsAudit || metadata.AggregateVersion != marker.PreFenceProgramVersion + 1 ||
            metadata.OccurredOn == default || metadata.OccurredOn.Offset != TimeSpan.Zero)
            throw InvalidFence();

        return new FindingMigrationFence(marker.TenantId, marker.ProgramId, marker.MigrationId,
            metadata.EventId, marker.PreFenceProgramVersion, record.ResourceOffset,
            metadata.AggregateVersion);
    }

    /// <summary>True only for the exact committed marker identified by this snapshot.</summary>
    public bool IsMarker(DomainEventRecord record) =>
        record.Stream == SourceStream && record.ResourceOffset == MarkerResourceOffset &&
        record.Event is RemediationPartitioningStarted marker &&
        marker.TenantId == TenantId && marker.ProgramId == ProgramId &&
        marker.MigrationId == MigrationId &&
        marker.PreFenceProgramVersion == PreFenceProgramVersion &&
        record.Event.Metadata.EventId == MarkerEventId &&
        record.Event.Metadata.AggregateVersion == MarkerProgramVersion;

    /// <summary>
    ///     Reports whether a record is before the marker's exclusive physical boundary. Callers
    ///     must advance their cursor for every true result, including audit records; audit records
    ///     are intentionally not returned by <see cref="IsImportableFact"/>. Audit metadata keeps
    ///     the logical state version unchanged, so it may be zero on an otherwise empty stream but
    ///     must never exceed the pre-fence logical version.
    /// </summary>
    public bool IsBeforeMarker(DomainEventRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Stream != SourceStream)
            throw InvalidFence();
        if (record.ResourceOffset >= MarkerResourceOffset)
        {
            if (record.ResourceOffset == MarkerResourceOffset && !IsMarker(record))
                throw InvalidFence();
            return false;
        }

        var metadata = record.Event.Metadata;
        if (metadata.AggregateId != ProgramId || metadata.EventId == Uuid.Empty ||
            metadata.AggregateVersion > PreFenceProgramVersion ||
            !metadata.IsAudit && metadata.AggregateVersion == 0)
            throw InvalidFence();
        return true;
    }

    /// <summary>Audits remain part of physical cursor progress but are not copied as domain facts.</summary>
    public bool IsImportableFact(DomainEventRecord record) =>
        IsBeforeMarker(record) && !record.Event.Metadata.IsAudit;

    static InvalidOperationException InvalidFence() =>
        new("The remediation partitioning marker does not define a valid source fence.");
}
