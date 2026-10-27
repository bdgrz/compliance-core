using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Queries;
using Bdgrz.Compliance.Features.AuditorRequests.Readers;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Handlers;

public sealed class ListAuditorRequestsHandler(IAuditorRequestDirectoryReader directory,
    AuditorRequestDirectoryReadConsistency consistency, AuditorRequestReader requests)
    : IRequestHandler<ListAuditorRequests, Page<AuditorRequestSummaryView>>
{
    public ValueTask<Result<Page<AuditorRequestSummaryView>>> HandleAsync(IRequestContext<ListAuditorRequests> context,
        CancellationToken ct) => AuditorRequestRead.ClassifyAsync<Page<AuditorRequestSummaryView>>(async () =>
        {
            var request = context.Request;
            if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty)
                return Result<Page<AuditorRequestSummaryView>>.Failure(AuditorRequestRead.InvalidIdentity());
            if (request.Limit is < 1 or > 200)
                return Fail(new RequestError(RequestErrorKind.Validation, "Limit must be between 1 and 200."));
            var limit = request.Limit ?? 50;
            if (!AuditorRequestDirectoryCursor.TryUnwrap(request.TenantId, request.ProgramId, limit, request.Cursor, out var cursor))
                return Fail(new RequestError(RequestErrorKind.Validation, "The auditor-request cursor is invalid for this scope or page size."));
            var fence = await consistency.CaptureAsync(request.TenantId, ct).ConfigureAwait(false);
            if (!fence.IsSuccess)
                return Fail(fence.Error!);
            Page<AuditorRequestSummaryView> identities;
            try
            {
                identities = await directory.ListProgramAsync(request.TenantId, request.ProgramId, limit,
                    cursor, ct).ConfigureAwait(false);
            }
            catch (KvDirectoryQueryException)
            {
                return Fail(new RequestError(RequestErrorKind.Validation, "The auditor-request cursor is invalid."));
            }
            if (identities.Items.Any(row => row.TenantId != request.TenantId || row.ProgramId != request.ProgramId ||
                row.AuditorRequestId == Uuid.Empty) ||
                identities.Items.Select(static row => row.AuditorRequestId).Distinct().Count() != identities.Items.Count)
                return Fail(new RequestError(RequestErrorKind.Conflict, "The auditor-request directory contains another or duplicate scope."));
            var views = new List<AuditorRequestSummaryView>(identities.Items.Count);
            foreach (var identity in identities.Items)
            {
                var snapshot = await requests.ReadAsync(request.TenantId, request.ProgramId, identity.AuditorRequestId, ct)
                    .ConfigureAwait(false);
                if (snapshot.View is not { } view)
                    return Fail(new RequestError(RequestErrorKind.Conflict, "A listed request is not visible at its exact native scope.", true));
                if (view.TenantId != request.TenantId || view.ProgramId != request.ProgramId || view.AuditorRequestId != identity.AuditorRequestId)
                    return Fail(new RequestError(RequestErrorKind.Conflict, "A listed request has another exact native owner."));
                views.Add(new AuditorRequestSummaryView(view.TenantId, view.ProgramId, view.AuditorRequestId,
                    view.Wording, view.Status, view.UpdatedAt));
            }
            var confirmed = await consistency.ConfirmAsync(request.TenantId, fence.Value, ct).ConfigureAwait(false);
            return confirmed.IsSuccess
                ? Result<Page<AuditorRequestSummaryView>>.Success(new Page<AuditorRequestSummaryView>(views, AuditorRequestDirectoryCursor.Wrap(
                    request.TenantId, request.ProgramId, limit, identities.NextCursor)))
                : Fail(confirmed.Error!);
        });

    static Result<Page<AuditorRequestSummaryView>> Fail(RequestError error) => Result<Page<AuditorRequestSummaryView>>.Failure(error);
}
