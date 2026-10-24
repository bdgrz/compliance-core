using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     A personal approval of a proposed expectation. The proposer cannot approve it without an
///     exact-scope waiver. HTTP-only; it is not registered as an MCP tool.
///     <paramref name="ExpectedRevision" /> is the revision of the expectation line.
/// </summary>
[Discriminator("bdgrz.access_expectation.approve", 2)]
public sealed record ApproveAccessExpectation(Uuid CommandId, Uuid TenantId, Uuid SystemInstanceId,
    Uuid ExpectationId, long ExpectedRevision, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<AccessExpectationView>, IAccessReviewMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
