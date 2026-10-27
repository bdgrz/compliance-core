using Cntryl.Portia;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Features.UserIdentities.Authorizers;

sealed class EmailOwnershipAuthorizer : IRequestAuthorizer<IEmailOwnershipRequest>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IEmailOwnershipRequest> context, CancellationToken ct)
    {
        if (RequestActor.IsSystem(context.Actor))
            return ValueTask.FromResult(Result.Success);

        var allowed = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId) &&
            userId == context.Request.UserId;
        return ValueTask.FromResult(allowed
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The actor may manage only their own email addresses.")));
    }
}
