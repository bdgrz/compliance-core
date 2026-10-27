using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class AccessReceiverPreparationSafetyTests
{
    [Theory]
    [InlineData(false, "intent")]
    [InlineData(false, "position")]
    [InlineData(false, "root")]
    [InlineData(true, "intent")]
    [InlineData(true, "position")]
    [InlineData(true, "root")]
    public async Task ShouldRefuseChangedSourceTermsGivenPreviouslyMaterializedNativeReceipt(bool batch, string term)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        if (batch)
        {
            var context = await AccessBatchReceiverPreparationTests.SeedAsync(fixture);
            var handler = AccessBatchReceiverPreparationTests.Handler(services, content);
            var first = await handler.HandleAsync(context, CancellationToken.None);
            Assert.True(first.IsSuccess, first.Error?.Message);
            var command = context.Request;
            var changed = term switch
            {
                "intent" => command with { Intent = command.Intent with { ContentId = Uuid.CreateVersion4() } },
                "position" => command with { Source = command.Source with { AggregateVersion = command.Source.AggregateVersion + 1 } },
                _ => command with { OperationId = Uuid.CreateVersion4() },
            };

            // Act
            var refused = await handler.HandleAsync(Context(changed), CancellationToken.None);

            // Assert
            Assert.False(refused.IsSuccess);
            Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
            Assert.Equal(0, (await fixture.BatchAsync(command.CampaignId, command.DecisionBatchId)).Revision);
        }
        else
        {
            var context = await AccessItemReceiverPreparationTests.SeedAsync(fixture);
            var handler = AccessItemReceiverPreparationTests.Handler(services, content);
            var first = await handler.HandleAsync(context, CancellationToken.None);
            Assert.True(first.IsSuccess, first.Error?.Message);
            var command = context.Request;
            var changed = term switch
            {
                "intent" => command with { Intent = command.Intent with { ContentId = Uuid.CreateVersion4() } },
                "position" => command with { Source = command.Source with { AggregateVersion = command.Source.AggregateVersion + 1 } },
                _ => command with { OperationId = Uuid.CreateVersion4() },
            };

            // Act
            var refused = await handler.HandleAsync(Context(changed), CancellationToken.None);

            // Assert
            Assert.False(refused.IsSuccess);
            Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
            Assert.Equal(1, (await fixture.ItemAsync(command.CampaignId, command.ItemId)).Revision);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayOriginalReceiptGivenDifferentDeliveryIdWithSameExactMaterialization(bool batch)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        Result<ReceiverOutcome> first;
        Result<ReceiverOutcome> replay;
        if (batch)
        {
            var context = await AccessBatchReceiverPreparationTests.SeedAsync(fixture);
            var handler = AccessBatchReceiverPreparationTests.Handler(services, content);
            first = await handler.HandleAsync(context, CancellationToken.None);

            // Act
            replay = await handler.HandleAsync(Context(context.Request with { CommandId = Uuid.CreateVersion4() }), CancellationToken.None);
        }
        else
        {
            var context = await AccessItemReceiverPreparationTests.SeedAsync(fixture);
            var handler = AccessItemReceiverPreparationTests.Handler(services, content);
            first = await handler.HandleAsync(context, CancellationToken.None);

            // Act
            replay = await handler.HandleAsync(Context(context.Request with { CommandId = Uuid.CreateVersion4() }), CancellationToken.None);
        }

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(Assert.IsType<ReceiverOutcome.Applied>(first.Value).Fact,
            Assert.IsType<ReceiverOutcome.Replayed>(replay.Value).Fact);
    }

    [Theory]
    [InlineData("apply", "unavailable")]
    [InlineData("apply", "corrupt")]
    [InlineData("apply", "programmer")]
    [InlineData("fence", "unavailable")]
    [InlineData("fence", "corrupt")]
    [InlineData("fence", "programmer")]
    [InlineData("cancel", "unavailable")]
    [InlineData("cancel", "corrupt")]
    [InlineData("cancel", "programmer")]
    [InlineData("publish", "unavailable")]
    [InlineData("publish", "corrupt")]
    [InlineData("publish", "programmer")]
    [InlineData("complete-cancel", "unavailable")]
    [InlineData("complete-cancel", "corrupt")]
    [InlineData("complete-cancel", "programmer")]
    public async Task ShouldPreserveTypedFailureWithoutAppendingGivenNoNativeWinnerAndRequiredIntentFault(
        string receiver, string kind)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var command = await AccessBulkReceiverPreparationTests.SeedAsync(fixture, receiver);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var expected = kind switch
        {
            "unavailable" => (Exception)new ImmutableContentUnavailableException("Missing required actual intent."),
            "corrupt" => new ImmutableContentIntegrityException("Corrupt required actual intent."),
            _ => new InvalidOperationException("Unexpected programmer fault."),
        };
        var fault = new TypedFaultStore(content, expected);
        var before = await PositionAsync(fixture, command);
        var result = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "Not executed yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () => result = await AccessBulkReceiverPreparationTests
            .HandleAsync(services, fault, command, CancellationToken.None));

        // Assert
        if (kind == "programmer")
            Assert.Same(expected, failure);
        else
        {
            Assert.Null(failure);
            Assert.False(result.IsSuccess);
            Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
            Assert.Equal(kind == "unavailable", result.Error!.IsTransient);
        }
        Assert.Equal(before, await PositionAsync(fixture, command));
        Assert.Equal(1, fault.Reads);
    }

    [Fact]
    public async Task ShouldVerifyAlternateAbortProofGivenNativeCancellationRecordedAnotherValidFailure()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var pending = await AccessCompletionReceiverPreparationTests.PendingAsync(fixture);
        var command = (AbortAccessReviewCampaignCompletion)await fixture.CompletionStepAsync(pending,
            "abort", nameof(AbortAccessReviewCampaignCompletion));
        var first = await fixture.ReceiveAsync(command);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var content = fixture.Provider.GetRequiredService<IImmutableContentStore>();
        var original = await content.ReadJsonAsync<AccessReviewCompletionFailure>(fixture.TenantId.ToString(), command.FailureProof);
        var alternate = await content.WriteJsonAsync(fixture.TenantId.ToString(), Uuid.CreateVersion4(),
            AccessReviewContent.FailureProofSchema, original with { Reason = "A later driver observed another permanent blocker." });

        // Act
        var replay = await fixture.ReceiveAsync(command with { FailureProof = alternate });
        var refused = await fixture.ReceiveAsync(command with { FailureProof = pending.Intent });

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(Assert.IsType<ReceiverOutcome.Applied>(first.Value).Fact,
            Assert.IsType<ReceiverOutcome.Replayed>(replay.Value).Fact);
        Assert.False(refused.IsSuccess);
        Assert.False(refused.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldWaitForRequiredIntentBindingGivenAcceptedClosureAndUnavailableManifestAuthority()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var pending = await AccessCompletionReceiverPreparationTests.PendingAsync(fixture);
        var campaignId = Uuid.Parse(pending.Source.Stream.Resource, CultureInfo.InvariantCulture);
        var completed = await fixture.RunCompletionAsync(campaignId, pending.Key.RootOperationId);
        Assert.Equal(ProcessState.Completed, completed.State);
        var command = (CompleteAccessReviewCampaignClosure)await fixture.CompletionStepAsync(completed,
            "complete", nameof(CompleteAccessReviewCampaignClosure));
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fault = new TypedFaultStore(services.GetRequiredService<IImmutableContentStore>(),
            new ImmutableContentUnavailableException("The required incoming-manifest binding is unavailable."), command.Intent.Schema);

        // Act
        var result = await AccessCompletionReceiverPreparationTests.Complete(services, fault)
            .HandleAsync(Context(command), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.IsTransient);
        Assert.Equal(1, fault.Reads);
        Assert.Equal(3, (await fixture.CampaignAggregateAsync(campaignId)).Revision);
    }

    [Theory]
    [InlineData("publish")]
    [InlineData("complete-cancel")]
    public async Task ShouldReturnRecordedProofSetGivenIrreversibleNativeReceiverRetryWithAnotherProofClaim(string receiver)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var command = await AccessBulkReceiverPreparationTests.SeedAsync(fixture, receiver);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var first = await AccessBulkReceiverPreparationTests.HandleAsync(services, content, command, CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var changed = command switch
        {
            PublishAccessReviewDecisionBatch value => (IRequest<ReceiverOutcome>)(value with { ItemDecisionProofs = value.Intent }),
            CompleteAccessReviewDecisionBatchCancellation value => value with { ItemFenceProofs = value.Intent },
            _ => throw new ArgumentException("The receiver has no irreversible proof-set receipt.", nameof(receiver)),
        };

        // Act
        var replay = await AccessBulkReceiverPreparationTests.HandleAsync(services, content, changed, CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(Assert.IsType<ReceiverOutcome.Applied>(first.Value).Fact,
            Assert.IsType<ReceiverOutcome.Replayed>(replay.Value).Fact);
    }

    static Cntryl.Portia.RequestContext<T> Context<T>(T command) where T : class, IRequest<ReceiverOutcome> =>
        new(command, new RequestDispatchContext(RequestActor.System));

    static async Task<ulong> PositionAsync(AccessReviewFixture fixture, IRequest<ReceiverOutcome> command) => command switch
    {
        ApplyAccessReviewItemDecision value => (await fixture.ItemAsync(value.CampaignId, value.ItemId)).CommittedStreamPosition,
        FenceAccessReviewItemDecision value => (await fixture.ItemAsync(value.CampaignId, value.ItemId)).CommittedStreamPosition,
        CancelAccessReviewDecisionBatch value => (await fixture.BatchAsync(value.CampaignId, value.DecisionBatchId)).CommittedStreamPosition,
        PublishAccessReviewDecisionBatch value => (await fixture.BatchAsync(Uuid.Parse(value.Source.Stream.Resource,
            CultureInfo.InvariantCulture), value.DecisionBatchId)).CommittedStreamPosition,
        CompleteAccessReviewDecisionBatchCancellation value => (await fixture.BatchAsync(Uuid.Parse(value.Source.Stream.Resource,
            CultureInfo.InvariantCulture), value.DecisionBatchId)).CommittedStreamPosition,
        _ => throw new ArgumentException("The fixture does not support this receiver.", nameof(command)),
    };

    sealed class TypedFaultStore(IImmutableContentStore inner, Exception failure,
        string failedSchema = BulkAccessDecisionIntent.Schema) : IImmutableContentStore
    {
        public int Reads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (reference.Schema != failedSchema)
                return inner.ReadVerifiedAsync(realm, reference, ct);
            Reads++;
            throw failure;
        }
    }
}
