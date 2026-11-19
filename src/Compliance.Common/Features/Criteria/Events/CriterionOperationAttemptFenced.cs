using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>A recovering intent fences stale attempts at this entry's stream position.</summary>
[Discriminator("bdgrz.criteria.entry-attempt-fenced", 1)]
public sealed record CriterionOperationAttemptFenced(CriterionChangeIntent Intent, long ExecutionGeneration,
    ActorAttribution Actor, AccessAdmissionEvidence Admission) : DomainEvent;
