using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Composes complete native gap history at the actual hydrated physical and metadata cut.</summary>
public sealed class ProviderGapReader(IAggregateReader reader, IDomainEventReader events)
{
    public async ValueTask<ProviderGapSnapshot> ReadAsync(Uuid tenantId, Uuid providerId, Uuid gapId,
        Uuid selectedLogicalId = default, Uuid selectedAdmissionId = default, string? selectedPair = null,
        CancellationToken ct = default) => await ReadAsync(await reader.HydrateAsync(new ProviderAssuranceGap(tenantId,
            providerId, gapId, selectedLogicalId, selectedAdmissionId, selectedPair), ct).ConfigureAwait(false), ct)
            .ConfigureAwait(false);

    public async ValueTask<ProviderGapSnapshot> ReadAsync(ProviderAssuranceGap owner, CancellationToken ct = default) =>
        new(owner, await ProviderGapHistory.ReadAsync(owner, events, ct).ConfigureAwait(false));

    /// <summary>
    /// Discovers the scoped provider id from the actual native record, then verifies the complete owner before using
    /// any selected admission. The discovery record is only a locator, never authority on its own.
    /// </summary>
    public async ValueTask<ProviderGapSnapshot> ReadFromSourceAsync(NativeFactReference source, Uuid gapId,
        Uuid selectedAdmissionId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!Uuid.TryParse(source.Stream.Realm, System.Globalization.CultureInfo.InvariantCulture, out var tenantId) ||
            tenantId == Uuid.Empty || source.Stream.Area != ProviderAssuranceStreams.Area || gapId == Uuid.Empty)
            throw new ImmutableContentIntegrityException("The gap source names another tenant or native owner area.");
        var pattern = EventStreamPattern.ForPattern(source.Stream.Realm, source.Stream.Area, source.Stream.Resource);
        ProviderCoverageGapRecorded? locator = null;
        await foreach (var record in events.ReadAsync(pattern, EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Stream != source.Stream)
                throw new ImmutableContentIntegrityException("The exact gap source lookup returned another owner stream.");
            if (record.Event.Metadata.IsAudit)
                continue;
            locator = record.Event as ProviderCoverageGapRecorded ??
                throw new ImmutableContentIntegrityException("The source is not the native record of a provider gap.");
            break;
        }
        if (locator is null)
            throw new ImmutableContentUnavailableException("The exact provider gap record is not visible yet.");
        if (locator.TenantId != tenantId || locator.GapId != gapId || locator.ProviderId == Uuid.Empty ||
            ProviderAssuranceStreams.Gap(tenantId, locator.ProviderId, gapId) != source.Stream)
            throw new ImmutableContentIntegrityException("The source record belongs to another exact scoped provider gap.");
        return await ReadAsync(tenantId, locator.ProviderId, gapId, selectedAdmissionId: selectedAdmissionId, ct: ct)
            .ConfigureAwait(false);
    }
}
