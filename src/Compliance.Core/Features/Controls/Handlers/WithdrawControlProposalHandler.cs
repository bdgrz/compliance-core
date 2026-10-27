using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Controls.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls.Handlers;

public sealed class WithdrawControlProposalHandler(ControlOperations operations, ControlLifecycleReleaseGate releaseGate)
    : IRequestHandler<WithdrawControlProposal>
{
    public ValueTask<Result> HandleAsync(IRequestContext<WithdrawControlProposal> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteUnitAsync(context, request.TenantId, request.ProgramId, request.ControlId,
            request.CommandId, nameof(WithdrawControlProposal), request.ExpectedRevision,
            (snapshot, actor, at, token) => ValueTask.FromResult(releaseGate.IsEnabled ? Result.Success
                : Result.Failure(ControlLifecycleReleaseGate.Unavailable)),
            (rules, actor, at) => ControlOperations.Outcome(rules.Withdraw(request.ProgramId, request.ExpectedRevision,
                request.CommandId, request.Rationale, actor.MemberId, actor.Snapshot, at)), null, null, ct);
    }
}
