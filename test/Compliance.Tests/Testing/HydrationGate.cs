using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Responsibilities.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Holds hydrations of one aggregate type until every racer has loaded the stream.</summary>
sealed class HydrationGate<TOwner>(IAggregateReader inner, int participants) : IAggregateReader where TOwner : Aggregate
{
    readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    int _arrived;

    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
        CancellationToken ct = default) where TAggregate : Aggregate
    {
        var hydrated = await inner.HydrateAsync(aggregate, ct);
        if (hydrated is not TOwner)
            return hydrated;
        if (Interlocked.Increment(ref _arrived) >= participants)
            _released.TrySetResult();
        await _released.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        return hydrated;
    }
}
