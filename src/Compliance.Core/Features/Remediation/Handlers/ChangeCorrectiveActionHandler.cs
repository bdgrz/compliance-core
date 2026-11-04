using Cntryl.Portia;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

/// <summary>Changes open corrective work for a lead or the finding's owner.</summary>
public sealed class ChangeCorrectiveActionHandler : IRequestHandler<ChangeCorrectiveAction, FindingView>
{
    public ValueTask<Result<FindingView>> HandleAsync(IRequestContext<ChangeCorrectiveAction> context, CancellationToken ct) =>
        ValueTask.FromResult(Result<FindingView>.Failure(new RequestError(RequestErrorKind.Conflict,
            $"Changing action {context.Request.ActionId} is not implemented.")));
}
