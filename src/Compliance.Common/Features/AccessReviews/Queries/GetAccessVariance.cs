using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Queries;

/// <summary>Explains an accepted population against approved expectations without deciding any item.</summary>
[Discriminator("bdgrz.access_population.variance.get", 1)]
public sealed record GetAccessVariance(Uuid TenantId, Uuid PopulationId)
    : IRequest<AccessVarianceView>, IAccessReviewRequest, ICallable;
