using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>The exact authored request, original operator and server-frozen prior message/attempt authority.</summary>
public sealed record WorkDigestRetryAuthorizationContent(AuthorizeWorkDigestUnknownRetry Request,
    ActorReference Actor, ActorAttribution OriginalActor, WorkDigestDispatchStatusView Before, DateTimeOffset RetryDeadline,
    int MaximumAttempts, DateTimeOffset AcceptedAt);
