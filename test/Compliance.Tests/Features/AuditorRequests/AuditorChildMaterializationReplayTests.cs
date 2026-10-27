using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.AuditorRequests.Handlers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AuditorRequests;

public sealed class AuditorChildMaterializationReplayTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayOriginalMaterializationGivenFrozenAdmissionContentUnavailable(bool corrupt)
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var responseId = Uuid.CreateVersion4();
        Assert.True((await fixture.DraftAsync(requestId, responseId)).IsSuccess);
        var parent = await fixture.RequestAsync(requestId);
        AuditorResponseAdmitted? admitted = null;
        await foreach (var record in fixture.Events.ReadAsync(parent.Stream, 0, CancellationToken.None))
            if (record.Event is AuditorResponseAdmitted fact)
                admitted = fact;
        Assert.NotNull(admitted);
        var target = new AuditorResponse(fixture.TenantId, fixture.ProgramId, requestId, responseId).Stream;
        var command = new MaterializeAuditorResponse(Uuid.CreateVersion4(), NativeFactReference.Of(parent.Stream, admitted),
            target, responseId, admitted.Generation, null, admitted.Intent, requestId, responseId);
        var context = new Cntryl.Portia.RequestContext<MaterializeAuditorResponse>(command,
            new RequestDispatchContext(RequestActor.System));
        var first = await fixture.Handler<MaterializeAuditorResponseHandler>().HandleAsync(context, CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var before = await fixture.ResponseAsync(requestId, responseId);
        var store = new UnreadableStore(fixture.Content, corrupt);
        var handler = ActivatorUtilities.CreateInstance<MaterializeAuditorResponseHandler>(fixture.Services, store);

        // Act
        var replay = await handler.HandleAsync(context, CancellationToken.None);
        var changed = await handler.HandleAsync(new Cntryl.Portia.RequestContext<MaterializeAuditorResponse>(command with
        { Generation = command.Generation + 1 }, new RequestDispatchContext(RequestActor.System)), CancellationToken.None);
        var after = await fixture.ResponseAsync(requestId, responseId);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(((ReceiverOutcome.Applied)first.Value!).Fact, ((ReceiverOutcome.Replayed)replay.Value!).Fact);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.False(changed.Error!.IsTransient);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Equal(0, store.Reads);
    }

    sealed class UnreadableStore(IImmutableContentStore inner, bool corrupt) : IImmutableContentStore
    {
        public int Reads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            Reads++;
            throw corrupt ? new ImmutableContentIntegrityException("The frozen admission was corrupted.")
                : new ImmutableContentUnavailableException("The frozen admission is temporarily missing.");
        }
    }
}
