using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Policies.Queries;
using Bdgrz.Compliance.Features.Policies.Readers;
namespace Bdgrz.Compliance.Features.Policies.Handlers;

/// <summary>
///     Lists a program's policies from the projection after proving it reached the source, and
///     flags any whose periodic review is overdue today.
/// </summary>
public sealed class ListPoliciesHandler(IPolicyDirectoryReader directory,
    PolicyDirectoryReadConsistency consistency, TimeProvider clock)
    : IRequestHandler<ListPolicies, Page<PolicySummaryView>>
{
    public async ValueTask<Result<Page<PolicySummaryView>>> HandleAsync(
        IRequestContext<ListPolicies> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<PolicySummaryView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "Limit must be between 1 and 200."));
        var capture = await consistency.CaptureAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!capture.IsSuccess)
            return Result<Page<PolicySummaryView>>.Failure(capture.Error);
        Page<PolicySummaryView> page;
        try
        {
            page = await directory.ListProgramAsync(request.TenantId, request.ProgramId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<PolicySummaryView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The policy cursor is invalid."));
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<Page<PolicySummaryView>>.Failure(PolicyReadFailure.Of(error));
        }
        if (page.Items.Any(item => item.TenantId != request.TenantId ||
                                   item.ProgramId != request.ProgramId))
            return Result<Page<PolicySummaryView>>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The policy projection has an invalid program scope."));
        var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(request.TenantId,
            capture.Value, ct).ConfigureAwait(false);
        if (!confirmed.IsSuccess)
            return Result<Page<PolicySummaryView>>.Failure(confirmed.Error);
        var today = PolicySource.Today(clock);
        return Result<Page<PolicySummaryView>>.Success(new Page<PolicySummaryView>(
            page.Items.Select(item => item with
            {
                ReviewOverdue = item.NextReviewDueOn is { } due && today > due,
            }).ToArray(), page.NextCursor));
    }
}
