using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.History;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Boundaries.Services;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Boundaries.Queries;
using Bdgrz.Compliance.Features.Boundaries.Readers;
namespace Bdgrz.Compliance.Features.Boundaries.Handlers;

public sealed class GetEffectiveBoundaryVersionHandler(IBoundaryDirectoryReader directory,
    BoundaryHistoryReadConsistency consistency)
    : IRequestHandler<GetEffectiveBoundaryVersion, BoundaryVersionView>
{
    public async ValueTask<Result<BoundaryVersionView>> HandleAsync(
        IRequestContext<GetEffectiveBoundaryVersion> context, CancellationToken ct)
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
            var version = await directory.GetEffectiveVersionAsync(request.TenantId,
                request.BoundaryId, request.EffectiveOn, ct).ConfigureAwait(false);
            return version is null
                ? Result<BoundaryVersionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "No approved boundary version was effective on that date."))
                : Result<BoundaryVersionView>.Success(version);
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<BoundaryVersionView>.Failure(BoundaryReadFailure.Of(failure));
        }
    }
}
