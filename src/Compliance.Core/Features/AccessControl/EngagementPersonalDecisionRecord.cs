using Bdgrz.Compliance.Features.AccessControl.Events;
namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The facts of one personal decision on its engagement owner, in the order they were appended. The ordinal is the
///     admission order on the owner; it decides which materialized decision stands for its kind.
/// </summary>
public sealed class EngagementPersonalDecisionRecord(EngagementPersonalDecisionAdmitted admitted, long ordinal)
{
    /// <summary>The root admission, which freezes the decision intent and pins the engagement content.</summary>
    public EngagementPersonalDecisionAdmitted Admitted { get; } = admitted;

    /// <summary>The admission order of this decision on the engagement owner.</summary>
    public long Ordinal { get; } = ordinal;

    /// <summary>The irreversible authorization, once appended.</summary>
    public EngagementPersonalDecisionAuthorized? Authorized { get; internal set; }

    /// <summary>The pre-authorization cancellation, once appended.</summary>
    public EngagementPersonalDecisionCanceled? Canceled { get; internal set; }

    /// <summary>The materialization, once appended; it releases the engagement content pin.</summary>
    public EngagementPersonalDecisionMaterialized? Materialized { get; internal set; }

    /// <summary>True while the decision still pins the engagement content.</summary>
    public bool IsPending => Canceled is null && Materialized is null;
}
