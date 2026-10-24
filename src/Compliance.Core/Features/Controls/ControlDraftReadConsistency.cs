using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class ControlDraftReadConsistency(IControlDraftDirectoryReader directory, ControlReader controls)
{
    public async ValueTask<Result<ControlDraftView>> GetAsync(Uuid tenantId, Uuid programId, Uuid controlId,
        long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result<ControlDraftView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum control draft revision must be positive."));
        try
        {
            var source = await controls.ReadAsync(tenantId, programId, controlId, ct).ConfigureAwait(false);
            if (!source.IsVisible || source.Draft is null)
                return Result<ControlDraftView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The control draft was not found."));
            var discovered = await directory.GetAsync(tenantId, controlId, ct).ConfigureAwait(false);
            if (discovered is not null && discovered.TenantId == tenantId && discovered.ProgramId == programId &&
                discovered.ControlId == controlId && discovered.Revision >= source.Revision &&
                (minimumRevision is null || source.Revision >= minimumRevision))
                return Result<ControlDraftView>.Success(source.Draft);
            return Result<ControlDraftView>.Failure(new RequestError(RequestErrorKind.Conflict,
                minimumRevision is { } minimum && source.Revision < minimum
                    ? $"The control draft source has not reached revision {minimum}."
                    : "The control draft projection has not reached the requested revision.", isTransient: true));
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<ControlDraftView>.Failure(ControlReadFailure.Of(error));
        }
    }
}
