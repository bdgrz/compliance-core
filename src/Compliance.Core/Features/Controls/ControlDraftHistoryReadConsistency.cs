using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class ControlDraftHistoryReadConsistency(IControlDraftHistoryDirectoryReader directory, ControlReader controls)
{
    public async ValueTask<Result> EnsureAsync(Uuid tenantId, Uuid programId, Uuid controlId,
        long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum control draft revision must be positive."));
        try
        {
            var source = await controls.ReadAsync(tenantId, programId, controlId, ct).ConfigureAwait(false);
            if (!source.IsVisible)
                return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The control draft was not found."));
            if (minimumRevision is { } minimum && source.Revision < minimum)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    $"The control draft source has not reached revision {minimum}.", isTransient: true));
            var current = await directory.GetRevisionAsync(tenantId, controlId, source.Revision, ct).ConfigureAwait(false);
            return current is not null && current.TenantId == tenantId && current.ProgramId == programId &&
                current.ControlId == controlId && current.Revision == source.Revision ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The control draft history projection has not reached the requested revision.", isTransient: true));
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result.Failure(ControlReadFailure.Of(error));
        }
    }
}
