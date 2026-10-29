using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

[Discriminator("bdgrz.independence.rules.revise", 1)]
public sealed record ReviseIndependenceRules(Uuid CommandId, long ExpectedSequence,
    IndependenceRuleContent Content) : IRequest<IndependenceRuleVersionView>,
    IIndependenceRuleAdministrationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
