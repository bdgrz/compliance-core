using Cntryl.Fitz.Extensions;
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
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Queries;
using Bdgrz.Compliance.Features.Risks.Readers;
namespace Bdgrz.Compliance.Features.Risks.Handlers;

public sealed class ListRiskEvaluationHistoryHandler(IRiskEvaluationDirectoryReader directory,
    RiskEvaluationReadConsistency consistency)
    : IRequestHandler<ListRiskEvaluationHistory, Page<RiskEvaluationHistoryEntryView>>
{
    public async ValueTask<Result<Page<RiskEvaluationHistoryEntryView>>> HandleAsync(
        IRequestContext<ListRiskEvaluationHistory> context, CancellationToken ct)
    {
        try
        {
            var request = context.Request;
            if (request.Limit is < 1 or > 200)
                return Result<Page<RiskEvaluationHistoryEntryView>>.Failure(new RequestError(
                    RequestErrorKind.Validation,
                    "The risk evaluation history limit must be between 1 and 200."));
            var freshness = await consistency.GetAsync(request.TenantId, request.ProgramId,
                request.RiskId, request.MinimumRevision, ct).ConfigureAwait(false);
            if (!freshness.IsSuccess)
                return Result<Page<RiskEvaluationHistoryEntryView>>.Failure(freshness.Error);
            Page<RiskEvaluationHistoryEntryView> page;
            try
            {
                page = await directory.ListHistoryAsync(request.TenantId, request.RiskId,
                    request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
            }
            catch (KvDirectoryQueryException)
            {
                return Result<Page<RiskEvaluationHistoryEntryView>>.Failure(new RequestError(
                    RequestErrorKind.Validation, "The risk evaluation history cursor is invalid."));
            }
            return page.Items.Any(item => item.TenantId != request.TenantId ||
                                          item.ProgramId != request.ProgramId ||
                                          item.RiskId != request.RiskId)
                ? Result<Page<RiskEvaluationHistoryEntryView>>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The risk evaluation history projection has an invalid scope."))
                : Result<Page<RiskEvaluationHistoryEntryView>>.Success(page);
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<Page<RiskEvaluationHistoryEntryView>>.Failure(RiskEvaluationReadFailure.Of(failure));
        }
    }
}
