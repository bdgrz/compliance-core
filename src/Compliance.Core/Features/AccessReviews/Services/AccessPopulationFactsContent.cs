using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.AccessReviews.Services;

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
