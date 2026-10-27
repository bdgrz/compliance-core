using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Only the trusted process worker may dispatch the access review launch and completion receivers.</summary>
sealed class AccessReviewReceiverAuthorizer : IRequestAuthorizer<IAccessReviewReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IAccessReviewReceiver> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the internal access review processes may dispatch access review receivers.")));
}
