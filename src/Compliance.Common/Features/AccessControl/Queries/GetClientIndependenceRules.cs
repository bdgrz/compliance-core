using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Queries;

/// <summary>Reads platform draft classifications within a currently authorized client's administration context.</summary>
[Discriminator("bdgrz.independence.client-rules.get", 1)]
public sealed record GetClientIndependenceRules(Uuid TenantId)
    : IRequest<IReadOnlyList<IndependenceRuleVersionView>>, IIndependenceAdministrationRequest, ICallable;
