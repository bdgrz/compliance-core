using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Commands;

/// <summary>
///     A personal approval that an in-scope system instance may lack an accepted population until
///     an expiry. HTTP-only; it is not registered as an MCP tool. <paramref name="ExceptionId" /> is the immutable
///     creation identity of this independent exception and <paramref name="ExpectedRevision" /> must be zero.
/// </summary>
[Discriminator("bdgrz.access_population.exception.record", 2)]
public sealed record ExemptMissingAccessPopulation(Uuid ExceptionId, Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, long ExpectedRevision, string Reason, DateTimeOffset ExpiresAt)
    : IRequest<AccessPopulationExceptionView>, IAccessReviewMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => ExceptionId;
}
