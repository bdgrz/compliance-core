using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Authorizers;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Handlers;

public sealed class ContinueWithOidcProviderHandler(UserIdentityContinuation continuation)
    : IRequestHandler<ContinueWithOidcProvider, AuthenticatedUserIdentity>
{
    public ValueTask<Result<AuthenticatedUserIdentity>> HandleAsync(
        IRequestContext<ContinueWithOidcProvider> context,
        CancellationToken ct)
    {
        if (!OidcProviderClaims.TryGet(context.Actor, out var provider,
                out var identifier, out var emailAddress, out var displayName))
        {
            return ValueTask.FromResult(Result<AuthenticatedUserIdentity>.Failure(new RequestError(
                RequestErrorKind.Unauthorized,
                "An authenticated OIDC issuer and subject are required.")));
        }

        return continuation.ContinueAsync(
            provider,
            identifier,
            emailAddress,
            // An existing session is not consent to link a second external identity.
            // Identity linking needs its own reviewed, authorized request.
            existingUserId: null,
            context,
            ct, observeProfile: true, displayName);
    }
}
