using Bdgrz.Compliance.Features.Changes;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

public sealed class ListComplianceIncidentsHandler(IComplianceIncidentDirectoryReader directory,
    RestrictedComplianceRecordVisibility visibility, ComplianceIncidentDirectoryReadConsistency consistency)
    : IRequestHandler<ListComplianceIncidents, Page<ComplianceIncidentSummaryView>>
{
    public async ValueTask<Result<Page<ComplianceIncidentSummaryView>>> HandleAsync(
        IRequestContext<ListComplianceIncidents> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty)
            return Result<Page<ComplianceIncidentSummaryView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "Tenant and program identities are required."));
        if (request.Limit is < 1 or > 200)
            return Result<Page<ComplianceIncidentSummaryView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The compliance-incident list limit must be between 1 and 200."));

        var fence = await consistency.CaptureAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<Page<ComplianceIncidentSummaryView>>.Failure(fence.Error!);
        var items = new List<ComplianceIncidentSummaryView>(request.Limit ?? 50);
        var cursor = request.Cursor;
        string? nextCursor = null;
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);
        if (cursor is not null)
            seenCursors.Add(cursor);
        try
        {
            while (items.Count < (request.Limit ?? 50))
            {
                var sourcePage = await directory.ListAsync(request.TenantId, request.ProgramId,
                    1, cursor, ct).ConfigureAwait(false);
                if (sourcePage.Items.Count == 0)
                {
                    nextCursor = null;
                    break;
                }
                if (sourcePage.Items.Count != 1)
                    throw new InvalidOperationException("A one-row compliance-incident read returned multiple rows.");

                var summary = sourcePage.Items[0];
                if (summary.TenantId != request.TenantId || summary.ProgramId != request.ProgramId)
                    return Result<Page<ComplianceIncidentSummaryView>>.Failure(new RequestError(
                        RequestErrorKind.NotFound, "The compliance incidents were not found."));
                var detail = await directory.GetAsync(request.TenantId,
                    summary.ComplianceIncidentId, ct).ConfigureAwait(false);
                if (detail is not { Current: { } current } || detail.TenantId != request.TenantId ||
                    detail.ProgramId != request.ProgramId ||
                    detail.ComplianceIncidentId != summary.ComplianceIncidentId)
                    return Result<Page<ComplianceIncidentSummaryView>>.Failure(new RequestError(
                        RequestErrorKind.NotFound, "The compliance incidents were not found."));

                if (await visibility.CanReadAsync(request.TenantId, context.Actor,
                        current.Content.HandlingClass,
                        detail.Assessments.Select(static assessment => assessment.AssessedByMemberId).ToArray(), ct)
                    .ConfigureAwait(false))
                    items.Add(new ComplianceIncidentSummaryView(detail.TenantId, detail.ProgramId, detail.ComplianceIncidentId,
                        current.Content.Title, detail.Status, detail.UpdatedAt));

                cursor = sourcePage.NextCursor;
                if (cursor is not null && !seenCursors.Add(cursor))
                    throw new InvalidOperationException("The compliance-incident directory cursor did not advance.");
                nextCursor = cursor;
                if (cursor is null)
                    break;
            }
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<Page<ComplianceIncidentSummaryView>>.Failure(IncidentOperations.ContentFailure(exception));
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ComplianceIncidentSummaryView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The compliance-incident cursor is invalid."));
        }

        try
        {
            if (items.Count == (request.Limit ?? 50) && nextCursor is not null &&
                !await HasVisibleAfterAsync(request.TenantId, request.ProgramId, context.Actor,
                    nextCursor, ct).ConfigureAwait(false))
                nextCursor = null;

            var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(request.TenantId, fence.Value, ct).ConfigureAwait(false);
            if (!confirmed.IsSuccess)
                return Result<Page<ComplianceIncidentSummaryView>>.Failure(confirmed.Error!);
            return Result<Page<ComplianceIncidentSummaryView>>.Success(
                new Page<ComplianceIncidentSummaryView>(items, nextCursor));
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ComplianceIncidentSummaryView>>.Failure(new RequestError(RequestErrorKind.Validation, "The compliance-incident cursor is invalid."));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<Page<ComplianceIncidentSummaryView>>.Failure(IncidentOperations.ContentFailure(exception));
        }
    }

    async ValueTask<bool> HasVisibleAfterAsync(Uuid tenantId, Uuid programId,
        System.Security.Claims.ClaimsPrincipal actor, string cursor, CancellationToken ct)
    {
        var seenCursors = new HashSet<string>(StringComparer.Ordinal) { cursor };
        while (true)
        {
            var page = await directory.ListAsync(tenantId, programId, 1, cursor, ct)
                .ConfigureAwait(false);
            if (page.Items.Count == 0)
                return false;
            if (page.Items.Count != 1)
                throw new InvalidOperationException("A one-row compliance-incident read returned multiple rows.");
            var summary = page.Items[0];
            if (summary.TenantId != tenantId || summary.ProgramId != programId)
                throw new ImmutableContentIntegrityException("The change discovery lookahead returned another scope.");
            var detail = await directory.GetAsync(tenantId, summary.ComplianceIncidentId, ct)
                .ConfigureAwait(false);
            if (detail is not { Current: { } current } || detail.TenantId != tenantId ||
                detail.ProgramId != programId || detail.ComplianceIncidentId != summary.ComplianceIncidentId)
                throw new ImmutableContentUnavailableException("A listed native change is not readable yet.");
            if (await visibility.CanReadAsync(tenantId, actor, current.Content.HandlingClass,
                    detail.Assessments.Select(static assessment => assessment.AssessedByMemberId).ToArray(), ct)
                .ConfigureAwait(false))
                return true;
            if (page.NextCursor is null)
                return false;
            if (!seenCursors.Add(page.NextCursor))
                throw new InvalidOperationException("The compliance-incident directory cursor did not advance.");
            cursor = page.NextCursor;
        }
    }
}
