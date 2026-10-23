using System.Text;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Content;

public sealed class FitzKvImmutableContentStoreTests
{
    const string Realm = "tenant-a";
    const string Schema = "test.schema.v1";
    const string Media = "application/octet-stream";
    static readonly Uuid Id = Uuid.Parse("ab3c0000-0000-4000-8000-000000000001", System.Globalization.CultureInfo.InvariantCulture);
    const int Chunk = 48 * 1024;
    const string RoutePrefix = "kv://bdgrz/immutable-content/tenant-a-ab";

    [Fact]
    public async Task ShouldRetryFromBeginGivenIsolationConflictOnSharedShard()
    {
        // Arrange
        var (store, kv) = Create();
        kv.FailNext(KvTestOperation.Begin, nameof(FitzErrorCodes.KvIsolationConflict), "lost route lock", null);
        kv.FailNext(KvTestOperation.Begin, nameof(FitzErrorCodes.KvIsolationConflict), "lost route lock", null);

        // Act
        var reference = await store.WriteVerifiedAsync(Realm, Id, Schema, Media, Bytes(100));

        // Assert
        Assert.Equal(Bytes(100), (await store.ReadVerifiedAsync(Realm, reference)).ToArray());
    }

    [Fact]
    public async Task ShouldPropagateIsolationConflictGivenEveryAttemptLosesTheRouteLock()
    {
        // Arrange
        var (store, kv) = Create();
        for (var i = 0; i < FitzKvImmutableContentStore.WriteAttempts; i++)
            kv.FailNext(KvTestOperation.Begin, nameof(FitzErrorCodes.KvIsolationConflict), "lost route lock", null);

        // Act
        var failure = await Record.ExceptionAsync(() => store.WriteVerifiedAsync(Realm, Id, Schema, Media, Bytes(100)));

        // Assert
        var kvFailure = Assert.IsType<KvException>(failure);
        Assert.True(FitzKvImmutableContentStore.IsIsolationConflict(kvFailure));
        Assert.Empty(kv.Snapshot(RoutePrefix));
    }

    [Fact]
    public async Task ShouldRejectDifferentBytesGivenExistingContentId()
    {
        // Arrange
        var (store, _) = Create();
        await store.WriteVerifiedAsync(Realm, Id, Schema, Media, Bytes(10));

        // Act
        var failure = await Record.ExceptionAsync(() => store.WriteVerifiedAsync(Realm, Id, Schema, Media, Bytes(11)));

        // Assert
        Assert.IsType<ImmutableContentConflictException>(failure);
    }

    [Fact]
    public async Task ShouldReturnSameReferenceGivenIdenticalRewrite()
    {
        // Arrange
        var (store, kv) = Create();
        var first = await store.WriteVerifiedAsync(Realm, Id, Schema, Media, Bytes(100));
        var before = kv.Snapshot(RoutePrefix).Count;

        // Act
        var second = await store.WriteVerifiedAsync(Realm, Id, Schema, Media, Bytes(100));

        // Assert
        Assert.Equal(first, second);
        Assert.Equal(before, kv.Snapshot(RoutePrefix).Count);
    }

    [Fact]
    public async Task ShouldRejectGivenSameBytesWithDifferentSchema()
    {
        // Arrange
        var (store, _) = Create();
        await store.WriteVerifiedAsync(Realm, Id, Schema, Media, Bytes(10));

        // Act
        var failure = await Record.ExceptionAsync(() => store.WriteVerifiedAsync(Realm, Id, "other", Media, Bytes(10)));

        // Assert
        Assert.IsType<ImmutableContentConflictException>(failure);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(Chunk - 1, 1)]
    [InlineData(Chunk, 1)]
    [InlineData(Chunk + 1, 2)]
    [InlineData((Chunk * 3) + 7, 4)]
    public async Task ShouldRoundTripAndChunkGivenPayloadSize(int length, int chunks)
    {
        // Arrange
        var (store, kv) = Create();
        var bytes = Bytes(length);

        // Act
        var reference = await store.WriteVerifiedAsync(Realm, Id, Schema, Media, bytes);
        var read = await store.ReadVerifiedAsync(Realm, reference);

        // Assert
        Assert.Equal(length, reference.Bytes);
        Assert.Equal(bytes, read.ToArray());
        Assert.Equal(chunks, kv.Snapshot(RoutePrefix).Count(pair => Encoding.UTF8.GetString(pair.Key.Span).StartsWith("c/", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ShouldFailClosedGivenCorruptChunk()
    {
        // Arrange
        var (store, kv) = Create();
        var reference = await store.WriteVerifiedAsync(Realm, Id, Schema, Media, Bytes(1000));
        var chunk = kv.Snapshot(RoutePrefix).Single(pair => Encoding.UTF8.GetString(pair.Key.Span).StartsWith("c/", StringComparison.Ordinal));
        var tampered = chunk.Value.ToArray();
        tampered[0] ^= 0xFF;
        kv.Seed(RoutePrefix, chunk.Key, tampered);

        // Act
        var failure = await Record.ExceptionAsync(() => store.ReadVerifiedAsync(Realm, reference));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
    }

    [Fact]
    public async Task ShouldFailClosedGivenMissingChunk()
    {
        // Arrange
        var (store, kv) = Create();
        var reference = await store.WriteVerifiedAsync(Realm, Id, Schema, Media, Bytes(Chunk + 5));
        var chunk = kv.Snapshot(RoutePrefix).First(pair => Encoding.UTF8.GetString(pair.Key.Span).StartsWith("c/", StringComparison.Ordinal));
        await using (var tx = await kv.BeginAsync(RoutePrefix, KvMode.ReadWrite))
        {
            await tx.DeleteAsync(chunk.Key);
            await tx.CommitAsync(KvDurability.Sync);
        }

        // Act
        var failure = await Record.ExceptionAsync(() => store.ReadVerifiedAsync(Realm, reference));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
    }

    [Theory]
    [InlineData("length")]
    [InlineData("digest")]
    [InlineData("schema")]
    [InlineData("missing")]
    public async Task ShouldFailClosedGivenMismatchedReference(string mismatch)
    {
        // Arrange
        var (store, _) = Create();
        var reference = await store.WriteVerifiedAsync(Realm, Id, Schema, Media, Bytes(50));
        var wrong = mismatch switch
        {
            "length" => reference with { Bytes = 49 },
            "digest" => reference with { Sha256 = new string('0', 64) },
            "schema" => reference with { Schema = "other" },
            _ => reference with { ContentId = Uuid.Parse("ab3c0000-0000-4000-8000-000000000002", System.Globalization.CultureInfo.InvariantCulture) },
        };

        // Act
        var failure = await Record.ExceptionAsync(() => store.ReadVerifiedAsync(Realm, wrong));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
    }

    [Fact]
    public async Task ShouldNotReadGivenDifferentRealm()
    {
        // Arrange
        var (store, _) = Create();
        var reference = await store.WriteVerifiedAsync(Realm, Id, Schema, Media, Bytes(50));

        // Act
        var failure = await Record.ExceptionAsync(() => store.ReadVerifiedAsync("tenant-b", reference));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
    }

    [Fact]
    public async Task ShouldRoundTripJsonGivenTypedHelpers()
    {
        // Arrange
        var (store, _) = Create();

        // Act
        var reference = await store.WriteJsonAsync(Realm, Id, Schema, "hello");
        var read = await store.ReadJsonAsync<string>(Realm, reference);

        // Assert
        Assert.Equal("application/json", reference.MediaType);
        Assert.Equal("hello", read);
    }

    static (FitzKvImmutableContentStore Store, InMemoryKvClient Kv) Create()
    {
        var kv = new InMemoryKvClient();
        return (new FitzKvImmutableContentStore(new PersistenceKvClient(kv)), kv);
    }

    static byte[] Bytes(int length)
    {
        var bytes = new byte[length];
        for (var index = 0; index < length; index++)
            bytes[index] = (byte)((index * 31) + 7);
        return bytes;
    }
}
