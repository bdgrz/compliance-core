using Cntryl.Portia;
using Bdgrz.Compliance.Features.Changes.Services;
using Bdgrz.Compliance.Features.Programs.Services;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Changes.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.Changes.Handlers;

public sealed class ReviseSignificantChangeHandler(SignificantChangeOperations operations)
    : IRequestHandler<ReviseSignificantChange, UnitResult>
{
    public ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<ReviseSignificantChange> context, CancellationToken ct)
    {
        var request = context.Request;
        var (member, display) = ClientServiceActor.Snapshot(context);
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.ChangeId,
            request.CommandId, nameof(ReviseSignificantChange), request.ExpectedRevision, ActorReference.ForMember(member, display),
            (snapshot, operation, token) => ValueTask.FromResult(SignificantChangeOperations.Outcome(snapshot.Rules.Revise(request.ExpectedRevision, request.Content, request.ChangeReason, member, display, operation.AcceptedAt))), ct);
    }
}
