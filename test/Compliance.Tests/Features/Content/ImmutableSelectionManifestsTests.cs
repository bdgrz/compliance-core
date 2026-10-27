using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Text;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Content;

public sealed class ImmutableSelectionManifestsTests
{
    const string Realm = "tenant-a";
    const string ItemSchema = "authoritative-read-cut.v1";

    [Fact]
    public async Task ShouldReadEveryItemInOrderGivenWrittenSelection()
    {
        // Arrange
        var store = Create();
        var items = Enumerable.Range(0, 500).Select(index => new AuthoritativeReadCut($"realm/area/{index}",
            (ulong)index, DateTimeOffset.UnixEpoch)).ToArray();

        // Act
        var manifest = await store.WriteSelectionAsync(Realm, Uuid.CreateVersion4(), ItemSchema, items,
            SelectionOrder.Authored);
        var read = await store.ReadSelectionAsync<AuthoritativeReadCut>(Realm, manifest);

        // Assert
        Assert.Equal(500, manifest.Count);
        Assert.Equal(ItemSchema, manifest.ItemSchema);
        Assert.Equal(items, read);
    }

    [Fact]
    public async Task ShouldReadEmptySelectionGivenNoItems()
    {
        // Arrange
        var store = Create();

        // Act
        var manifest = await store.WriteSelectionAsync(Realm, Uuid.CreateVersion4(), ItemSchema,
            Array.Empty<AuthoritativeReadCut>(), SelectionOrder.CanonicalKey);
        var read = await store.ReadSelectionAsync<AuthoritativeReadCut>(Realm, manifest);

        // Assert
        Assert.Equal(0, manifest.Count);
        Assert.Empty(read);
    }

    [Fact]
    public async Task ShouldFailClosedGivenManifestCountDigestOrSchemaThatDoNotMatchTheStoredItems()
    {
        // Arrange
        var store = Create();
        var manifest = await store.WriteSelectionAsync(Realm, Uuid.CreateVersion4(), ItemSchema,
            new[] { new AuthoritativeReadCut("realm/area/1", 1, DateTimeOffset.UnixEpoch) }, SelectionOrder.Authored);

        // Act
        var count = await Record.ExceptionAsync(() => store.ReadSelectionAsync<AuthoritativeReadCut>(Realm,
            manifest with { Count = 2 }));
        var digest = await Record.ExceptionAsync(() => store.ReadSelectionAsync<AuthoritativeReadCut>(Realm,
            manifest with { ItemsSha256 = new string('0', 64) }));
        var schema = await Record.ExceptionAsync(() => store.ReadSelectionAsync<AuthoritativeReadCut>(Realm,
            manifest with { ItemSchema = "other.v1" }));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(count);
        Assert.IsType<ImmutableContentIntegrityException>(digest);
        Assert.IsType<ImmutableContentIntegrityException>(schema);
    }

    static FitzKvImmutableContentStore Create() => new(new PersistenceKvClient(new InMemoryKvClient()));

    [Fact]
    public async Task ShouldReadEveryItemInOrderGivenManifestOfMoreThanAThousandItems()
    {
        // Arrange
        var store = Store();
        var items = Enumerable.Range(0, 1500).Select(index => new AdmittedChild(Uuid.CreateVersion4(),
            new NativeFactReference(new EventStreamAddress(Realm, "findings", "f"), Uuid.CreateVersion4(),
                (ulong)index + 1))).ToArray();

        // Act
        var manifest = await ImmutableSelectionManifests.WriteAsync<AdmittedChild>(store, Realm, Uuid.CreateVersion4(),
            "admitted-child.v1", items, SelectionOrder.Authored);
        var read = await ImmutableSelectionManifests.ReadAsync<AdmittedChild>(store, Realm, manifest);

        // Assert
        Assert.Equal(1500, manifest.Count);
        Assert.Equal(items.Select(item => item.ChildId), read.Select(item => item.ChildId));
        Assert.Equal(items.Select(item => item.Admission.AggregateVersion), read.Select(item => item.Admission.AggregateVersion));
    }

    [Fact]
    public async Task ShouldRejectManifestGivenDigestOrCountThatDoesNotCoverTheStoredItems()
    {
        // Arrange
        var store = Store();
        var manifest = await ImmutableSelectionManifests.WriteAsync<AdmittedChild>(store, Realm, Uuid.CreateVersion4(),
            "admitted-child.v1", [new AdmittedChild(Uuid.CreateVersion4(), new NativeFactReference(
                new EventStreamAddress(Realm, "findings", "f"), Uuid.CreateVersion4(), 1))], SelectionOrder.CanonicalKey);

        // Act
        var wrongDigest = await Record.ExceptionAsync(() => ImmutableSelectionManifests.ReadAsync<AdmittedChild>(store, Realm,
            manifest with { ItemsSha256 = new string("0"[0], 64) }));
        var wrongCount = await Record.ExceptionAsync(() => ImmutableSelectionManifests.ReadAsync<AdmittedChild>(store, Realm,
            manifest with { Count = 2 }));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(wrongDigest);
        Assert.IsType<ImmutableContentIntegrityException>(wrongCount);
    }

    static FitzKvImmutableContentStore Store() => new(new PersistenceKvClient(new InMemoryKvClient()));
}
