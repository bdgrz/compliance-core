using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Projections;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Commitments.Readers;
namespace Bdgrz.Compliance.Features.Commitments.Services;

/// <summary>Reads immutable effective versions after proving the projection reached the source.</summary>
public sealed class CommitmentVersionReadConsistency(ICommitmentDraftDirectoryReader directory,
    CommitmentReader commitments)
{
    public async ValueTask<Result<CommitmentReadSnapshot>> SourceAsync(Uuid tenantId, Uuid programId,
        Uuid draftId, CancellationToken ct)
    {
        try
        {
            var source = await commitments.ReadAsync(tenantId, programId, draftId, ct).ConfigureAwait(false);
            return !source.IsCreated || source.ProgramId != programId
                ? Result<CommitmentReadSnapshot>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The draft was not found."))
                : Result<CommitmentReadSnapshot>.Success(source);
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<CommitmentReadSnapshot>.Failure(CommitmentReadFailure.Of(failure));
        }
    }

    public async ValueTask<Result<CommitmentVersionView>> GetAsync(Uuid tenantId,
        Uuid programId, Uuid draftId, long version, CancellationToken ct)
    {
        try
        {
            if (version < 1)
                return Result<CommitmentVersionView>.Failure(new RequestError(
                    RequestErrorKind.Validation, "The version must be positive."));
            var source = await SourceAsync(tenantId, programId, draftId, ct).ConfigureAwait(false);
            if (!source.IsSuccess)
                return Result<CommitmentVersionView>.Failure(source.Error);
            if (version > source.Value.EffectiveVersionCount)
                return Result<CommitmentVersionView>.Failure(new RequestError(
                    RequestErrorKind.NotFound, "The commitment version was not found."));
            var view = await directory.GetVersionAsync(tenantId, draftId, version, ct)
                .ConfigureAwait(false);
            return view is not null && view.TenantId == tenantId && view.ProgramId == programId &&
                   view.DraftId == draftId
                ? Result<CommitmentVersionView>.Success(view)
                : Result<CommitmentVersionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The commitment version projection has not reached the source.",
                    isTransient: true));
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<CommitmentVersionView>.Failure(CommitmentReadFailure.Of(failure));
        }
    }

    public async ValueTask<Result> EnsureVersionsAsync(Uuid tenantId, Uuid programId,
        Uuid draftId, CancellationToken ct)
    {
        try
        {
            var source = await SourceAsync(tenantId, programId, draftId, ct).ConfigureAwait(false);
            if (!source.IsSuccess)
                return Result.Failure(source.Error);
            if (source.Value.EffectiveVersionCount == 0)
                return Result.Success;
            var latest = await GetAsync(tenantId, programId, draftId,
                source.Value.EffectiveVersionCount, ct).ConfigureAwait(false);
            return latest.IsSuccess ? Result.Success : Result.Failure(latest.Error);
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result.Failure(CommitmentReadFailure.Of(failure));
        }
    }
}
