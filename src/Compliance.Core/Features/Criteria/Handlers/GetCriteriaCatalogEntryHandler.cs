using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Queries;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Criteria.Handlers;

public sealed class GetCriteriaCatalogEntryHandler(IRuntimeCriteriaCatalogReader catalog,
    ICriteriaTextOverlayReader overlays)
    : IRequestHandler<GetCriteriaCatalogEntry, Criterion>
{
    public async ValueTask<Result<Criterion>> HandleAsync(IRequestContext<GetCriteriaCatalogEntry> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var result = await catalog.GetEntryAsync(request.EditionId, request.Identifier, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return Result<Criterion>.Failure(result.Error!);
        var item = result.Value!;
        if (item.Status == "ineffective")
        {
            var edition = await catalog.GetEditionAsync(request.EditionId, ct).ConfigureAwait(false);
            if (!edition.IsSuccess)
                return Result<Criterion>.Failure(edition.Error!);
            if (edition.Value!.Coverage == "complete" && edition.Value.PublicationEventId is { } publication &&
                publication != Uuid.Empty)
                return Result<Criterion>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The criterion is not effective in this published edition."));
        }
        if (!CriteriaCatalogEntriesPage.TryConvert(item, request.EditionId, out var entry))
            return Result<Criterion>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The criterion or its publication coverage is not available yet.", true));
        return await overlays.ApplyAsync(request.TenantId, entry, isExport: false, ct)
            .ConfigureAwait(false);
    }
}
