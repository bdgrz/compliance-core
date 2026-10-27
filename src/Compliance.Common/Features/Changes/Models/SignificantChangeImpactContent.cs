using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes.Models;

/// <summary>Human significance decision and exact compliance impact references.</summary>
public sealed record SignificantChangeImpactContent(
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
    bool RequiresSystemDescriptionReview);
