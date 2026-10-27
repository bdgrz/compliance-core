using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Queries;

[Discriminator("bdgrz.criteria.entry.get", 1)]
public sealed record GetCriteriaCatalogEntry(Uuid TenantId, Uuid EditionId,
    string Identifier) : IRequest<Criterion>, ITenantAccessRequest, ICallable;
