using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.History;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Queries;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Controls.Handlers;

public sealed class ListControlDraftRevisionsHandler(IControlDraftHistoryDirectoryReader directory,
    ControlDraftHistoryReadConsistency consistency)
    : IRequestHandler<ListControlDraftRevisions, Page<ControlDraftRevisionView>>
{
    public async ValueTask<Result<Page<ControlDraftRevisionView>>> HandleAsync(
        IRequestContext<ListControlDraftRevisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ControlDraftRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The control draft revision list limit must be between 1 and 200."));
        var freshness = await consistency.EnsureAsync(request.TenantId, request.ProgramId,
            request.ControlId, request.MinimumControlDraftRevision, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<Page<ControlDraftRevisionView>>.Failure(freshness.Error);
        Page<ControlDraftRevisionView> page;
        try
        {
            page = await directory.ListRevisionsAsync(request.TenantId, request.ControlId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ControlDraftRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The control draft revision cursor is invalid."));
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<Page<ControlDraftRevisionView>>.Failure(ControlReadFailure.Of(error));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.ProgramId != request.ProgramId ||
                                      item.ControlId != request.ControlId)
            ? Result<Page<ControlDraftRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "The control draft history projection is incomplete.", isTransient: true))
            : Result<Page<ControlDraftRevisionView>>.Success(page);
    }
}
