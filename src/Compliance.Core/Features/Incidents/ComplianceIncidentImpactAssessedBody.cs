using Bdgrz.Compliance.Features.Incidents.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Transient local rule delta, never a native aggregate fact.</summary>
public sealed record ComplianceIncidentImpactAssessedBody(Uuid TenantId, Uuid SubjectId, long Revision, Uuid? ActorMemberId, ComplianceIncidentView State, DateTimeOffset OccurredAt);
