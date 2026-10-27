using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.independence.rules.ratify", 1)]
public sealed record RatifyIndependenceRuleVersion(Uuid CommandId, Uuid RatificationId, long RuleVersion,
    long ExpectedRuleCatalogSequence, long ExpectedRatificationSequence, string ExpectedContentDigest,
    string SourceReference, long ExpectedRevision) : IRequest<IndependenceRuleRatificationView>, IPersonalRuleRatificationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
