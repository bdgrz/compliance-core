using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed class ListProgramBoundariesHandler(IBoundaryDirectoryReader directory,
    IProgramDirectoryReader programs, ProgramSetupWorkReadConsistency? consistency = null)
    : IRequestHandler<ListProgramBoundaries, Page<BoundaryView>>
{
    public async ValueTask<Result<Page<BoundaryView>>> HandleAsync(
        IRequestContext<ListProgramBoundaries> context, CancellationToken ct)
    {
        try
        {
            var request = context.Request;
            if (request.Limit is < 1 or > 200)
                return Result<Page<BoundaryView>>.Failure(new RequestError(RequestErrorKind.Validation,
                    "The boundary list limit must be between 1 and 200."));
            var program = await programs.GetAsync(request.TenantId, request.ProgramId, ct)
                .ConfigureAwait(false);
            if (program is null || program.TenantId != request.TenantId ||
                program.ProgramId != request.ProgramId)
                return Result<Page<BoundaryView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The program was not found."));
            ProgramSetupWorkReadFence? fence = null;
            if (consistency is not null)
            {
                var captured = await consistency.CaptureAsync(request.TenantId, ct).ConfigureAwait(false);
                if (!captured.IsSuccess)
                    return Result<Page<BoundaryView>>.Failure(captured.Error!);
                fence = captured.Value;
            }
            Page<BoundaryView> page;
            try
            {
                page = await directory.ListProgramAsync(request.TenantId, request.ProgramId,
                    request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
            }
            catch (KvDirectoryQueryException)
            {
                return Result<Page<BoundaryView>>.Failure(new RequestError(RequestErrorKind.Validation,
                    "The boundary cursor is invalid."));
            }
            if (consistency is not null && fence is not null)
            {
                var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(request.TenantId, fence, ct).ConfigureAwait(false);
                if (!confirmed.IsSuccess)
                    return Result<Page<BoundaryView>>.Failure(confirmed.Error!);
            }
            return page.Items.Any(item => item.TenantId != request.TenantId ||
                                          item.ProgramId != request.ProgramId)
                ? Result<Page<BoundaryView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The boundary projection has an invalid program scope."))
                : Result<Page<BoundaryView>>.Success(page);
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<Page<BoundaryView>>.Failure(BoundaryReadFailure.Of(failure));
        }
    }
}
