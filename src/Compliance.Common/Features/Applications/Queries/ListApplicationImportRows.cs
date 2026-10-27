using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Models;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Queries;

[Discriminator("bdgrz.application_import.rows.list", 1)]
public sealed record ListApplicationImportRows(Uuid TenantId, Uuid BatchId,
    int? Limit = null, string? Cursor = null, long? MinimumRevision = null)
    : IRequest<Page<ApplicationImportRowView>>, IApplicationInventoryRequest, ICallable;
