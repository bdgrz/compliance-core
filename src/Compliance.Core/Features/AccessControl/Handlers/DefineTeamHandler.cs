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
///     Defines a team as a native operation on the team owner, accepted under the team id as its immutable creation
///     identity. An exact retry replays the original acceptance, even after deletion, and the same team id with a
///     different name conflicts instead of being silently ignored.
/// </summary>
public sealed class DefineTeamHandler(NativeOperationExecutor operations, IImmutableContentStore content,
    TimeProvider clock) : IRequestHandler<DefineTeam>
{
    const string CommandKind = "DefineTeam";
    const string InitialEpisode = "initial";

    public ValueTask<Result> HandleAsync(IRequestContext<DefineTeam> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.TeamId == Uuid.Empty)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A tenant and team are required.")));
        return UnitOperations.RunAsync(operations, content, clock, context, RegisterMemberHandler.ActorOf(context.Actor),
            request.TeamId, CommandKind, InitialEpisode, null,
            selected => new Team(request.TenantId, request.TeamId, selected), team => team.Acceptance,
            team => team.PrepareDefine(request.Name),
            (Team team, OperationProvenance operation, ImmutableContentRef response) => team.Define(operation, response, request.Name));
    }
}
