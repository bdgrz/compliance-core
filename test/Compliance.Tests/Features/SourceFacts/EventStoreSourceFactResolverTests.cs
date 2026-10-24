using System.Globalization;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.SourceFacts;

public sealed class EventStoreSourceFactResolverTests
{
    static readonly EventStreamAddress Stream = new("tenant-a", "tenants", "acme");
    static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
    static readonly Uuid FirstId = Uuid.Parse("00000000-0000-4000-8000-0000000000a1", CultureInfo.InvariantCulture);
    static readonly Uuid SecondId = Uuid.Parse("00000000-0000-4000-8000-0000000000a2", CultureInfo.InvariantCulture);
    static readonly Uuid AuditId = Uuid.Parse("00000000-0000-4000-8000-0000000000a3", CultureInfo.InvariantCulture);

    [Fact]
    public void ShouldUseWireDiscriminatorGivenEventType()
    {
        // Arrange
        var discriminator = typeof(TenantRegistered).GetCustomAttributes(typeof(DiscriminatorAttribute), false)
            .Cast<DiscriminatorAttribute>().Single();

        // Act
        var kind = EventKind.Of(typeof(TenantRegistered));

        // Assert
        Assert.Equal($"{discriminator.Name}@{discriminator.Version}", kind);
        Assert.DoesNotContain(nameof(TenantRegistered), kind);
    }

    [Fact]
    public async Task ShouldRejectGivenWrongPayloadDigest()
    {
        // Arrange
        var (resolver, proof) = await SeedAsync();

        // Act
        var failure = await Record.ExceptionAsync(() =>
            resolver.VerifyAsync(proof with { PayloadSha256 = new string('0', 64) }));

        // Assert
        Assert.IsType<SourceFactVerificationException>(failure);
    }

    [Fact]
    public async Task ShouldResolveLocatorGivenKnownRecord()
    {
        // Arrange
        var (resolver, proof) = await SeedAsync();

        // Act
        var fact = await resolver.ResolveAsync(Stream, 1);

        // Assert
        Assert.Equal(SecondId, fact.Locator.EventId);
        Assert.Equal(1UL, fact.Locator.ResourcePosition);
        Assert.Equal(EventStoreSourceFactResolver.RealmBase + 1, fact.Locator.RealmPosition);
        Assert.Equal(EventStoreSourceFactResolver.GlobalBase + 1, fact.Locator.GlobalPosition);
        Assert.Equal(2UL, fact.Locator.AggregateVersion);
        Assert.Equal(ContentDigest.Sha256(fact.Event, typeof(TenantRegistered)), fact.Locator.PayloadSha256);
        Assert.Equal(proof.PayloadSha256, fact.Locator.PayloadSha256);
    }

    [Fact]
    public async Task ShouldReturnVerifiedProofGivenMatchingRecord()
    {
        // Arrange
        var (resolver, proof) = await SeedAsync();

        // Act
        var verified = await resolver.VerifyAsync(proof);

        // Assert
        Assert.Equal(SecondId, verified.EventId);
        Assert.Equal(proof.PhysicalPosition, verified.PhysicalPosition);
        Assert.Equal(proof.OwnerRevision, verified.OwnerRevision);
        Assert.Equal(proof.GlobalPosition, verified.GlobalPosition);
        Assert.Equal(proof.RealmPosition, verified.RealmPosition);
        Assert.Equal("Second", verified.Facts.Name);
    }

    [Theory]
    [InlineData("stream")]
    [InlineData("realm")]
    [InlineData("event")]
    [InlineData("kind")]
    [InlineData("realm_position")]
    [InlineData("global_position")]
    [InlineData("revision")]
    [InlineData("position")]
    public async Task ShouldRejectGivenMismatchedProof(string mismatch)
    {
        // Arrange
        var (resolver, proof) = await SeedAsync();
        var wrong = mismatch switch
        {
            "stream" => proof with { Source = new EventStreamAddress("tenant-a", "tenants", "other") },
            "realm" => proof with { Source = new EventStreamAddress("tenant-b", "tenants", "acme") },
            "event" => proof with { EventId = FirstId },
            "kind" => proof with { EventKind = nameof(TenantRegistered) },
            "realm_position" => proof with { RealmPosition = proof.PhysicalPosition },
            "global_position" => proof with { GlobalPosition = proof.PhysicalPosition },
            "revision" => proof with { OwnerRevision = 99 },
            _ => proof with { PhysicalPosition = 0 },
        };

        // Act
        var failure = await Record.ExceptionAsync(() => resolver.VerifyAsync(wrong));

        // Assert
        Assert.IsType<SourceFactVerificationException>(failure);
    }

    [Fact]
    public async Task ShouldRejectGivenAuditRecord()
    {
        // Arrange
        var (resolver, _) = await SeedAsync();

        // Act
        var failure = await Record.ExceptionAsync(() => resolver.ResolveAsync(Stream, 2));

        // Assert
        Assert.IsType<SourceFactVerificationException>(failure);
    }

    [Fact]
    public async Task ShouldRejectGivenMissingRecord()
    {
        // Arrange
        var (resolver, proof) = await SeedAsync();

        // Act
        var failure = await Record.ExceptionAsync(() => resolver.VerifyAsync(proof with { PhysicalPosition = 42 }));

        // Assert
        Assert.IsType<SourceFactVerificationException>(failure);
    }

    static async Task<(EventStoreSourceFactResolver Resolver, SourceFactProof<TenantRegistered> Proof)> SeedAsync()
    {
        var store = new InMemoryEventStore();
        var aggregate = Uuid.CreateVersion4();
        DomainEvent[] events = [Registered("First", FirstId, aggregate, 1, false),
            Registered("Second", SecondId, aggregate, 2, false)];
        await store.AppendAsync(Stream, 0, events);
        await store.AppendAsync(Stream, 2, [Registered("Audit", AuditId, aggregate, 2, true)]);
        var resolver = new EventStoreSourceFactResolver(store);
        var fact = await resolver.ResolveAsync(Stream, 1);
        var proof = new SourceFactProof<TenantRegistered>(Stream, SecondId, 1, 2, EventKind.Of(typeof(TenantRegistered)),
            fact.Locator.PayloadSha256,
            new ImmutableContentRef(Uuid.CreateVersion4(), new string('a', 64), 1, "application/json", "facts.v1"),
            fact.Locator.GlobalPosition, fact.Locator.RealmPosition);
        return (resolver, proof);
    }

    static TenantRegistered Registered(string name, Uuid eventId, Uuid aggregate, ulong version, bool audit)
    {
        var ev = new TenantRegistered(Uuid.CreateVersion4(), Uuid.CreateVersion4(), name, name.ToLowerInvariant());
        ev.AttachMetadata(new DomainEventMetadata(eventId, aggregate, version, Now, null, null, audit));
        return ev;
    }
}
