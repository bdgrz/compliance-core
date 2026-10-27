using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Tenants.Readers;
namespace Bdgrz.Compliance.Features.AccessControl.Authorizers;

/// <summary>Adds the retained Attest wall to human client administration without granting bootstrap authority.</summary>
sealed class ClientRbacMutationAuthorizer(ClientManagementIndependenceGuard independence,
    ITenantMembershipDirectoryReader memberships) : IRequestAuthorizer<IClientRbacMutationRequest>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<IClientRbacMutationRequest> context,
        CancellationToken ct)
    {
        // The ordinary RBAC authorizer still decides whether this trusted system operation is allowed.
        if (RequestActor.IsSystem(context.Actor))
            return Result.Success;
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Client administration requires a Bdgrz user identity."));
        var membership = await memberships.GetAsync(context.Request.TenantId.ToString(), userId, ct)
            .ConfigureAwait(false);
        if (membership is null || membership.TenantId != context.Request.TenantId || membership.UserId != userId ||
            membership.IsSuspended || membership.IsDeprovisioned)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The tenant was not found."));
        return await independence.CanAuthorAsync(context.Request.TenantId, userId, ct).ConfigureAwait(false)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "A person with actual Attest assignment history cannot author this client's administration records."));
    }
}
