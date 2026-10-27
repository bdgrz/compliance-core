using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Queries;

[Discriminator("bdgrz.workforce.person.list", 1)]
public sealed record ListPeople(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<PersonView>>, IWorkforceRequest, ICallable;
