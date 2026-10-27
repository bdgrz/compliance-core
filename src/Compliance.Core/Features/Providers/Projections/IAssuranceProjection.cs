using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Providers.Models;
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
