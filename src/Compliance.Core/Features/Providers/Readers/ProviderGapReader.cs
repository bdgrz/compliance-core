using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
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
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Providers.Readers;

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
