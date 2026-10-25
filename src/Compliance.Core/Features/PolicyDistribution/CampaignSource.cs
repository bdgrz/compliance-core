using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Loads a campaign as its owner and participants say it is. The campaign owner is hydrated first; its audience
///     and teams are read from verified immutable content, and only the participants that have an admitted result are
///     hydrated, because only they can have an applied result.
/// </summary>
public sealed class CampaignSource(IAggregateReader reader, IImmutableContentStore content, IDomainEventReader events)
{
    internal async ValueTask<Result<PolicyDistributionCampaign>> ReadOwnerAsync(Uuid tenantId, Uuid programId,
        Uuid campaignId, Uuid selectedLogicalId, CancellationToken ct)
    {
        var owner = await reader.HydrateAsync(new PolicyDistributionCampaign(tenantId, campaignId, selectedLogicalId), ct)
            .ConfigureAwait(false);
        _ = await PolicyCampaignHistory.ReadAsync(owner, events, ct).ConfigureAwait(false);
        return owner.IsLaunched && owner.ProgramId == programId
            ? Result<PolicyDistributionCampaign>.Success(owner)
            : Result<PolicyDistributionCampaign>.Failure(new RequestError(RequestErrorKind.NotFound, "The campaign was not found."));
    }

    internal async ValueTask<Result<CampaignState>> ReadAsync(Uuid tenantId, Uuid programId, Uuid campaignId,
        CancellationToken ct, Uuid selectedLogicalId = default)
    {
        var campaign = await reader.HydrateAsync(new PolicyDistributionCampaign(tenantId, campaignId,
            selectedLogicalId), ct).ConfigureAwait(false);
        return campaign.IsLaunched && campaign.ProgramId == programId
            ? await StateOfAsync(campaign, ct).ConfigureAwait(false)
            : Result<CampaignState>.Failure(new RequestError(RequestErrorKind.NotFound, "The campaign was not found."));
    }

    internal async ValueTask<Result<CampaignState>> StateOfAsync(PolicyDistributionCampaign campaign,
        CancellationToken ct)
    {
        try
        {
            var native = new PolicyCampaignReader(reader, events);
            var snapshot = await native.ReadAsync(campaign, ct).ConfigureAwait(false);
            var launch = snapshot.Launch!;
            var realm = campaign.Stream.Realm;
            var roster = await CampaignRoster.ReadAsync(content, snapshot, ct).ConfigureAwait(false);
            var teams = await content.ReadJsonAsync<IReadOnlyList<string>>(realm, launch.AudienceTeamsManifest, ct)
                .ConfigureAwait(false);
            var held = snapshot.Admitted.Select(static admitted => admitted.Operation.AffectedOwner).ToHashSet();
            var participants = new Dictionary<Uuid, PolicyCampaignParticipantSnapshot>();
            foreach (var member in roster.Order)
            {
                var stream = PolicyCampaignParticipant.StreamOf(campaign.TenantId, campaign.Id, member.PersonId);
                if (held.Contains(stream))
                    participants.Add(member.PersonId, await native.ReadParticipantAsync(campaign.TenantId, campaign.Id,
                        member.PersonId, ct: ct).ConfigureAwait(false));
            }

            var closingTotals = campaign.Closing is { } closed
                ? await content.ReadJsonAsync<CampaignTotalsView>(realm, closed.TotalsContent, ct).ConfigureAwait(false)
                : null;
            return Result<CampaignState>.Success(new CampaignState(snapshot, roster, teams, participants,
                closingTotals));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            ct.ThrowIfCancellationRequested();
            return Result<CampaignState>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The campaign content cannot be verified yet: {exception.Message}", true));
        }
    }
}
