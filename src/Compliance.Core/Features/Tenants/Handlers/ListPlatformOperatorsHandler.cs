using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Queries;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Handlers;

public sealed class ListPlatformOperatorsHandler(IPlatformOperatorDirectoryReader directory)
    : IRequestHandler<ListPlatformOperators, Page<PlatformOperatorDirectoryEntry>>
{
    public async ValueTask<Result<Page<PlatformOperatorDirectoryEntry>>> HandleAsync(
        IRequestContext<ListPlatformOperators> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<PlatformOperatorDirectoryEntry>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The platform operator list limit must be between 1 and 200."));
        try
        {
            return Result<Page<PlatformOperatorDirectoryEntry>>.Success(await directory.ListAsync(
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false));
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<PlatformOperatorDirectoryEntry>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The platform operator cursor is invalid."));
        }
    }
}
