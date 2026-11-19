using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Aggregates;
using Bdgrz.Compliance.Features.FirmServices.Models;
namespace Bdgrz.Compliance.Features.FirmServices.Readers;

/// <summary>Source reads select the highest ratified consecutive version, regardless of later author status.</summary>
sealed class IndependenceRuleSetReader(IAggregateReader reader)
{
    public async ValueTask<IndependenceRuleSetView?> ReadActiveAsync(CancellationToken ct = default)
    {
        IndependenceRuleSetView? active = null;
        for (long version = 1; ; version++)
        {
            var owner = await reader.HydrateAsync(new IndependenceRuleSet(version), ct).ConfigureAwait(false);
            if (owner.View is not { IsRatified: true } current)
                return active;
            active = current;
            if (version == long.MaxValue)
                return active;
        }
    }
}
