using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Models;
namespace Bdgrz.Compliance.Features.FirmServices.Commands;

/// <summary>One named decision on a numbered independence rule set.</summary>
[Discriminator("bdgrz.firm-services.rule-set.draft", 1)]
public sealed record DraftIndependenceRuleSet(Uuid CommandId, long Version, IndependenceRuleSetTerms Terms)
    : IRequest<IndependenceRuleSetView>, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
