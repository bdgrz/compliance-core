using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Processes;

/// <summary>Restricts primary participant reconciliation to system workers.</summary>
sealed class PlatformPrimarySignalAuthorizer : IRequestAuthorizer<ReconcilePlatformPrimarySignal>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<ReconcilePlatformPrimarySignal> context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(RequestActor.IsSystem(context.Actor) ? Result.Success :
            Result.Failure(new RequestError(RequestErrorKind.Forbidden, "A system participant is required.")));
    }
}
