using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Records the ComplianceIncidentImpactAssessed domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.incidents.compliance-incident-impact-assessed", 2)]
public sealed record ComplianceIncidentImpactAssessed(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ComplianceIncidentView State,
    DateTimeOffset OccurredAt) : DomainEvent;
