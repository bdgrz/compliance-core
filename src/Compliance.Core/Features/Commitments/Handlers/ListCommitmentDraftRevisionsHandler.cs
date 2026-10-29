using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Commitments.History;
using Bdgrz.Compliance.Features.Commitments.Services;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Commitments.Queries;
using Bdgrz.Compliance.Features.Commitments.Readers;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.Commitments.Handlers;

public sealed class ListCommitmentDraftRevisionsHandler(
    ICommitmentDraftHistoryDirectoryReader directory,
    CommitmentDraftHistoryReadConsistency consistency)
    : IRequestHandler<ListCommitmentDraftRevisions, Page<CommitmentDraftRevisionView>>
{
    public async ValueTask<Result<Page<CommitmentDraftRevisionView>>> HandleAsync(
        IRequestContext<ListCommitmentDraftRevisions> context, CancellationToken ct)
    {
        try
        {
            var request = context.Request;
            if (request.Limit is < 1 or > 200)
                return Result<Page<CommitmentDraftRevisionView>>.Failure(new RequestError(
                    RequestErrorKind.Validation,
                    "The draft revision list limit must be between 1 and 200."));
            var fresh = await consistency.EnsureAsync(request.TenantId, request.ProgramId,
                request.DraftId, request.MinimumDraftRevision, ct).ConfigureAwait(false);
            if (!fresh.IsSuccess)
                return Result<Page<CommitmentDraftRevisionView>>.Failure(fresh.Error);
            Page<CommitmentDraftRevisionView> page;
            try
            {
                page = await directory.ListRevisionsAsync(request.TenantId, request.DraftId,
                    request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
            }
            catch (KvDirectoryQueryException)
            {
                return Result<Page<CommitmentDraftRevisionView>>.Failure(new RequestError(
                    RequestErrorKind.Validation, "The draft revision cursor is invalid."));
            }
            return page.Items.Any(item => item.TenantId != request.TenantId ||
                                          item.ProgramId != request.ProgramId ||
                                          item.DraftId != request.DraftId)
                ? Result<Page<CommitmentDraftRevisionView>>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The draft history projection has an invalid scope."))
                : Result<Page<CommitmentDraftRevisionView>>.Success(page);
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<Page<CommitmentDraftRevisionView>>.Failure(CommitmentReadFailure.Of(failure));
        }
    }
}
