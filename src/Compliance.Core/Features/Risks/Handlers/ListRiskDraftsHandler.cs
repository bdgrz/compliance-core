using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
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
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Queries;
using Bdgrz.Compliance.Features.Risks.Readers;
namespace Bdgrz.Compliance.Features.Risks.Handlers;

public sealed class ListRiskDraftsHandler(IRiskDraftDirectoryReader directory,
    IAggregateReader reader, RiskDraftListReadConsistency consistency)
    : IRequestHandler<ListRiskDrafts, Page<RiskDraftView>>
{
    public async ValueTask<Result<Page<RiskDraftView>>> HandleAsync(
        IRequestContext<ListRiskDrafts> context, CancellationToken ct)
    {
        try
        {
            var request = context.Request;
            if (request.Limit is < 1 or > 200)
                return Result<Page<RiskDraftView>>.Failure(new RequestError(
                    RequestErrorKind.Validation, "Limit must be between 1 and 200."));
            var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
                request.ProgramId), ct).ConfigureAwait(false);
            if (!program.IsCreated)
                return Result<Page<RiskDraftView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The program was not found."));
            var ready = await consistency.CaptureFenceAsync(request.TenantId, ct)
                .ConfigureAwait(false);
            if (!ready.IsSuccess)
                return Result<Page<RiskDraftView>>.Failure(ready.Error);
            Page<RiskDraftView> page;
            try
            {
                page = await directory.ListProgramAsync(request.TenantId, request.ProgramId,
                    request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
            }
            catch (KvDirectoryQueryException)
            {
                return Result<Page<RiskDraftView>>.Failure(new RequestError(
                    RequestErrorKind.Validation, "The risk draft cursor is invalid."));
            }
            var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(request.TenantId, ready.Value, ct).ConfigureAwait(false);
            if (!confirmed.IsSuccess)
                return Result<Page<RiskDraftView>>.Failure(confirmed.Error!);
            return page.Items.Any(item => item.TenantId != request.TenantId ||
                                          item.ProgramId != request.ProgramId)
                ? Result<Page<RiskDraftView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The risk draft projection has an invalid program scope."))
                : Result<Page<RiskDraftView>>.Success(page);
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<Page<RiskDraftView>>.Failure(RiskDraftReadFailure.Of(failure));
        }
    }
}
