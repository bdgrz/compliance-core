using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.History;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Retention.Events;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Examinations.Models;

sealed record EvidenceGovernanceTypeIBaselineSourceContent(
    DateOnly AsOf,
    Uuid ProgramId,
    Uuid ArtifactId,
    long ArtifactRevision,
    EvidenceArtifactRegistered Registration,
    string ArtifactState,
    string? ArtifactStateReason,
    IReadOnlyList<EvidenceGovernanceSourceEvent> ArtifactHistory,
    IReadOnlyList<EvidenceGovernanceSourceEvent> SupportHistory,
    IReadOnlyList<EvidenceGovernanceSourceEvent> RelatedGovernanceHistory,
    EvidenceHandlingPolicyView? HandlingPolicy,
    ArtifactRetentionBasisRecorded? RetentionBasis,
    IReadOnlyList<ArtifactLegalHoldView> LegalHolds);
