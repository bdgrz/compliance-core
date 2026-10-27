using Bdgrz.Compliance.Features.Changes;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Changes.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes.Handlers;

public sealed class AssessSignificantChangeHandler(SignificantChangeOperations operations)
    : IRequestHandler<AssessSignificantChange, UnitResult>
{
    public ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<AssessSignificantChange> context, CancellationToken ct)
    {
        var request = context.Request;
        var (member, display) = ClientServiceActor.Snapshot(context);
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.ChangeId,
            request.CommandId, nameof(AssessSignificantChange), request.ExpectedRevision, ActorReference.ForMember(member, display),
            (snapshot, operation, token) => ValueTask.FromResult(SignificantChangeOperations.Outcome(snapshot.Rules.Assess(request.ExpectedRevision, request.Content, member, display, operation.AcceptedAt))), ct);
    }
}
