using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     Reads criterion applicability lines from their own owner streams. A program's lines and the criterion a
///     proposal belongs to are discovered from the committed facts of the tenant's applicability area, never from a
///     projection, so a projector miss cannot hide a line.
/// </summary>
public sealed class ApplicabilityReader(IAggregateReader reader, IDomainEventReader events)
{
    /// <summary>The criterion a proposal belongs to.</summary>
    public sealed record ProposalCriterion(Uuid EditionId, string CriterionIdentifier);

    /// <summary>Opens the applicability line of one criterion, optionally selecting a logical operation.</summary>
    public async ValueTask<CriterionApplicability> ReadLineAsync(Uuid tenantId, Uuid programId, Uuid editionId,
        string criterionIdentifier, Uuid selectedLogicalId = default, CancellationToken ct = default) =>
        await reader.HydrateAsync(new CriterionApplicability(tenantId, programId, editionId, criterionIdentifier,
            selectedLogicalId), ct).ConfigureAwait(false);

    /// <summary>
    ///     Finds the criterion whose line holds the proposal, or null when none does. Proposal ids are operation ids
    ///     and belong to exactly one criterion; a second criterion holding the same id fails closed.
    /// </summary>
    public async ValueTask<ProposalCriterion?> LocateAsync(Uuid tenantId, Uuid programId, Uuid decisionId,
        CancellationToken ct = default)
    {
        ProposalCriterion? found = null;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                           CriterionApplicability.Area), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit || record.Event is not CriterionNotApplicableProposed proposed ||
                proposed.ProgramId != programId || proposed.DecisionId != decisionId)
                continue;
            var criterion = new ProposalCriterion(proposed.EditionId, proposed.CriterionIdentifier);
            if (found is not null && found != criterion)
                throw new InvalidOperationException(
                    "A not-applicable proposal id is held by more than one criterion.");
            found = criterion;
        }

        return found;
    }

    /// <summary>Reads the view of the line holding the proposal, or null when no line holds it.</summary>
    public async ValueTask<CriterionApplicabilityView?> ReadViewAsync(Uuid tenantId, Uuid programId, Uuid decisionId,
        CancellationToken ct = default)
    {
        if (await LocateAsync(tenantId, programId, decisionId, ct).ConfigureAwait(false) is not { } criterion)
            return null;
        return (await ReadLineAsync(tenantId, programId, criterion.EditionId, criterion.CriterionIdentifier, default,
            ct).ConfigureAwait(false)).View;
    }

    /// <summary>Reads every applicability line of the program with the fence of the facts it was folded from.</summary>
    public async ValueTask<ApplicabilitySnapshot> ReadProgramAsync(Uuid tenantId, Uuid programId,
        CancellationToken ct = default)
    {
        var order = new List<string>();
        var views = new Dictionary<string, CriterionApplicabilityView>(StringComparer.Ordinal);
        var facts = 0L;
        Uuid? last = null;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                           CriterionApplicability.Area), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit || !IsProgramFact(record.Event, programId))
                continue;
            var key = record.Stream.Resource;
            views.TryGetValue(key, out var current);
            if (current is null)
                order.Add(key);
            views[key] = CriterionApplicabilityReducer.Apply(current, record.Event);
            facts++;
            last = record.Event.Metadata.EventId;
        }

        return new ApplicabilitySnapshot([.. order.Select(key => views[key])], new SourceFence(facts, last));
    }

    static bool IsProgramFact(DomainEvent fact, Uuid programId) => fact switch
    {
        CriterionNotApplicableProposed proposed => proposed.ProgramId == programId,
        CriterionApplicabilityReviewed reviewed => reviewed.ProgramId == programId,
        CriterionNotApplicableWithdrawn withdrawn => withdrawn.ProgramId == programId,
        _ => false,
    };
}
