using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

public sealed class AdvisorPreparationRaceTests
{
    [Fact]
    public async Task ShouldReplayOriginalUnitResponseGivenEqualWinnerCommitsBeforeImmutableFreezeFails()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoff, feedback, _) = await host.SeedFeedbackAsync();
        var command = new RespondToAdvisorFeedback(Uuid.CreateVersion4(), host.TenantId, host.ProgramId, handoff,
            feedback, "The client responds once.", 1);
        var winner = Result.Failure(new RequestError(RequestErrorKind.Conflict, "No winner yet."));
        var transport = new Trace();
        var faulty = new RaceStore(host.Content, async () =>
        {
            winner = await host.RespondHandler.HandleAsync(host.Http(command), CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
        });
        var native = new NativeOperationExecutor(host.Reader, host.Services.GetRequiredService<IAggregateExecutor>(),
            faulty, transport);
        var loser = new RespondToAdvisorFeedbackHandler(new AdvisorUnitOperation(native, faulty),
            host.Threads, faulty, host.Clock);
        var actual = Result.Failure(new RequestError(RequestErrorKind.Conflict, "No losing response yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () =>
            actual = await loser.HandleAsync(host.Http(command), CancellationToken.None));
        var thread = await host.ThreadAsync(handoff, feedback);
        var fact = Assert.Single(await host.EventsAsync<AdvisorFeedbackResponded>(thread.Stream));

        // Assert
        Assert.Null(failure);
        Assert.True(winner.IsSuccess);
        Assert.True(actual.IsSuccess, actual.Error?.Message);
        var replay = Assert.IsType<AcceptedAppendResult.Replayed>(transport.Observed);
        Assert.Equal(fact.FirstResponse, replay.FirstResponse);
        Assert.Equal(NativeFactReference.Of(thread.Stream, fact), replay.FirstFact);
        Assert.Equal(200, replay.Response.HttpStatus);
        Assert.Equal(UnitResult.Schema, replay.Response.ResultSchema);
        Assert.Equal(0, transport.Classifications);
        Assert.Equal(2, thread.Revision);
    }

    [Fact]
    public async Task ShouldPreserveRequestedCancellationGivenEqualWinnerAndImmutablePreparationFailure()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoff, feedback, _) = await host.SeedFeedbackAsync();
        var command = new RespondToAdvisorFeedback(Uuid.CreateVersion4(), host.TenantId, host.ProgramId, handoff,
            feedback, "The accepted client response.", 1);
        using var cancellation = new CancellationTokenSource();
        var transport = new Trace();
        var faulty = new RaceStore(host.Content, async () =>
        {
            var winner = await host.RespondHandler.HandleAsync(host.Http(command), CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            cancellation.Cancel();
        });
        var native = new NativeOperationExecutor(host.Reader, host.Services.GetRequiredService<IAggregateExecutor>(),
            faulty, transport);
        var loser = new RespondToAdvisorFeedbackHandler(new AdvisorUnitOperation(native, faulty),
            host.Threads, faulty, host.Clock);

        // Act
        var failure = await Record.ExceptionAsync(async () =>
            await loser.HandleAsync(host.Http(command), cancellation.Token));
        var thread = await host.ThreadAsync(handoff, feedback);

        // Assert
        var canceled = Assert.IsAssignableFrom<OperationCanceledException>(failure);
        Assert.Equal(cancellation.Token, canceled.CancellationToken);
        Assert.Null(transport.Observed);
        Assert.Equal(0, transport.Classifications);
        Assert.Single(await host.EventsAsync<AdvisorFeedbackResponded>(thread.Stream));
        Assert.Equal(2, thread.Revision);
    }

    sealed class RaceStore(IImmutableContentStore inner, Func<Task> commitWinner) : IImmutableContentStore
    {
        bool _failed;
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (!_failed && schema == UnitResult.Schema)
            {
                _failed = true;
                await commitWinner();
                throw new ImmutableContentUnavailableException("The losing real unit-response write failed.");
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
