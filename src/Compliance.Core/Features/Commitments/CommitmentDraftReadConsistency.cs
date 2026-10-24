using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class CommitmentDraftReadConsistency(ICommitmentDraftDirectoryReader directory,
    CommitmentReader commitments)
{
    public async ValueTask<Result<CommitmentDraftView>> GetAsync(Uuid tenantId, Uuid programId,
        Uuid draftId, long? minimumRevision, CancellationToken ct)
    {
        try
        {
            if (minimumRevision is < 1)
                return Result<CommitmentDraftView>.Failure(new RequestError(
                    RequestErrorKind.Validation, "The minimum draft revision must be positive."));
            var source = await commitments.ReadAsync(tenantId, programId, draftId, ct).ConfigureAwait(false);
            if (!source.IsCreated || source.ProgramId != programId)
                return Result<CommitmentDraftView>.Failure(new RequestError(
                    RequestErrorKind.NotFound, "The draft was not found."));
            var view = await directory.GetAsync(tenantId, draftId, ct).ConfigureAwait(false);
            if (view is not null && view.TenantId == tenantId && view.ProgramId == programId &&
                view.DraftId == draftId && view.Revision >= source.Revision &&
                (minimumRevision is null || source.Revision >= minimumRevision))
                return Result<CommitmentDraftView>.Success(source.Draft!);
            return Result<CommitmentDraftView>.Failure(new RequestError(RequestErrorKind.Conflict,
                minimumRevision is { } minimum && source.Revision < minimum
                    ? $"The draft source has not reached revision {minimum}."
                    : "The draft projection has not reached the requested revision.",
                isTransient: true));
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<CommitmentDraftView>.Failure(CommitmentReadFailure.Of(failure));
        }
    }
}
