using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Controls.Commands;
namespace Bdgrz.Compliance.Features.Controls.Handlers;

public sealed class ProposeControlRetirementHandler(ControlOperations operations, ControlLifecycleReleaseGate releaseGate)
    : IRequestHandler<ProposeControlRetirement, ControlRetirementRegistration>
{
    public ValueTask<Result<ControlRetirementRegistration>> HandleAsync(IRequestContext<ProposeControlRetirement> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync<ProposeControlRetirement, ControlRetirementRegistration>(context, request.TenantId,
            request.ProgramId, request.ControlId, request.CommandId, nameof(ProposeControlRetirement), null,
            (snapshot, actor, at, token) => ValueTask.FromResult(releaseGate.IsEnabled ? Result.Success
                : Result.Failure(ControlLifecycleReleaseGate.Unavailable)),
            (rules, actor, at) => ControlOperations.Outcome(rules.ProposeRetirement(request.ProgramId,
                request.ExpectedApprovedVersionId, request.EffectiveUntil, request.Rationale, actor.MemberId, actor.Display, at)),
            null, null, ct);
    }
}
