using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Events;
namespace Bdgrz.Compliance.Features.Tenants.Services;

/// <summary>
///     Rejects an obviously-occupied slug before a tenant is created, so the RBAC bootstrap and
///     owner-registration reactors <see cref="Tenant" /> triggers on <c>TenantRegistered</c> never
///     fire for a registration the slug claim would reject anyway. Not authoritative: the slug can
///     still be claimed by another registration between this read and
///     <see cref="TenantSlug.Register" />, which is what actually decides ownership and drives
///     <see cref="Tenant" /> through its pending/confirmed/rejected slug states.
/// </summary>
sealed class RegisterTenantSlugAvailabilityGuard(IAggregateReader reader) : IRequestGuard<RegisterTenant>
{
    public async ValueTask<Result> GuardAsync(IRequestContext<RegisterTenant> context, CancellationToken ct)
    {
        if (!TenantSlugs.TryNormalize(context.Request.Slug, out var slug))
            return Result.Success; // Tenant.Register produces the authoritative validation error.

        var tenantSlug = await reader.HydrateAsync(new TenantSlug(slug), ct).ConfigureAwait(false);
        return tenantSlug.IsAvailableFor(context.RequestId)
            ? Result.Success
            : Result.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "That tenant slug is already registered."));
    }
}
