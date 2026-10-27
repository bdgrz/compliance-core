using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Projections;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessReviews.Services;

static class RestrictedAccessReviewVisibility
{
    public static async ValueTask<bool> CanReadPopulationAsync(IAggregateReader reader,
        RestrictedApplicationVisibility visibility, Uuid tenantId, Uuid userId,
        Uuid populationId, CancellationToken ct)
    {
        var population = await reader.HydrateAsync(new AccessPopulation(tenantId, populationId), ct)
            .ConfigureAwait(false);
        return population.Opened is { } opened &&
               await visibility.CanReadSystemInstanceAsync(tenantId, userId,
                   opened.ApplicationId, opened.SystemInstanceId, ct).ConfigureAwait(false);
    }

    public static async ValueTask<bool> CanReadCampaignItemsAsync(
        RestrictedApplicationVisibility visibility, Uuid tenantId, Uuid userId,
        AccessReviewCampaignSnapshot campaign, IReadOnlyCollection<Uuid>? itemIds = null,
        CancellationToken ct = default)
    {
        if (campaign.Launched is not { } launched)
            return false;

        var items = launched.Items.AsEnumerable();
        if (itemIds is not null)
        {
            var selected = new List<AccessReviewItemView>(itemIds.Count);
            foreach (var itemId in itemIds.Distinct())
            {
                var item = launched.Items.FirstOrDefault(candidate => candidate.ItemId == itemId);
                if (item is null)
                    return false;
                selected.Add(item);
            }
            items = selected;
        }

        foreach (var systemInstanceId in items.Select(static item => item.SystemInstanceId)
                     .Distinct())
        {
            if (!await visibility.CanReadSystemInstanceAsync(tenantId, userId,
                    systemInstanceId, ct).ConfigureAwait(false))
                return false;
        }
        return true;
    }
}
