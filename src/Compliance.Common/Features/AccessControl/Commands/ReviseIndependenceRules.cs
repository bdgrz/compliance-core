using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

[Discriminator("bdgrz.independence.rules.revise", 1)]
public sealed record ReviseIndependenceRules(Uuid CommandId, long ExpectedSequence,
    IndependenceRuleContent Content) : IRequest<IndependenceRuleVersionView>,
    IIndependenceRuleAdministrationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
