using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     Owns one broker stack per test class. Workers scan shared tenant/identity source patterns,
///     so unique tenant and application names alone do not isolate earlier classes' history.
///     <see cref="BrokerCollectionDefinition" /> runs the classes one at a time.
/// </summary>
public sealed class BrokerStackFixture : IAsyncLifetime, IAsyncDisposable
{
    BrokerStack? _stack;

    public string WebSocketEndpoint =>
        (_stack ?? throw new InvalidOperationException("The broker has not started.")).WebSocketEndpoint;

    public async Task InitializeAsync()
        => await StartAsync(CancellationToken.None);

    public async Task StartAsync(CancellationToken ct)
    {
        if (_stack is not null)
            throw new InvalidOperationException("The broker fixture has already started.");
        _stack = new BrokerStack();
        await _stack.StartAsync(ct);
    }

    public Task DisposeAsync() => ((IAsyncDisposable)this).DisposeAsync().AsTask();

    ValueTask IAsyncDisposable.DisposeAsync() => _stack?.DisposeAsync() ?? ValueTask.CompletedTask;
}
