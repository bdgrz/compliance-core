using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>Records one exact admitted summary operation that matched the current text and revision.</summary>
[Discriminator("bdgrz.criteria.summary-unchanged", 1)]
public sealed record CriterionSummaryUnchanged(CriterionSummaryIntent Intent, long ExecutionGeneration,
    ActorAttribution Actor, AccessAdmissionEvidence Admission, Uuid OperationClaimEventId) : DomainEvent;
