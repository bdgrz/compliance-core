using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>
/// A bounded observation, not an approval or complete retirement clearance. Boundary, control,
/// policy, evidence-source, access-population, review-campaign, provider, and readiness collections
/// include direct application and indexed system-instance references when their projections are
/// current and the caller has the required access.
/// </summary>
public sealed record ApplicationChangePreview(Uuid TenantId, Uuid ApplicationId,
    long ApplicationRevision, string ChangeKind,
    IReadOnlyList<ApplicationFieldChange> Changes,
    IReadOnlyList<ApplicationBoundaryReferenceView> BoundaryReferences,
    IReadOnlyList<string> PendingContexts, bool Complete)
{
    /// <summary>Digest of the normalized, bounded evidence and pending context set.</summary>
    public string ImpactDigest { get; init; } = string.Empty;

    /// <summary>Up to 200 visible outgoing direct relationships.</summary>
    public IReadOnlyList<ApplicationRelationshipView> ApplicationRelationships { get; init; } = [];

    /// <summary>Up to 200 visible direct relationships pointing to this application.</summary>
    public IReadOnlyList<ApplicationIncomingRelationshipView>
        IncomingApplicationRelationships
    { get; init; } = [];

    /// <summary>Current control draft references to the application or one of its system instances.</summary>
    public IReadOnlyList<ApplicationControlDraftReferenceView> ControlDraftReferences { get; init; } = [];

    /// <summary>Approved control versions and lifecycle facts for the application and its systems.</summary>
    public IReadOnlyList<ApplicationControlVersionReferenceView> ApprovedControlReferences { get; init; } = [];

    /// <summary>Direct policy applicability and lifecycle facts for the application and its systems.</summary>
    public IReadOnlyList<ApplicationPolicyReferenceView> PolicyReferences { get; init; } = [];

    /// <summary>Evidence support metadata for affected controls, filtered by exact artifact read access.</summary>
    public IReadOnlyList<ApplicationEvidenceSourceReferenceView> EvidenceSourceReferences { get; init; } = [];

    /// <summary>Minimal population history for each affected reviewed system.</summary>
    public IReadOnlyList<ApplicationAccessPopulationReferenceView> AccessPopulationReferences { get; init; } = [];

    /// <summary>Minimal review-campaign status for each affected reviewed system.</summary>
    public IReadOnlyList<ApplicationReviewCampaignReferenceView> ReviewCampaignReferences { get; init; } = [];

    /// <summary>Up to 200 visible provider dependencies on this application's systems.</summary>
    public IReadOnlyList<ApplicationVendorReferenceView> VendorReferences { get; init; } = [];

    /// <summary>Up to 200 source references from current readiness assessments.</summary>
    public IReadOnlyList<ApplicationReadinessReferenceView> ReadinessReferences { get; init; } = [];

    /// <summary>Up to 200 projected system instances that a change or retirement would affect.</summary>
    public IReadOnlyList<SystemInstanceView> SystemInstanceReferences { get; init; } = [];
}
