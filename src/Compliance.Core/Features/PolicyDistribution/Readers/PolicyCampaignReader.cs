using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.History;
using Bdgrz.Compliance.Features.PolicyDistribution.Projections;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Readers;

/// <summary>Reads explicit complete history at a verified native cut, without latest audience or material lookups.</summary>
public sealed class PolicyCampaignReader(IAggregateReader reader, IDomainEventReader events)
{
    public async ValueTask<PolicyCampaignSnapshot> ReadAsync(Uuid tenantId, Uuid campaignId,
        Uuid selectedLogicalId = default, Uuid selectedRosterSnapshotId = default, Uuid selectedClosureRootId = default,
        CancellationToken ct = default) => await ReadAsync(await reader.HydrateAsync(new PolicyDistributionCampaign(
            tenantId, campaignId, selectedLogicalId, selectedRosterSnapshotId, selectedClosureRootId), ct)
            .ConfigureAwait(false), ct).ConfigureAwait(false);

    public async ValueTask<PolicyCampaignSnapshot> ReadAsync(PolicyDistributionCampaign owner,
        CancellationToken ct = default) => new(owner, await PolicyCampaignHistory.ReadAsync(owner, events, ct).ConfigureAwait(false));

    public async ValueTask<PolicyCampaignParticipantSnapshot> ReadParticipantAsync(Uuid tenantId, Uuid campaignId,
        Uuid personId, Uuid selectedLogicalId = default, Uuid selectedResultRootId = default, CancellationToken ct = default) =>
        await ReadParticipantAsync(await reader.HydrateAsync(new PolicyCampaignParticipant(tenantId, campaignId, personId,
            selectedLogicalId, selectedResultRootId), ct).ConfigureAwait(false), ct).ConfigureAwait(false);

    public async ValueTask<PolicyCampaignParticipantSnapshot> ReadParticipantAsync(PolicyCampaignParticipant owner,
        CancellationToken ct = default) => new(owner, await PolicyCampaignHistory.ReadAsync(owner, events, ct).ConfigureAwait(false));
}
