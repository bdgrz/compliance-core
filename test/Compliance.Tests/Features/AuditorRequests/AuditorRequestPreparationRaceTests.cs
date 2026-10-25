using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AuditorRequests;

public sealed class AuditorRequestPreparationRaceTests
{
    [Fact]
    public async Task ShouldReplayEqualRecordedWinnerGivenLosingUnitFreezeFailsAfterConcurrentAdmission()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        await fixture.SeedMemberAsync(fixture.UserId);
        var request = fixture.NewRecordCommand();
        var context = fixture.Context(request);
        var other = fixture.Handler<RecordAuditorRequestHandler>();
        var store = new LosingUnitStore(fixture.Content, async () =>
        {
            var winner = await other.HandleAsync(context, CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
        });
        var operations = new NativeOperationExecutor(fixture.Reader,
            fixture.Services.GetRequiredService<IAggregateExecutor>(), store);
        var handler = ActivatorUtilities.CreateInstance<RecordAuditorRequestHandler>(fixture.Services, operations, store);
        Result replay = default;

        // Act
        var error = await Record.ExceptionAsync(async () => replay = await handler.HandleAsync(context, CancellationToken.None));
        var saved = await fixture.RequestAsync(request.RequestId);

        // Assert
        Assert.Null(error);
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.True(saved.IsOpen);
        Assert.Equal(1, saved.Revision);
        Assert.Equal(1, store.CompetingAdmissions);
    }

    sealed class LosingUnitStore(IImmutableContentStore inner, Func<Task> admit) : IImmutableContentStore
    {
        bool _armed = true;
        public int CompetingAdmissions { get; private set; }
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (_armed && schema == UnitResult.Schema)
            {
                _armed = false;
                await admit();
                CompetingAdmissions++;
                throw new ImmutableContentUnavailableException("The losing unit-response write was temporarily unavailable.");
            }
            return await inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => inner.ReadVerifiedAsync(realm, reference, ct);
    }
}
