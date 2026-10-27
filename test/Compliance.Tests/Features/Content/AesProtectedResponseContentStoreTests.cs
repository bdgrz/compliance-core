using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Content;

public sealed class AesProtectedResponseContentStoreTests
{
    const string Schema = "evidence-artifact-download-link.v1";
    static readonly byte[] Plaintext = "{\"location\":\"https://content.example.test/secret-ticket\",\"expires_at\":\"2026-10-24T18:00:00Z\"}"u8.ToArray();
    static ProtectedResponseScope Scope() => new("tenant-a", new EventStreamAddress("tenant-a", "artifacts", "one"),
        Uuid.CreateVersion4(), ActorReference.ForMember(Uuid.CreateVersion4(), "Member"));
    static ProtectedResponseKeyRing Keys(string active = "v1", bool old = true) => new(active,
        old ? new Dictionary<string, byte[]> { ["v1"] = Key(1), ["v2"] = Key(2) }
            : new Dictionary<string, byte[]> { ["v2"] = Key(2) });
    static byte[] Key(byte value) => Enumerable.Repeat(value, 32).ToArray();

    [Fact]
    public async Task ShouldRecoverOriginalPlaintextGivenNewStoreInstanceAndRetainedRotatedKey()
    {
        // Arrange
        var kv = new InMemoryKvClient();
        var scope = Scope();
        var first = new AesProtectedResponseContentStore(new FitzKvImmutableContentStore(new PersistenceKvClient(kv)), Keys());
        var reference = await first.WriteVerifiedAsync(scope, Schema, Plaintext);

        // Act
        var restarted = new AesProtectedResponseContentStore(new FitzKvImmutableContentStore(new PersistenceKvClient(kv)), Keys("v2"));
        var original = await restarted.ReadVerifiedAsync(scope, reference);
        var next = await restarted.WriteVerifiedAsync(scope, Schema, Plaintext);

        // Assert
        Assert.Equal(Plaintext, original.ToArray());
        Assert.Equal("v1", reference.KeyVersionId);
        Assert.Equal("v2", next.KeyVersionId);
        Assert.NotEqual(reference.ContentId, next.ContentId);
        Assert.NotEqual(reference.CiphertextSha256, next.CiphertextSha256);
    }

    [Theory]
    [InlineData("realm")]
    [InlineData("owner")]
    [InlineData("operation")]
    [InlineData("actor")]
    [InlineData("schema")]
    [InlineData("key_version")]
    public async Task ShouldFailClosedGivenSwappedAuthorityScopeOrReference(string variant)
    {
        // Arrange
        var scope = Scope();
        var memory = new MemoryContentStore();
        var store = new AesProtectedResponseContentStore(memory, Keys());
        var reference = await store.WriteVerifiedAsync(scope, Schema, Plaintext);
        var changedScope = variant switch
        {
            "realm" => scope with { Realm = "tenant-b", AffectedOwner = new EventStreamAddress("tenant-b", "artifacts", "one") },
            "owner" => scope with { AffectedOwner = new EventStreamAddress(scope.Realm, "artifacts", "two") },
            "operation" => scope with { RootOperationId = Uuid.CreateVersion4() },
            "actor" => scope with { OriginalActor = ActorReference.ForMember(Uuid.CreateVersion4(), "Other") },
            _ => scope,
        };
        var changedReference = variant switch
        {
            "schema" => reference with { Schema = "other-result.v1" },
            "key_version" => reference with { KeyVersionId = "v2" },
            _ => reference,
        };

        // Act
        var failure = await Record.ExceptionAsync(() => store.ReadVerifiedAsync(changedScope, changedReference));

        // Assert
        var error = Assert.IsAssignableFrom<ImmutableContentIntegrityException>(failure);
        Assert.DoesNotContain("secret-ticket", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldUseFreshNonceAndKeepTicketOutOfStoredEnvelopeGivenEqualCandidates()
    {
        // Arrange
        var memory = new MemoryContentStore();
        var store = new AesProtectedResponseContentStore(memory, Keys());
        var scope = Scope();

        // Act
        var one = await store.WriteVerifiedAsync(scope, Schema, Plaintext);
        var two = await store.WriteVerifiedAsync(scope, Schema, Plaintext);

        // Assert
        Assert.NotEqual(one.ContentId, two.ContentId);
        Assert.NotEqual(one.CiphertextSha256, two.CiphertextSha256);
        Assert.All(memory.Values.Values, bytes => Assert.DoesNotContain("secret-ticket", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal));
        Assert.Equal(Plaintext, (await store.ReadVerifiedAsync(scope, one)).ToArray());
        Assert.Equal(Plaintext, (await store.ReadVerifiedAsync(scope, two)).ToArray());
    }

    [Fact]
    public async Task ShouldFailAuthenticationGivenTamperedCiphertextAndRecomputedDigest()
    {
        // Arrange
        var memory = new MemoryContentStore();
        var store = new AesProtectedResponseContentStore(memory, Keys());
        var scope = Scope();
        var reference = await store.WriteVerifiedAsync(scope, Schema, Plaintext);
        var tampered = memory.Values[reference.ContentId];
        tampered[^17] ^= 1;
        var forged = reference with { CiphertextSha256 = Convert.ToHexStringLower(SHA256.HashData(tampered)) };

        // Act
        var failure = await Record.ExceptionAsync(() => store.ReadVerifiedAsync(scope, forged));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
    }

    [Fact]
    public async Task ShouldFailClosedGivenKeyUnavailableAfterRotation()
    {
        // Arrange
        var memory = new MemoryContentStore();
        var scope = Scope();
        var reference = await new AesProtectedResponseContentStore(memory, Keys()).WriteVerifiedAsync(scope, Schema, Plaintext);
        var rotated = new AesProtectedResponseContentStore(memory, Keys("v2", old: false));

        // Act
        var failure = await Record.ExceptionAsync(() => rotated.ReadVerifiedAsync(scope, reference));

        // Assert
        Assert.IsType<ImmutableContentUnavailableException>(failure);
    }

    [Fact]
    public async Task ShouldPublishNothingGivenMissingConfiguredActiveKey()
    {
        // Arrange
        var memory = new MemoryContentStore();
        var store = new AesProtectedResponseContentStore(memory, new ProtectedResponseKeyRing(null,
            new Dictionary<string, byte[]>()));

        // Act
        var failure = await Record.ExceptionAsync(() => store.WriteVerifiedAsync(Scope(), Schema, Plaintext));

        // Assert
        Assert.IsType<ImmutableContentUnavailableException>(failure);
        Assert.Empty(memory.Values);
    }

    [Fact]
    public async Task ShouldRefusePublicationGivenCiphertextNotReadableAfterWrite()
    {
        // Arrange
        var memory = new MemoryContentStore { ReadUnavailable = true };
        var store = new AesProtectedResponseContentStore(memory, Keys());

        // Act
        var failure = await Record.ExceptionAsync(() => store.WriteVerifiedAsync(Scope(), Schema, Plaintext));

        // Assert
        Assert.IsType<ImmutableContentUnavailableException>(failure);
    }

    sealed class MemoryContentStore : IImmutableContentStore
    {
        public Dictionary<Uuid, byte[]> Values { get; } = [];
        public bool ReadUnavailable { get; init; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            Values.Add(contentId, bytes.ToArray());
            return Task.FromResult(new ImmutableContentRef(contentId, Convert.ToHexStringLower(SHA256.HashData(bytes.Span)),
                bytes.Length, mediaType, schema));
        }
        // Deliberately permissive adapter: the encryption boundary must verify ciphertext and scope itself.
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => ReadUnavailable
            ? throw new ImmutableContentUnavailableException("The encrypted candidate is not visible yet.")
            : Task.FromResult<ReadOnlyMemory<byte>>(Values[reference.ContentId]);
    }
}
