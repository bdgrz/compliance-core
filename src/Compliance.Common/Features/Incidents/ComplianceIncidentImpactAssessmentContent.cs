using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Human incident significance decision and reviewed compliance impacts.</summary>
public sealed record ComplianceIncidentImpactAssessmentContent(
    string Significance,
    string Rationale,
    bool ScopeImpactReviewed,
    bool ControlImpactReviewed,
    bool RiskImpactReviewed,
    bool VendorImpactReviewed,
    IReadOnlyList<string> ImpactDomains,
    IReadOnlyList<ComplianceIncidentAffectedRecordReference> AffectedRecords,
    IReadOnlyList<Uuid> FollowUpWorkIds,
    IReadOnlyList<Uuid> EvidenceArtifactIds,
    bool RequiresSystemDescriptionReview,
    bool CustomerDataAffected,
    bool AvailabilitySlaAffected);
