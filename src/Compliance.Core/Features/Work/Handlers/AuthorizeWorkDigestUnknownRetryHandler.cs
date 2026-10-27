using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.Work.Handlers;

/// <summary>Current platform authorization runs before selected native admission and original-result recovery.</summary>
public sealed class AuthorizeWorkDigestUnknownRetryHandler(WorkDigestRetryOperations operations)
    : IRequestHandler<AuthorizeWorkDigestUnknownRetry, WorkDigestDispatchStatusView>
{
    public ValueTask<Result<WorkDigestDispatchStatusView>> HandleAsync(
        IRequestContext<AuthorizeWorkDigestUnknownRetry> context, CancellationToken ct) =>
        WorkDigestSourceFailures.ClassifyAsync(() => operations.AuthorizeAsync(context, ct));
}
