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
///     Defines a built-in role as a native operation on the role owner, accepted under the role id as its immutable
///     creation identity. An exact retry replays the original acceptance, even after later renames or deletion, and
///     the same role id with a different name conflicts instead of being silently ignored.
/// </summary>
public sealed class DefineRoleHandler(NativeOperationExecutor operations, IImmutableContentStore content,
    TimeProvider clock) : IRequestHandler<DefineRole>
{
    const string CommandKind = "DefineRole";
    const string InitialEpisode = "initial";

    public ValueTask<Result> HandleAsync(IRequestContext<DefineRole> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!BuiltInRbac.IsBuiltInRole(request.TenantId, request.RoleId) || !RequestActor.IsSystem(context.Actor))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The role catalog is fixed and managed by the system.")));

        return UnitOperations.RunAsync(operations, content, clock, context, RegisterMemberHandler.ActorOf(context.Actor),
            request.RoleId, CommandKind, InitialEpisode, null,
            selected => new Role(request.TenantId, request.RoleId, selected), role => role.Acceptance,
            role => role.PrepareDefine(request.Name),
            (Role role, OperationProvenance operation, ImmutableContentRef response) => role.Define(operation, response, request.Name));
    }
}
