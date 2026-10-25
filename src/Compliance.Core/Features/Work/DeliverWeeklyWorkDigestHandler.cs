using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Existing trusted delivery receiver; selected native retry precedes mutable source/recipient preparation.</summary>
public sealed class DeliverWeeklyWorkDigestHandler(WeeklyDigestOperations operations) : IRequestHandler<DeliverWeeklyWorkDigest>
{
    public ValueTask<Result> HandleAsync(IRequestContext<DeliverWeeklyWorkDigest> context, CancellationToken ct) =>
        WorkDigestSourceFailures.ClassifyAsync(() => operations.DeliverAsync(context, ct));
}
