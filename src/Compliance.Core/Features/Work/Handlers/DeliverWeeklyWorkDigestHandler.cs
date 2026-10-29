using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Processes;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Handlers;

/// <summary>Existing trusted delivery receiver; selected native retry precedes mutable source/recipient preparation.</summary>
public sealed class DeliverWeeklyWorkDigestHandler(WeeklyDigestOperations operations) : IRequestHandler<DeliverWeeklyWorkDigest>
{
    public ValueTask<Result> HandleAsync(IRequestContext<DeliverWeeklyWorkDigest> context, CancellationToken ct) =>
        WorkDigestSourceFailures.ClassifyAsync(() => operations.DeliverAsync(context, ct));
}
