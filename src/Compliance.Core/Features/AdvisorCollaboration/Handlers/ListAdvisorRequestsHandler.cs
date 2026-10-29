using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Queries;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Handlers;

public sealed class ListAdvisorRequestsHandler(IAdvisorHandoffDirectoryReader directory,
    AdvisorHandoffListReadConsistency consistency, IAdvisorHandoffListAccess listAccess,
    TimeProvider clock)
    : IRequestHandler<ListAdvisorRequests, Page<AdvisorRequestSummaryView>>
{
    const int DefaultLimit = 50;

    public async ValueTask<Result<Page<AdvisorRequestSummaryView>>> HandleAsync(
        IRequestContext<ListAdvisorRequests> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.Limit is < 1 or > 200)
            return Invalid();

        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result<Page<AdvisorRequestSummaryView>>.Failure(new RequestError(
                RequestErrorKind.Unauthorized,
                "Listing advisor handoffs requires a Bdgrz user identity."));
        var visibility = await listAccess.GetListVisibilityAsync(request.TenantId,
            request.ProgramId, userId, ct).ConfigureAwait(false);
        if (visibility == AdvisorHandoffListVisibility.Denied)
            return Denied();

        var fence = await consistency.CaptureAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<Page<AdvisorRequestSummaryView>>.Failure(fence.Error);

        Page<AdvisorRequestSummaryView> page;
        try
        {
            page = visibility == AdvisorHandoffListVisibility.SharedOnly
                ? await directory.ListSharedProgramAsync(request.TenantId, request.ProgramId,
                    DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime),
                    request.Limit ?? DefaultLimit, request.Cursor, ct).ConfigureAwait(false)
                : await directory.ListProgramAsync(request.TenantId, request.ProgramId,
                    request.Limit ?? DefaultLimit, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<AdvisorRequestSummaryView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The advisor handoff cursor is invalid."));
        }

        if (page.Items.Any(item => item.TenantId != request.TenantId ||
                                   item.ProgramId != request.ProgramId))
            return Result<Page<AdvisorRequestSummaryView>>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "The advisor handoff projection has an invalid program scope."));

        var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(request.TenantId,
            fence.Value, ct).ConfigureAwait(false);
        if (!confirmed.IsSuccess)
            return Result<Page<AdvisorRequestSummaryView>>.Failure(confirmed.Error);

        var currentVisibility = await listAccess.GetListVisibilityAsync(request.TenantId,
            request.ProgramId, userId, ct).ConfigureAwait(false);
        if (currentVisibility == AdvisorHandoffListVisibility.Denied)
            return Denied();
        return currentVisibility == visibility
            ? Result<Page<AdvisorRequestSummaryView>>.Success(page)
            : Result<Page<AdvisorRequestSummaryView>>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "Advisor handoff list access changed during the read. Retry the query.",
                isTransient: true));
    }

    static Result<Page<AdvisorRequestSummaryView>> Invalid() =>
        Result<Page<AdvisorRequestSummaryView>>.Failure(new RequestError(RequestErrorKind.Validation,
            "Advisor handoff lists require tenant and program identities and a limit between 1 and 200."));

    static Result<Page<AdvisorRequestSummaryView>> Denied() =>
        Result<Page<AdvisorRequestSummaryView>>.Failure(new RequestError(RequestErrorKind.Forbidden,
            "The actor may not list advisor handoffs in this program."));
}
