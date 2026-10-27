using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Criteria.Models;
namespace Bdgrz.Compliance.Features.Criteria.Queries;

[Discriminator("bdgrz.criteria.edition.get", 1)]
public sealed record GetCriteriaCatalogEdition(Uuid TenantId, Uuid EditionId)
    : IRequest<CriteriaCatalogEdition>, ITenantAccessRequest, ICallable;
