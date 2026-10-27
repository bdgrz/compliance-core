using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Commands;

/// <summary>
///     Proposes an expected, required, or prohibited access rule; it has no effect until approved.
///     <paramref name="ExpectationId" /> is the immutable expectation identity. <paramref name="ExpectedRevision" />
///     is the revision of the expectation line the proposal joins: zero for a new line, otherwise the line's current
///     revision when it supersedes an approved expectation.
/// </summary>
[Discriminator("bdgrz.access_expectation.propose", 2)]
public sealed record ProposeAccessExpectation(Uuid ExpectationId, Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, long ExpectedRevision, string RuleKind,
    AccessExpectationParameters Parameters, string Rationale, DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveUntil = null, Uuid? SupersedesExpectationId = null)
    : IRequest<AccessExpectationView>, IAccessReviewMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => ExpectationId;
}
