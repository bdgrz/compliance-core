using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Tenants.Readers;
namespace Bdgrz.Compliance.Features.AccessControl.Authorizers;

/// <summary>Requires the client's active member and ordinary manager grant; operators get no client authority.</summary>
sealed class IndependenceAdministrationAuthorizer(IPermissionAuthorizer permissions,
    ITenantActivity tenants, ITenantMembershipDirectoryReader memberships)
    : IRequestAuthorizer<IIndependenceAdministrationRequest>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IIndependenceAdministrationRequest> context,
        CancellationToken ct) => new RbacManagementAuthorizer(permissions, tenants, memberships)
        .AuthorizeTenantAsync(context.Actor, context.Request.TenantId, ct);
}
