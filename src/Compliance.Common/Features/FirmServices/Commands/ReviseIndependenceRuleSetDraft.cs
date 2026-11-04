using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Models;
namespace Bdgrz.Compliance.Features.FirmServices.Commands;

/// <summary>One named decision on a numbered independence rule set.</summary>
[Discriminator("bdgrz.firm-services.rule-set.revise-draft", 1)]
public sealed record ReviseIndependenceRuleSetDraft(Uuid CommandId, long Version, long ExpectedRevision, IndependenceRuleSetTerms Terms)
    : IRequest<IndependenceRuleSetView>, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
