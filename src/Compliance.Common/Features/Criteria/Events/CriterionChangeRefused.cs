using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>The durable refusal of one exact structural proposal.</summary>
[Discriminator("bdgrz.criteria.entry-change-refused", 1)]
public sealed record CriterionChangeRefused(Uuid EditionId, string Identifier, Uuid OperationId,
    long ExecutionGeneration, Uuid ProposalEventId, string Reason, CriteriaEntryDecisionEvidence Evidence) : DomainEvent;
