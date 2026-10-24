using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

/// <summary>
///     The unordered launch fan-out: every frozen item is materialized once from the launch manifest, a crashed
///     worker resumes with identical command ids and no duplicates, and a receiver only trusts the launch that its
///     campaign confirms.
/// </summary>
public sealed class AccessReviewLaunchFanOutTests
{
    [Fact]
    public async Task ShouldMaterializeEveryFrozenItemOnceGivenFanOut()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var launched = await LaunchWithoutItemsAsync(fixture);
        var launchFact = (await fixture.CampaignAggregateAsync(launched.CampaignId)).Launched!;

        // Act
        var run = await fixture.MaterializeAsync(launched.CampaignId);
        var rerun = await fixture.MaterializeAsync(launched.CampaignId);
        var opened = await OpenedAsync(fixture, launched.CampaignId);

        // Assert
        Assert.Equal(ProcessState.Completed, run.State);
        Assert.Equal(ProcessState.Completed, rerun.State);
        Assert.Equal(launched.ItemCount, opened.Count);
        Assert.All(opened, fact =>
        {
            Assert.Equal(1, fact.Revision);
            Assert.Equal(launched.CampaignId, fact.Operation.RootOperationId);
            Assert.Equal(launchFact.Metadata!.EventId, fact.Operation.CausationEventId);
            Assert.Null(fact.FirstResponse);
            Assert.Equal(AccessReviewLaunchFanOut.ReceiverCommandId(launchFact.Metadata.EventId,
                fact.ItemId.ToString(), "initial"), fact.Operation.LogicalId);
        });
        Assert.Equal(opened.Count, opened.Select(fact => fact.ItemId).Distinct().Count());
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        Assert.All(campaign.Items, item => Assert.Equal(1, item.Revision));
    }

    [Fact]
    public async Task ShouldStartFanOutFromCommittedLaunchGivenRootReactor()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var launched = await LaunchWithoutItemsAsync(fixture);
        var aggregate = await fixture.CampaignAggregateAsync(launched.CampaignId);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var record = await ReadAsync<AccessReviewCampaignLaunched>(events, aggregate.Stream);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var reactor = new AccessReviewLaunchFanOutReactor(new InMemoryProjectionCheckpointStore(),
            scope.ServiceProvider.GetRequiredService<ProcessRootReaction>(),
            scope.ServiceProvider.GetRequiredService<IImmutableContentStore>());

        // Act
        await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<AccessReviewCampaignLaunched>(record),
            CancellationToken.None);
        await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<AccessReviewCampaignLaunched>(record),
            CancellationToken.None);
        var process = await fixture.ProcessAsync(await fixture.LaunchKey(launched.CampaignId));

        // Assert
        Assert.Equal(ProcessState.Completed, process!.State);
        Assert.Equal(launched.ItemCount, process.Targets.Count);
        Assert.Equal(aggregate.Launched!.ItemIntentManifest, process.Intent);
        Assert.Equal(launched.ItemCount, (await OpenedAsync(fixture, launched.CampaignId)).Count);
    }

    [Fact]
    public async Task ShouldResumeWithoutDuplicateItemsGivenCrashAfterSomeReceivers()
    {
        // Arrange
        var crash = new CrashAfterProcessReplaces();
        await using var fixture = await AccessReviewFixture.CreateAsync(processStore: crash.Wrap);
        var launched = await LaunchWithoutItemsAsync(fixture);
        crash.ArmAfterReplaceCount(4);

        // Act
        var crashed = await Record.ExceptionAsync(() => fixture.MaterializeAsync(launched.CampaignId));
        var partial = await OpenedAsync(fixture, launched.CampaignId);
        var interrupted = await fixture.ProcessAsync(await fixture.LaunchKey(launched.CampaignId));
        crash.Disarm();
        var resumed = await fixture.MaterializeAsync(launched.CampaignId);
        var opened = await OpenedAsync(fixture, launched.CampaignId);

        // Assert
        Assert.IsType<InvalidOperationException>(crashed);
        Assert.InRange(partial.Count, 1, launched.ItemCount - 1);
        Assert.NotEqual(ProcessState.Completed, interrupted!.State);
        Assert.Equal(ProcessState.Completed, resumed.State);
        Assert.Equal(launched.ItemCount, opened.Count);
        Assert.Equal(opened.Select(fact => fact.ItemId).Distinct().Count(), opened.Count);
        foreach (var fact in partial)
        {
            var final = Assert.Single(opened, candidate => candidate.ItemId == fact.ItemId);
            Assert.Equal(fact.Metadata!.EventId, final.Metadata!.EventId);
            Assert.Equal(fact.Operation.LogicalId, final.Operation.LogicalId);
        }
    }

    [Fact]
    public async Task ShouldReturnOriginalFactGivenReplayedMaterialization()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var launched = await LaunchWithoutItemsAsync(fixture);
        var record = await StartAsync(fixture, launched.CampaignId);
        var command = await fixture.MaterializeStepAsync(record, record.Targets[0]);
        var applied = await fixture.ReceiveAsync(command);
        var other = await fixture.ReceiveAsync(command with { CommandId = Uuid.CreateVersion4() });

        // Act
        var replay = await fixture.ReceiveAsync(command);
        var opened = await OpenedAsync(fixture, launched.CampaignId);

        // Assert
        var original = Assert.IsType<ReceiverOutcome.Applied>(applied.Value).Fact;
        Assert.Equal(original, Assert.IsType<ReceiverOutcome.Replayed>(replay.Value).Fact);
        Assert.Equal(original, Assert.IsType<ReceiverOutcome.Replayed>(other.Value).Fact);
        Assert.Single(opened);
    }

    [Fact]
    public async Task ShouldConflictGivenChangedFrozenIntentForSameItem()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var launched = await LaunchWithoutItemsAsync(fixture);
        var record = await StartAsync(fixture, launched.CampaignId);
        var command = await fixture.MaterializeStepAsync(record, record.Targets[0]);
        var content = fixture.Provider.GetRequiredService<IImmutableContentStore>();
        var realm = fixture.TenantId.ToString();
        var frozen = await content.ReadJsonAsync<AccessReviewItemCreationIntent>(realm, command.Intent);
        var changedRef = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), AccessReviewItemCreationIntent.Schema,
            frozen with { ReviewerMemberId = Uuid.CreateVersion4() });

        // Act
        var forged = await fixture.ReceiveAsync(command with { Intent = changedRef });
        var opened = await OpenedAsync(fixture, launched.CampaignId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, forged.Error?.Kind);
        Assert.False(forged.Error!.IsTransient);
        Assert.Empty(opened);
    }

    [Fact]
    public async Task ShouldNotMaterializeGivenSourceThatIsNotTheLaunch()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var launched = await LaunchWithoutItemsAsync(fixture);
        var record = await StartAsync(fixture, launched.CampaignId);
        var command = await fixture.MaterializeStepAsync(record, record.Targets[0]);
        var missing = new NativeFactReference(command.Source.Stream, Uuid.CreateVersion4(), 2);

        // Act
        var unseen = await fixture.ReceiveAsync(command with { Source = missing });
        var wrongVersion = await fixture.ReceiveAsync(command with
        {
            Source = command.Source with { AggregateVersion = command.Source.AggregateVersion + 1 },
        });
        var wrongTarget = await fixture.ReceiveAsync(command with
        {
            Target = AccessReviewEffects.ItemStream(fixture.TenantId, launched.CampaignId, Uuid.CreateVersion4()),
        });

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, unseen.Error?.Kind);
        Assert.True(unseen.Error!.IsTransient);
        Assert.False(wrongVersion.IsSuccess);
        Assert.False(wrongTarget.IsSuccess);
        Assert.False(wrongTarget.Error!.IsTransient);
        Assert.Empty(await OpenedAsync(fixture, launched.CampaignId));
    }

    [Fact]
    public async Task ShouldRetainCompleteManifestGivenMoreItemsThanAnyEventCap()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var content = fixture.Provider.GetRequiredService<IImmutableContentStore>();
        var realm = fixture.TenantId.ToString();
        var intent = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), AccessReviewItemCreationIntent.Schema,
            new AccessReviewItemCreationIntent(new DurableIntentHeader(Uuid.CreateVersion4(),
                    new EventStreamAddress(realm, "a", "b"), new EventStreamAddress(realm, "a", "b"),
                    new EventStreamAddress(realm, "a", "b"), ActorReference.ForMember(Uuid.CreateVersion4(), "x"),
                    "initial", null, new string('a', 64), DateTimeOffset.UtcNow), Uuid.CreateVersion4(), 1,
                Uuid.CreateVersion4(), Uuid.CreateVersion4(), new ImmutableContentRef(Uuid.CreateVersion4(),
                    new string('b', 64), 1, "application/json", "x"), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                new NativeFactReference(new EventStreamAddress(realm, "a", "b"), Uuid.CreateVersion4(), 1)));
        var selections = Enumerable.Range(0, 5000).Select(_ => new AccessReviewItemSelection(Uuid.CreateVersion4(),
            intent)).ToArray();

        // Act
        var reference = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), AccessReviewItemIntentManifest.Schema,
            new AccessReviewItemIntentManifest(selections));
        var read = await content.ReadJsonAsync<AccessReviewItemIntentManifest>(realm, reference);
        var index = await fixture.Provider.GetRequiredService<AccessReviewManifests>().IntentsAsync(realm, reference);

        // Assert
        Assert.Equal(5000, read.Selections.Count);
        Assert.Equal(selections.Select(selection => selection.ItemId), read.Selections.Select(selection => selection.ItemId));
        Assert.Equal(5000, index.Count);
    }

    [Fact]
    public async Task ShouldDefineUnorderedPerTargetMaterializationGivenFanOutDefinition()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();

        // Act
        var definition = fixture.Provider.GetRequiredService<ProcessCatalog>().Get(AccessReviewEffects.LaunchFanOutName);

        // Assert
        Assert.False(definition.Ordered);
        Assert.Equal([nameof(AccessReviewCampaignLaunched)], definition.RootSourceEvents);
        var stage = Assert.Single(definition.Stages);
        Assert.Equal("materialize", stage.Stage);
        Assert.True(stage.IsPerTarget);
        Assert.Equal([nameof(MaterializeAccessReviewItem)], stage.Receivers);
        Assert.Empty(definition.Abort);
    }

    [Fact]
    public void ShouldDeriveOneIdentityPerCampaignAndItemGivenAnyReader()
    {
        // Arrange
        var (tenant, campaign, item) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        var first = AccessReviewEffects.ItemStreamId(tenant, campaign, item);
        var again = AccessReviewEffects.ItemStreamId(tenant, campaign, item);
        var otherCampaign = AccessReviewEffects.ItemStreamId(tenant, Uuid.CreateVersion4(), item);
        var otherItem = AccessReviewEffects.ItemStreamId(tenant, campaign, Uuid.CreateVersion4());
        var otherTenant = AccessReviewEffects.ItemStreamId(Uuid.CreateVersion4(), campaign, item);

        // Assert
        Assert.Equal(first, again);
        Assert.NotEqual(first, otherCampaign);
        Assert.NotEqual(first, otherItem);
        Assert.NotEqual(first, otherTenant);
        Assert.Equal(new AccessReviewItem(tenant, campaign, item).Stream,
            AccessReviewEffects.ItemStream(tenant, campaign, item));
    }

    static async Task<AccessReviewCampaignRegistration> LaunchWithoutItemsAsync(AccessReviewFixture fixture)
    {
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        return await fixture.SendAsync(fixture.ManagerUserId, fixture.LaunchRequest(populationId));
    }

    static async Task<ProcessRecord> StartAsync(AccessReviewFixture fixture, Uuid campaignId)
    {
        var aggregate = await fixture.CampaignAggregateAsync(campaignId);
        var launched = aggregate.Launched!;
        var manifest = await fixture.Provider.GetRequiredService<IImmutableContentStore>()
            .ReadJsonAsync<AccessReviewItemIntentManifest>(fixture.TenantId.ToString(), launched.ItemIntentManifest);
        return await fixture.Provider.GetRequiredService<IProcessRecordStore>().CreateIfAbsentAsync(
            ProcessRecord.Start(await fixture.LaunchKey(campaignId), NativeFactReference.Of(aggregate.Stream, launched),
                launched.Operation.Episode, launched.ItemIntentManifest,
                manifest.Selections.Select(static selection => selection.ItemId.ToString()), DateTimeOffset.UtcNow));
    }

    static async Task<DomainEventRecord> ReadAsync<TEvent>(IDomainEventReader events, EventStreamAddress stream)
        where TEvent : DomainEvent
    {
        await foreach (var record in events.ReadAsync(stream, 0))
            if (record.Event is TEvent)
                return record;
        throw new InvalidOperationException($"No {typeof(TEvent).Name} was found.");
    }

    internal static async Task<List<AccessReviewItemOpened>> OpenedAsync(AccessReviewFixture fixture, Uuid campaignId)
    {
        var campaign = await fixture.CampaignAggregateAsync(campaignId);
        var launched = campaign.Launched!;
        var manifest = await fixture.Provider.GetRequiredService<IImmutableContentStore>()
            .ReadJsonAsync<AccessReviewItemIntentManifest>(fixture.TenantId.ToString(), launched.ItemIntentManifest);
        var opened = new List<AccessReviewItemOpened>();
        foreach (var selection in manifest.Selections)
            opened.AddRange((await fixture.ItemAsync(campaignId, selection.ItemId)).Facts
                .OfType<AccessReviewItemOpened>());
        return opened;
    }
}
