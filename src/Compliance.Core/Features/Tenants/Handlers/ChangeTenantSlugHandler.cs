using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
namespace Bdgrz.Compliance.Features.Tenants.Handlers;

/// <summary>
///     Requests a tenant slug change as a native operation on the tenant owner. The exact retry is resolved from the
///     tenant's accepted facts before any mutable state is read, a changed intent under the same command id conflicts,
///     and a new operation must expect the tenant revision the caller observed.
/// </summary>
public sealed class ChangeTenantSlugHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, IPlatformOperatorAccess operators, TimeProvider clock)
    : IRequestHandler<ChangeTenantSlug>
{
    const string CommandKind = "ChangeTenantSlug";
    const string CurrentEpisode = "current";

    public async ValueTask<Result> HandleAsync(IRequestContext<ChangeTenantSlug> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A tenant is required."));
        if (request.ExpectedRevision < 0 && !RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The expected tenant revision must not be negative."));
        if (!TenantSlugs.TryNormalize(request.Slug, out var slug, out var reason))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, reason));

        var requestedBy = await RequesterAsync(context, ct).ConfigureAwait(false);
        // The slug owner is read only as a possibly stale preflight; an exact retry is resolved from the tenant's
        // accepted facts first and never depends on it.
        var candidate = await reader.HydrateAsync(new TenantSlug(slug), ct).ConfigureAwait(false);
        var available = candidate.IsAvailableFor(request.TenantId);
        return await UnitOperations.RunAsync(operations, content, clock, context, requestedBy, request.CommandId,
            CommandKind, CurrentEpisode, request.ExpectedRevision >= 0 ? (ulong)request.ExpectedRevision : null,
            selected => new Tenant(request.TenantId, selected), tenant => tenant.Acceptance,
            tenant => available
                ? tenant.PrepareRequestSlugChange(slug, request.ExpectedRevision)
                : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The tenant slug is already reserved or retired.")),
            (Tenant tenant, OperationProvenance operation, ImmutableContentRef response) => tenant.RequestSlugChange(operation, response, slug,
                request.ExpectedRevision, requestedBy, operation.AcceptedAt),
            Tenant.SlugUnchanged).ConfigureAwait(false);
    }

    async ValueTask<ActorReference?> RequesterAsync(IRequestContext<ChangeTenantSlug> context, CancellationToken ct)
    {
        if (RequestActor.IsSystem(context.Actor))
            return ActorReference.ForSystemProcess("system", "system");
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return null;
        var display = UserIdentityClaims.BdgrzDisplay(context.Actor, userId);
        return await operators.IsOperatorAsync(userId, ct).ConfigureAwait(false)
            ? ActorReference.ForPlatformOperator(userId, display)
            : ActorReference.ForMember(RbacIds.Member(context.Request.TenantId, userId), display);
    }
}
