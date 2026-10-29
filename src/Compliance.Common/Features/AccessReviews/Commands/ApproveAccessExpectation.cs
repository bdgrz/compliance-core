using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
namespace Bdgrz.Compliance.Features.AccessReviews.Commands;

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
