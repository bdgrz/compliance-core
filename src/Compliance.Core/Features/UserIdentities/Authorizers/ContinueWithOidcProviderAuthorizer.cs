using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Handlers;
namespace Bdgrz.Compliance.Features.UserIdentities.Authorizers;

/// <summary>Requires an authenticated external provider identity before continuing an OIDC session.</summary>
sealed class ContinueWithOidcProviderAuthorizer : IRequestAuthorizer<ContinueWithOidcProvider>
{
    public ValueTask<Result> AuthorizeAsync(
        IRequestContext<ContinueWithOidcProvider> context,
        CancellationToken ct) =>
        ValueTask.FromResult(OidcProviderClaims.TryGet(context.Actor, out _, out _, out _)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "An authenticated OIDC issuer and subject are required.")));
}
