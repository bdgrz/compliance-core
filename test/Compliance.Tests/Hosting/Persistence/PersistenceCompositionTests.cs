using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.E2E;
using Cntryl.Fitz;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Tests;
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
