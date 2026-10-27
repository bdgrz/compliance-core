using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Controls.Models;

/// <summary>
///     Downstream effects of one pending successor or retirement. The digest binds a decision to
///     this exact preview; it does not infer criteria coverage, risk treatment, operating
///     effectiveness, or an audit conclusion.
/// </summary>
/// <param name="Kind">successor or retirement.</param>
/// <param name="TargetId">The successor draft version ID or the retirement proposal ID.</param>
public sealed record ControlImpactPreview(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    string Kind, Uuid TargetId, long Revision, Uuid? CurrentVersionId,
    IReadOnlyList<ControlChange> Changes,
    IReadOnlyList<ControlImpactContribution> Contributions,
    IReadOnlyList<string> PendingContexts, bool Complete, string Digest);
