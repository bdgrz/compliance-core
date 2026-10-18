using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Records an approved successor or no-change disposition for an incident's description impact.</summary>
[Discriminator("bdgrz.incidents.compliance-incident-system-description-decision-recorded", 1)]
public sealed record ComplianceIncidentSystemDescriptionDecisionRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ComplianceIncidentView State,
    DateTimeOffset OccurredAt) : DomainEvent;
