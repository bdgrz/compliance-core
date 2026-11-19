using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>The accepted decision for one exact value proposal.</summary>
[Discriminator("bdgrz.criteria.edition-value-accepted", 1)]
public sealed record CriteriaEditionValueAccepted(Uuid EditionId, Uuid OperationId, long ExecutionGeneration,
    Uuid ProposalEventId, CriteriaEditionValueDecisionEvidence Evidence) : DomainEvent;
