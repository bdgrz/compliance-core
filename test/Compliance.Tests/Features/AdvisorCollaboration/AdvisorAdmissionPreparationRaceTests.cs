using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Handlers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

public sealed class AdvisorAdmissionPreparationRaceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRecoverEqualAdmissionOrRequestedCancellationGivenWinnerBeforeAuthoredWriteFailure(bool cancel)
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, source) = await host.SeedReadyAsync();
        var feedbackId = Uuid.CreateVersion4();
        var command = host.Feedback(handoffId, feedbackId, source, 2);
        using var cancellation = new CancellationTokenSource();
        var transport = new Trace();
        var winner = Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Conflict, "No winner yet."));
        var fault = new FaultStore(host.Content, async () =>
        {
            winner = await host.RecordAsync(command);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            if (cancel)
                cancellation.Cancel();
        });
        var native = new NativeOperationExecutor(host.Reader, host.Services.GetRequiredService<IAggregateExecutor>(),
            fault, transport);
        var loser = new RecordAdvisorFeedbackHandler(native, host.Handoffs, fault, host.Clock);
        var result = Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Conflict, "No losing response yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () =>
            result = await loser.HandleAsync(host.Http(command), cancellation.Token));
        var handoff = await host.HandoffAsync(handoffId);
        var admitted = Assert.Single(handoff.Admitted);

        // Assert
        Assert.True(winner.IsSuccess);
        if (cancel)
        {
            var canceled = Assert.IsAssignableFrom<OperationCanceledException>(failure);
            Assert.Equal(cancellation.Token, canceled.CancellationToken);
            Assert.Null(transport.Observed);
        }
        else
        {
            Assert.Null(failure);
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(winner.Value, result.Value);
            var replay = Assert.IsType<AcceptedAppendResult.Replayed>(transport.Observed);
            Assert.Equal(admitted.FirstResponse, replay.FirstResponse);
            Assert.Equal(NativeFactReference.Of(handoff.Stream, admitted), replay.FirstFact);
            Assert.Equal(202, replay.Response.HttpStatus);
        }
        Assert.Equal(0, transport.Classifications);
        Assert.Equal(3, handoff.Revision);
        Assert.Empty(await host.EventsAsync<AdvisorFeedbackRecorded>(
            new AdvisorFeedbackThread(host.TenantId, host.ProgramId, handoffId, feedbackId).Stream));
    }

    sealed class FaultStore(IImmutableContentStore inner, Func<Task> compete) : IImmutableContentStore
    {
        bool _failed;
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (!_failed && schema == AdvisorContent.FeedbackAuthoredSchema)
            {
                _failed = true;
                await compete();
                throw new ImmutableContentUnavailableException("The losing authored-feedback write failed.");
            }
            return await inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => inner.ReadVerifiedAsync(realm, reference, ct);
    }

    sealed class Trace : INativeOperationTransportObserver
    {
        public AcceptedAppendResult? Observed { get; private set; }
        public int Classifications { get; private set; }
        public RequestError? ClassifyFailure(AcceptedOperationBinding binding, IExecutionContext context,
            ImmutableContentIntegrityException exception)
        {
            Classifications++;
            return new RequestError(RequestErrorKind.Conflict, exception.Message,
                isTransient: exception is ImmutableContentUnavailableException);
        }
        public void Observe(AcceptedOperationBinding binding, IExecutionContext context, AcceptedAppendResult accepted) =>
            Observed = accepted;
    }
}
