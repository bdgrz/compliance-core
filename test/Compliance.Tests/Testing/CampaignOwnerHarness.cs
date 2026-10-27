using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     The campaign, participant and requirement owners over an in-memory store with the production content store and
///     no request pipeline: facts are seeded through the real owner paths and read back through the production
///     <see cref="CampaignSource" />, so owner behavior is tested without composing the application.
/// </summary>
sealed class CampaignOwnerHarness : IAsyncDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    public CampaignOwnerHarness()
    {
        Events = new InMemoryEventStore();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(Events);
        services.AddSingleton<IDomainEventReader>(Events);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(
            new PersistenceKvClient(new InMemoryKvClient())));
        services.AddScoped<NativeOperationExecutor>();
        services.AddPortia();
        services.AddVerifiedSourceUseReader();
        services.AddVerifiedPolicyCampaignReader();
        Provider = services.BuildServiceProvider();
    }

    public InMemoryEventStore Events { get; }

    public ServiceProvider Provider { get; }

    public Uuid TenantId { get; } = Uuid.CreateVersion4();

    public Uuid ProgramId { get; } = Uuid.CreateVersion4();

    public Uuid ManagerId { get; } = Uuid.CreateVersion4();

    public ActorReference Manager => ActorReference.ForMember(ManagerId, "Manager");

    public async Task<CampaignState> StateAsync(Uuid campaignId)
    {
        await using var scope = Provider.CreateAsyncScope();
        var state = await new CampaignSource(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(),
            scope.ServiceProvider.GetRequiredService<IDomainEventReader>()).ReadAsync(TenantId, ProgramId,
            campaignId, CancellationToken.None);
        Assert.True(state.IsSuccess, state.Error?.Message);
        return state.Value;
    }

    public ValueTask DisposeAsync() => Provider.DisposeAsync();
}
