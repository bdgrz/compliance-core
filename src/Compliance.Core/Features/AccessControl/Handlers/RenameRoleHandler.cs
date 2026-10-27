using Bdgrz.Compliance.Features.AccessControl;
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
///     Migrates the name of a built-in role as a native operation on the role owner. An exact retry replays the
///     original acceptance, the same command id with another name conflicts, and renaming to the current name
///     appends nothing.
/// </summary>
public sealed class RenameRoleHandler(NativeOperationExecutor operations, IImmutableContentStore content,
    TimeProvider clock) : IRequestHandler<RenameRole>
{
    const string CommandKind = "RenameRole";
    const string CurrentEpisode = "current";

    public ValueTask<Result> HandleAsync(IRequestContext<RenameRole> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!BuiltInRbac.IsBuiltInRole(request.TenantId, request.RoleId) || !RequestActor.IsSystem(context.Actor))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the system may migrate built-in role names.")));
        if (request.CommandId == request.RoleId)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A command id distinct from the role is required.")));

        return UnitOperations.RunAsync(operations, content, clock, context, RegisterMemberHandler.ActorOf(context.Actor),
            request.CommandId, CommandKind, CurrentEpisode, null,
            selected => new Role(request.TenantId, request.RoleId, selected), role => role.Acceptance,
            role => role.PrepareRename(request.Name),
            (Role role, OperationProvenance operation, ImmutableContentRef response) => role.Rename(operation, response, request.Name), Role.NameUnchanged);
    }
}
