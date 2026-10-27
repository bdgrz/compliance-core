using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

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
