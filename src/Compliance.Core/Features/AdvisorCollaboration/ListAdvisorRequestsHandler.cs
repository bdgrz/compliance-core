using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

public sealed class ListAdvisorRequestsHandler(IAdvisorHandoffDirectoryReader directory,
    AdvisorHandoffListReadConsistency consistency)
    : IRequestHandler<ListAdvisorRequests, Page<AdvisorRequestSummaryView>>
{
    const int DefaultLimit = 50;

    public async ValueTask<Result<Page<AdvisorRequestSummaryView>>> HandleAsync(
        IRequestContext<ListAdvisorRequests> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.Limit is < 1 or > 200)
            return Invalid();

        var fence = await consistency.CaptureAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<Page<AdvisorRequestSummaryView>>.Failure(fence.Error);

        Page<AdvisorRequestSummaryView> page;
        try
        {
            page = await directory.ListProgramAsync(request.TenantId, request.ProgramId,
                request.Limit ?? DefaultLimit, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<AdvisorRequestSummaryView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The advisor handoff cursor is invalid."));
        }

        if (page.Items.Any(item => item.TenantId != request.TenantId ||
                                   item.ProgramId != request.ProgramId))
            return Result<Page<AdvisorRequestSummaryView>>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "The advisor handoff projection has an invalid program scope."));

        var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(request.TenantId,
            fence.Value, ct).ConfigureAwait(false);
        return confirmed.IsSuccess
            ? Result<Page<AdvisorRequestSummaryView>>.Success(page)
            : Result<Page<AdvisorRequestSummaryView>>.Failure(confirmed.Error);
    }

    static Result<Page<AdvisorRequestSummaryView>> Invalid() =>
        Result<Page<AdvisorRequestSummaryView>>.Failure(new RequestError(RequestErrorKind.Validation,
            "Advisor handoff lists require tenant and program identities and a limit between 1 and 200."));
}
