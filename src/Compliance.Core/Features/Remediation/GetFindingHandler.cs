using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

public sealed class GetFindingHandler(IAggregateReader reader, IDomainEventReader events,
    TimeProvider clock)
    : IRequestHandler<GetFinding, FindingView>
{
    public async ValueTask<Result<FindingView>> HandleAsync(IRequestContext<GetFinding> context,
        CancellationToken ct)
    {
        var request = context.Request;
        return await new FindingReadSource(reader, events).ReadOneAsync(request.TenantId,
            request.ProgramId, request.FindingId, clock.GetUtcNow(), ct).ConfigureAwait(false);
    }
}
