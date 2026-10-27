using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.UserIdentities.Queries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Authorizers;

sealed class IdentityRecoveryOptionsAuthorizer : IRequestAuthorizer<GetIdentityRecoveryOptions>
{
    public ValueTask<Result> AuthorizeAsync(
        IRequestContext<GetIdentityRecoveryOptions> context, CancellationToken ct)
    {
        _ = ct;
        return ValueTask.FromResult(OidcProviderClaims.TryGet(context.Actor,
            out _, out _, out _)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Identity recovery options require proof of a provider identity.")));
    }
}
