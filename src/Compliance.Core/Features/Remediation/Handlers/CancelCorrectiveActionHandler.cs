using Cntryl.Portia;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

/// <summary>Records a lead's reason for cancelling an open corrective action.</summary>
public sealed class CancelCorrectiveActionHandler : IRequestHandler<CancelCorrectiveAction, FindingView>
{
    public ValueTask<Result<FindingView>> HandleAsync(IRequestContext<CancelCorrectiveAction> context,
        CancellationToken ct) => ValueTask.FromResult(Result<FindingView>.Failure(new RequestError(
            RequestErrorKind.Conflict, $"Cancellation of action {context.Request.ActionId} is not implemented.")));
}
