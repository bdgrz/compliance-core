using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Commands;

/// <summary>
///     A personal approval of an expiring exception to an approved expectation. HTTP-only; it is
///     not registered as an MCP tool. <paramref name="ExpectedRevision" /> is the revision of the expectation line.
/// </summary>
[Discriminator("bdgrz.access_expectation.exception.record", 2)]
public sealed record ExemptAccessExpectation(Uuid CommandId, Uuid TenantId, Uuid SystemInstanceId,
    Uuid ExpectationId, long ExpectedRevision, string ProviderSubjectId,
    string Rationale, DateTimeOffset ExpiresAt, string? ProviderEntitlementId = null)
    : IRequest<AccessExpectationExceptionView>, IAccessReviewMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
