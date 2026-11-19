using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaPublicationAuthorityAvailabilityTests
{
    [Fact]
    public async Task ShouldReturnRetryablePendingGivenPublicationSourceReadFailure()
    {
        // Arrange
        var authority = new SourceCriteriaPublicationAuthority(new UnavailableAggregateReader(), new UnusedSource());

        // Act
        var result = await authority.ReadPublishedAsync(Uuid.CreateVersion4());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.True(result.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldPreserveCancellationGivenPublicationSourceReadFailure()
    {
        // Arrange
        var authority = new SourceCriteriaPublicationAuthority(new UnavailableAggregateReader(), new UnusedSource());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await authority.ReadPublishedAsync(Uuid.CreateVersion4(), cancellation.Token));
    }

    sealed class UnavailableAggregateReader : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate => throw new IOException("The event source is unavailable.");
    }

    sealed class UnusedSource : ICriteriaExactSourceReader
    {
        public ValueTask<CriteriaOrderedSourceEvent?> FindAsync(EventStreamAddress stream, Uuid aggregateId,
            Uuid eventId, CancellationToken ct = default) => throw new InvalidOperationException();

        public IAsyncEnumerable<CriteriaOrderedSourceEvent> ReadAsync(EventStreamAddress stream, Uuid aggregateId,
            CancellationToken ct = default) => throw new InvalidOperationException();
    }
}
