using Cntryl.Portia;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Services;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Rules;

static class CampaignWorkforceChangeRules
{
    public static Uuid CommandId(Uuid sourceEventId, Uuid campaignId) =>
        AggregateIdentity.Create("CampaignWorkforceChange", sourceEventId, campaignId);

    public static CampaignAudienceAmendment? Plan(Uuid personId, string displayName,
        CampaignParticipant? existing, RosterAudience current, string sourceKind, DateOnly campaignDueOn)
    {
        if (current.Matching.TryGetValue(personId, out var person))
        {
            if (existing?.IsActive == true)
                return null;
            var joiner = sourceKind is "started" or "recorded";
            return new(personId, displayName, joiner ? "joiner" : "mover_in", person.WorkerType,
                person.Department, joiner ? person.StartDate.AddDays(30) : campaignDueOn);
        }
        return existing?.IsActive == true ? new(personId, existing.DisplayName,
            current.Present.Contains(personId) ? "mover_out" : "leaver", null, null, null) : null;
    }
}
