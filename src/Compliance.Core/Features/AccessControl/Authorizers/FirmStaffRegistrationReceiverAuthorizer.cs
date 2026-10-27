using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Authorizers;

/// <summary>Only the trusted process worker may dispatch registration receivers.</summary>
sealed class FirmStaffRegistrationReceiverAuthorizer : IRequestAuthorizer<IFirmStaffRegistrationReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IFirmStaffRegistrationReceiver> context,
        CancellationToken ct) => ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            "Only the internal registration process may dispatch staff registration receivers.")));
}
