using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Authorizers;

/// <summary>Only the trusted process worker may dispatch engagement acceptance receivers.</summary>
sealed class EngagementAcceptanceReceiverAuthorizer : IRequestAuthorizer<IEngagementAcceptanceReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IEngagementAcceptanceReceiver> context,
        CancellationToken ct) => ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            "Only the internal acceptance process may dispatch its receivers.")));
}
