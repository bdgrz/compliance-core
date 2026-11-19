using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>The accepted outcome of one exact structural proposal.</summary>
[Discriminator("bdgrz.criteria.entry-changed", 1)]
public sealed record CriterionChanged(Uuid EditionId, string Identifier, Uuid OperationId,
    long ExecutionGeneration, Uuid ProposalEventId, CriteriaEntryDecisionEvidence Evidence) : DomainEvent;
