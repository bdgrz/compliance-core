using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Queries;

/// <summary>Reads a population draft or its accepted immutable snapshot.</summary>
[Discriminator("bdgrz.access_population.get", 1)]
public sealed record GetAccessPopulation(Uuid TenantId, Uuid PopulationId)
    : IRequest<AccessPopulationView>, IAccessReviewRequest, ICallable;
