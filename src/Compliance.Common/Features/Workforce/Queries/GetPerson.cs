using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Queries;

[Discriminator("bdgrz.workforce.person.get", 1)]
public sealed record GetPerson(Uuid TenantId, Uuid PersonId, long? MinimumRevision = null)
    : IRequest<PersonView>, IWorkforceRequest, ICallable;
