using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Projections;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Operations.Readers;

/// <summary>
///     Reads a program's operating plan lines and occurrences from their own owner streams and composes them as a
///     <see cref="ControlOperationsSnapshot" />. Streams are discovered from the committed facts of the tenant's plan
///     and occurrence areas; every attestation, review and plan is verified immutable content.
/// </summary>
public sealed class ControlOperationsReader(IAggregateReader reader, IDomainEventReader events,
    IImmutableContentStore content)
{
    /// <summary>Reads every plan line and occurrence of the program.</summary>
    public ValueTask<ControlOperationsSnapshot> ReadAsync(Uuid tenantId, Uuid programId, CancellationToken ct = default) =>
        ReadCoreAsync(tenantId, programId, null, null, ct);

    /// <summary>Reads the plan line and occurrences of one control.</summary>
    public ValueTask<ControlOperationsSnapshot> ReadControlAsync(Uuid tenantId, Uuid programId, Uuid controlId,
        CancellationToken ct = default) => ReadCoreAsync(tenantId, programId, controlId, null, ct);

    /// <summary>Reads the plan line of one control and one recorded occurrence of it.</summary>
    public ValueTask<ControlOperationsSnapshot> ReadOccurrenceAsync(Uuid tenantId, Uuid programId, Uuid controlId,
        Uuid occurrenceId, CancellationToken ct = default) =>
        ReadCoreAsync(tenantId, programId, controlId, occurrenceId, ct);

    /// <summary>The plan line of one control.</summary>
    public async ValueTask<(ControlOperatingPlan Plan, ControlOperatingPlanLine Line)> ReadLineAsync(Uuid tenantId,
        Uuid programId, Uuid controlId, Uuid selectedLogicalId, CancellationToken ct = default)
    {
        var plan = await reader.HydrateAsync(new ControlOperatingPlan(tenantId, programId, controlId,
            selectedLogicalId), ct).ConfigureAwait(false);
        return (plan, await ControlOperatingPlanLine.ReadAsync(plan, events, content, tenantId, ct).ConfigureAwait(false));
    }

    /// <summary>Composes only the exact native/physical cut already hydrated for admission.</summary>
    public Task<ControlOperatingPlanLine> ComposeLineAsync(ControlOperatingPlan plan, CancellationToken ct = default) =>
        ControlOperatingPlanLine.ReadAsync(plan, events, content, plan.TenantId, ct);

    /// <summary>The materialized occurrence, or null when it is not materialized.</summary>
    public async ValueTask<(ControlOccurrence Occurrence, ControlOccurrenceView? View)> ReadStateAsync(Uuid tenantId,
        Uuid programId, Uuid controlId, Uuid occurrenceId, Uuid selectedLogicalId, CancellationToken ct = default)
    {
        var occurrence = await reader.HydrateAsync(new ControlOccurrence(tenantId, programId, controlId, occurrenceId,
            selectedLogicalId), ct).ConfigureAwait(false);
        return (occurrence, await ControlOccurrenceFold.ReadAsync(occurrence, events, content, tenantId, ct)
            .ConfigureAwait(false));
    }

    async ValueTask<ControlOperationsSnapshot> ReadCoreAsync(Uuid tenantId, Uuid programId, Uuid? controlId,
        Uuid? occurrenceId, CancellationToken ct)
    {
        var controls = new SortedSet<Uuid>(Comparer<Uuid>.Create(static (left, right) =>
            string.CompareOrdinal(left.ToString(), right.ToString())));
        if (controlId is { } single)
            controls.Add(single);
        else
            await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                               ControlOperatingPlan.Area), EventCursor.Start, ct).ConfigureAwait(false))
                if (!record.Event.Metadata.IsAudit && record.Event is ControlOperatingPlanProposed proposed &&
                    proposed.ProgramId == programId)
                    controls.Add(proposed.ControlId);

        var lines = new List<ControlOperatingPlanLine>();
        foreach (var control in controls)
        {
            var plan = await reader.HydrateAsync(new ControlOperatingPlan(tenantId, programId, control), ct)
                .ConfigureAwait(false);
            if (plan.Revision > 0)
                lines.Add(await ControlOperatingPlanLine.ReadAsync(plan, events, content, tenantId, ct).ConfigureAwait(false));
        }

        var occurrences = new SortedSet<(Uuid Control, Uuid Occurrence)>(Comparer<(Uuid Control, Uuid Occurrence)>
            .Create(static (left, right) => string.CompareOrdinal(left.Occurrence.ToString(),
                right.Occurrence.ToString())));
        if (occurrenceId is { } onlyOccurrence && controlId is { } onlyControl)
            occurrences.Add((onlyControl, onlyOccurrence));
        else
            await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                               ControlOccurrence.Area), EventCursor.Start, ct).ConfigureAwait(false))
                if (!record.Event.Metadata.IsAudit && record.Event is ControlOccurrenceOpened opened &&
                    opened.ProgramId == programId && (controlId is null || opened.ControlId == controlId))
                    occurrences.Add((opened.ControlId, opened.OccurrenceId));

        var views = new List<ControlOccurrenceView>();
        foreach (var (control, occurrenceKey) in occurrences)
        {
            var (_, view) = await ReadStateAsync(tenantId, programId, control, occurrenceKey, Uuid.Empty, ct)
                .ConfigureAwait(false);
            if (view is not null)
                views.Add(view);
        }

        return new ControlOperationsSnapshot(tenantId, programId, lines, views);
    }
}
