using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Models;

/// <summary>The exact authored request, original operator and server-frozen prior message/attempt authority.</summary>
public sealed record WorkDigestRetryAuthorizationContent(AuthorizeWorkDigestUnknownRetry Request,
    ActorReference Actor, ActorAttribution OriginalActor, WorkDigestDispatchStatusView Before, DateTimeOffset RetryDeadline,
    int MaximumAttempts, DateTimeOffset AcceptedAt);
