using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Commitments.Services;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Commitments.Queries;
using Bdgrz.Compliance.Features.Commitments.Readers;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Commitments.Handlers;

public sealed class GetCommitmentDraftRevisionHandler(
    ICommitmentDraftDirectoryReader directory, CommitmentDraftReadConsistency consistency)
    : IRequestHandler<GetCommitmentDraftRevision, CommitmentDraftRevisionView>
{
    public async ValueTask<Result<CommitmentDraftRevisionView>> HandleAsync(
        IRequestContext<GetCommitmentDraftRevision> context, CancellationToken ct)
    {
        try
        {
            var request = context.Request;
            if (request.Revision < 1)
                return Result<CommitmentDraftRevisionView>.Failure(new RequestError(
                    RequestErrorKind.Validation, "The draft revision must be positive."));
            var freshness = await consistency.GetAsync(request.TenantId, request.ProgramId,
                request.DraftId, request.Revision, ct).ConfigureAwait(false);
            if (!freshness.IsSuccess)
                return Result<CommitmentDraftRevisionView>.Failure(freshness.Error);
            var revision = await directory.GetRevisionAsync(request.TenantId, request.DraftId,
                request.Revision, ct).ConfigureAwait(false);
            return revision is not null && revision.TenantId == request.TenantId &&
                   revision.ProgramId == request.ProgramId && revision.DraftId == request.DraftId
                ? Result<CommitmentDraftRevisionView>.Success(revision)
                : Result<CommitmentDraftRevisionView>.Failure(new RequestError(
                    RequestErrorKind.Conflict, "The draft revision projection is incomplete.",
                    isTransient: true));
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<CommitmentDraftRevisionView>.Failure(CommitmentReadFailure.Of(failure));
        }
    }
}
