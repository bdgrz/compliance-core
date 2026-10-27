using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Criteria.Models;
namespace Bdgrz.Compliance.Features.Criteria.Queries;

[Discriminator("bdgrz.criteria.editions.list", 1)]
public sealed record ListCriteriaCatalogEditions(Uuid TenantId)
    : IRequest<IReadOnlyList<CriteriaCatalogEdition>>, ITenantAccessRequest, ICallable;
