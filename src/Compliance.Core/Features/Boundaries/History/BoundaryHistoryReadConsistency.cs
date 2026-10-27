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
using Bdgrz.Compliance.Features.Boundaries.Readers;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Boundaries.History;

public sealed class BoundaryHistoryReadConsistency(IBoundaryDirectoryReader directory,
    BoundaryReader boundaries)
{
    public async ValueTask<Result> EnsureAsync(Uuid tenantId, Uuid boundaryId,
        long minimumRevision, CancellationToken ct)
    {
        try
        {
            if (minimumRevision < 1)
                return Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "The minimum boundary revision must be positive."));
            var view = await directory.GetAsync(tenantId, boundaryId, ct).ConfigureAwait(false);
            if (view is not null && view.Revision >= minimumRevision)
                return Result.Success;
            var source = await boundaries.ReadAsync(tenantId, boundaryId, ct).ConfigureAwait(false);
            if (!source.IsCreated || !source.IsVisible)
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The boundary was not found."));
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                source.Revision < minimumRevision
                    ? $"The boundary source has not reached revision {minimumRevision}."
                    : $"The boundary projection has not reached revision {minimumRevision}."));
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result.Failure(BoundaryReadFailure.Of(failure));
        }
    }
}
