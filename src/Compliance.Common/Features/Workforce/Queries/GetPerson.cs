using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Features.Workforce.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Queries;

[Discriminator("bdgrz.workforce.person.get", 1)]
public sealed record GetPerson(Uuid TenantId, Uuid PersonId, long? MinimumRevision = null)
    : IRequest<PersonView>, IWorkforceRequest, ICallable;
