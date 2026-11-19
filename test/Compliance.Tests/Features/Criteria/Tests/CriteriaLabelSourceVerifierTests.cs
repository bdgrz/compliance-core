using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaLabelSourceVerifierTests
{
    static readonly EventStreamAddress Stream = new("bdgrz", "criteria-editions", Uuid.CreateVersion4().ToString());
    static readonly Uuid AggregateId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldReturnTheStoredFactGivenExactStreamPositionAndIdentity()
    {
        // Arrange
        var store = new InMemoryEventStore();
        var fact = Fact("label");
        await store.AppendAsync(Stream, 0, [fact]);
        var verifier = new CriteriaLabelSourceVerifier(store);

        // Act
        var actual = await verifier.ReadAsync<ProbeFact>(Stream, 0, fact.Metadata.EventId, CancellationToken.None);

        // Assert
        Assert.Equal(fact, actual);
    }

    [Fact]
    public async Task ShouldReturnUnobservedGivenMissingSourcePosition()
    {
        // Arrange
        var verifier = new CriteriaLabelSourceVerifier(new InMemoryEventStore());

        // Act
        var actual = await verifier.ReadAsync<ProbeFact>(Stream, 0, Uuid.CreateVersion4(), CancellationToken.None);

        // Assert
        Assert.Null(actual);
    }

    [Fact]
    public async Task ShouldRejectGivenSourceEventIdentityDiffers()
    {
        // Arrange
        var store = new InMemoryEventStore();
        await store.AppendAsync(Stream, 0, [Fact("label")]);
        var verifier = new CriteriaLabelSourceVerifier(store);

        // Act
        var failure = await Record.ExceptionAsync(async () =>
            await verifier.ReadAsync<ProbeFact>(Stream, 0, Uuid.CreateVersion4(), CancellationToken.None));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public async Task ShouldRejectGivenTheSourceFactHasAnotherType()
    {
        // Arrange
        var store = new InMemoryEventStore();
        var fact = Fact("label");
        await store.AppendAsync(Stream, 0, [fact]);
        var verifier = new CriteriaLabelSourceVerifier(store);

        // Act
        var failure = await Record.ExceptionAsync(async () =>
            await verifier.ReadAsync<OtherFact>(Stream, 0, fact.Metadata.EventId, CancellationToken.None));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public async Task ShouldFindAnExactLaterFactGivenTheOwnersOwnHistory()
    {
        // Arrange
        var store = new InMemoryEventStore();
        var first = Fact("first");
        var later = Fact("later", version: 2);
        await store.AppendAsync(Stream, 0, [first, later]);
        var verifier = new CriteriaLabelSourceVerifier(store);

        // Act
        var actual = await verifier.FindAsync<ProbeFact>(Stream, later.Metadata.EventId, CancellationToken.None);

        // Assert
        Assert.Equal(later, actual);
    }

    [Fact]
    public async Task ShouldReturnUnobservedGivenAnUnknownEventIdentity()
    {
        // Arrange
        var store = new InMemoryEventStore();
        await store.AppendAsync(Stream, 0, [Fact("label")]);
        var verifier = new CriteriaLabelSourceVerifier(store);

        // Act
        var actual = await verifier.FindAsync<ProbeFact>(Stream, Uuid.CreateVersion4(), CancellationToken.None);

        // Assert
        Assert.Null(actual);
    }

    static ProbeFact Fact(string value, ulong version = 1)
    {
        var fact = new ProbeFact(value);
        fact.AttachMetadata(new(Uuid.CreateVersion4(), AggregateId, version, DateTimeOffset.UtcNow));
        return fact;
    }

    sealed record ProbeFact(string Value) : DomainEvent;
    sealed record OtherFact(string Value) : DomainEvent;
}
