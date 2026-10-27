using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.E2E;
using Cntryl.Fitz;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;

namespace Bdgrz.Compliance.Tests.Hosting.Persistence;

public sealed class PersistenceCompositionTests
{
    [Fact]
    public async Task ShouldPreflightActualSaveAttributionGivenCustomStoreInApplicationComposition()
    {
        // Arrange
        var events = new InMemoryEventStore();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(events);
        _ = services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            }).Build());
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var role = new Role(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(role.Define("Audit").IsSuccess);
        var scenario = new AggregateScenario<Role>(role);
        var subject = new string('a', 66_000);
        var context = new RequestDispatchContext(RequestActor.CreateSystem(subject, "issuer"));

        // Act
        await Assert.ThrowsAsync<EventFrameSizeException>(async () => await writer.SaveAsync(role, context));
        var written = 0;
        await foreach (var _ in events.ReadAsync(role.Stream, 0, CancellationToken.None))
            written++;

        // Assert
        Assert.Equal(0, written);
        Assert.Equal(subject, Assert.Single(scenario.PendingEvents).Metadata.Actor!.Subject);
        Assert.IsType<PersistenceEventStore>(provider.GetRequiredService<IEventStore>());
    }

    [Fact]
    public void ShouldMeasureConcreteEscapedPayloadGivenBaseEventReference()
    {
        // Arrange
        DomainEvent ev = new TenantRegistered(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            "\"é😺\\company", "acme");

        // Act
        var size = DomainEventPayload.Size(ev);
        var concrete = JsonSerializer.SerializeToUtf8Bytes((TenantRegistered)ev,
            ComplianceCoreJsonContext.Default.TenantRegistered);

        // Assert
        Assert.Equal(concrete.Length, size);
        Assert.True(size > Encoding.UTF8.GetByteCount(((TenantRegistered)ev).Name));
    }

    [Fact]
    public async Task ShouldUseCheckedPersistenceAndSharedNotifierGivenApplicationComposition()
    {
        // Arrange
        await using var provider = EventPersistenceFrameTests.Compose();
        await using var scope = provider.CreateAsyncScope();

        // Act
        var store = provider.GetRequiredService<IEventStore>();
        var reader = provider.GetRequiredService<IDomainEventReader>();
        var writer = provider.GetRequiredService<IDomainEventWriter>();
        var notifier = provider.GetRequiredService<IDomainEventNotifier>();
        var kv = provider.GetRequiredService<IKvClient>();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();

        // Assert
        Assert.IsType<PersistenceEventStore>(store);
        Assert.Same(store, reader);
        Assert.Same(store, writer);
        Assert.Same(store, notifier);
        Assert.IsType<PersistenceKvClient>(kv);
        Assert.IsType<PersistenceValidationRequestBus>(bus);
    }

    [Fact]
    public void ShouldProvideConcreteGeneratedMetadataGivenEveryDomainEventContract()
    {
        // Arrange
        var types = new[] { typeof(CoreAssembly).Assembly, typeof(TenantRegistered).Assembly }
            .SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => !type.IsAbstract && typeof(DomainEvent).IsAssignableFrom(type)).ToArray();

        // Act
        var missing = types.Where(static type => ComplianceCoreJsonContext.Default.GetTypeInfo(type) is null)
            .Select(static type => type.FullName).ToArray();

        // Assert
        Assert.NotEmpty(types);
        Assert.Empty(missing);
    }

    [Fact]
    public async Task ShouldComposeActivationProbeGivenFactoryBackedPersistenceDecorator()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            }).Build());
        var probe = new ActivationReactionProbe();

        // Act
        ActivationReactionProbe.Install(services, probe);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();

        // Assert
        Assert.NotNull(bus);
        Assert.Same(probe, provider.GetRequiredService<ActivationReactionProbe>());
    }
}
