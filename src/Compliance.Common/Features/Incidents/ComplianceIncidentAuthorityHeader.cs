using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Bounded current incident authority; exact version/decision bodies and chronology remain immutable content.</summary>
public sealed record ComplianceIncidentAuthorityHeader(Uuid TenantId, Uuid ProgramId, Uuid IncidentId, long Revision,
    int CurrentVersionNumber, string ContentSha256, string Title, string Status, string HandlingClass, DateTimeOffset OccurredAt,
    DateTimeOffset DetectedAt, Uuid OwnerMemberId, Uuid ActorMemberId, string ActorDisplay, DateTimeOffset UpdatedAt,
    bool PeriodCloseEligible, string? CurrentAssessmentSha256, Uuid? CurrentAssessorMemberId, bool RequiresSystemDescriptionReview,
    string AssessmentHistorySha256, string HistorySha256);
