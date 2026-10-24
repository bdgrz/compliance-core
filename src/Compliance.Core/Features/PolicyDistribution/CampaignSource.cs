using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Loads a campaign as its owner and participants say it is. The campaign owner is hydrated first; its audience
///     and teams are read from verified immutable content, and only the participants that have an admitted result are
///     hydrated, because only they can have an applied result.
/// </summary>
public sealed class CampaignSource(IAggregateReader reader, IImmutableContentStore content)
{
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
            var launch = campaign.Launch!;
            var realm = campaign.Stream.Realm;
            var roster = await CampaignRoster.ReadAsync(content, campaign, ct).ConfigureAwait(false);
            var teams = await content.ReadJsonAsync<IReadOnlyList<string>>(realm, launch.AudienceTeamsManifest, ct)
                .ConfigureAwait(false);
            var held = campaign.Admitted.Select(static admitted => admitted.Operation.AffectedOwner).ToHashSet();
            var participants = new Dictionary<Uuid, PolicyCampaignParticipant>();
            foreach (var member in roster.Order)
            {
                var stream = PolicyCampaignParticipant.StreamOf(campaign.TenantId, campaign.Id, member.PersonId);
                if (held.Contains(stream))
                    participants.Add(member.PersonId, await reader.HydrateAsync(new PolicyCampaignParticipant(
                        campaign.TenantId, campaign.Id, member.PersonId), ct).ConfigureAwait(false));
            }

            var closingTotals = campaign.Closing is { } closed
                ? await content.ReadJsonAsync<CampaignTotalsView>(realm, closed.TotalsContent, ct).ConfigureAwait(false)
                : null;
            return Result<CampaignState>.Success(new CampaignState(campaign, roster, teams, participants,
                closingTotals));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<CampaignState>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The campaign content cannot be verified yet: {exception.Message}", true));
        }
    }
}
