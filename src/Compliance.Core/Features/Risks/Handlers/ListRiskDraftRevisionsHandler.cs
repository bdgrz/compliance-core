using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Risks.History;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Queries;
using Bdgrz.Compliance.Features.Risks.Readers;
namespace Bdgrz.Compliance.Features.Risks.Handlers;

public sealed class ListRiskDraftRevisionsHandler(IRiskDraftHistoryDirectoryReader directory,
    RiskDraftHistoryReadConsistency consistency)
    : IRequestHandler<ListRiskDraftRevisions, Page<RiskDraftRevisionView>>
{
    public async ValueTask<Result<Page<RiskDraftRevisionView>>> HandleAsync(
        IRequestContext<ListRiskDraftRevisions> context, CancellationToken ct)
    {
        try
        {
            var request = context.Request;
            if (request.Limit is < 1 or > 200)
                return Result<Page<RiskDraftRevisionView>>.Failure(new RequestError(
                    RequestErrorKind.Validation,
                    "The risk draft revision list limit must be between 1 and 200."));
            var freshness = await consistency.EnsureAsync(request.TenantId, request.ProgramId,
                request.RiskId, request.MinimumRiskRevision, ct).ConfigureAwait(false);
            if (!freshness.IsSuccess)
                return Result<Page<RiskDraftRevisionView>>.Failure(freshness.Error);
            Page<RiskDraftRevisionView> page;
            try
            {
                page = await directory.ListRevisionsAsync(request.TenantId, request.RiskId,
                    request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
            }
            catch (KvDirectoryQueryException)
            {
                return Result<Page<RiskDraftRevisionView>>.Failure(new RequestError(
                    RequestErrorKind.Validation, "The risk draft revision cursor is invalid."));
            }
            return page.Items.Any(item => item.TenantId != request.TenantId ||
                                          item.ProgramId != request.ProgramId ||
                                          item.RiskId != request.RiskId)
                ? Result<Page<RiskDraftRevisionView>>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The risk draft history projection has an invalid scope."))
                : Result<Page<RiskDraftRevisionView>>.Success(page);
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<Page<RiskDraftRevisionView>>.Failure(RiskDraftReadFailure.Of(failure));
        }
    }
}
