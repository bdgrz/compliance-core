using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>An immutable significance and impact decision for one exact change version.</summary>
public sealed record SignificantChangeImpactView(
    int ContentVersion,
    string Significance,
    string Rationale,
    bool ChangesSystemBoundary,
    bool ChangesServiceCommitment,
    bool ChangesKeyControl,
    bool ChangesInScopeInfrastructure,
    IReadOnlyList<string> ImpactDomains,
    IReadOnlyList<SignificantChangeAffectedRecordReference> AffectedRecords,
    IReadOnlyList<Uuid> RelatedReviewWorkIds,
    IReadOnlyList<Uuid> RelatedCorrectiveActionIds,
    IReadOnlyList<Uuid> RelatedExceptionIds,
    IReadOnlyList<Uuid> EvidenceArtifactIds,
    bool RequiresSystemDescriptionReview,
    string SystemDescriptionStatus,
    Uuid AssessedByMemberId,
    string AssessedByDisplay,
    DateTimeOffset AssessedAt);
