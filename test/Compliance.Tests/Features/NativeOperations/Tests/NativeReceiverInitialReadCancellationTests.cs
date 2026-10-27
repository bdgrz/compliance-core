using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Processes;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;

public sealed class NativeReceiverInitialReadCancellationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPreserveRequestedCancellationGivenInitialReceiptHydrationThrowsExpectedIntegrity(bool unavailable)
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var reader = new CancelingReader(cancellation, unavailable);
        var prepared = false;

        // Act
        var error = await Record.ExceptionAsync(async () => await NativeReceiverPreparation.PrepareAsync<Owner, object>(reader,
            static () => new Owner(), static _ => null, (_, _) =>
            {
                prepared = true;
                return ValueTask.FromResult(Result<object>.Success(new object()));
            }, cancellation.Token));

        // Assert
        Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(error).CancellationToken);
        Assert.Equal(1, reader.Reads);
        Assert.False(prepared);
    }

    sealed class CancelingReader(CancellationTokenSource cancellation, bool unavailable) : IAggregateReader
    {
        public int Reads { get; private set; }
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate
        {
            Reads++;
            cancellation.Cancel();
            throw unavailable ? new ImmutableContentUnavailableException("Initial actual receipt bytes unavailable.")
                : new ImmutableContentIntegrityException("Initial actual receipt bytes corrupt.");
        }
    }

    sealed class Owner : Aggregate
    {
        public Owner() : this(Uuid.CreateVersion4())
        {
        }

        Owner(Uuid id) : base(id, new EventStreamAddress("00000000-0000-4000-8000-000000000001", "native-receiver-test", id.ToString()))
        {
        }
    }
}
