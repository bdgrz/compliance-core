using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AuditorRequests;

public sealed class AuditorResponsePreparationRaceTests
{
    [Fact]
    public async Task ShouldReplayEqualSubmittedWinnerGivenLosingUnitFreezeFailsAfterConcurrentAdmission()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var responseId = Uuid.CreateVersion4();
        Assert.True((await fixture.DraftAsync(requestId, responseId)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await fixture.MaterializeAsync(responseId)).State);
        var request = new SubmitAuditResponse(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, requestId,
            responseId, 1, "Submitted exact response.", 1);
        var context = fixture.Context(request);
        var other = fixture.Handler<SubmitAuditResponseHandler>();
        var store = new LosingUnitStore(fixture.Content, async () =>
        {
            var winner = await other.HandleAsync(context, CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
        });
        var operations = new NativeOperationExecutor(fixture.Reader,
            fixture.Services.GetRequiredService<IAggregateExecutor>(), store);
        var handler = ActivatorUtilities.CreateInstance<SubmitAuditResponseHandler>(fixture.Services, operations, store);
        Result replay = default;

        // Act
        var error = await Record.ExceptionAsync(async () => replay = await handler.HandleAsync(context, CancellationToken.None));
        var saved = await fixture.ResponseAsync(requestId, responseId);

        // Assert
        Assert.Null(error);
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal("submitted", saved.Status);
        Assert.Equal(2, saved.Revision);
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
