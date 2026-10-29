using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.History;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Boundaries.Services;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Boundaries.Queries;
using Bdgrz.Compliance.Features.Boundaries.Readers;
namespace Bdgrz.Compliance.Features.Boundaries.Handlers;

public sealed class GetBoundaryVersionHandler(IBoundaryDirectoryReader directory,
    BoundaryHistoryReadConsistency consistency)
    : IRequestHandler<GetBoundaryVersion, BoundaryVersionView>
{
    public async ValueTask<Result<BoundaryVersionView>> HandleAsync(
        IRequestContext<GetBoundaryVersion> context, CancellationToken ct)
    {
        try
        {
            var request = context.Request;
            if (request.MinimumBoundaryRevision is { } minimum)
            {
                var freshness = await consistency.EnsureAsync(request.TenantId, request.BoundaryId,
                    minimum, ct).ConfigureAwait(false);
                if (!freshness.IsSuccess)
                    return Result<BoundaryVersionView>.Failure(freshness.Error);
            }
            var version = await directory.GetVersionAsync(request.TenantId, request.BoundaryId,
                request.VersionId, ct).ConfigureAwait(false);
            return version is null
                ? Result<BoundaryVersionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The boundary version was not found."))
                : Result<BoundaryVersionView>.Success(version);
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<BoundaryVersionView>.Failure(BoundaryReadFailure.Of(failure));
        }
    }
}
