using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Providers.Models;
using System.Text.Json;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderMalformedInputTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("{\"name\":\"Provider\",\"provider_kind\":\"Supplier\",\"dependencies\":[null]}")]
    [InlineData("{\"name\":\"Provider\",\"provider_kind\":\"Supplier\",\"materiality_basis\":[null]}")]
    public async Task ShouldRejectMalformedNestedInputGivenRecordRevisionAndReferenceResolution(string json)
    {
        // Arrange
        var content = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.ProviderContent)!;
        var tenantId = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var services = new ServiceCollection();
        var store = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(store);
        services.AddSingleton<IDomainEventReader>(store);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var references = new ProviderReferences(scope.ServiceProvider.GetRequiredService<IAggregateReader>(), store);
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());
        var admission = new NamedRecordAdmission(new NativeOperationExecutor(
            scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), null!),
            scope.ServiceProvider.GetRequiredService<IAggregateReader>(), null!, TimeProvider.System);

        // Act
        var creation = await new RecordProviderHandler(admission, references).HandleAsync(
            new RequestContext<RecordProvider>(new RecordProvider(id, tenantId, content), actor), CancellationToken.None);
        var revision = await new ReviseProviderHandler(admission, references).HandleAsync(
            new RequestContext<ReviseProvider>(new ReviseProvider(Uuid.CreateVersion4(), tenantId, id, 1, content),
                actor), CancellationToken.None);
        var resolved = await references.ResolveAsync(tenantId, content, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, creation.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, revision.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, resolved.Error!.Kind);
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new Provider(tenantId, id));
        Assert.Equal(0UL, owner.CommittedStreamPosition);
    }
}
