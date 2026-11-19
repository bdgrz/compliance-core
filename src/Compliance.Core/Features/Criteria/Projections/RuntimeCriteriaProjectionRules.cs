using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Projections;

/// <summary>Pure per-entry projection transitions. The durable store owns checkpoint atomicity.</summary>
static class RuntimeCriteriaProjectionRules
{
    internal static RuntimeCriteriaEntryIndexRow Propose(RuntimeCriteriaEntryIndexRow current,
        RuntimeCriteriaPendingIndex proposal)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(proposal);
        if (current.Pending is not null || proposal.OperationId == Uuid.Empty ||
            proposal.ProposalEventId == Uuid.Empty || proposal.ExecutionGeneration < 1 ||
            proposal.Action is not ("create" or "replace" or "remove"))
            throw new InvalidOperationException("A criterion projection requires one exact unresolved proposal.");

        return current with { Pending = proposal };
    }

    internal static RuntimeCriteriaEntryIndexRow Settle(RuntimeCriteriaEntryIndexRow current,
        Uuid operationId, long executionGeneration, Uuid proposalEventId, bool accepted, bool published)
    {
        ArgumentNullException.ThrowIfNull(current);
        var pending = current.Pending;
        if (pending is null || pending.OperationId != operationId ||
            pending.ExecutionGeneration != executionGeneration || pending.ProposalEventId != proposalEventId)
            throw new InvalidOperationException("A criterion outcome must answer its exact projected proposal.");

        if (!accepted)
            return current with { Pending = null };
        if (published && !pending.BeforePublication)
            throw new InvalidOperationException("A post-publication structural proposal cannot change the published cut.");

        if (pending.Action == "remove")
        {
            if (!current.Included)
                throw new InvalidOperationException("An absent criterion cannot be removed.");
            return current with { Included = false, Pending = null };
        }

        if (pending.Action == "create" && current.Included ||
            pending.Action == "replace" && !current.Included || pending.IncarnationId == Uuid.Empty ||
            pending.Kind is not ("criterion" or "point_of_focus") ||
            pending.Category is not ("security" or "availability" or "confidentiality" or
                "processing_integrity" or "privacy") || string.IsNullOrWhiteSpace(pending.Summary))
            throw new InvalidOperationException("The accepted structural outcome has invalid entry content.");

        if (pending.Kind == "criterion" && (pending.ParentIdentifier is not null || pending.ParentIncarnationId is not null) ||
            pending.Kind == "point_of_focus" && (string.IsNullOrWhiteSpace(pending.ParentIdentifier) ||
                pending.ParentIncarnationId is null || pending.ParentIncarnationId == Uuid.Empty))
            throw new InvalidOperationException("An accepted point of focus needs exactly one parent incarnation.");

        return current with
        {
            Included = true,
            IncarnationId = pending.IncarnationId,
            Kind = pending.Kind,
            Category = pending.Category,
            ParentIdentifier = pending.ParentIdentifier,
            ParentIncarnationId = pending.ParentIncarnationId,
            Summary = pending.Summary,
            Pending = null,
        };
    }

    internal static bool IsEffective(RuntimeCriteriaEntryIndexRow entry, RuntimeCriteriaEntryIndexRow? parent)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!entry.Included)
            return false;
        if (entry.Kind == "criterion")
            return true;
        return entry.Kind == "point_of_focus" && parent is not null && parent.Included &&
            parent.EditionId == entry.EditionId && parent.DraftGeneration == entry.DraftGeneration &&
            parent.Kind == "criterion" && parent.IncarnationId == entry.ParentIncarnationId &&
            string.Equals(parent.Identifier, entry.ParentIdentifier, StringComparison.Ordinal) &&
            string.Equals(parent.Category, entry.Category, StringComparison.Ordinal);
    }
}
