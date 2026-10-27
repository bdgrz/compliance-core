using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Proves, from authoritative facts, whether every recurring occurrence a Type II period expects is both selected
///     and materialized. Selected work is the complete manifest of each <see cref="DueControlOccurrencesGenerated" />
///     of the period; materialized work is a <see cref="ControlOccurrenceOpened" /> fact of the canonical occurrence
///     stream whose frozen creation intent carries exactly the selected terms. A selected occurrence whose stream has
///     no such fact, or whose terms differ, is missing: coverage never infers an occurrence from a count, a projection
///     or the absence of a conflict.
/// </summary>
public sealed class TypeIIOccurrenceCoverage(IAggregateReader reader, IImmutableContentStore content)
{
    /// <summary>Proves the coverage of the period as hydrated at its admitted revision.</summary>
    public async ValueTask<TypeIIOccurrenceCoverageProof> ProveAsync(TypeIIPeriodReadSnapshot period, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(period);
        var current = period.View ?? throw new InvalidOperationException("Coverage requires an opened period.");
        var tenantId = current.TenantId;
        var expected = current.ExpectedOccurrences.Where(static item => item.DueOn is not null)
            .Select(static item => item.OccurrenceId).ToHashSet();
        var selectedExpected = new HashSet<Uuid>();
        var selected = new SortedDictionary<string, Uuid>(StringComparer.Ordinal);
        var materialized = new List<Uuid>();
        var missing = new List<Uuid>();
        foreach (var generation in period.OccurrenceGenerations)
        {
            var manifest = await ControlOperatingContent.ReadAsync<ControlOccurrenceSelectionManifest>(content,
                tenantId, generation.CreationIntentManifest, ct).ConfigureAwait(false);
            foreach (var selection in manifest.Selections)
            {
                selectedExpected.Add(selection.ExpectedOccurrenceId);
                if (!selected.TryAdd(selection.OccurrenceId.ToString(), selection.OccurrenceId))
                    continue;
                var intent = await ControlOperatingContent.ReadAsync<ControlOccurrenceCreationIntent>(content,
                    tenantId, selection.Intent, ct).ConfigureAwait(false);
                var occurrence = await reader.HydrateAsync(new ControlOccurrence(tenantId, current.ProgramId,
                    intent.ControlId, selection.OccurrenceId), ct).ConfigureAwait(false);
                if (await IsMaterializedAsync(occurrence, intent, tenantId, ct).ConfigureAwait(false))
                    materialized.Add(selection.OccurrenceId);
                else
                    missing.Add(selection.OccurrenceId);
            }
        }

        return new TypeIIOccurrenceCoverageProof(selected.Values.ToArray(), materialized.OrderBy(static id =>
                id.ToString(), StringComparer.Ordinal).ToArray(),
            missing.OrderBy(static id => id.ToString(), StringComparer.Ordinal).ToArray(),
            expected.Where(id => !selectedExpected.Contains(id)).OrderBy(static id => id.ToString(),
                StringComparer.Ordinal).ToArray());
    }

    async ValueTask<bool> IsMaterializedAsync(ControlOccurrence occurrence, ControlOccurrenceCreationIntent selected,
        Uuid tenantId, CancellationToken ct)
    {
        if (occurrence.Opened is not { } opened)
            return false;
        var materialized = await ControlOperatingContent.ReadAsync<ControlOccurrenceCreationIntent>(content,
            tenantId, opened.CreationIntent, ct).ConfigureAwait(false);
        return materialized.SameTermsAs(selected);
    }
}
