using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Readers;

public interface ICriteriaTextOverlayReader
{
    ValueTask<Result<Criterion>> ApplyAsync(Uuid tenantId, Criterion entry, bool isExport,
        CancellationToken ct);

    ValueTask<Result<IReadOnlyList<Criterion>>> ApplyPageAsync(Uuid tenantId,
        IReadOnlyList<Criterion> entries, bool isExport, CancellationToken ct);
}
