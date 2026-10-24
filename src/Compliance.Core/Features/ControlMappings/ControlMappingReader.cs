using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     Reads control mapping lines from their own owner streams. A program's lines and the tuple a proposal belongs to
///     are discovered from the committed facts of the tenant's mapping area, never from a projection, so a projector
///     miss cannot hide a line.
/// </summary>
public sealed class ControlMappingReader(IAggregateReader reader, IDomainEventReader events)
{
    /// <summary>The tuple a proposal belongs to.</summary>
    public sealed record ProposalTuple(Uuid ControlId, Uuid EditionId, string CriterionIdentifier);

    /// <summary>Opens the mapping line of one tuple, optionally selecting a logical operation.</summary>
    public async ValueTask<ControlCriterionMapping> ReadLineAsync(Uuid tenantId, Uuid programId, Uuid controlId,
        Uuid editionId, string criterionIdentifier, Uuid selectedLogicalId = default, CancellationToken ct = default) =>
        await reader.HydrateAsync(new ControlCriterionMapping(tenantId, programId, controlId, editionId,
            criterionIdentifier, selectedLogicalId), ct).ConfigureAwait(false);

    /// <summary>
    ///     Finds the tuple whose line holds the proposal, or null when none does. Proposal ids are operation ids and
    ///     belong to exactly one tuple; a second tuple holding the same id fails closed.
    /// </summary>
    public async ValueTask<ProposalTuple?> LocateAsync(Uuid tenantId, Uuid programId, Uuid mappingId,
        CancellationToken ct = default)
    {
        ProposalTuple? found = null;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                           ControlCriterionMapping.Area), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit || record.Event is not ControlCriterionMappingProposed proposed ||
                proposed.ProgramId != programId || proposed.MappingId != mappingId)
                continue;
            var tuple = new ProposalTuple(proposed.ControlId, proposed.EditionId, proposed.CriterionIdentifier);
            if (found is not null && found != tuple)
                throw new InvalidOperationException(
                    "A mapping proposal id is held by more than one control criterion tuple.");
            found = tuple;
        }

        return found;
    }

    /// <summary>Reads the view of the line holding the proposal, or null when no line holds it.</summary>
    public async ValueTask<ControlCriterionMappingView?> ReadViewAsync(Uuid tenantId, Uuid programId, Uuid mappingId,
        CancellationToken ct = default)
    {
        if (await LocateAsync(tenantId, programId, mappingId, ct).ConfigureAwait(false) is not { } tuple)
            return null;
        return (await ReadLineAsync(tenantId, programId, tuple.ControlId, tuple.EditionId, tuple.CriterionIdentifier,
            default, ct).ConfigureAwait(false)).View;
    }

    /// <summary>Reads every mapping line of the program with the fence of the facts it was folded from.</summary>
    public async ValueTask<ControlMappingSnapshot> ReadProgramAsync(Uuid tenantId, Uuid programId,
        CancellationToken ct = default)
    {
        var order = new List<string>();
        var views = new Dictionary<string, ControlCriterionMappingView>(StringComparer.Ordinal);
        var facts = 0L;
        Uuid? last = null;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                           ControlCriterionMapping.Area), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit || !IsProgramFact(record.Event, programId))
                continue;
            var key = record.Stream.Resource;
            views.TryGetValue(key, out var current);
            if (current is null)
                order.Add(key);
            views[key] = ControlCriterionMappingReducer.Apply(current, record.Event);
            facts++;
            last = record.Event.Metadata.EventId;
        }

        return new ControlMappingSnapshot([.. order.Select(key => views[key])], new SourceFence(facts, last));
    }

    static bool IsProgramFact(DomainEvent fact, Uuid programId) => fact switch
    {
        ControlCriterionMappingProposed proposed => proposed.ProgramId == programId,
        ControlCriterionMappingReviewed reviewed => reviewed.ProgramId == programId,
        ControlCriterionMappingRetired retired => retired.ProgramId == programId,
        _ => false,
    };
}
