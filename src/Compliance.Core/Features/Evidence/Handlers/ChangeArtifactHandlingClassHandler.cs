using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Services;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Versioning.Services;

namespace Bdgrz.Compliance.Features.Evidence.Handlers;

sealed class ChangeArtifactHandlingClassHandler(IAggregateExecutor executor,
    EvidenceArtifactReadAccess artifacts, IAccessGrantRolePermissionAuthorizer roles, TimeProvider clock)
    : IRequestHandler<ChangeArtifactHandlingClass>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ChangeArtifactHandlingClass> context, CancellationToken ct)
    {
        var request = context.Request;
        var allowed = await artifacts.RequireAsync(context, request.TenantId, request.ArtifactId, ct).ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return allowed;
        UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user);
        var member = RbacIds.Member(request.TenantId, user);
        if (!await roles.IsAllowedAsOrganizationRoleAsync(request.TenantId, user, member,
                BuiltInRbac.ComplianceManagementRoleId(request.TenantId), EvidenceArtifactPermissions.ChangeHandlingClass, ct)
                .ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Changing a shared artifact handling class requires a current organization Compliance Lead."));
        var actor = ActorReference.ForMember(member, UserIdentityClaims.BdgrzDisplay(context.Actor, user));
        var at = clock.GetUtcNow();
        return await executor.ExecuteAsync(new EvidenceArtifact(request.TenantId, request.ArtifactId, request.CommandId),
            artifact => CommandFailureRequestAdapter.ToOutcome(artifact.ChangeHandlingClass(request, actor, at)), context, ct)
            .ConfigureAwait(false);
    }
}
