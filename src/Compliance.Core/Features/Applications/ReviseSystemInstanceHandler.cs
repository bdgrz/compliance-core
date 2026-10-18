using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class ReviseSystemInstanceHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReviseSystemInstance>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseSystemInstance> context,
        CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        return executor.ExecuteAsync(new DeclaredSystemInstance(request.TenantId,
                request.SystemInstanceId), instance => instance.IsCreated &&
                instance.ApplicationId != request.ApplicationId
            ? AggregateOutcome.Discard(Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The system instance was not found.")))
            : CommandFailureRequestAdapter.ToOutcome(instance.Revise(request.ExpectedRevision,
                request.Name, request.SourceIdentifier, request.AccessBoundary, request.Rationale,
                memberId, display, clock.GetUtcNow())), context, ct);
    }
}
