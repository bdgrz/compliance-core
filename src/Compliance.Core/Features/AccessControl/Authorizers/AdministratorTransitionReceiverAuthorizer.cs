using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The receivers of the administrator transition process run as the trusted process principal only; the tenant
///     they act for is the realm of their verified source and target, checked by each receiver against its proof.
/// </summary>
public sealed class AdministratorTransitionReceiverAuthorizer : IRequestAuthorizer<IAdministratorTransitionReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IAdministratorTransitionReceiver> context,
        CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Administrator transition receivers run only as the system process.")));
}
