using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Handlers;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Readers;

/// <summary>Verifies full client service/use history before current reads, accepted retries and appends.</summary>
public sealed class ClientServiceAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (owner is ClientServiceIndependence client)
            _ = await ClientServiceNativeHistory.ReadAsync(events, client, ct).ConfigureAwait(false);
        else if (owner is ClientIndependenceDecision decision)
            await ClientServiceNativeHistory.VerifyDecisionAsync(events, decision, ct).ConfigureAwait(false);
        return owner;
    }
}
