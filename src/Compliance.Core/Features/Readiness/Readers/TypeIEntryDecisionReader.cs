using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Readiness.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Readers;

/// <summary>Current decision follows committed decision facts, independently of later assessments.</summary>
public sealed class TypeIEntryDecisionReader(IAggregateReader reader, IDomainEventReader events)
{
    const int MaximumDecisions = 10000;

    public async ValueTask<TypeIEntryDecisionRecord?> GetAsync(Uuid tenantId, Uuid programId,
        Uuid decisionId, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new TypeIEntryDecision(tenantId, decisionId), ct).ConfigureAwait(false);
        return owner.Recorded?.ProgramId == programId ? owner.View() : null;
    }

    public async ValueTask<IReadOnlyList<TypeIEntryDecisionRecord>> ListAsync(Uuid tenantId,
        Uuid programId, CancellationToken ct = default)
    {
        var result = new List<TypeIEntryDecisionRecord>();
        var seen = new HashSet<Uuid>();
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
            TypeIEntryDecision.Area), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            if (record.Event is not TypeIEntryDecided fact || record.Stream.Realm != tenantId.ToString() ||
                record.Stream.Area != TypeIEntryDecision.Area || fact.TenantId != tenantId ||
                record.Stream.Resource != fact.DecisionId.ToString() || record.ResourceOffset != 0 ||
                fact.Metadata.EventId == Uuid.Empty || fact.Metadata.AggregateVersion != 1 ||
                fact.Metadata.AggregateId != fact.DecisionId || !seen.Add(fact.DecisionId))
                throw new ImmutableContentIntegrityException("The Type I decision history has an invalid source or repeated decision.");
            if (seen.Count > MaximumDecisions)
                throw new ImmutableContentIntegrityException("The complete Type I decision history exceeds its supported read bound.");
            // Hydration validates the complete business payload, not only its index fields.
            var view = await GetAsync(tenantId, fact.ProgramId, fact.DecisionId, ct).ConfigureAwait(false)
                ?? throw new ImmutableContentUnavailableException("A committed decision is not available from its owner yet.");
            if (view.ProgramId == programId)
                result.Add(view);
        }
        result.Reverse();
        return result;
    }

    public async ValueTask<TypeIEntryDecisionRecord?> CurrentAsync(Uuid tenantId, Uuid programId,
        CancellationToken ct = default) =>
        (await ListAsync(tenantId, programId, ct).ConfigureAwait(false)).FirstOrDefault();
}
