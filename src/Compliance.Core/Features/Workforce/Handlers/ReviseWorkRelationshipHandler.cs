using Bdgrz.Compliance.Features.Workforce.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Handlers;

/// <summary>Refuses the retired generic mutation while its legacy request contract remains registered.</summary>
public sealed class ReviseWorkRelationshipHandler : IRequestHandler<ReviseWorkRelationship>
{
    ValueTask<Result> IRequestHandler<ReviseWorkRelationship>.HandleAsync(
        IRequestContext<ReviseWorkRelationship> context, CancellationToken ct) =>
        ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
            "Generic work relationship revision is retired; use the named work relationship commands.")));
}
