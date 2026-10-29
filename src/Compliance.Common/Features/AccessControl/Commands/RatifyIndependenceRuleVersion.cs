using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

[Discriminator("bdgrz.independence.rules.ratify", 1)]
public sealed record RatifyIndependenceRuleVersion(Uuid CommandId, Uuid RatificationId, long RuleVersion,
    long ExpectedRuleCatalogSequence, long ExpectedRatificationSequence, string ExpectedContentDigest,
    string SourceReference, long ExpectedRevision) : IRequest<IndependenceRuleRatificationView>, IPersonalRuleRatificationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
