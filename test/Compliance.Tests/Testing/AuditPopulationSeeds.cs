using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
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
