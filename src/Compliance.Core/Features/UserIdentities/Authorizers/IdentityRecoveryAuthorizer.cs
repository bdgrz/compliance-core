using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Handlers;
namespace Bdgrz.Compliance.Features.UserIdentities.Authorizers;

sealed class IdentityRecoveryAuthorizer : IRequestAuthorizer<CompleteIdentityRecovery>
{
    public ValueTask<Result> AuthorizeAsync(
        IRequestContext<CompleteIdentityRecovery> context, CancellationToken ct)
    {
        _ = ct;
        return ValueTask.FromResult(OidcProviderClaims.TryGet(context.Actor,
            out _, out _, out _)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Completing identity recovery requires proof of the replacement provider identity.")));
    }
}
