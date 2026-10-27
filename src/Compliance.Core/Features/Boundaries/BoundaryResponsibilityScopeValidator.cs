using Bdgrz.Compliance.Features.Boundaries.Readers;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

sealed class BoundaryResponsibilityScopeValidator(BoundaryReader boundaries)
    : IResponsibilityScopeValidator
{
    public async ValueTask<Result> ValidateAsync(Uuid tenantId, ResponsibilityScope scope,
        CancellationToken ct = default)
    {
        try
        {
            if (!StringComparer.Ordinal.Equals(scope.RecordType, "boundary"))
                return Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "The responsibility record type is not supported."));
            var boundary = await boundaries.ReadAsync(tenantId, scope.RecordId, ct).ConfigureAwait(false);
            if (!boundary.IsCreated || !boundary.IsVisible)
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The responsibility source record was not found."));
            return boundary.DraftVersionId == scope.VersionId &&
                   boundary.DraftRevision == scope.Revision
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "Responsibilities must target the current exact boundary draft revision."));
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result.Failure(BoundaryReadFailure.Of(failure));
        }
    }
}
