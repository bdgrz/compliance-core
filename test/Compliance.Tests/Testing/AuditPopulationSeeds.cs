using Bdgrz.Compliance.Features.AuditPopulations;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Publishes explicit occupied inline legacy fixture facts; every subsequent read uses the production history reader.</summary>
static class AuditPopulationSeeds
{
    public static async Task AppendLegacyAsync(IEventStore events, AuditPopulationRules rules, ulong position = 0)
    {
        var pending = rules.PendingChanges.ToArray();
        var first = rules.Revision - pending.Length;
        for (var index = 0; index < pending.Length; index++)
            DomainEventSeed.Attach(pending[index], rules.Id, (ulong)(first + index + 1));
        await events.AppendAsync(rules.Stream, position, pending);
        rules.ClearPendingChanges();
    }
}
