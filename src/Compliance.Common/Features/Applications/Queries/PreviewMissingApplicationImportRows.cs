using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
namespace Bdgrz.Compliance.Features.Applications.Queries;

[Discriminator("bdgrz.application_import.missing.preview", 1)]
public sealed record PreviewMissingApplicationImportRows(Uuid TenantId, Uuid BatchId,
    int? Limit = null, string? Cursor = null, long? MinimumRevision = null)
    : IRequest<Page<ApplicationImportMissingRow>>, IApplicationInventoryRequest, ICallable;
