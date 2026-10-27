using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.independence.history.get", 1)]
public sealed record GetClientIndependenceHistory(Uuid TenantId)
    : IRequest<IndependenceHistoryView>, IIndependenceAdministrationRequest, ICallable;
