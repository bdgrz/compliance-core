using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class AccessItemReceiverPreparationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayOpenedItemOrRequestedCancellationGivenWinnerBeforeCreationIntentReadFailure(bool cancel)
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
        var owner = await fixture.ItemAsync(context.Request.CampaignId, context.Request.ItemId);

        // Assert
        Assert.True(winner.IsSuccess);
        Assert.NotNull(owner.Opened);
        var original = Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact;
        Assert.Equal(NativeFactReference.Of(owner.Stream, owner.Opened!), original);
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(failure).CancellationToken);
        else
        {
            Assert.Null(failure);
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(original, Assert.IsType<ReceiverOutcome.Replayed>(result.Value).Fact);
        }
        Assert.Equal(1, owner.Revision);
        Assert.Equal(1, fault.Reads);
    }

    [Fact]
    public async Task ShouldReplayOriginalItemWithoutCreationBodyGivenAcceptedExactIntentReference()
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
        Assert.Equal(1, (await fixture.ItemAsync(context.Request.CampaignId, context.Request.ItemId)).Revision);
    }

    [Fact]
    public async Task ShouldWaitTransientlyGivenRequiredCreationIntentUnavailableAndNoNativeWinner()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var context = await SeedAsync(fixture);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var fault = new FaultStore(scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(), null);

        // Act
        var result = await Handler(scope.ServiceProvider, fault).HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error!.IsTransient);
        Assert.Null((await fixture.ItemAsync(context.Request.CampaignId, context.Request.ItemId)).Opened);
    }

    internal static MaterializeAccessReviewItemHandler Handler(IServiceProvider services, IImmutableContentStore content) =>
        new(services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<IAggregateExecutor>(), content,
            new AccessReviewManifests(content), services.GetRequiredService<TimeProvider>());

    internal static async Task<Cntryl.Portia.RequestContext<MaterializeAccessReviewItem>> SeedAsync(AccessReviewFixture fixture)
    {
        var (population, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(population);
        var launched = await fixture.SendAsync(fixture.ManagerUserId, fixture.LaunchRequest(population));
        var campaign = await fixture.CampaignAggregateAsync(launched.CampaignId);
        var fact = campaign.Launched!;
        var intents = await fixture.Provider.GetRequiredService<AccessReviewManifests>()
            .IntentsAsync(fixture.TenantId.ToString(), fact.ItemIntentManifest, CancellationToken.None);
        var chosen = intents.OrderBy(static value => value.Key.ToString(), StringComparer.Ordinal).First();
        var command = new MaterializeAccessReviewItem(Uuid.CreateVersion4(), NativeFactReference.Of(campaign.Stream, fact),
            AccessReviewEffects.ItemStream(fixture.TenantId, campaign.Id, chosen.Key), campaign.Id,
            AccessReviewEffects.Generation, null, chosen.Value, campaign.Id, chosen.Key);
        return new Cntryl.Portia.RequestContext<MaterializeAccessReviewItem>(command, new RequestDispatchContext(RequestActor.System));
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
            if (reference.Schema == AccessReviewItemCreationIntent.Schema)
            {
                Reads++;
                if (beforeFailure is not null)
                    await beforeFailure();
                throw new ImmutableContentUnavailableException("The actual item creation intent is unavailable.");
            }
            return await inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
}
