using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>Fixes one entry operation identity to an authored intent and actor.</summary>
/// <remarks>Exactly one of the structural and summary intents is present.</remarks>
[Discriminator("bdgrz.criteria.entry-operation-claimed", 1)]
public sealed record CriterionEntryOperationClaimed(CriterionChangeIntent? Structural,
    CriterionSummaryIntent? Summary, long ExecutionGeneration, ActorAttribution Actor,
    AccessAdmissionEvidence Admission) : DomainEvent;
