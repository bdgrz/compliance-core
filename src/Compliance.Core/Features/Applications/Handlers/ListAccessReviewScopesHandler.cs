using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Services;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.Applications.Handlers;

/// <summary>
/// Joins the application's projected instances with the projected scope histories. Both
/// projections must have reached their sources, so an unresolved status never reflects lag.
/// </summary>
public sealed class ListAccessReviewScopesHandler(IApplicationDirectoryReader directory,
    SystemInstanceReadConsistency consistency, IAccessReviewScopeDirectoryReader scopes,
    IDomainEventReader events, TimeProvider clock,
    RestrictedApplicationVisibility visibility)
    : IRequestHandler<ListAccessReviewScopes, Page<AccessReviewScopeStatusView>>
{
    public async ValueTask<Result<Page<AccessReviewScopeStatusView>>> HandleAsync(
        IRequestContext<ListAccessReviewScopes> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Failure(RequestErrorKind.Validation,
                "The access-review scope list limit must be between 1 and 200.");
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        var canReadApplication = await visibility.CanReadApplicationAsync(request.TenantId,
            userId, request.ApplicationId, ct).ConfigureAwait(false);
        Page<SystemInstanceView> instances;
        try
        {
            instances = await VisibleApplicationPage.ReadAsync(request.Limit ?? 50,
                request.Cursor,
                (limit, cursor) => directory.ListInstancesAsync(request.TenantId,
                    request.ApplicationId, limit, cursor, ct),
                item => visibility.CanReadSystemInstanceAsync(request.TenantId, userId,
                    item.ApplicationId, item.SystemInstanceId, ct),
                item => item.TenantId == request.TenantId &&
                        item.ApplicationId == request.ApplicationId);
        }
        catch (KvDirectoryQueryException)
        {
            return Failure(RequestErrorKind.Validation,
                "The access-review scope cursor is invalid.");
        }
        catch (VisibleApplicationPage.ForeignDirectoryItemException)
        {
            return Failure(RequestErrorKind.NotFound, "The system instances were not found.");
        }
        if (!canReadApplication && instances.Items.Count == 0)
            return Failure(RequestErrorKind.NotFound, "The system instances were not found.");

        var freshness = await consistency.EnsureAsync(request.TenantId, request.ApplicationId,
            null, null, null, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<Page<AccessReviewScopeStatusView>>.Failure(freshness.Error);
        var checkpoint = await scopes.LoadCheckpointAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        await using (var pending = events.ReadAsync(
                         AccessReviewScopeStreams.TenantPattern(request.TenantId),
                         checkpoint.Cursor, ct).GetAsyncEnumerator(ct))
        {
            if (await pending.MoveNextAsync().ConfigureAwait(false))
                return Failure(RequestErrorKind.Conflict,
                    "The access-review scope projection has not reached the source.", true);
        }
        var asOf = request.AsOf ?? clock.GetUtcNow();
        var items = new List<AccessReviewScopeStatusView>(instances.Items.Count);
        foreach (var instance in instances.Items)
        {
            if (instance.TenantId != request.TenantId ||
                instance.ApplicationId != request.ApplicationId)
                return Failure(RequestErrorKind.NotFound, "The system instances were not found.");
            var record = await scopes.GetAsync(request.TenantId, instance.SystemInstanceId, ct)
                .ConfigureAwait(false);
            if (record is not null && (record.TenantId != request.TenantId ||
                                       record.ApplicationId != request.ApplicationId))
                return Failure(RequestErrorKind.NotFound, "The system instances were not found.");
            items.Add(AccessReviewScopeStatus.Evaluate(instance, record?.Decisions ?? [], asOf));
        }
        return Result<Page<AccessReviewScopeStatusView>>.Success(
            new Page<AccessReviewScopeStatusView>(items, instances.NextCursor));
    }

    static Result<Page<AccessReviewScopeStatusView>> Failure(RequestErrorKind kind,
        string message, bool transient = false) =>
        Result<Page<AccessReviewScopeStatusView>>.Failure(new RequestError(kind, message,
            isTransient: transient));
}
