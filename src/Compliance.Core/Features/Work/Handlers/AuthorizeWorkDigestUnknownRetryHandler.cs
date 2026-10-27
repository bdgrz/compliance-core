using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Handlers;

/// <summary>Current platform authorization runs before selected native admission and original-result recovery.</summary>
public sealed class AuthorizeWorkDigestUnknownRetryHandler(WorkDigestRetryOperations operations)
    : IRequestHandler<AuthorizeWorkDigestUnknownRetry, WorkDigestDispatchStatusView>
{
    public ValueTask<Result<WorkDigestDispatchStatusView>> HandleAsync(
        IRequestContext<AuthorizeWorkDigestUnknownRetry> context, CancellationToken ct) =>
        WorkDigestSourceFailures.ClassifyAsync(() => operations.AuthorizeAsync(context, ct));
}
