using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.Work.Commands;

/// <summary>Identifies one same-week retry authorization after relay evidence confirms non-acceptance.</summary>
[Discriminator("bdgrz.work.digest.dispatch.unknown_retry.authorize", 2)]
public sealed record AuthorizeWorkDigestUnknownRetry(Uuid CommandId, Uuid TenantId, Uuid MemberId,
    DateOnly WeekOf, string EvidenceReference, string Rationale, long ExpectedRevision)
    : IRequest<WorkDigestDispatchStatusView>, ICallable, IPlatformOperatorRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
