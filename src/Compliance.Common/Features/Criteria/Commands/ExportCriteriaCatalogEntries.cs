using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Criteria.Models;
namespace Bdgrz.Compliance.Features.Criteria.Commands;

[Discriminator("bdgrz.criteria.entries.export", 1)]
public sealed record ExportCriteriaCatalogEntries(Uuid TenantId, Uuid EditionId,
    string? Category = null, string? Kind = null, string? ParentIdentifier = null,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<Criterion>>, ITenantAccessRequest, ICallable;
