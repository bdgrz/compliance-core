using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>Finalizes the exact native program prefix for explicit reads, accepted retries and executor hydration.</summary>
public sealed class ProgramAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        TAggregate owner;
        try
        {
            owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
            if (owner is ComplianceProgram program)
                _ = await ProgramHistory.ReadAsync(events, program, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return owner;
        }
        catch (ImmutableContentIntegrityException)
        {
            ct.ThrowIfCancellationRequested();
            throw;
        }
        catch (InvalidOperationException error) when (aggregate is ComplianceProgram)
        {
            ct.ThrowIfCancellationRequested();
            throw new ImmutableContentIntegrityException("The program cannot hydrate its native transition: " + error.Message);
        }
    }
}
