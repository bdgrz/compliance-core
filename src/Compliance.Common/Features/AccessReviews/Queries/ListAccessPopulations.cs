using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Queries;

/// <summary>Lists one system instance's populations, newest observation first.</summary>
[Discriminator("bdgrz.access_population.list", 1)]
public sealed record ListAccessPopulations(Uuid TenantId, Uuid SystemInstanceId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<AccessPopulationSummaryView>>, IAccessReviewRequest, ICallable;
