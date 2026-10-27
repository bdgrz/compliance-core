using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     Reads control mapping lines from their own owner streams. A program's lines and the tuple a proposal belongs to
///     are discovered from the committed facts of the tenant's mapping area, never from a projection, so a projector
///     miss cannot hide a line.
/// </summary>
public sealed class ControlMappingReader(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content)
{
    /// <summary>The tuple a proposal belongs to.</summary>
    public sealed record ProposalTuple(Uuid ControlId, Uuid EditionId, string CriterionIdentifier);

    /// <summary>Opens the mapping line of one tuple, optionally selecting a logical operation.</summary>
    public async ValueTask<ControlCriterionMapping> ReadLineAsync(Uuid tenantId, Uuid programId, Uuid controlId,
        Uuid editionId, string criterionIdentifier, Uuid selectedLogicalId = default, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new ControlCriterionMapping(tenantId, programId, controlId, editionId,
            criterionIdentifier, selectedLogicalId), ct).ConfigureAwait(false);
        _ = await MappingLineNativeHistory.ReadAsync(owner, events, ct).ConfigureAwait(false);
        return owner;
    }

    /// <summary>Composes full immutable history only at the owner's exact committed cut.</summary>
    public async Task<ControlCriterionMappingRules> ComposeAsync(ControlCriterionMapping owner, CancellationToken ct = default)
    {
        ControlCriterionMappingRules? rules = null;
        var facts = await MappingLineNativeHistory.ReadAsync(owner, events, ct).ConfigureAwait(false);
        foreach (var native in facts)
        {
            if (native is ControlCriterionMappingProposed fact)
                rules ??= new ControlCriterionMappingRules(fact.TenantId, fact.ProgramId, fact.ControlId, fact.EditionId, fact.CriterionIdentifier);
            if (rules is null)
                throw new ImmutableContentIntegrityException("A line history must begin with its proposal.");
            var decoded = await MappingLineContent.ReadAsync(content, native, ct).ConfigureAwait(false);
            rules.Apply(decoded.Change);
            MappingLineContent.ValidateCurrent(decoded.Current, MappingLineContent.Current(rules));
        }
        if (rules is not null)
            return rules;
        return new ControlCriterionMappingRules(Uuid.Parse(owner.Stream.Realm, System.Globalization.CultureInfo.InvariantCulture), owner.ProgramId, owner.ControlId, owner.EditionId, owner.CriterionIdentifier);
    }

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
            var sourceOwner = MappingLineContent.Owner(record.Event);
            if (record.Stream != sourceOwner.Stream || record.Event.Metadata.AggregateId != sourceOwner.Id)
                throw new ImmutableContentIntegrityException("The located proposal has another actual owner stream.");
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
        var owner = await ReadLineAsync(tenantId, programId, tuple.ControlId, tuple.EditionId, tuple.CriterionIdentifier,
            default, ct).ConfigureAwait(false);
        return (await ComposeAsync(owner, ct).ConfigureAwait(false)).View;
    }

    /// <summary>Reads every mapping line of the program with the fence of the facts it was folded from.</summary>
    public async ValueTask<ControlMappingSnapshot> ReadProgramAsync(Uuid tenantId, Uuid programId,
        CancellationToken ct = default)
    {
        var order = new List<string>();
        var lines = new Dictionary<string, ControlCriterionMappingRules>(StringComparer.Ordinal);
        var headers = new Dictionary<string, MappingLineHeader>(StringComparer.Ordinal);
        var offsets = new Dictionary<string, ulong>(StringComparer.Ordinal);
        var ids = new HashSet<Uuid>();
        var logicalIds = new Dictionary<string, HashSet<Uuid>>(StringComparer.Ordinal);
        var facts = 0L;
        Uuid? last = null;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                           ControlCriterionMapping.Area), EventCursor.Start, ct).ConfigureAwait(false))
        {
            var key = record.Stream.Resource;
            var offset = offsets.GetValueOrDefault(key);
            if (record.ResourceOffset != offset)
                throw new ImmutableContentIntegrityException("The program line history has another physical prefix.");
            offsets[key] = offset + 1;
            if (!record.Event.Metadata.IsAudit && record.Event is IMappingLineFact)
            {
                var sourceOwner = MappingLineContent.Owner(record.Event);
                if (record.Stream != sourceOwner.Stream || record.Event.Metadata.AggregateId != sourceOwner.Id)
                    throw new ImmutableContentIntegrityException("A native area fact has another actual line identity.");
            }
            if (record.Event.Metadata.IsAudit || !IsProgramFact(record.Event, programId))
                continue;
            var first = record.Event switch
            {
                ControlCriterionMappingProposed fact => new ControlCriterionMappingRules(fact.TenantId, fact.ProgramId, fact.ControlId, fact.EditionId, fact.CriterionIdentifier),
                ControlCriterionMappingReviewed fact => new ControlCriterionMappingRules(fact.TenantId, fact.ProgramId, fact.ControlId, fact.EditionId, fact.CriterionIdentifier),
                ControlCriterionMappingRetired fact => new ControlCriterionMappingRules(fact.TenantId, fact.ProgramId, fact.ControlId, fact.EditionId, fact.CriterionIdentifier),
                _ => throw new ImmutableContentIntegrityException("The program line contains another fact."),
            };
            if (!lines.TryGetValue(key, out var current))
            {
                current = first;
                lines.Add(key, current);
                headers.Add(key, new MappingLineHeader(0, 0, Uuid.Empty, null, null, false));
                logicalIds.Add(key, []);
                order.Add(key);
            }
            var metadata = record.Event.Metadata;
            var owner = new ControlCriterionMapping(current.TenantId, current.ProgramId, current.ControlId, current.EditionId, current.CriterionIdentifier);
            if (record.Stream != owner.Stream || metadata.AggregateId != owner.Id ||
                metadata.AggregateVersion != (ulong)current.Revision + 1 || metadata.EventId == Uuid.Empty ||
                !ids.Add(metadata.EventId) || !logicalIds[key].Add(((IMappingLineFact)record.Event).Operation.LogicalId))
                throw new ImmutableContentIntegrityException("A program line fact has another scope, version or operation identity.");
            headers[key] = MappingLineTransitions.Next(owner, headers[key], record.Event);
            var decoded = await MappingLineContent.ReadAsync(content, record.Event, ct).ConfigureAwait(false);
            current.Apply(decoded.Change);
            MappingLineContent.ValidateCurrent(decoded.Current, MappingLineContent.Current(current));
            facts++;
            last = metadata.EventId;
        }
        return new ControlMappingSnapshot([.. order.Select(key => lines[key].View!)], new SourceFence(facts, last));
    }

    static bool IsProgramFact(DomainEvent fact, Uuid programId) => fact switch
    {
        ControlCriterionMappingProposed proposed => proposed.ProgramId == programId,
        ControlCriterionMappingReviewed reviewed => reviewed.ProgramId == programId,
        ControlCriterionMappingRetired retired => retired.ProgramId == programId,
        _ => false,
    };
}
