using Cntryl.Portia;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Incidents.Models;

/// <summary>An immutable significance and impact decision tied to one exact incident version.</summary>
public sealed record ComplianceIncidentImpactAssessmentView(
    int ContentVersion,
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
    bool AvailabilitySlaAffected,
    Uuid AssessedByMemberId,
    string AssessedByDisplay,
    DateTimeOffset AssessedAt);
