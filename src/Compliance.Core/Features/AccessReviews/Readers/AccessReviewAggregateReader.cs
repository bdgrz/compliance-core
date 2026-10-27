using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.History;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.AccessReviews.Readers;

/// <summary>Verifies the complete native Access owner prefix before an installed read or append.</summary>
public sealed class AccessReviewAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (owner is AccessExpectation expectation)
            _ = await AccessExpectationHistory.ReadAsync(events, expectation, ct).ConfigureAwait(false);
        else if (owner is AccessReviewCampaign campaign)
            _ = await AccessReviewCampaignHistory.ReadAsync(events, campaign, ct).ConfigureAwait(false);
        else if (owner is AccessReviewItem item)
            _ = await AccessReviewItemHistory.ReadAsync(events, item, ct).ConfigureAwait(false);
        return owner;
    }
}
