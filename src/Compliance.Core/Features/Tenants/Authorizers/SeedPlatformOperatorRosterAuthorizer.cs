using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Commands;
namespace Bdgrz.Compliance.Features.Tenants.Authorizers;

sealed class SeedPlatformOperatorRosterAuthorizer : IRequestAuthorizer<SeedPlatformOperatorRoster>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<SeedPlatformOperatorRoster> context,
        CancellationToken ct) => ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            "Only the system may seed platform operators.")));
}
