using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Queries;
using Bdgrz.Compliance.Features.Tenants.Readers;
namespace Bdgrz.Compliance.Features.Tenants.Handlers;

/// <summary>
///     Reads one tenant from the directory and stamps the tenant's current revision from its owner stream, so a
///     caller can expect exactly the revision it observed when it changes the slug.
/// </summary>
public sealed class GetTenantHandler(ITenantDirectoryReader directory, IAggregateReader reader)
    : IRequestHandler<GetTenant, TenantView>
{
    public async ValueTask<Result<TenantView>> HandleAsync(IRequestContext<GetTenant> context,
        CancellationToken ct)
    {
        var tenant = await directory.GetAsync(context.Request.TenantId, ct).ConfigureAwait(false);
        if (tenant is null)
            return Result<TenantView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The tenant was not found."));
        var owner = await reader.HydrateAsync(new Tenant(context.Request.TenantId), ct).ConfigureAwait(false);
        return Result<TenantView>.Success(tenant with { Revision = owner.Revision });
    }
}
