using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>Records an exact summary operation refused at a different current business revision.</summary>
[Discriminator("bdgrz.criteria.summary-refused", 1)]
public sealed record CriterionSummaryRefused(CriterionSummaryIntent Intent, long ExecutionGeneration,
    ActorAttribution Actor, AccessAdmissionEvidence Admission, Uuid OperationClaimEventId,
    long ObservedRevision) : DomainEvent;
