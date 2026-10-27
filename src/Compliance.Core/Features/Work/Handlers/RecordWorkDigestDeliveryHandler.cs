using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Records only the exact admitted attempt, with original native result recovery before live guard reads.</summary>
public sealed class RecordWorkDigestDeliveryHandler(WeeklyDigestOperations operations) : IRequestHandler<RecordWorkDigestDelivery>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordWorkDigestDelivery> context, CancellationToken ct) =>
        WorkDigestSourceFailures.ClassifyAsync(() => operations.RecordAsync(context, ct));
}
