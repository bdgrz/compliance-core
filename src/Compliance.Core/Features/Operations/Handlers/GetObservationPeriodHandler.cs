using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Returns current scoped period authority after its native opening has applied.</summary>
public sealed class GetObservationPeriodHandler(IAggregateReader reader)
    : IRequestHandler<GetObservationPeriod, ObservationPeriodView>
{
    public ValueTask<Result<ObservationPeriodView>> HandleAsync(IRequestContext<GetObservationPeriod> context, CancellationToken ct)
    {
        var request = context.Request;
        return ObservationPeriodRead.GetAsync(reader, request.TenantId, request.ProgramId, request.ObservationPeriodId, ct);
    }
}
