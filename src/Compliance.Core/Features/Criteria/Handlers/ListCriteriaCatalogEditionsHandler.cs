using System.Globalization;
using System.Text;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Queries;
namespace Bdgrz.Compliance.Features.Criteria.Handlers;

public sealed class ListCriteriaCatalogEditionsHandler(ICriteriaCatalog catalog)
    : IRequestHandler<ListCriteriaCatalogEditions, IReadOnlyList<CriteriaCatalogEdition>>
{
    public ValueTask<Result<IReadOnlyList<CriteriaCatalogEdition>>> HandleAsync(
        IRequestContext<ListCriteriaCatalogEditions> context, CancellationToken ct) =>
        ValueTask.FromResult(Result<IReadOnlyList<CriteriaCatalogEdition>>.Success(catalog.Editions));
}
