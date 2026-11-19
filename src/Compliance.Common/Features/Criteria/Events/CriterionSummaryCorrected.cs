using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>An admitted correction of one entry's original product-written summary.</summary>
[Discriminator("bdgrz.criteria.summary-corrected", 1)]
public sealed record CriterionSummaryCorrected(CriterionSummaryIntent Intent, long ExecutionGeneration, ActorAttribution Actor,
    AccessAdmissionEvidence Admission, Uuid OperationClaimEventId) : DomainEvent;
