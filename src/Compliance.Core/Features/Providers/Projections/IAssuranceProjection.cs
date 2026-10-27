using Bdgrz.Compliance.Features.Providers.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Projections;

/// <summary>
///     The write side of the assurance directory. The projector resolves each fact's immutable content and applies
///     complete views, so the directory never holds a cumulative parent and never depends on a record count.
/// </summary>
public interface IAssuranceProjection : IProjectionStore
{
    /// <summary>Applies one report revision, retaining the current and every revision view.</summary>
    ValueTask ApplyReportAsync(AssuranceReportView view, CancellationToken ct = default);

    /// <summary>Applies one recorded review.</summary>
    ValueTask ApplyReviewAsync(ProviderReviewView view, CancellationToken ct = default);

    /// <summary>Applies a newly recorded, open gap at revision one.</summary>
    ValueTask ApplyGapRecordedAsync(ProviderCoverageGapView view, CancellationToken ct = default);

    /// <summary>Applies the revision a link admission produced on the gap; the gap stays open.</summary>
    ValueTask ApplyGapAdmissionAsync(Uuid tenantId, Uuid providerId, Uuid gapId, long revision,
        CancellationToken ct = default);

    /// <summary>Applies the single closure of a gap.</summary>
    ValueTask ApplyGapClosedAsync(Uuid tenantId, Uuid providerId, Uuid gapId, long revision,
        ProviderCoverageGapClosureView closure, CancellationToken ct = default);

    /// <summary>Applies one materialized risk acceptance link to its gap without changing the gap's status.</summary>
    ValueTask ApplyGapLinkAsync(Uuid tenantId, Uuid providerId, Uuid gapId,
        ProviderCoverageGapRiskAcceptanceView link, CancellationToken ct = default);
}
