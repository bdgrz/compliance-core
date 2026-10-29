using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Commitments.Readers;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Commitments.Services;

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
