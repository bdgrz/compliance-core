using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>A same-stream fence against an unresolved summary correction attempt.</summary>
[Discriminator("bdgrz.criteria.summary-attempt-fenced", 1)]
public sealed record CriterionSummaryAttemptFenced(CriterionSummaryIntent Intent, long ExecutionGeneration,
    ActorAttribution Actor, AccessAdmissionEvidence Admission) : DomainEvent;
