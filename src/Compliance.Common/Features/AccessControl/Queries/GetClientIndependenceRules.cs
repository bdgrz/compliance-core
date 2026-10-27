using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Queries;

/// <summary>Reads platform draft classifications within a currently authorized client's administration context.</summary>
[Discriminator("bdgrz.independence.client-rules.get", 1)]
public sealed record GetClientIndependenceRules(Uuid TenantId)
    : IRequest<IReadOnlyList<IndependenceRuleVersionView>>, IIndependenceAdministrationRequest, ICallable;
