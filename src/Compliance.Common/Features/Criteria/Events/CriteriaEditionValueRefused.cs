using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>The refused decision for one exact value proposal.</summary>
[Discriminator("bdgrz.criteria.edition-value-refused", 1)]
public sealed record CriteriaEditionValueRefused(Uuid EditionId, Uuid OperationId, long ExecutionGeneration,
    Uuid ProposalEventId, string Reason, CriteriaEditionValueDecisionEvidence Evidence) : DomainEvent;
