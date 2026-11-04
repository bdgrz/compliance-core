using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Readers;
using Bdgrz.Compliance.Features.Workforce.Services;

namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class ChangeSponsorHandler(IAggregateExecutor executor, IAggregateReader reader,
    IWorkforceObservationDirectoryReader roster, WorkforceObservationReadConsistency consistency, TimeProvider clock)
    : IRequestHandler<ChangeSponsor>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ChangeSponsor> context, CancellationToken ct)
    {
        _ = executor;
        _ = reader;
        _ = roster;
        _ = consistency;
        _ = clock;
        return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
            $"Named changes for work relationship {context.Request.RelationshipId} are not implemented.")));
    }
}
