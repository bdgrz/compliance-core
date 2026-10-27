using System.Globalization;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Queries;
using Bdgrz.Compliance.Features.Tenants.Readers;
namespace Bdgrz.Compliance.Features.Tenants.Handlers;

public sealed class ListMyTenantsHandler(
    ITenantDirectory activeTenants,
    ITenantMembershipDirectoryReader memberships,
    ITenantDirectoryReader tenants,
    IAggregateReader reader, TimeProvider? clock = null)
    : IRequestHandler<ListMyTenants, Page<TenantMembershipSummary>>
{
    const int DefaultLimit = 50;
    const int MaxLimit = 200;

    public async ValueTask<Result<Page<TenantMembershipSummary>>> HandleAsync(
        IRequestContext<ListMyTenants> context,
        CancellationToken ct)
    {
        // ListMyTenantsAuthorizer guarantees this before the handler ever runs.
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException(
                "ListMyTenantsAuthorizer must reject requests without a Bdgrz user identity.");

        var request = context.Request;
        var asOfDate = DateOnly.FromDateTime((clock ?? TimeProvider.System).GetUtcNow().UtcDateTime);
        if (request.Limit is < 1 or > MaxLimit)
            return Result<Page<TenantMembershipSummary>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The tenant membership list limit must be between 1 and 200."));
        string? cursor = null;
        if (request.Cursor is not null)
        {
            if (!Uuid.TryParse(request.Cursor, CultureInfo.InvariantCulture, out var cursorId))
                return Result<Page<TenantMembershipSummary>>.Failure(new RequestError(
                    RequestErrorKind.Validation, "The tenant membership cursor is invalid."));
            cursor = cursorId.ToString();
        }
        var limit = request.Limit ?? DefaultLimit;

        // GetActiveTenantsAsync has no ordering guarantee (it folds a HashSet), so the scan itself
        // has to sort lexicographically by tenant ID before a "resume after this ID" cursor means
        // anything — same convention KvDirectory cursors already use everywhere else.
        var ordered = new List<TenantId>();
        await foreach (var tenantId in activeTenants.GetActiveTenantsAsync(ct))
        {
            ordered.Add(tenantId);
        }

        ordered.Sort(static (left, right) => string.CompareOrdinal(left.Value, right.Value));

        var items = new List<TenantMembershipSummary>();
        string? nextCursor = null;
        for (var index = 0; index < ordered.Count; index++)
        {
            var tenantId = ordered[index];
            if (cursor is not null && string.CompareOrdinal(tenantId.Value, cursor) <= 0)
            {
                continue;
            }

            if (items.Count >= limit)
            {
                nextCursor = ordered[index - 1].Value;
                break;
            }

            if (!await memberships.IsMemberAsync(tenantId.Value, userId, ct))
            {
                continue;
            }

            // A membership row can briefly outrun the tenant directory's own projector; skip rather
            // than fail — it self-heals once TenantDirectory catches up.
            var resolvedTenantId = Uuid.Parse(tenantId.Value, CultureInfo.InvariantCulture);
            var tenant = await tenants.GetAsync(resolvedTenantId, ct);
            if (tenant is null)
                continue;
            if (tenant.TenantId != resolvedTenantId)
                return Result<Page<TenantMembershipSummary>>.Failure(new RequestError(
                    RequestErrorKind.NotFound, "The tenant was not found."));
            // A suspension can outrun the membership projection or commit while tenant metadata
            // is read. Use the tenant-addressed member stream at the final disclosure point.
            var member = await reader.HydrateAsync(new Member(resolvedTenantId, userId), ct)
                .ConfigureAwait(false);
            if (!member.IsAccessEligibleOn(asOfDate))
                continue;
            items.Add(new TenantMembershipSummary(tenant.TenantId, tenant.Name, tenant.Slug,
                tenant.Status));
        }

        return Result<Page<TenantMembershipSummary>>.Success(new Page<TenantMembershipSummary>(items, nextCursor));
    }
}
