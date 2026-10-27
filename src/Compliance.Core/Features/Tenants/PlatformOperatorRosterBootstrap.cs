using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Seeds configured operators once; concurrent API and worker starts share one stream.</summary>
public sealed class PlatformOperatorRosterBootstrap(IServiceScopeFactory scopes,
    PlatformOperatorAuthority configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (configuration.DeveloperAuthentication && configuration.BootstrapUserIds.Count == 0)
            return;

        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var roster = await reader.HydrateAsync(new PlatformOperatorRoster(), cancellationToken)
            .ConfigureAwait(false);
        if (roster.IsInitialized)
        {
            await PlatformOperatorRosterContent.ReadAsync(services.GetRequiredService<IImmutableContentStore>(), roster, cancellationToken)
                .ConfigureAwait(false);
            return;
        }
        if (configuration.BootstrapUserIds.Count == 0)
            throw new InvalidOperationException(
                "Configure PlatformOperators:UserIds to seed the initial operator roster.");

        var command = new SeedPlatformOperatorRoster(PlatformOperatorRoster.CreationId,
            [.. configuration.BootstrapUserIds]);
        var bus = services.GetRequiredService<IRequestBus>();
        Result result;
        try
        {
            result = await bus.SendAsync(command, RequestActor.System, cancellationToken).ConfigureAwait(false);
        }
        catch (EventStreamConcurrencyException)
        {
            // Resolve the exact accepted creation after another host won the append.
            result = await bus.SendAsync(command, RequestActor.System, cancellationToken).ConfigureAwait(false);
        }
        if (!result.IsSuccess)
            throw new InvalidOperationException($"Operator roster bootstrap failed: {result.Error.Message}");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
