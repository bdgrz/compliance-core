using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class PreviewApplicationImportHandler(
    IApplicationImportDirectoryReader directory, ApplicationImportReadConsistency consistency,
    IAggregateReader reader, IImmutableContentStore content, ApplicationPublicationReader publication, IDomainEventReader events)
    : IRequestHandler<PreviewApplicationImport, Page<ApplicationImportPreviewRow>>
{
    public async ValueTask<Result<Page<ApplicationImportPreviewRow>>> HandleAsync(
        IRequestContext<PreviewApplicationImport> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ApplicationImportPreviewRow>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The import preview limit must be between 1 and 200."));
        var unwrapped = ApplicationPublicationCursor.Unwrap(request.Cursor);
        if (!unwrapped.IsSuccess)
            return Result<Page<ApplicationImportPreviewRow>>.Failure(unwrapped.Error);
        var (carried, innerCursor) = unwrapped.Value;
        var fresh = await consistency.GetFreshAsync(request.TenantId, request.BatchId,
            request.MinimumRevision, ct).ConfigureAwait(false);
        if (!fresh.IsSuccess)
            return Result<Page<ApplicationImportPreviewRow>>.Failure(fresh.Error);
        var loaded = await ApplicationImportOmissionSources.LoadAsync(reader, content, events, request.TenantId,
            request.BatchId, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return Result<Page<ApplicationImportPreviewRow>>.Failure(loaded.Error);
        var (source, view, stagedRows) = loaded.Value;
        var history = await ApplicationImportBatchHistory.ReadAsync(events, source, ct).ConfigureAwait(false);
        if (source.Revision != fresh.Value.Revision)
            return Result<Page<ApplicationImportPreviewRow>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The import lifecycle changed while reading the preview.", isTransient: true));
        var missing = view.Missing(source, stagedRows);
        var claims = view.Claims.ToDictionary(claim => claim.Observation.SourceRecordId, StringComparer.Ordinal);
        var sourcePosition = view.Source.CommittedStreamPosition;
        var cursorError = ApplicationImportPaging.Decode(request.TenantId, request.BatchId,
            fresh.Value.Revision, innerCursor, out var rowsCursor, sourcePosition);
        if (cursorError is not null)
            return Result<Page<ApplicationImportPreviewRow>>.Failure(cursorError);
        Page<ApplicationImportRowView> page;
        try
        {
            page = await directory.ListRowsAsync(request.TenantId, request.BatchId,
                request.Limit ?? 50, rowsCursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ApplicationImportPreviewRow>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The import preview cursor is invalid."));
        }
        var legacyError = ApplicationImportPaging.CheckLegacyRevision(fresh.Value.Revision, innerCursor, sourcePosition);
        if (legacyError is not null)
            return Result<Page<ApplicationImportPreviewRow>>.Failure(legacyError);
        if (page.Items.Any(row => row.TenantId != request.TenantId ||
                                  row.BatchId != request.BatchId) ||
            (innerCursor is null && page.Items.Count == 0 && fresh.Value.RowCount > 0))
            return Result<Page<ApplicationImportPreviewRow>>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The import row projection is incomplete.",
                isTransient: true));
        var items = new List<ApplicationImportPreviewRow>();
        var cut = await publication.OpenAsync(carried, ct).ConfigureAwait(false);
        foreach (var row in page.Items)
        {
            var choice = history.GetCorrelation(row.RowId);
            var raw = stagedRows.FirstOrDefault(candidate => candidate.RowId == row.RowId);
            if (raw is null || raw.RowNumber != row.RowNumber || raw.SourceRecordId != row.SourceRecordId ||
                raw.Name != row.Name || raw.Purpose != row.Purpose || raw.OwnerReference != row.OwnerReference ||
                !raw.ValidationFindings.SequenceEqual(row.ValidationFindings))
                return Result<Page<ApplicationImportPreviewRow>>.Failure(new RequestError(
                    RequestErrorKind.Conflict, "The import row projection differs from its staged source.", isTransient: true));
            var claim = row.SourceRecordId is not null ? claims.GetValueOrDefault(row.SourceRecordId) : null;
            var blockers = new List<string>(row.ValidationFindings);
            if (missing.Count > 0)
                blockers.Add("missing_source_retirement_unavailable");
            if (source.State is "canceled" or "canceling")
                blockers.Add("batch_canceled");
            var targetId = claim?.Observation.ApplicationId ?? choice?.ApplicationId;
            var changed = claim?.ChangedFields(raw) ?? Array.Empty<string>();
            var state = row.ValidationFindings.Contains("duplicate_source_record_id") ? "duplicate" :
                row.ValidationFindings.Count > 0 ? "invalid" : claim is not null ?
                    changed.Count > 0 ? "changed" : "unchanged" : choice is not null ? "new" : "unmatched";
            if (row.ValidationFindings.Count == 0)
            {
                if (claim is null && choice is null)
                    blockers.Add("correlation_required");
                if (claim is not null && choice is not null &&
                    (choice.ApplicationId != claim.Observation.ApplicationId || choice.Decision != "link_existing"))
                {
                    blockers.Add("source_claim_rebinding");
                    state = "conflicting";
                }
                if (claim is not null || choice is { Decision: "link_existing" })
                {
                    var target = await cut.ReadAsync(request.TenantId, targetId!.Value, ct)
                        .ConfigureAwait(false);
                    var expected = choice?.ExpectedApplicationRevision ?? claim!.Observation.ExpectedApplicationRevision ?? 1;
                    if (!target.IsCreated || target.IsRetired || target.Id != targetId ||
                        target.Stream.Realm != request.TenantId.ToString() || target.Revision != expected)
                    {
                        blockers.Add(claim is not null ? "source_claim_target_changed" : "correlation_target_changed");
                        state = "conflicting";
                    }
                }
            }
            items.Add(new ApplicationImportPreviewRow(row.TenantId, row.BatchId, row.RowId,
                row.RowNumber, row.SourceRecordId, row.Name, row.Purpose, row.OwnerReference, row.ValidationFindings,
                state, targetId is { } id ? [id] : [], changed, blockers)
            {
                Correlation = choice is not null
                        ? new ApplicationImportCorrelationView(choice.Decision, choice.ApplicationId,
                            choice.ExpectedApplicationRevision, choice.Reason, choice.ActorMemberId,
                            choice.ActorDisplay, choice.RecordedAt, choice.Revision)
                        : null,
            });
        }
        if (!await ApplicationImportOmissionSources.UnchangedAsync(reader, content, events, request.TenantId, source, view, ct)
                .ConfigureAwait(false))
            return Result<Page<ApplicationImportPreviewRow>>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The import source changed while reading the preview.", isTransient: true));
        var nextCursor = ApplicationPublicationCursor.Wrap(cut.Cut.GlobalWatermarkExclusive,
            ApplicationImportPaging.Encode(request.TenantId, request.BatchId,
                fresh.Value.Revision, page.NextCursor, sourcePosition));
        return Result<Page<ApplicationImportPreviewRow>>.Success(new Page<ApplicationImportPreviewRow>(items, nextCursor));
    }
}
