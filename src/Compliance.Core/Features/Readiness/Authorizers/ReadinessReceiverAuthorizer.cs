using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Only the trusted process worker may dispatch readiness run and readiness decision receivers.</summary>
sealed class ReadinessReceiverAuthorizer : IRequestAuthorizer<IReadinessReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IReadinessReceiver> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the internal readiness processes may dispatch readiness receivers.")));
}
