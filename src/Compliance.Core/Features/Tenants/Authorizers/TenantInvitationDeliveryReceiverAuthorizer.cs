using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     The receivers of the invitation delivery process run as the trusted process principal only; the tenant they
///     act for is the realm of their verified invitation fact, checked by each receiver against its proof.
/// </summary>
public sealed class TenantInvitationDeliveryReceiverAuthorizer : IRequestAuthorizer<ITenantInvitationDeliveryReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<ITenantInvitationDeliveryReceiver> context,
        CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Invitation delivery receivers run only as the system process.")));
}
