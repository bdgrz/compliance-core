using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.History;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Boundaries.Services;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Boundaries.Queries;
using Bdgrz.Compliance.Features.Boundaries.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.Boundaries.Handlers;

public sealed class ListBoundaryVersionsHandler(IBoundaryDirectoryReader directory,
    BoundaryHistoryReadConsistency consistency)
    : IRequestHandler<ListBoundaryVersions, Page<BoundaryVersionView>>
{
    public async ValueTask<Result<Page<BoundaryVersionView>>> HandleAsync(
        IRequestContext<ListBoundaryVersions> context, CancellationToken ct)
    {
        try
        {
            var request = context.Request;
            if (request.Limit is < 1 or > 200)
                return Result<Page<BoundaryVersionView>>.Failure(new RequestError(
                    RequestErrorKind.Validation, "The boundary version list limit must be between 1 and 200."));
            if (request.MinimumBoundaryRevision is { } minimum)
            {
                var freshness = await consistency.EnsureAsync(request.TenantId, request.BoundaryId,
                    minimum, ct).ConfigureAwait(false);
                if (!freshness.IsSuccess)
                    return Result<Page<BoundaryVersionView>>.Failure(freshness.Error);
            }
            Page<BoundaryVersionView>? page;
            try
            {
                page = await directory.ListVersionsAsync(request.TenantId,
                    request.BoundaryId, request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
            }
            catch (KvDirectoryQueryException)
            {
                return Result<Page<BoundaryVersionView>>.Failure(new RequestError(
                    RequestErrorKind.Validation, "The boundary version cursor is invalid."));
            }
            return page is null
                ? Result<Page<BoundaryVersionView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The boundary was not found."))
                : page.Items.Any(item => item.TenantId != request.TenantId ||
                                         item.BoundaryId != request.BoundaryId)
                    ? Result<Page<BoundaryVersionView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The boundary version projection has an invalid scope."))
                    : Result<Page<BoundaryVersionView>>.Success(page);
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<Page<BoundaryVersionView>>.Failure(BoundaryReadFailure.Of(failure));
        }
    }
}
