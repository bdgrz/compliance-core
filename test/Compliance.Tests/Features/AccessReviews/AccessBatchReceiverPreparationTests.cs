using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Handlers;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
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

public sealed class AccessBatchReceiverPreparationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayMaterializedBatchOrRequestedCancellationGivenWinnerBeforeBulkIntentReadFailure(bool cancel)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var context = await SeedAsync(fixture);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var healthy = Handler(services, content);
        using var cancellation = new CancellationTokenSource();
        var winner = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "No winner yet."));
        var fault = new FaultStore(content, async () =>
        {
            winner = await healthy.HandleAsync(context, CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            if (cancel)
                cancellation.Cancel();
        });
        var losing = Handler(services, fault);
        var result = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "Not executed yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () => result = await losing.HandleAsync(context, cancellation.Token));
        var owner = await fixture.BatchAsync(context.Request.CampaignId, context.Request.DecisionBatchId);

        // Assert
        Assert.True(winner.IsSuccess);
        Assert.NotNull(owner.Accepted);
        var original = Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact;
        Assert.Equal(NativeFactReference.Of(owner.Stream, owner.Accepted!), original);
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(failure).CancellationToken);
        else
        {
            Assert.Null(failure);
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(original, Assert.IsType<ReceiverOutcome.Replayed>(result.Value).Fact);
        }
        Assert.Equal(0, owner.Revision);
        Assert.Equal(1, fault.Reads);
    }

    [Fact]
    public async Task ShouldReplayOriginalBatchWithoutBulkIntentBodyGivenAcceptedExactIntentReference()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var context = await SeedAsync(fixture);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var winner = await Handler(scope.ServiceProvider, content).HandleAsync(context, CancellationToken.None);
        Assert.True(winner.IsSuccess, winner.Error?.Message);
        var fault = new FaultStore(content, null);

        // Act
        var replay = await Handler(scope.ServiceProvider, fault).HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact,
            Assert.IsType<ReceiverOutcome.Replayed>(replay.Value).Fact);
        Assert.Equal(0, fault.Reads);
        Assert.Equal(0, (await fixture.BatchAsync(context.Request.CampaignId, context.Request.DecisionBatchId)).Revision);
    }

    internal static MaterializeAccessReviewDecisionBatchHandler Handler(IServiceProvider services, IImmutableContentStore content)
    {
        var reader = services.GetRequiredService<IAggregateReader>();
        return new MaterializeAccessReviewDecisionBatchHandler(reader, services.GetRequiredService<IAggregateExecutor>(),
            new AccessReviewBulkVerifier(reader, content, new AccessReviewManifests(content)), services.GetRequiredService<TimeProvider>());
    }

    internal static async Task<Cntryl.Portia.RequestContext<MaterializeAccessReviewDecisionBatch>> SeedAsync(AccessReviewFixture fixture)
    {
        var (campaignId, selected) = await AccessReviewBulkDecisionTests.LaunchedAsync(fixture);
        var (request, _) = await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);
        var campaign = await fixture.CampaignAggregateAsync(campaignId, selectedRootId: request.DecisionBatchId);
        var admitted = campaign.AdmittedFor(request.DecisionBatchId)!;
        var command = new MaterializeAccessReviewDecisionBatch(Uuid.CreateVersion4(), NativeFactReference.Of(campaign.Stream, admitted),
            AccessReviewEffects.BatchStream(fixture.TenantId, campaignId, request.DecisionBatchId), request.DecisionBatchId,
            AccessReviewEffects.Generation, null, admitted.Intent, campaignId, request.DecisionBatchId);
        return new Cntryl.Portia.RequestContext<MaterializeAccessReviewDecisionBatch>(command, new RequestDispatchContext(RequestActor.System));
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
                throw new ImmutableContentUnavailableException("The actual bulk decision intent is unavailable.");
            }
            return await inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
}
