using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.SourceFacts;

public sealed class SourceFactProofsTests
{
    [Theory]
    [InlineData("stream")]
    [InlineData("physical_position")]
    [InlineData("native_version")]
    [InlineData("payload_digest")]
    [InlineData("metadata_event")]
    [InlineData("metadata_version")]
    public async Task ShouldWriteNoProofGivenResolverReturnsAnotherRequestedNativeTuple(string mismatch)
    {
        // Arrange
        var source = Source(mismatch);
        var content = new RecordingContent();
        SourceFactProof<TenantRegistered>? accepted = null;

        // Act
        var failure = await Record.ExceptionAsync(async () =>
        {
            var built = await SourceFactProofs.BuildAsync<TenantRegistered>(new RedirectingResolver(source.Resolved), content,
                source.Reference.Stream.Realm, source.Reference);
            accepted = built.Proof;
        });

        // Assert
        Assert.Equal(0, content.Writes);
        Assert.Null(accepted);
        Assert.IsType<SourceFactVerificationException>(failure);
    }

    [Fact]
    public async Task ShouldStoreVerifiedLocatorGivenExactRequestedNativeTuple()
    {
        // Arrange
        var source = Source("none");
        var content = new RecordingContent();

        // Act
        var built = await SourceFactProofs.BuildAsync<TenantRegistered>(new RedirectingResolver(source.Resolved), content,
            source.Reference.Stream.Realm, source.Reference);
        var stored = await content.ReadJsonAsync<SourceFactLocator>(source.Reference.Stream.Realm, built.Proof.RequiredFacts);

        // Assert
        Assert.Equal(1, content.Writes);
        Assert.Equal(source.Resolved.Locator, built.Locator);
        Assert.Equal(source.Resolved.Locator, stored);
        Assert.Same(source.Resolved.Event, built.Fact);
    }

    static (NativeFactReference Reference, ResolvedSourceFact Resolved) Source(string mismatch)
    {
        var tenant = Uuid.CreateVersion4();
        var stream = new EventStreamAddress("bdgrz", "tenants", tenant.ToString());
        var id = Uuid.CreateVersion4();
        var fact = new TenantRegistered(tenant, Uuid.CreateVersion4(), "Source", "source");
        fact.AttachMetadata(new(mismatch == "metadata_event" ? Uuid.CreateVersion4() : id, tenant,
            mismatch == "metadata_version" ? 9UL : 1UL, DateTimeOffset.UnixEpoch.AddDays(1)));
        var locator = new SourceFactLocator(mismatch == "stream" ? new("bdgrz", "tenants", Uuid.CreateVersion4().ToString()) : stream,
            id, mismatch == "physical_position" ? 7UL : 0UL, 1001, 10001,
            mismatch == "native_version" ? 9UL : 1UL,
            mismatch == "payload_digest" ? new string('f', 64) : ContentDigest.Sha256(fact, typeof(TenantRegistered)));
        return (new NativeFactReference(stream, id, 1), new ResolvedSourceFact(fact, locator));
    }

    sealed class RedirectingResolver(ResolvedSourceFact value) : ISourceFactResolver
    {
        public Task<ResolvedSourceFact> ResolveAsync(EventStreamAddress stream, ulong resourcePosition, CancellationToken ct = default) =>
            Task.FromResult(value);
        public Task<VerifiedSourceProof<T>> VerifyAsync<T>(SourceFactProof<T> proof, CancellationToken ct = default) where T : DomainEvent =>
            throw new InvalidOperationException("This test exercises source proof creation.");
    }

    sealed class RecordingContent : IImmutableContentStore
    {
        readonly FitzKvImmutableContentStore _inner = new(new InMemoryKvClient());
        public int Writes { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            Writes++;
            return _inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            _inner.ReadVerifiedAsync(realm, reference, ct);
    }
}
