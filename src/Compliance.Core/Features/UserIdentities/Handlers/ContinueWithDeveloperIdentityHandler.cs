using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Features.UserIdentities.Handlers;

public sealed class ContinueWithDeveloperIdentityHandler(
    UserIdentityContinuation continuation,
    DeveloperUserRegistration developerRegistration)
    : IRequestHandler<ContinueWithDeveloperIdentity, AuthenticatedUserIdentity>
{
    public ValueTask<Result<AuthenticatedUserIdentity>> HandleAsync(
        IRequestContext<ContinueWithDeveloperIdentity> context,
        CancellationToken ct)
    {
        if (!developerRegistration.Enabled)
        {
            return ValueTask.FromResult(Result<AuthenticatedUserIdentity>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "Developer identity is only available when developer authentication is enabled.")));
        }

        if (!EmailAddresses.TryNormalize(context.Request.EmailAddress, out var normalizedEmail))
        {
            return ValueTask.FromResult(Result<AuthenticatedUserIdentity>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "Enter a valid email address.")));
        }

        var identifier = Uuid.CreateVersion5(
            DeveloperUserRegistration.IdentifierNamespaceId,
            normalizedEmail).ToString();
        return continuation.ContinueAsync(
            DeveloperUserRegistration.Provider,
            identifier,
            normalizedEmail,
            UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId) ? userId : null,
            context,
            ct);
    }
}
