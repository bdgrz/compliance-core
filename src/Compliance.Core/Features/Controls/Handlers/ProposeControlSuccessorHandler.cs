using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class ProposeControlSuccessorHandler(ControlOperations operations,
    IControlApplicabilityReferenceValidator applicability, ControlLifecycleReleaseGate releaseGate)
    : IRequestHandler<ProposeControlSuccessor, ControlSuccessorRegistration>
{
    public ValueTask<Result<ControlSuccessorRegistration>> HandleAsync(IRequestContext<ProposeControlSuccessor> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync<ProposeControlSuccessor, ControlSuccessorRegistration>(context, request.TenantId,
            request.ProgramId, request.ControlId, request.CommandId, nameof(ProposeControlSuccessor), null,
            async (snapshot, actor, at, token) =>
            {
                if (!releaseGate.IsEnabled)
                    return Result.Failure(ControlLifecycleReleaseGate.Unavailable);
                if (ControlDraft.ValidateContent(request.Content) is { } structural)
                    return Result.Failure(structural);
                return await applicability.ValidateAsync(request.TenantId, request.Content, token).ConfigureAwait(false);
            },
            (rules, actor, at) => ControlOperations.Outcome(rules.ProposeSuccessor(request.ProgramId,
                request.ExpectedApprovedVersionId, request.Content, actor.MemberId, actor.Display, at)), null, null, ct);
    }
}
