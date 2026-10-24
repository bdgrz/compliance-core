using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Reads the complete staged facts of a population from their verified immutable content.</summary>
static class AccessPopulationFactsContent
{
    static readonly AccessPopulationFacts NoFacts = new([], [], [], []);

    /// <summary>The facts currently staged on the population; an unrecorded draft has none.</summary>
    public static async Task<AccessPopulationFacts> ReadAsync(IImmutableContentStore content, Uuid tenantId,
        AccessPopulation population, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(population);
        return population.FactsContent is { } reference
            ? await AccessReviewContent.ReadAsync<AccessPopulationFacts>(content, tenantId, reference, ct)
                .ConfigureAwait(false)
            : NoFacts;
    }
}
