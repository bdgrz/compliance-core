using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.independence.history.get", 1)]
public sealed record GetClientIndependenceHistory(Uuid TenantId)
    : IRequest<IndependenceHistoryView>, IIndependenceAdministrationRequest, ICallable;
