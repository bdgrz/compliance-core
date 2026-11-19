using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class PlatformOperatorExactSourceVerifierTests
{
    [Fact]
    public async Task ShouldReturnPendingGivenNoQualifiedProvider()
    {
        // Arrange
        var verifier = new PlatformOperatorExactSourceVerifier();

        // Act
        var result = await verifier.FindAsync<PrimaryPlatformOperatorSeatReserved>(PrimaryPlatformOperators.Address,
            PrimaryPlatformOperators.OwnerId, Uuid.CreateVersion4());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ShouldReturnExactFactGivenACompleteCurrentBoundSession()
    {
        // Arrange
        var fact = Reservation();
        var session = new Session([Record(fact, 0)], 1, true);
        var verifier = new PlatformOperatorExactSourceVerifier(new Source(session));

        // Act
        var result = await verifier.FindAsync<PrimaryPlatformOperatorSeatReserved>(PrimaryPlatformOperators.Address,
            PrimaryPlatformOperators.OwnerId, fact.Metadata.EventId);

        // Assert
        Assert.Same(fact, result);
        Assert.True(session.Disposed);
    }

    [Fact]
    public async Task ShouldReturnPendingGivenGapOrUnqualifiedCurrentHead()
    {
        // Arrange
        var fact = Reservation();
        var gap = new PlatformOperatorExactSourceVerifier(new Source(new Session([Record(fact, 1)], 2, true)));
        var stale = new PlatformOperatorExactSourceVerifier(new Source(new Session([Record(fact, 0)], 1, false)));
        var truncated = new PlatformOperatorExactSourceVerifier(new Source(new Session([Record(fact, 0)], 2, true)));

        // Act
        var gapResult = await gap.FindAsync<PrimaryPlatformOperatorSeatReserved>(PrimaryPlatformOperators.Address, PrimaryPlatformOperators.OwnerId, fact.Metadata.EventId);
        var staleResult = await stale.FindAsync<PrimaryPlatformOperatorSeatReserved>(PrimaryPlatformOperators.Address, PrimaryPlatformOperators.OwnerId, fact.Metadata.EventId);
        var truncatedResult = await truncated.FindAsync<PrimaryPlatformOperatorSeatReserved>(PrimaryPlatformOperators.Address, PrimaryPlatformOperators.OwnerId, fact.Metadata.EventId);

        // Assert
        Assert.Null(gapResult);
        Assert.Null(staleResult);
        Assert.Null(truncatedResult);
    }

    [Fact]
    public async Task ShouldReturnPendingGivenWrongSourceOwnerOrDuplicateEventIdentity()
    {
        // Arrange
        var wrongOwner = Reservation();
        var wrong = new PlatformOperatorExactSourceVerifier(new Source(new Session([Record(wrongOwner, 0)], 1, true)));
        var fact = Reservation();
        var duplicate = new PlatformOperatorExactSourceVerifier(new Source(new Session([Record(fact, 0), Record(fact, 1)], 2, true)));

        // Act
        var ownerResult = await wrong.FindAsync<PrimaryPlatformOperatorSeatReserved>(PrimaryPlatformOperators.Address,
            Uuid.CreateVersion4(), wrongOwner.Metadata.EventId);
        var duplicateResult = await duplicate.FindAsync<PrimaryPlatformOperatorSeatReserved>(PrimaryPlatformOperators.Address,
            PrimaryPlatformOperators.OwnerId, fact.Metadata.EventId);

        // Assert
        Assert.Null(ownerResult);
        Assert.Null(duplicateResult);
    }

    [Fact]
    public async Task ShouldPreserveCancellationGivenAnInterruptedSourceRead()
    {
        // Arrange
        var fact = Reservation();
        var verifier = new PlatformOperatorExactSourceVerifier(new Source(new Session([Record(fact, 0)], 1, true)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act and assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await verifier.FindAsync<PrimaryPlatformOperatorSeatReserved>(
            PrimaryPlatformOperators.Address, PrimaryPlatformOperators.OwnerId, fact.Metadata.EventId, cancellation.Token));
    }

    static PrimaryPlatformOperatorSeatReserved Reservation()
    {
        var fact = new PrimaryPlatformOperatorSeatReserved(Uuid.CreateVersion4(), 0, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, true, Uuid.CreateVersion4(), Uuid.Empty,
            new ActorAttribution("portia:system", "Portia"));
        fact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), PrimaryPlatformOperators.OwnerId, 1,
            new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero)));
        return fact;
    }

    static DomainEventRecord Record(DomainEvent fact, ulong offset) =>
        new(PrimaryPlatformOperators.Address, fact, offset, EventCursor.Start);

    sealed class Source(Session session) : IPlatformOperatorQualifiedSource
    {
        public ValueTask<IPlatformOperatorQualifiedSourceSession?> OpenAsync(EventStreamAddress stream, Uuid aggregateId,
            CancellationToken ct = default) => ValueTask.FromResult<IPlatformOperatorQualifiedSourceSession?>(session);
    }

    sealed class Session(DomainEventRecord[] records, ulong head, bool current) : IPlatformOperatorQualifiedSourceSession
    {
        public ulong ExclusiveHead => head;
        public bool Disposed { get; private set; }
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync([EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            foreach (var record in records)
            {
                ct.ThrowIfCancellationRequested();
                yield return record;
            }
        }
        public ValueTask<bool> IsCurrentAsync(CancellationToken ct = default) => ValueTask.FromResult(current);
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}
