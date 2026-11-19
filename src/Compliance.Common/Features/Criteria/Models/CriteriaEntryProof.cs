using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>Identifies the immutable source facts establishing one entry at a publication cut.</summary>
/// <remarks>
/// <see cref="Entry" /> is the structural entry and product summary as of that cut.
/// Later summary corrections do not change this proof; use the runtime catalog
/// for current display text, and do not treat this summary as a current value.
/// </remarks>
public sealed record CriteriaEntryProof(Criterion Entry, Uuid IncarnationId, Uuid PublicationEventId,
    Uuid ProposalEventId, Uuid DecisionEventId, Uuid? ParentIncarnationId, Uuid? ParentProposalEventId,
    Uuid? ParentDecisionEventId);
