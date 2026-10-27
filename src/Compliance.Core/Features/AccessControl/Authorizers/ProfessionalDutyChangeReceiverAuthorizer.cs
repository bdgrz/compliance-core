using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Only the trusted process worker may dispatch professional-duty change receivers.</summary>
sealed class ProfessionalDutyChangeReceiverAuthorizer : IRequestAuthorizer<IProfessionalDutyChangeReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IProfessionalDutyChangeReceiver> context,
        CancellationToken ct) => ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            "Only the internal professional-duty process may dispatch change receivers.")));
}
