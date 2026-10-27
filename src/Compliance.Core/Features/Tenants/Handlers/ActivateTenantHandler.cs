using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class ActivateTenantHandler(NativeOperationExecutor operations, IImmutableContentStore content,
    TimeProvider clock, IAggregateReader reader, ITenantMembershipDirectoryReader memberships, IPermissionAuthorizer permissions)
    : IRequestHandler<ActivateTenant>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ActivateTenant> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only tenant bootstrap reactions may activate a tenant."));
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.FirstAdministratorUserId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Activation requires its command, tenant and administrator identities."));
        var owner = new Tenant(request.TenantId);
        var operation = OwnerProvenance.For(owner, request.CommandId, "ActivateTenant",
            ActorReference.ForSystemProcess("system", "system"), "activation", null,
            AuthoredIntentDigest.Compute(request), clock.GetUtcNow());
        var accepted = await operations.ExecuteAsync(new NativeOperation<Tenant>(AcceptedOperationBinding.Of(operation),
            () => new Tenant(request.TenantId, request.CommandId), tenant => tenant.Acceptance,
            async (tenant, token) =>
            {
                var prepared = tenant.PrepareActivation(request.FirstAdministratorUserId, request.FirstAdministratorEmail);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                var user = request.FirstAdministratorUserId;
                var memberId = RbacIds.Member(request.TenantId, user);
                var member = await reader.HydrateAsync(new Member(request.TenantId, user), token).ConfigureAwait(false);
                var assignment = await reader.HydrateAsync(new TeamMember(request.TenantId,
                    BuiltInRbac.AdministratorsTeamId(request.TenantId), memberId), token).ConfigureAwait(false);
                if (member.IsRegistered && member.Affiliation != "client_personnel")
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The first administrator must be client personnel."));
                if (!member.IsRegistered || !assignment.IsAssigned || assignment.MembershipEpisodeId != member.MembershipEpisodeId ||
                    !await memberships.IsMemberAsync(request.TenantId.ToString(), user, token).ConfigureAwait(false) ||
                    !await permissions.IsAllowedAsync(request.TenantId, user, memberId, RbacPermissions.TenantAccess, token).ConfigureAwait(false) ||
                    !await permissions.IsAllowedAsync(request.TenantId, user, memberId, RbacPermissions.TenantRbacManage, token).ConfigureAwait(false) ||
                    !await permissions.IsAllowedAsync(request.TenantId, user, memberId, RbacPermissions.ProgramManage, token).ConfigureAwait(false))
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The first administrator is still being provisioned.", true));
                return Result<FirstResponseDescriptor>.Success(await UnitResponses.FreezeAsync(content, "bdgrz", token)
                    .ConfigureAwait(false));
            }, (Tenant tenant, ImmutableContentRef response) => tenant.Activate(operation, response,
                request.FirstAdministratorUserId, request.FirstAdministratorEmail)), context, ct).ConfigureAwait(false);
        return UnitResponses.ToResult(accepted, Tenant.ActivationNotNeeded);
    }
}
