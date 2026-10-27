using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Controls.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls.Handlers;

public sealed class DiscardControlDraftHandler(ControlOperations operations, ControlDraftDiscardReleaseGate releaseGate)
    : IRequestHandler<DiscardControlDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<DiscardControlDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteUnitAsync(context, request.TenantId, request.ProgramId, request.ControlId,
            request.CommandId, nameof(DiscardControlDraft), request.ExpectedRevision,
            (snapshot, actor, at, token) => ValueTask.FromResult(releaseGate.IsEnabled ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "Control draft discard is unavailable until all active event readers are upgraded."))),
            (rules, actor, at) => ControlOperations.Outcome(rules.Discard(request.ProgramId, request.ExpectedRevision,
                request.Rationale, actor.MemberId, actor.Display, at)), null, null, ct);
    }
}
