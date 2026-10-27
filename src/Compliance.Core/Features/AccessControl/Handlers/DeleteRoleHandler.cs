using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     Deletes a role as a native operation on the role owner. An exact retry replays the original acceptance and
///     deleting an already deleted role appends nothing.
/// </summary>
public sealed class DeleteRoleHandler(NativeOperationExecutor operations, IImmutableContentStore content,
    TimeProvider clock) : IRequestHandler<DeleteRole>
{
    const string CommandKind = "DeleteRole";
    const string CurrentEpisode = "current";

    public ValueTask<Result> HandleAsync(IRequestContext<DeleteRole> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.RoleId == Uuid.Empty || request.CommandId == request.RoleId)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A tenant, role and a command id distinct from the role are required.")));
        return UnitOperations.RunAsync(operations, content, clock, context, RegisterMemberHandler.ActorOf(context.Actor),
            request.CommandId, CommandKind, CurrentEpisode, null,
            selected => new Role(request.TenantId, request.RoleId, selected), role => role.Acceptance,
            role => role.PrepareDelete(), (Role role, OperationProvenance operation, ImmutableContentRef response) => role.Delete(operation, response),
            Role.AlreadyDeleted);
    }
}
