using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Handlers;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class AccessBulkReceiverPreparationTests
{
    [Theory]
    [InlineData("apply", false)]
    [InlineData("apply", true)]
    [InlineData("fence", false)]
    [InlineData("fence", true)]
    [InlineData("cancel", false)]
    [InlineData("cancel", true)]
    [InlineData("publish", false)]
    [InlineData("publish", true)]
    [InlineData("complete-cancel", false)]
    [InlineData("complete-cancel", true)]
    public async Task ShouldReplayExactReceiverOrRequestedCancellationGivenWinnerBeforeFrozenIntentReadFailure(
        string receiver, bool cancel)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var command = await SeedAsync(fixture, receiver);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        using var cancellation = new CancellationTokenSource();
        var winner = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "No winner yet."));
        var fault = new FaultStore(content, async () =>
        {
            winner = await HandleAsync(services, content, command, CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            if (cancel)
                cancellation.Cancel();
        });
        var result = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "Not executed yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () => result = await HandleAsync(services, fault,
            command, cancellation.Token));

        // Assert
        Assert.True(winner.IsSuccess);
        var original = Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact;
        Assert.NotEqual(Uuid.Empty, original.EventId);
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(failure).CancellationToken);
        else
        {
            Assert.Null(failure);
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(original, Assert.IsType<ReceiverOutcome.Replayed>(result.Value).Fact);
        }
        Assert.Equal(1, fault.Reads);
    }

    [Theory]
    [InlineData("apply")]
    [InlineData("fence")]
    [InlineData("cancel")]
    [InlineData("publish")]
    [InlineData("complete-cancel")]
    public async Task ShouldReplayNativeReceiptWithoutFrozenIntentGivenExactPreviouslyAcceptedReceiver(string receiver)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var command = await SeedAsync(fixture, receiver);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var winner = await HandleAsync(services, content, command, CancellationToken.None);
        Assert.True(winner.IsSuccess, winner.Error?.Message);
        var fault = new FaultStore(content, null);

        // Act
        var replay = await HandleAsync(services, fault, command, CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact,
            Assert.IsType<ReceiverOutcome.Replayed>(replay.Value).Fact);
        Assert.Equal(0, fault.Reads);
    }

    [Theory]
    [InlineData("apply", false)]
    [InlineData("apply", true)]
    [InlineData("publish", false)]
    [InlineData("publish", true)]
    public async Task ShouldReplayReceiptOrRequestedCancellationGivenWinnerBeforeDerivedResultWriteFailure(
        string receiver, bool cancel)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var command = await SeedAsync(fixture, receiver);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        using var cancellation = new CancellationTokenSource();
        var winner = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "No winner yet."));
        var fault = new WriteFaultStore(content, async () =>
        {
            winner = await HandleAsync(services, content, command, CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            if (cancel)
                cancellation.Cancel();
        });
        var result = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "Not executed yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () => result = await HandleAsync(services, fault,
            command, cancellation.Token));

        // Assert
        Assert.True(winner.IsSuccess);
        var original = Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact;
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(failure).CancellationToken);
        else
        {
            Assert.Null(failure);
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(original, Assert.IsType<ReceiverOutcome.Replayed>(result.Value).Fact);
        }
        Assert.Equal(1, fault.Writes);
    }

    internal static async ValueTask<Result<ReceiverOutcome>> HandleAsync(IServiceProvider services, IImmutableContentStore content,
        IRequest<ReceiverOutcome> command, CancellationToken ct)
    {
        var reader = services.GetRequiredService<IAggregateReader>();
        var executor = services.GetRequiredService<IAggregateExecutor>();
        var clock = services.GetRequiredService<TimeProvider>();
        var manifests = new AccessReviewManifests(content);
        var verifier = new AccessReviewBulkVerifier(reader, content, manifests);
        return command switch
        {
            ApplyAccessReviewItemDecision value => await new ApplyAccessReviewItemDecisionHandler(reader, executor,
                content, verifier, clock).HandleAsync(Context(value), ct),
            FenceAccessReviewItemDecision value => await new FenceAccessReviewItemDecisionHandler(reader, executor,
                verifier, clock).HandleAsync(Context(value), ct),
            CancelAccessReviewDecisionBatch value => await new CancelAccessReviewDecisionBatchHandler(reader, executor,
                content, clock).HandleAsync(Context(value), ct),
            PublishAccessReviewDecisionBatch value => await new PublishAccessReviewDecisionBatchHandler(reader, executor,
                content, verifier, manifests, clock).HandleAsync(Context(value), ct),
            CompleteAccessReviewDecisionBatchCancellation value => await new CompleteAccessReviewDecisionBatchCancellationHandler(
                reader, executor, content, verifier, manifests, clock).HandleAsync(Context(value), ct),
            _ => throw new ArgumentException("The receiver is not part of this fixture.", nameof(command)),
        };
    }

    static Cntryl.Portia.RequestContext<T> Context<T>(T command) where T : class, IRequest<ReceiverOutcome> =>
        new(command, new RequestDispatchContext(RequestActor.System));

    internal static async Task<IRequest<ReceiverOutcome>> SeedAsync(AccessReviewFixture fixture, string receiver)
    {
        var (campaignId, items) = await AccessReviewBulkDecisionTests.LaunchedAsync(fixture);
        var (request, _) = await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, items);
        var process = await fixture.StartBulkAsync(campaignId, request.DecisionBatchId);
        var materialized = await fixture.ReceiveAsync(await fixture.BulkStepAsync(process, "materialize",
            nameof(MaterializeAccessReviewDecisionBatch)));
        Assert.True(materialized.IsSuccess, materialized.Error?.Message);
        if (receiver == "cancel")
            return await fixture.BulkStepAsync(process, "abort", nameof(CancelAccessReviewDecisionBatch));
        if (receiver is "fence" or "complete-cancel")
        {
            var canceled = await fixture.ReceiveAsync(await fixture.BulkStepAsync(process, "abort",
                nameof(CancelAccessReviewDecisionBatch)));
            Assert.True(canceled.IsSuccess, canceled.Error?.Message);
            var proofs = new List<BulkItemFenceProof>();
            foreach (var target in process.Targets)
            {
                var item = AccessReviewCompletionTarget.Parse(target);
                var command = new FenceAccessReviewItemDecision(BulkAccessDecisionProcess.ReceiverCommandId(
                    process.Source.EventId, nameof(FenceAccessReviewItemDecision), target, process.Episode), process.Source,
                    AccessReviewEffects.ItemStream(fixture.TenantId, campaignId, item.ItemId), request.DecisionBatchId,
                    AccessReviewEffects.Generation, null, process.Intent, campaignId, item.ItemId, request.DecisionBatchId);
                if (receiver == "fence")
                    return command;
                var fenced = await fixture.ReceiveAsync(command);
                Assert.True(fenced.IsSuccess, fenced.Error?.Message);
                proofs.Add(new BulkItemFenceProof(item.ItemId, Assert.IsType<ReceiverOutcome.Applied>(fenced.Value).Fact));
            }
            var commandId = BulkAccessDecisionProcess.ReceiverCommandId(process.Source.EventId,
                nameof(CompleteAccessReviewDecisionBatchCancellation), null, process.Episode);
            var proofSet = await fixture.Provider.GetRequiredService<IImmutableContentStore>()
                .WriteJsonAsync<IReadOnlyList<BulkItemFenceProof>>(fixture.TenantId.ToString(),
                    Uuid.CreateVersion5(commandId, "item-fence-proofs"), BulkItemFenceProof.Schema, proofs);
            return new CompleteAccessReviewDecisionBatchCancellation(commandId, process.Source,
                AccessReviewEffects.BatchStream(fixture.TenantId, campaignId, request.DecisionBatchId), request.DecisionBatchId,
                AccessReviewEffects.Generation, null, process.Intent, request.DecisionBatchId, proofSet);
        }
        foreach (var target in process.Targets)
        {
            var held = await fixture.ReceiveAsync(await fixture.BulkStepAsync(process, "reserve",
                nameof(ReserveAccessReviewItemDecision), target));
            Assert.True(held.IsSuccess, held.Error?.Message);
        }
        if (receiver == "apply")
            return await fixture.BulkStepAsync(process, "apply", nameof(ApplyAccessReviewItemDecision), process.Targets[0]);
        foreach (var target in process.Targets)
        {
            var applied = await fixture.ReceiveAsync(await fixture.BulkStepAsync(process, "apply",
                nameof(ApplyAccessReviewItemDecision), target));
            Assert.True(applied.IsSuccess, applied.Error?.Message);
        }
        return await fixture.BulkStepAsync(process, "publish", nameof(PublishAccessReviewDecisionBatch));
    }

    sealed class WriteFaultStore(IImmutableContentStore inner, Func<Task> beforeFailure) : IImmutableContentStore
    {
        public int Writes { get; private set; }
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (schema is AccessReviewContent.DecisionsSchema or AccessReviewContent.BulkResultSchema)
            {
                Writes++;
                await beforeFailure();
                throw new ImmutableContentUnavailableException("The actual derived receiver result cannot be written.");
            }
            return await inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => inner.ReadVerifiedAsync(realm, reference, ct);
    }

    sealed class FaultStore(IImmutableContentStore inner, Func<Task>? beforeFailure) : IImmutableContentStore
    {
        public int Reads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public async Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (reference.Schema == BulkAccessDecisionIntent.Schema)
            {
                Reads++;
                if (beforeFailure is not null)
                    await beforeFailure();
                throw new ImmutableContentUnavailableException("The actual frozen bulk intent is unavailable.");
            }
            return await inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
}
