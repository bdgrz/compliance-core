using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Boundaries.Services;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Boundaries.Queries;
using Bdgrz.Compliance.Features.Boundaries.Readers;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Boundaries.Handlers;

public sealed class GetBoundaryHandler(IBoundaryDirectoryReader directory,
    BoundaryReader boundaries)
    : IRequestHandler<GetBoundary, BoundaryView>
{
    public async ValueTask<Result<BoundaryView>> HandleAsync(IRequestContext<GetBoundary> context,
        CancellationToken ct)
    {
        try
        {
            var request = context.Request;
            if (request.MinimumRevision is < 1)
                return Result<BoundaryView>.Failure(new RequestError(RequestErrorKind.Validation,
                    "The minimum revision must be positive."));
            var view = await directory.GetAsync(request.TenantId,
                request.BoundaryId, ct).ConfigureAwait(false);
            if (request.MinimumRevision is { } minimum &&
                (view is null || view.Revision < minimum))
            {
                var current = await boundaries.ReadAsync(request.TenantId, request.BoundaryId, ct).ConfigureAwait(false);
                if (!current.IsCreated || !current.IsVisible)
                    return Result<BoundaryView>.Failure(new RequestError(RequestErrorKind.NotFound,
                        "The boundary was not found."));
                return Result<BoundaryView>.Failure(new RequestError(RequestErrorKind.Conflict,
                    current.Revision < minimum
                        ? $"The boundary source has not reached revision {minimum}."
                        : $"The boundary projection has not reached revision {minimum}."));
            }
            return view is null
                ? Result<BoundaryView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The boundary was not found."))
                : Result<BoundaryView>.Success(view);
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<BoundaryView>.Failure(BoundaryReadFailure.Of(failure));
        }
    }
}
