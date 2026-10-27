using System.Text.Json;
using Cntryl.Fitz;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Providers.Readers;

sealed class FitzAssuranceDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/provider-assurance-v1/projection", ProjectorName), IAssuranceReader, IAssuranceProjection
{
    public const string ProjectorName = "ProviderAssuranceV3";
    Uuid? _batchTenantId;

    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context,
        CancellationToken ct = default) => BeginAssuranceBatchAsync(context, ct);

    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context,
        CancellationToken ct) => BeginAssuranceBatchAsync(context, ct);

    async ValueTask<IProjectionBatch> BeginAssuranceBatchAsync(ProjectionBatchContext context, CancellationToken ct)
    {
        if (context.Identity.Pattern.Area != ProviderAssuranceStreams.Area ||
            context.Identity.Pattern.Resource is not null ||
            !Uuid.TryParse(context.Identity.Pattern.Realm, null, out var tenantId) || tenantId == Uuid.Empty)
            throw new InvalidOperationException("An assurance projection batch requires its tenant's assurance area.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _batchTenantId = tenantId;
        return batch;
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), ProviderAssuranceStreams.Area)), ct);

    public async ValueTask<AssuranceReportView?> GetReportRevisionAsync(Uuid tenantId, Uuid providerId,
        Uuid reportId, long revision, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var view = await AssuranceDirectorySchema.ReportRevisions.GetAsync(tx,
            AssuranceDirectorySchema.ReportRevisionKey(providerId, reportId, revision), ct).ConfigureAwait(false);
        return view?.TenantId == tenantId && view.ProviderId == providerId && view.ReportId == reportId &&
               view.Revision == revision
            ? view
            : null;
    }

    public async ValueTask<ProviderReviewView?> GetReviewAsync(Uuid tenantId, Uuid reviewId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var review = await AssuranceDirectorySchema.Reviews.GetAsync(tx, reviewId, ct).ConfigureAwait(false);
        return review?.TenantId == tenantId && review.ReviewId == reviewId ? review : null;
    }

    public async ValueTask<ProviderCoverageGapView?> GetCoverageGapAsync(Uuid tenantId, Uuid providerId,
        Uuid gapId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var gap = await AssuranceDirectorySchema.CoverageGaps.GetAsync(tx,
            AssuranceDirectorySchema.RecordKey(providerId, gapId), ct).ConfigureAwait(false);
        return gap?.TenantId == tenantId && gap.ProviderId == providerId && gap.GapId == gapId ? gap : null;
    }

    public async ValueTask<Page<ProviderCoverageGapView>> ListCoverageGapsAsync(Uuid tenantId,
        Uuid providerId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await AssuranceDirectorySchema.CoverageGaps.QueryAsync(tx,
            AssuranceDirectorySchema.CoverageGapsByProvider.Query()
                .WithPrefix(providerId.ToString()).Take(limit).After(cursor), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<AssuranceReportView>> ListReportsAsync(Uuid tenantId, Uuid providerId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await AssuranceDirectorySchema.Reports.QueryAsync(tx,
            AssuranceDirectorySchema.ReportsByProvider.Query().WithPrefix(providerId.ToString()).Take(limit).After(cursor), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<ProviderReviewView>> ListReviewsAsync(Uuid tenantId, Uuid providerId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await AssuranceDirectorySchema.Reviews.QueryAsync(tx,
            AssuranceDirectorySchema.ReviewsByProvider.Query().WithPrefix(providerId.ToString()).Take(limit).After(cursor), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask ApplyReportAsync(AssuranceReportView view, CancellationToken ct = default)
    {
        RequireBatchTenant(view.TenantId, view.ProviderId, view.ReportId);
        var revisionKey = AssuranceDirectorySchema.ReportRevisionKey(view.ProviderId, view.ReportId, view.Revision);
        var retainedRevision = await AssuranceDirectorySchema.ReportRevisions.GetAsync(
            Transaction, revisionKey, ct).ConfigureAwait(false);
        if (retainedRevision is not null && !Same(retainedRevision, view))
            throw new InvalidOperationException("An assurance report revision cannot replace retained content.");
        var current = await AssuranceDirectorySchema.Reports.GetAsync(Transaction,
            AssuranceDirectorySchema.RecordKey(view.ProviderId, view.ReportId), ct).ConfigureAwait(false);
        if (current is not null && current.Revision >= view.Revision)
        {
            // A replayed revision is idempotent only when it matches the retained current or a superseded fact.
            if (current.Revision == view.Revision && !Same(current, view))
                throw new InvalidOperationException("An assurance report revision cannot replace retained content.");
            if (retainedRevision is null)
                await AssuranceDirectorySchema.ReportRevisions.InsertAsync(Transaction, view, ct)
                    .ConfigureAwait(false);
            return;
        }
        if (current is null ? view.Revision != 1 : current.TenantId != view.TenantId ||
                current.ProviderId != view.ProviderId || current.Revision + 1 != view.Revision)
            throw new InvalidOperationException("An assurance report revision requires its provider's immediate predecessor.");
        if (current is null)
            await AssuranceDirectorySchema.Reports.InsertAsync(Transaction, view, ct).ConfigureAwait(false);
        else
            await AssuranceDirectorySchema.Reports.ReplaceAsync(Transaction, current, view, ct).ConfigureAwait(false);
        if (retainedRevision is null)
            await AssuranceDirectorySchema.ReportRevisions.InsertAsync(Transaction, view, ct)
                .ConfigureAwait(false);
    }

    public async ValueTask ApplyReviewAsync(ProviderReviewView view, CancellationToken ct = default)
    {
        RequireBatchTenant(view.TenantId, view.ProviderId, view.ReviewId);
        var retained = await AssuranceDirectorySchema.Reviews.GetAsync(Transaction, view.ReviewId, ct).ConfigureAwait(false);
        if (retained is not null)
        {
            if (!Same(retained, view))
                throw new InvalidOperationException("A provider review cannot replace retained content.");
            return;
        }
        await AssuranceDirectorySchema.Reviews.InsertAsync(Transaction, view, ct).ConfigureAwait(false);
    }

    public async ValueTask ApplyGapRecordedAsync(ProviderCoverageGapView view, CancellationToken ct = default)
    {
        RequireBatchTenant(view.TenantId, view.ProviderId, view.GapId);
        if (view.ProviderRevision < 1 || view.Revision != 1 || view.Status != "open" || view.Closure is not null)
            throw new InvalidOperationException("A new provider coverage gap must start open at revision one.");
        var retained = await AssuranceDirectorySchema.CoverageGaps.GetAsync(Transaction,
            AssuranceDirectorySchema.RecordKey(view.ProviderId, view.GapId), ct).ConfigureAwait(false);
        if (retained is not null)
        {
            if (!SameCoverageGapRecord(retained, view))
                throw new InvalidOperationException("A provider coverage gap cannot replace retained content.");
            return;
        }
        await AssuranceDirectorySchema.CoverageGaps.InsertAsync(Transaction, view, ct).ConfigureAwait(false);
    }

    public async ValueTask ApplyGapAdmissionAsync(Uuid tenantId, Uuid providerId, Uuid gapId, long revision,
        CancellationToken ct = default)
    {
        RequireBatchTenant(tenantId, providerId, gapId);
        var current = await RetainedGapAsync(tenantId, providerId, gapId, ct).ConfigureAwait(false);
        if (current.Revision >= revision)
            return;
        if (current.Status != "open" || current.Revision + 1 != revision)
            throw new InvalidOperationException("A link admission must immediately follow an open gap revision.");
        await AssuranceDirectorySchema.CoverageGaps.ReplaceAsync(Transaction, current,
            current with { Revision = revision }, ct).ConfigureAwait(false);
    }

    public async ValueTask ApplyGapClosedAsync(Uuid tenantId, Uuid providerId, Uuid gapId, long revision,
        ProviderCoverageGapClosureView closure, CancellationToken ct = default)
    {
        RequireBatchTenant(tenantId, providerId, gapId);
        if (ProviderCoverageGapRules.ClosureInputError(closure.Content) is not null)
            throw new InvalidOperationException("A provider coverage gap closure requires valid resolution evidence.");
        var current = await RetainedGapAsync(tenantId, providerId, gapId, ct).ConfigureAwait(false);
        var closed = current with
        {
            Revision = revision,
            Status = "closed",
            Closure = closure with { Content = ProviderCoverageGapRules.Normalize(closure.Content), Revision = revision },
        };
        if (current.Revision >= revision)
        {
            if (current.Revision == revision && !Same(current, closed))
                throw new InvalidOperationException("A provider coverage gap closure cannot replace retained content.");
            return;
        }
        if (current.Status != "open" || current.Revision + 1 != revision)
            throw new InvalidOperationException("A provider coverage gap closure must immediately follow an open revision.");
        await AssuranceDirectorySchema.CoverageGaps.ReplaceAsync(Transaction, current, closed, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask ApplyGapLinkAsync(Uuid tenantId, Uuid providerId, Uuid gapId,
        ProviderCoverageGapRiskAcceptanceView link, CancellationToken ct = default)
    {
        RequireBatchTenant(tenantId, providerId, gapId);
        if (link.ProgramId == Uuid.Empty || link.RiskId == Uuid.Empty || link.AcceptanceId == Uuid.Empty ||
            link.ExpiresAt <= link.LinkedAt)
            throw new InvalidOperationException("A provider coverage gap link must be a time-bounded decision.");
        var current = await RetainedGapAsync(tenantId, providerId, gapId, ct).ConfigureAwait(false);
        var links = current.RiskAcceptances ?? [];
        if (links.Any(item => item.RiskId == link.RiskId && item.AcceptanceId == link.AcceptanceId))
        {
            if (links.Any(item => item == link))
                return;
            throw new InvalidOperationException("A provider coverage gap link cannot replace retained content.");
        }
        // The admission already produced the gap revision; a link neither advances it nor changes the status.
        await AssuranceDirectorySchema.CoverageGaps.ReplaceAsync(Transaction, current,
            current with { RiskAcceptances = [.. links, link] }, ct).ConfigureAwait(false);
    }

    async ValueTask<ProviderCoverageGapView> RetainedGapAsync(Uuid tenantId, Uuid providerId, Uuid gapId,
        CancellationToken ct)
    {
        var current = await AssuranceDirectorySchema.CoverageGaps.GetAsync(Transaction,
            AssuranceDirectorySchema.RecordKey(providerId, gapId), ct).ConfigureAwait(false);
        return current is null || current.TenantId != tenantId || current.ProviderId != providerId
            ? throw new InvalidOperationException("A provider coverage gap fact requires its retained gap.")
            : current;
    }

    void RequireBatchTenant(Uuid tenantId, Uuid providerId, Uuid recordId)
    {
        if (tenantId == Uuid.Empty || providerId == Uuid.Empty || recordId == Uuid.Empty || tenantId != _batchTenantId)
            throw new InvalidOperationException("An assurance event must belong to its tenant projection batch.");
    }

    static bool Same(AssuranceReportView left, AssuranceReportView right) =>
        JsonSerializer.SerializeToUtf8Bytes(left, ComplianceCoreJsonContext.Default.AssuranceReportView).AsSpan()
            .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(right, ComplianceCoreJsonContext.Default.AssuranceReportView));

    static bool Same(ProviderReviewView left, ProviderReviewView right) =>
        JsonSerializer.SerializeToUtf8Bytes(left, ComplianceCoreJsonContext.Default.ProviderReviewView).AsSpan()
            .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(right, ComplianceCoreJsonContext.Default.ProviderReviewView));

    static bool Same(ProviderCoverageGapView left, ProviderCoverageGapView right) =>
        JsonSerializer.SerializeToUtf8Bytes(left,
            ComplianceCoreJsonContext.Default.ProviderCoverageGapView).AsSpan()
            .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(right,
                ComplianceCoreJsonContext.Default.ProviderCoverageGapView));

    static bool SameCoverageGapRecord(ProviderCoverageGapView retained,
        ProviderCoverageGapView recorded) => retained.TenantId == recorded.TenantId &&
        retained.GapId == recorded.GapId && retained.ProviderId == recorded.ProviderId &&
        retained.ProviderRevision == recorded.ProviderRevision && retained.Content == recorded.Content &&
        retained.RecordedBy == recorded.RecordedBy && retained.RecordedAt == recorded.RecordedAt;
}
