using Bdgrz.Compliance.Features.Workforce.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Handlers;

/// <summary>Refuses the retired generic mutation while its legacy request contract remains registered.</summary>
public sealed class ReviseServiceIdentityHandler : IRequestHandler<ReviseServiceIdentity>
{
    ValueTask<Result> IRequestHandler<ReviseServiceIdentity>.HandleAsync(
        IRequestContext<ReviseServiceIdentity> context, CancellationToken ct) =>
        ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
            "Generic service identity revision is retired; use the named service identity commands.")));
}
