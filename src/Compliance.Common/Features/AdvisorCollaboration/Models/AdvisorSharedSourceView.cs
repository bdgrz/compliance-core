using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Readiness.Models;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Models;

/// <summary>One exact source view returned only through its active advisor handoff.</summary>
public sealed record AdvisorSharedSourceView(Uuid TenantId, Uuid ProgramId, Uuid HandoffId,
    AdvisorRecordVersion SelectedSource, ControlDraftContent? ControlDraftContent,
    ControlVersionView? ControlVersion, PolicyContent? PolicyDraftContent,
    PolicyVersionView? PolicyVersion, EvidenceArtifactMetadataView? EvidenceArtifact,
    ReadinessGapView? ReadinessGap, BoundaryScopeEntry? BoundaryQuestion);
