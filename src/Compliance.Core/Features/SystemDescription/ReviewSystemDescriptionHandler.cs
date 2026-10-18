using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

public sealed class ReviewSystemDescriptionHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReviewSystemDescription>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviewSystemDescription> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!SystemDescriptionCommandActor.TryGet(context.Actor, request.TenantId,
                out var reviewerMemberId, out var reviewerDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "System description review requires a Bdgrz member identity.")));
        return executor.ExecuteAsync(new SystemDescription(request.TenantId, request.ProgramId,
                request.DescriptionId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.Review(request.Version, reviewerMemberId, reviewerDisplay, request.Outcome,
                    request.Comments, clock.GetUtcNow())), context, ct);
    }
}
