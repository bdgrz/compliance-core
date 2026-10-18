using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

public sealed class ApproveSystemDescriptionHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ApproveSystemDescription>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ApproveSystemDescription> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!SystemDescriptionCommandActor.TryGet(context.Actor, request.TenantId,
                out var approverMemberId, out var approverDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "System description approval requires a Bdgrz member identity.")));
        return executor.ExecuteAsync(new SystemDescription(request.TenantId, request.ProgramId,
                request.DescriptionId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.Approve(request.Version, approverMemberId, approverDisplay,
                    request.Rationale, clock.GetUtcNow())), context, ct);
    }
}
