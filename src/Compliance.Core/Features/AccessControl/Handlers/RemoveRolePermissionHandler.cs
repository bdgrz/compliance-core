using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class RemoveRolePermissionHandler(IAggregateExecutor executor)
    : IRequestHandler<RemoveRolePermission>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RemoveRolePermission> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.RoleId == Uuid.Empty || string.IsNullOrWhiteSpace(request.Permission))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A tenant, role and permission are required.")));
        var permission = Permissions.Normalize(request.Permission);
        if (request.RoleId == BuiltInRbac.TenantAdministrationRoleId(request.TenantId) &&
            permission is RbacPermissions.TenantAccess or RbacPermissions.TenantRbacManage)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The Tenant Administration role keeps tenant access and RBAC management.")));
        return executor.ExecuteAsync(
            new RolePermission(request.TenantId, request.RoleId, permission, request.CommandId),
            assignment => AggregateOutcome.CommitOnSuccess(assignment.Remove(request.CommandId, request.ExpectedRevision,
                request.AssignmentId, RbacOperationActor.From(context.Actor))), context, ct);
    }
}
