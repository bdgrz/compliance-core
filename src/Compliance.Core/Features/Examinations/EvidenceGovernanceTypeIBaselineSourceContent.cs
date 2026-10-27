using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Retention.Events;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Retention;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

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
