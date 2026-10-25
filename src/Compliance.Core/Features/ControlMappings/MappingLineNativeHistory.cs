using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

static class MappingLineNativeHistory
{
    public static async Task<IReadOnlyList<DomainEvent>> ReadAsync(Aggregate owner, IDomainEventReader events,
        CancellationToken ct)
    {
        var facts = new List<DomainEvent>();
        var eventIds = new HashSet<Uuid>();
        var digest = MappingLineReplayDigest.Empty;
        var physical = 0UL;
        var version = 0UL;
        var acceptance = owner switch
        {
            ControlCriterionMapping mapping => mapping.Acceptance,
            CriterionApplicability applicability => applicability.Acceptance,
            _ => throw new ArgumentException("The owner is not a mapping line.", nameof(owner)),
        };
        await foreach (var record in events.ReadAsync(owner.Stream, 0, ct).ConfigureAwait(false))
        {
            if (physical == owner.CommittedStreamPosition)
                break;
            var value = record.Event;
            var metadata = value.Metadata;
            if (record.Stream != owner.Stream || record.ResourceOffset != physical || metadata.EventId == Uuid.Empty ||
                metadata.AggregateId != owner.Id || !eventIds.Add(metadata.EventId))
                throw new ImmutableContentIntegrityException("The native line history has another exact physical/metadata source identity.");
            physical++;
            digest = MappingLineReplayDigest.Append(digest, value);
            if (metadata.IsAudit)
                continue;
            if (metadata.AggregateVersion != ++version || value is not IMappingLineFact native || native.Operation is null)
                throw new ImmutableContentIntegrityException("The native line history has another logical version or fact family.");
            if (native.Operation.LogicalId == acceptance.SelectedLogicalId &&
                (acceptance.SelectedFact is not { } selected || selected.GetType() != value.GetType() ||
                    !selected.Equals(value) || selected.Metadata != metadata))
                throw new ImmutableContentIntegrityException("The selected acceptance differs from its actual native payload and complete metadata.");
            facts.Add(value);
        }
        if (physical != owner.CommittedStreamPosition || version != owner.Version)
            throw new ImmutableContentUnavailableException("The exact hydrated line history cut is not visible yet.");
        var observedDigest = owner switch
        {
            ControlCriterionMapping mapping => mapping.ReplayDigest,
            CriterionApplicability applicability => applicability.ReplayDigest,
            _ => throw new ArgumentException("The owner is not a mapping line.", nameof(owner)),
        };
        if (digest != observedDigest)
            throw new ImmutableContentIntegrityException("The native line history differs from the complete payload/immutable-metadata prefix actually hydrated.");
        var header = new MappingLineHeader(0, 0, Uuid.Empty, null, null, false);
        var proposals = new HashSet<Uuid>();
        var operations = new HashSet<Uuid>();
        foreach (var fact in facts)
        {
            header = MappingLineTransitions.Next(owner, header, fact);
            var native = (IMappingLineFact)fact;
            if (!operations.Add(native.Operation.LogicalId))
                throw new ImmutableContentIntegrityException("A logical operation occurs more than once on the line.");
            var proposed = fact switch
            {
                ControlCriterionMappingProposed mapping => mapping.MappingId,
                CriterionNotApplicableProposed applicability => applicability.DecisionId,
                _ => Uuid.Empty,
            };
            if (proposed != Uuid.Empty && !proposals.Add(proposed))
                throw new ImmutableContentIntegrityException("A historical proposal identity was reused on the line.");
        }
        var actual = owner switch
        {
            ControlCriterionMapping mapping => (mapping.Header, mapping.CurrentContent),
            CriterionApplicability applicability => (applicability.Header, applicability.CurrentContent),
            _ => throw new ArgumentException("The owner is not a mapping line.", nameof(owner)),
        };
        if (header != actual.Header || (facts.Count == 0 ? null : ((IMappingLineFact)facts[^1]).Content) != actual.CurrentContent)
            throw new ImmutableContentIntegrityException("The native history differs from the hydrated current line cut.");
        ct.ThrowIfCancellationRequested();
        return facts;
    }
}
