using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;

namespace Bdgrz.Compliance.Features.Risks.Events;

[Discriminator("bdgrz.risk.program_trigger.triaged", 1)]
public sealed record RiskReassessmentTriggerTriaged(Uuid TenantId, Uuid ProgramId, Uuid TriggerId,
    Uuid CommandId, long OriginalExpectedRevision, IReadOnlyList<Uuid> AffectedRiskIds,
    string? ReasonWhenNone, string AuthoredIntentSha256, ActorReference TriagedBy,
    DateTimeOffset TriagedAt) : DomainEvent;
