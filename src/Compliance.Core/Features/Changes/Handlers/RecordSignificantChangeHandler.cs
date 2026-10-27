using Bdgrz.Compliance.Features.Changes;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Changes.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes.Handlers;

public sealed class RecordSignificantChangeHandler(SignificantChangeOperations operations)
    : IRequestHandler<RecordSignificantChange, UnitResult>
{
    public ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<RecordSignificantChange> context, CancellationToken ct)
    {
        var request = context.Request;
        var (member, display) = ClientServiceActor.Snapshot(context);
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.ChangeId,
            request.ChangeId, nameof(RecordSignificantChange), null, ActorReference.ForMember(member, display),
            (snapshot, operation, token) => ValueTask.FromResult(SignificantChangeOperations.Outcome(snapshot.Rules.Record(request.Content, member, display, operation.AcceptedAt))), ct);
    }
}
