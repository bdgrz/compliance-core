using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.independence.rules.list", 1)]
public sealed record GetIndependenceRuleVersions : IRequest<IReadOnlyList<IndependenceRuleVersionView>>,
    IIndependenceRuleAdministrationRequest, ICallable;
