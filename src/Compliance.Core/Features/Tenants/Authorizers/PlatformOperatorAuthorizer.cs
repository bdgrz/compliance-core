using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
namespace Bdgrz.Compliance.Features.Tenants.Authorizers;

/// <summary>Only a current platform operator may use platform-scoped operations.</summary>
sealed class PlatformOperatorAuthorizer(IPlatformOperatorAccess operators) : IRequestAuthorizer<IPlatformOperatorRequest>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<IPlatformOperatorRequest> context, CancellationToken ct)
    {
        if (RequestActor.IsSystem(context.Actor))
            return Result.Success;
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(
                RequestErrorKind.Unauthorized, "Platform operations require a Bdgrz user identity."));

        return await operators.IsOperatorAsync(userId, ct).ConfigureAwait(false)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The actor is not a platform operator."));
    }
}
