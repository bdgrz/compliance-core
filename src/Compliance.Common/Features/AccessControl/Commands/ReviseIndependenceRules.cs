using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.independence.rules.revise", 1)]
public sealed record ReviseIndependenceRules(Uuid CommandId, long ExpectedSequence,
    IndependenceRuleContent Content) : IRequest<IndependenceRuleVersionView>,
    IIndependenceRuleAdministrationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
