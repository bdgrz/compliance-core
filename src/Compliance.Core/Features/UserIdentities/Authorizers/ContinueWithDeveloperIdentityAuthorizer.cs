using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
namespace Bdgrz.Compliance.Features.UserIdentities.Authorizers;

/// <summary>Allows developer identity continuation only while developer authentication is enabled.</summary>
sealed class ContinueWithDeveloperIdentityAuthorizer(DeveloperUserRegistration developerRegistration)
    : IRequestAuthorizer<ContinueWithDeveloperIdentity>
{
    public ValueTask<Result> AuthorizeAsync(
        IRequestContext<ContinueWithDeveloperIdentity> context,
        CancellationToken ct) =>
        ValueTask.FromResult(developerRegistration.Enabled
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Developer identity is only available when developer authentication is enabled.")));
}
