using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Handlers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class AccessCompletionReceiverPreparationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayCancellationOrRequestedCancellationGivenWinnerBeforeFailureProofReadFails(bool cancel)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var pending = await PendingAsync(fixture);
        var command = (AbortAccessReviewCampaignCompletion)await fixture.CompletionStepAsync(pending,
            "abort", nameof(AbortAccessReviewCampaignCompletion));
        var context = Context(command);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var healthy = Abort(services, content);
        using var cancellation = new CancellationTokenSource();
        var winner = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "No winner yet."));
        var fault = new FaultStore(content, command.FailureProof, async () =>
        {
            winner = await healthy.HandleAsync(context, CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            if (cancel)
                cancellation.Cancel();
        });
        var result = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "Not executed yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () => result = await Abort(services, fault)
            .HandleAsync(context, cancellation.Token));
        var campaign = await fixture.CampaignAggregateAsync(Uuid.Parse(command.Target.Resource, System.Globalization.CultureInfo.InvariantCulture),
            selectedRootId: command.OperationId);

        // Assert
        Assert.True(winner.IsSuccess);
        var canceled = Assert.IsType<AccessReviewCampaignCompletionCanceled>(campaign.CanceledFor(command.OperationId));
        var original = Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact;
        Assert.Equal(NativeFactReference.Of(campaign.Stream, canceled), original);
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(failure).CancellationToken);
        else
        {
            Assert.Null(failure);
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(original, Assert.IsType<ReceiverOutcome.Replayed>(result.Value).Fact);
        }
        Assert.Equal(3, campaign.Revision);
        Assert.Equal(1, fault.Reads);
    }

    [Fact]
    public async Task ShouldReplayAcceptedAbortWithoutFailureBytesGivenExactNativeFailureReference()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var pending = await PendingAsync(fixture);
        var command = (AbortAccessReviewCampaignCompletion)await fixture.CompletionStepAsync(pending,
            "abort", nameof(AbortAccessReviewCampaignCompletion));
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var winner = await Abort(services, content).HandleAsync(Context(command), CancellationToken.None);
        Assert.True(winner.IsSuccess, winner.Error?.Message);
        var fault = new FaultStore(content, command.FailureProof, null);

        // Act
        var replay = await Abort(services, fault).HandleAsync(Context(command), CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact,
            Assert.IsType<ReceiverOutcome.Replayed>(replay.Value).Fact);
        Assert.Equal(0, fault.Reads);
    }

    [Fact]
    public async Task ShouldRejectChangedItemManifestGivenAlreadyCompletedNativeClosure()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var pending = await PendingAsync(fixture);
        var completed = await fixture.RunCompletionAsync(Uuid.Parse(pending.Source.Stream.Resource,
            System.Globalization.CultureInfo.InvariantCulture), pending.Key.RootOperationId);
        Assert.Equal(ProcessState.Completed, completed.State);
        var command = (CompleteAccessReviewCampaignClosure)await fixture.CompletionStepAsync(completed,
            "complete", nameof(CompleteAccessReviewCampaignClosure));

        // Act
        var replay = await fixture.ReceiveAsync(command with { ItemProofs = command.Intent });

        // Assert
        Assert.False(replay.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, replay.Error?.Kind);
        Assert.False(replay.Error!.IsTransient);
        Assert.Equal(3, (await fixture.CampaignAggregateAsync(command.CampaignId)).Revision);
    }

    [Fact]
    public async Task ShouldReplayCompletedClosureWithoutSelectionBytesGivenBoundIncomingManifest()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var pending = await PendingAsync(fixture);
        var campaignId = Uuid.Parse(pending.Source.Stream.Resource, System.Globalization.CultureInfo.InvariantCulture);
        var completed = await fixture.RunCompletionAsync(campaignId, pending.Key.RootOperationId);
        Assert.Equal(ProcessState.Completed, completed.State);
        var command = (CompleteAccessReviewCampaignClosure)await fixture.CompletionStepAsync(completed,
            "complete", nameof(CompleteAccessReviewCampaignClosure));
        var campaign = await fixture.CampaignAggregateAsync(campaignId, selectedRootId: command.OperationId);
        var original = Assert.IsType<AccessReviewCampaignCompleted>(campaign.ClosedFor(command.OperationId));
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fault = new FaultStore(services.GetRequiredService<IImmutableContentStore>(),
            campaign.Launched!.ItemsManifest, null);

        // Act
        var result = await Complete(services, fault).HandleAsync(Context(command), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(NativeFactReference.Of(campaign.Stream, original),
            Assert.IsType<ReceiverOutcome.Replayed>(result.Value).Fact);
        Assert.Equal(0, fault.Reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayClosureOrRequestedCancellationGivenWinnerBeforeSelectionReadFailure(bool cancel)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var pending = await PendingAsync(fixture);
        foreach (var target in pending.Targets)
        {
            var held = await fixture.ReceiveAsync(await fixture.CompletionStepAsync(pending, "acquire",
                nameof(AuthorizeAccessReviewItemCampaignCompletionUse), target));
            Assert.True(held.IsSuccess, held.Error?.Message);
        }
        var command = (CompleteAccessReviewCampaignClosure)await fixture.CompletionStepAsync(pending,
            "complete", nameof(CompleteAccessReviewCampaignClosure));
        var campaign = await fixture.CampaignAggregateAsync(command.CampaignId);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var healthy = Complete(services, content);
        using var cancellation = new CancellationTokenSource();
        var winner = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "No winner yet."));
        var fault = new FaultStore(content, campaign.Launched!.ItemsManifest, async () =>
        {
            winner = await healthy.HandleAsync(Context(command), CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            if (cancel)
                cancellation.Cancel();
        });
        var result = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "Not executed yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () => result = await Complete(services, fault)
            .HandleAsync(Context(command), cancellation.Token));
        var current = await fixture.CampaignAggregateAsync(command.CampaignId, selectedRootId: command.OperationId);

        // Assert
        Assert.True(winner.IsSuccess);
        var closed = Assert.IsType<AccessReviewCampaignCompleted>(current.ClosedFor(command.OperationId));
        var original = Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact;
        Assert.Equal(NativeFactReference.Of(current.Stream, closed), original);
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(failure).CancellationToken);
        else
        {
            Assert.Null(failure);
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(original, Assert.IsType<ReceiverOutcome.Replayed>(result.Value).Fact);
        }
        Assert.Equal(3, current.Revision);
        Assert.Equal(1, fault.Reads);
    }

    internal static AbortAccessReviewCampaignCompletionHandler Abort(IServiceProvider services, IImmutableContentStore content) =>
        new(services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<IAggregateExecutor>(), content,
            services.GetRequiredService<TimeProvider>());

    internal static CompleteAccessReviewCampaignClosureHandler Complete(IServiceProvider services, IImmutableContentStore content)
    {
        var reader = services.GetRequiredService<IAggregateReader>();
        var events = services.GetRequiredService<IDomainEventReader>();
        var manifests = new AccessReviewManifests(content);
        return new CompleteAccessReviewCampaignClosureHandler(reader, services.GetRequiredService<IAggregateExecutor>(),
            content, manifests, services.GetRequiredService<PopulationSnapshotFreezer>(),
            new AccessReviewCompletionVerifier(reader, content, manifests, events),
            services.GetRequiredService<AccessReviewCompletionFences>(), services.GetRequiredService<TimeProvider>(), events);
    }

    static Cntryl.Portia.RequestContext<T> Context<T>(T command) where T : class, IRequest<ReceiverOutcome> =>
        new(command, new RequestDispatchContext(RequestActor.System));

    internal static async Task<ProcessRecord> PendingAsync(AccessReviewFixture fixture)
    {
        var (population, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(population);
        var launched = await fixture.LaunchAsync(population, fixture.ManagerMemberId, "Owner away.", fixture.ApproverMemberId);
        foreach (var item in (await fixture.CampaignAsync(launched.CampaignId)).Items)
            await fixture.SendAsync(fixture.ManagerUserId, fixture.Decision(launched.CampaignId,
                item.Item.ItemId, 1, "keep", "Still needed."));
        var request = fixture.Completion(launched.CampaignId, 1);
        await fixture.SendAsync(fixture.ManagerUserId, request);
        return await fixture.StartCompletionAsync(launched.CampaignId, request.CommandId);
    }

    sealed class FaultStore(IImmutableContentStore inner, ImmutableContentRef failedReference,
        Func<Task>? beforeFailure) : IImmutableContentStore
    {
        public int Reads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public async Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (reference == failedReference)
            {
                Reads++;
                if (beforeFailure is not null)
                    await beforeFailure();
                throw new ImmutableContentUnavailableException("The actual selected completion proof is unavailable.");
            }
            return await inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
}
