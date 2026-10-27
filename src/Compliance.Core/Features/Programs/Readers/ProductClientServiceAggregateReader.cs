using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.History;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Handlers;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Programs.Readers;

/// <summary>Finalizes the exact native client service prefix for explicit reads, accepted retries and executor hydration.</summary>
public sealed class ProductClientServiceAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        TAggregate owner;
        try
        {
            owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
            if (owner is ClientService service)
                _ = await ProductClientServiceHistory.ReadAsync(events, service, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return owner;
        }
        catch (ImmutableContentIntegrityException)
        {
            ct.ThrowIfCancellationRequested();
            throw;
        }
        catch (InvalidOperationException error) when (aggregate is ClientService)
        {
            ct.ThrowIfCancellationRequested();
            throw new ImmutableContentIntegrityException("The client service cannot hydrate its native transition: " + error.Message);
        }
    }
}
