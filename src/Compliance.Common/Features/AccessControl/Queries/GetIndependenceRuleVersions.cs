using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.independence.rules.list", 1)]
public sealed record GetIndependenceRuleVersions : IRequest<IReadOnlyList<IndependenceRuleVersionView>>,
    IIndependenceRuleAdministrationRequest, ICallable;
