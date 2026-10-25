using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Identifies one same-week retry authorization after relay evidence confirms non-acceptance.</summary>
[Discriminator("bdgrz.work.digest.dispatch.unknown_retry.authorize", 2)]
public sealed record AuthorizeWorkDigestUnknownRetry(Uuid CommandId, Uuid TenantId, Uuid MemberId,
    DateOnly WeekOf, string EvidenceReference, string Rationale, long ExpectedRevision)
    : IRequest<WorkDigestDispatchStatusView>, ICallable, IPlatformOperatorRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
