using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Projectors;

/// <summary>
///     Projects every provider assurance record into the assurance directory. Each fact carries a reference to its
///     complete immutable content; the projector reads and verifies it, so the directory holds complete views and no
///     record count bounds what an owner may accept.
/// </summary>
public sealed partial class AssuranceProjector(IAssuranceProjection projection, IImmutableContentStore content)
    : Projector(projection, EventStreamPattern.ForTenant(ProviderAssuranceStreams.Area), FitzAssuranceDirectory.ProjectorName),
      IProjectorHandler<AssuranceReportRecorded>, IProjectorHandler<AssuranceReportRevised>,
      IProjectorHandler<ProviderReviewRecorded>, IProjectorHandler<ProviderCoverageGapRecorded>,
      IProjectorHandler<ProviderCoverageGapClosed>,
      IProjectorHandler<ProviderCoverageGapRiskAcceptanceAdmitted>,
      IProjectorHandler<ProviderCoverageGapRiskAcceptanceLinked>
{
    public async ValueTask HandleAsync(AssuranceReportRecorded ev, IProjectorContext context, CancellationToken ct) =>
        await projection.ApplyReportAsync(ReportView(ev.TenantId, ev.ReportId, ev.ProviderId, 1,
            await ReadAsync<AssuranceReportContent>(ev.TenantId, ev.ContentContent, ct).ConfigureAwait(false),
            ev.ProviderRevision, ev.Actor, ev.RecordedAt), ct).ConfigureAwait(false);

    public async ValueTask HandleAsync(AssuranceReportRevised ev, IProjectorContext context, CancellationToken ct) =>
        await projection.ApplyReportAsync(ReportView(ev.TenantId, ev.ReportId, ev.ProviderId, ev.Revision,
            await ReadAsync<AssuranceReportContent>(ev.TenantId, ev.ContentContent, ct).ConfigureAwait(false),
            ev.ProviderRevision, ev.Actor, ev.RecordedAt), ct).ConfigureAwait(false);

    public async ValueTask HandleAsync(ProviderReviewRecorded ev, IProjectorContext context, CancellationToken ct)
    {
        var reviewContent = await ReadAsync<ProviderReviewContent>(ev.TenantId, ev.ContentContent, ct)
            .ConfigureAwait(false);
        await projection.ApplyReviewAsync(new ProviderReviewView(ev.TenantId, ev.ReviewId, ev.ProviderId,
            AssuranceRules.Normalize(reviewContent), ev.ProviderRevision, ev.AssuranceReportRevision, ev.Actor,
            ev.RecordedAt), ct).ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(ProviderCoverageGapRecorded ev, IProjectorContext context,
        CancellationToken ct)
    {
        var gapContent = await ReadAsync<ProviderCoverageGapContent>(ev.TenantId, ev.ContentContent, ct)
            .ConfigureAwait(false);
        if (ProviderCoverageGapRules.InputError(gapContent) is not null || ev.RequestId == Uuid.Empty)
            throw new InvalidOperationException("A provider coverage gap event must have valid facts and a request identity.");
        await projection.ApplyGapRecordedAsync(new ProviderCoverageGapView(ev.TenantId, ev.GapId, ev.ProviderId,
            ev.ProviderRevision, 1, ProviderCoverageGapRules.Normalize(gapContent), "open", ev.Actor, ev.RecordedAt,
            null, []), ct).ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(ProviderCoverageGapClosed ev, IProjectorContext context,
        CancellationToken ct)
    {
        var closure = await ReadAsync<ProviderCoverageGapClosureContent>(ev.TenantId, ev.ContentContent, ct)
            .ConfigureAwait(false);
        await projection.ApplyGapClosedAsync(ev.TenantId, ev.ProviderId, ev.GapId, ev.Revision,
            new ProviderCoverageGapClosureView(closure, ev.Actor, ev.ClosedAt) { Revision = ev.Revision }, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(ProviderCoverageGapRiskAcceptanceAdmitted ev, IProjectorContext context,
        CancellationToken ct)
    {
        var tenantId = ProviderReviewEffects.TenantOf(ev.Owner);
        var intent = await ReadAsync<ProviderRiskLinkIntent>(tenantId, ev.Intent, ct).ConfigureAwait(false);
        await projection.ApplyGapAdmissionAsync(tenantId, intent.ProviderId, intent.GapId, ev.Revision, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(ProviderCoverageGapRiskAcceptanceLinked ev, IProjectorContext context,
        CancellationToken ct)
    {
        var link = await ReadAsync<ProviderCoverageGapRiskAcceptanceView>(ev.TenantId, ev.AcceptanceContent, ct)
            .ConfigureAwait(false);
        await projection.ApplyGapLinkAsync(ev.TenantId, ev.ProviderId, ev.GapId, link, ct).ConfigureAwait(false);
    }

    Task<T> ReadAsync<T>(Uuid tenantId, ImmutableContentRef reference, CancellationToken ct) where T : notnull =>
        ProviderAssuranceContent.ReadAsync<T>(content, tenantId, reference, ct);

    static AssuranceReportView ReportView(Uuid tenantId, Uuid reportId, Uuid providerId, long revision,
        AssuranceReportContent reportContent, long providerRevision,
        Bdgrz.Compliance.Features.AccessControl.Models.ActorReference actor, DateTimeOffset recordedAt)
    {
        reportContent = AssuranceRules.Normalize(reportContent);
        return new AssuranceReportView(tenantId, reportId, providerId, revision, reportContent, providerRevision,
            reportContent.Exceptions!.Count, reportContent.ComplementaryControls!.Count,
            reportContent.CoverageGaps!.Count, actor, recordedAt);
    }
}
