using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.NativeOperations;

public sealed class FirstResponsesTests
{
    static readonly ImmutableContentRef Body = new(Uuid.CreateVersion4(), new string('b', 64), 2, "application/json",
        "unit.v1");

    [Fact]
    public async Task ShouldReplayExactDescriptorGivenFrozenReference()
    {
        // Arrange
        var store = new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
        var descriptor = new FirstResponseDescriptor(202, "application/json", "operation-accepted-response.v1",
            new StandardResponseBody(Body));

        // Act
        var reference = await FirstResponses.FreezeAsync(store, "tenant-a", descriptor);
        var replayed = await FirstResponses.ReplayAsync(store, "tenant-a", reference);

        // Assert
        Assert.Equal(FirstResponses.Schema, reference.Schema);
        Assert.Equal(descriptor, replayed);
    }

    [Fact]
    public async Task ShouldFailClosedGivenReferenceToAnotherSchema()
    {
        // Arrange
        var store = new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
        var reference = await store.WriteJsonAsync("tenant-a", Uuid.CreateVersion4(), "unit.v1", Body);

        // Act
        var failure = await Record.ExceptionAsync(() => FirstResponses.ReplayAsync(store, "tenant-a", reference));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
    }

    [Fact]
    public void ShouldResolveRegisteredSchemasGivenAllowlist()
    {
        // Arrange
        // Act
        var found = ResultSchemaRegistry.TryGet(FirstResponses.Schema, out var info);
        var missing = ResultSchemaRegistry.TryGet("System.Object", out _);

        // Assert
        Assert.True(found);
        Assert.Equal(typeof(FirstResponseDescriptor), info!.Type);
        Assert.False(missing);
    }
}
