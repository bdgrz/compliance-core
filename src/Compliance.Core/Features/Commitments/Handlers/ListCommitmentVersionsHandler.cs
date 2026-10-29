using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Commitments.Services;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Commitments.Queries;
using Bdgrz.Compliance.Features.Commitments.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.Commitments.Handlers;

public sealed class ListCommitmentVersionsHandler(ICommitmentDraftDirectoryReader directory,
    CommitmentVersionReadConsistency consistency)
    : IRequestHandler<ListCommitmentVersions, Page<CommitmentVersionView>>
{
    public async ValueTask<Result<Page<CommitmentVersionView>>> HandleAsync(
        IRequestContext<ListCommitmentVersions> context, CancellationToken ct)
    {
        try
        {
            var request = context.Request;
            if (request.Limit is < 1 or > 200)
                return Result<Page<CommitmentVersionView>>.Failure(new RequestError(
                    RequestErrorKind.Validation, "The version list limit must be between 1 and 200."));
            var fresh = await consistency.EnsureVersionsAsync(request.TenantId, request.ProgramId,
                request.DraftId, ct).ConfigureAwait(false);
            if (!fresh.IsSuccess)
                return Result<Page<CommitmentVersionView>>.Failure(fresh.Error);
            Page<CommitmentVersionView> page;
            try
            {
                page = await directory.ListVersionsAsync(request.TenantId, request.DraftId,
                    request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
            }
            catch (KvDirectoryQueryException)
            {
                return Result<Page<CommitmentVersionView>>.Failure(new RequestError(
                    RequestErrorKind.Validation, "The version cursor is invalid."));
            }
            return page.Items.Any(item => item.TenantId != request.TenantId ||
                                          item.ProgramId != request.ProgramId ||
                                          item.DraftId != request.DraftId)
                ? Result<Page<CommitmentVersionView>>.Failure(new RequestError(
                    RequestErrorKind.Conflict, "The version projection has an invalid scope."))
                : Result<Page<CommitmentVersionView>>.Success(page);
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<Page<CommitmentVersionView>>.Failure(CommitmentReadFailure.Of(failure));
        }
    }
}
