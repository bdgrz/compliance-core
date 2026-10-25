using Bdgrz.Compliance.Features.SourceFacts;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

/// <summary>
///     The ordered completion process: every item is held at the head the completion pinned, the campaign closes with
///     the complete proof set, and the holds settle with the closure fence. A change that races the completion cancels
///     and fences it; nothing is released by time or by a missing read.
/// </summary>
public sealed class AccessReviewCompletionProcessTests
{
    [Fact]
    public async Task ShouldCompleteCampaignGivenEveryItemHeldAndSettled()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, itemIds) = await DecidedCampaignAsync(fixture);

        // Act
        var (acknowledgment, process) = await fixture.CompleteAsync(fixture.ManagerUserId, campaignId);
        var campaign = await fixture.CampaignAggregateAsync(campaignId);
        var accepted = Assert.IsType<AccessReviewCampaignCompletionAccepted>((await fixture.CampaignHistoryAsync(campaign)).Facts[1]);
        var closed = Assert.IsType<AccessReviewCampaignCompleted>((await fixture.CampaignHistoryAsync(campaign)).Facts[2]);
        var proofs = await fixture.Provider.GetRequiredService<IImmutableContentStore>()
            .ReadJsonAsync<IReadOnlyList<AccessReviewItemCompletionProof>>(fixture.TenantId.ToString(),
                closed.ItemCompletionProofs);
        var settled = new List<AccessReviewItemCampaignCompletionUseSettled>();
        foreach (var itemId in itemIds)
        {
            var item = await fixture.ItemAsync(campaignId, itemId);
            Assert.True(item.IsClosed);
            Assert.False(item.Uses.HasActiveUse);
            settled.AddRange((await UseFactsAsync(fixture, campaignId, itemId))
                .OfType<AccessReviewItemCampaignCompletionUseSettled>());
        }

        // Assert
        Assert.Equal(ProcessState.Completed, process.State);
        Assert.Equal(process.Key.RootOperationId, acknowledgment.OperationId);
        Assert.True(campaign.IsCompleted);
        Assert.Equal(3, campaign.Revision);
        Assert.Equal(accepted.Metadata!.EventId, closed.Operation.CausationEventId);
        Assert.Equal(AccessReviewCompletionProcess.ReceiverCommandId(accepted.Metadata.EventId,
            nameof(CompleteAccessReviewCampaignClosure), null, "initial"), closed.Operation.LogicalId);
        Assert.Equal(accepted.Operation.RootOperationId, closed.Operation.RootOperationId);
        Assert.Equal(itemIds.Count, proofs.Count);
        Assert.All(proofs, proof => Assert.True(proof.Grant.ReservationGeneration > 0));
        Assert.Equal(itemIds.OrderBy(static id => id.ToString(), StringComparer.Ordinal),
            proofs.Select(static proof => proof.ItemId).OrderBy(static id => id.ToString(), StringComparer.Ordinal));
        Assert.Equal(itemIds.Count, settled.Count);
        Assert.All(settled, fact => Assert.Equal(SourceUseSettlementDisposition.ParentClosed, fact.Disposition));
        var late = await fixture.FailAsync(fixture.ManagerUserId, fixture.Decision(campaignId, itemIds[0], 2, "revoke",
            "Late."), RequestErrorKind.Conflict);
        Assert.Contains("completed campaign cannot change", late.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReturnOriginalAcknowledgmentGivenExactCompletionRetryAfterClosure()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, _) = await DecidedCampaignAsync(fixture);
        var request = fixture.Completion(campaignId, 1);
        var first = await fixture.SendAsync(fixture.ManagerUserId, request);
        await fixture.RunCompletionAsync(campaignId, request.CommandId);

        // Act
        var retried = await fixture.SendAsync(fixture.ManagerUserId, request);
        var changed = await fixture.FailAsync(fixture.ManagerUserId, request with { Attestation = "A different attestation." },
            RequestErrorKind.Conflict);
        var changedRevision = await fixture.FailAsync(fixture.ManagerUserId, request with { ExpectedRevision = 2 },
            RequestErrorKind.Conflict);
        var campaign = await fixture.CampaignAggregateAsync(campaignId);

        // Assert
        Assert.Equal(first, retried);
        Assert.Contains("different", changed.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(changedRevision);
        Assert.Equal(3, (await fixture.CampaignHistoryAsync(campaign)).Facts.Count);
    }

    [Fact]
    public async Task ShouldRefuseSecondCompletionGivenOneInProgressAndStaleRevision()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, _) = await DecidedCampaignAsync(fixture);

        // Act
        var stale = await fixture.FailAsync(fixture.ManagerUserId, fixture.Completion(campaignId, 0),
            RequestErrorKind.Conflict);
        await fixture.SendAsync(fixture.ManagerUserId, fixture.Completion(campaignId, 1));
        var second = await fixture.FailAsync(fixture.ManagerUserId, fixture.Completion(campaignId, 2),
            RequestErrorKind.Conflict);

        // Assert
        Assert.Contains("revision", stale.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("in progress", second.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldCancelAndFenceCompletionGivenLateItemChangeRacingAdmission()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, itemIds) = await DecidedCampaignAsync(fixture);
        var request = fixture.Completion(campaignId, 1);
        await fixture.SendAsync(fixture.ManagerUserId, request);
        var started = await fixture.StartCompletionAsync(campaignId, request.CommandId);
        var lastTarget = ItemOf(started.Targets[^1]);
        var firstTarget = ItemOf(started.Targets[0]);

        // Act
        var late = await fixture.SendAsync(fixture.ManagerUserId, fixture.Decision(campaignId, lastTarget, 2, "keep",
            "I reread the evidence after the sign-off."));
        var run = await fixture.RunCompletionAsync(campaignId, request.CommandId);
        await SettleCanceledAsync(fixture, campaignId);
        var campaign = await fixture.CampaignAggregateAsync(campaignId);
        var canceled = Assert.IsType<AccessReviewCampaignCompletionCanceled>((await fixture.CampaignHistoryAsync(campaign)).Facts[^1]);

        // Assert
        Assert.Equal("keep", late.Decision);
        Assert.Equal(ProcessState.Completed, run.State);
        Assert.Contains("superseded", run.Note, StringComparison.Ordinal);
        Assert.False(campaign.IsCompleted);
        Assert.Equal(3, campaign.Revision);
        Assert.Equal(request.CommandId, canceled.Operation.RootOperationId);
        Assert.Equal(AccessReviewCompletionProcess.ReceiverCommandId(
            ((DomainEvent)(await fixture.CampaignHistoryAsync(campaign)).AcceptedFor(request.CommandId)!).Metadata!.EventId,
            nameof(AbortAccessReviewCampaignCompletion), null, "initial"), canceled.Operation.LogicalId);
        foreach (var itemId in itemIds)
        {
            var item = await fixture.ItemAsync(campaignId, itemId);
            var uses = await UseFactsAsync(fixture, campaignId, itemId);
            Assert.False(item.IsClosed);
            Assert.False(item.Uses.HasActiveUse);
            var settled = Assert.Single(uses.OfType<AccessReviewItemCampaignCompletionUseSettled>());
            Assert.Equal(SourceUseSettlementDisposition.Canceled, settled.Disposition);
        }

        var heldFirst = await UseFactsAsync(fixture, campaignId, firstTarget);
        Assert.Single(heldFirst.OfType<AccessReviewItemCampaignCompletionUseAuthorized>());
        var fencedLast = await UseFactsAsync(fixture, campaignId, lastTarget);
        Assert.Empty(fencedLast.OfType<AccessReviewItemCampaignCompletionUseAuthorized>());
        var again = await fixture.CompleteAsync(fixture.ManagerUserId, campaignId, 3);
        Assert.Equal(ProcessState.Completed, again.Process.State);
        Assert.True((await fixture.CampaignAggregateAsync(campaignId)).IsCompleted);
    }

    [Fact]
    public async Task ShouldRefuseDelayedAcquisitionGivenSettlementBeforeGrant()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, _) = await DecidedCampaignAsync(fixture);
        var request = fixture.Completion(campaignId, 1);
        await fixture.SendAsync(fixture.ManagerUserId, request);
        var record = await fixture.StartCompletionAsync(campaignId, request.CommandId);
        var target = record.Targets[0];
        var itemId = ItemOf(target);
        var abort = await fixture.CompletionStepAsync(record with { State = ProcessState.Aborting }, "abort",
            nameof(AbortAccessReviewCampaignCompletion));
        var settle = await fixture.CompletionStepAsync(record with { State = ProcessState.Aborting }, "settle",
            nameof(SettleAccessReviewItemCampaignCompletionUse), target);
        var acquire = await fixture.CompletionStepAsync(record, "acquire",
            nameof(AuthorizeAccessReviewItemCampaignCompletionUse), target);

        // Act
        var before = await fixture.ReceiveAsync(settle);
        var fenced = await fixture.ReceiveAsync(abort);
        var settled = await fixture.ReceiveAsync(settle);
        var delayed = await fixture.ReceiveAsync(acquire);
        var uses = await UseFactsAsync(fixture, campaignId, itemId);

        // Assert
        Assert.False(before.IsSuccess);
        Assert.True(before.Error!.IsTransient);
        Assert.IsType<ReceiverOutcome.Applied>(fenced.Value);
        Assert.IsType<ReceiverOutcome.Applied>(settled.Value);
        var superseded = Assert.IsType<ReceiverOutcome.Superseded>(delayed.Value);
        Assert.Contains("canceled", superseded.Reason, StringComparison.Ordinal);
        Assert.Empty(uses.OfType<AccessReviewItemCampaignCompletionUseAuthorized>());
        Assert.Single(uses.OfType<AccessReviewItemCampaignCompletionUseSettled>());
    }

    [Fact]
    public async Task ShouldRefuseItemChangeGivenCompletionHoldsItAndAllowItAfterCancellation()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, _) = await DecidedCampaignAsync(fixture);
        var request = fixture.Completion(campaignId, 1);
        await fixture.SendAsync(fixture.ManagerUserId, request);
        var record = await fixture.StartCompletionAsync(campaignId, request.CommandId);
        var target = record.Targets[0];
        var itemId = ItemOf(target);
        var acquire = await fixture.CompletionStepAsync(record, "acquire",
            nameof(AuthorizeAccessReviewItemCampaignCompletionUse), target);

        // Act
        var held = await fixture.ReceiveAsync(acquire);
        var refused = await fixture.FailAsync(fixture.ManagerUserId, fixture.Decision(campaignId, itemId, 2, "revoke",
            "Racing the completion."), RequestErrorKind.Conflict);
        var abort = await fixture.CompletionStepAsync(record with { State = ProcessState.Aborting }, "abort",
            nameof(AbortAccessReviewCampaignCompletion));
        await fixture.ReceiveAsync(abort);
        await SettleCanceledAsync(fixture, campaignId);
        var allowed = await fixture.SendAsync(fixture.ManagerUserId, fixture.Decision(campaignId, itemId, 2, "revoke",
            "Now allowed."));

        // Assert
        Assert.IsType<ReceiverOutcome.Applied>(held.Value);
        Assert.True(refused.IsTransient);
        Assert.Contains("holds this item", refused.Message, StringComparison.Ordinal);
        Assert.Equal("revoke", allowed.Decision);
    }

    [Fact]
    public async Task ShouldWaitGivenClosureBeforeEveryItemIsHeld()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, _) = await DecidedCampaignAsync(fixture);
        var request = fixture.Completion(campaignId, 1);
        await fixture.SendAsync(fixture.ManagerUserId, request);
        var record = await fixture.StartCompletionAsync(campaignId, request.CommandId);
        var closure = await fixture.CompletionStepAsync(record, "complete",
            nameof(CompleteAccessReviewCampaignClosure));

        // Act
        var early = await fixture.ReceiveAsync(closure);
        var campaign = await fixture.CampaignAggregateAsync(campaignId);

        // Assert
        Assert.False(early.IsSuccess);
        Assert.True(early.Error!.IsTransient);
        Assert.Contains("not held yet", early.Error.Message, StringComparison.Ordinal);
        Assert.False(campaign.IsCompleted);
    }

    [Fact]
    public async Task ShouldRecoverForwardGivenAbortAfterClosure()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, _) = await DecidedCampaignAsync(fixture);
        var (_, process) = await fixture.CompleteAsync(fixture.ManagerUserId, campaignId);
        var abort = await fixture.CompletionStepAsync(process with { State = ProcessState.Aborting }, "abort",
            nameof(AbortAccessReviewCampaignCompletion));

        // Act
        var late = await fixture.ReceiveAsync(abort);
        var campaign = await fixture.CampaignAggregateAsync(campaignId);

        // Assert
        var superseded = Assert.IsType<ReceiverOutcome.Superseded>(late.Value);
        Assert.Contains("recovers forward", superseded.Reason, StringComparison.Ordinal);
        Assert.True(campaign.IsCompleted);
        Assert.Equal(3, (await fixture.CampaignHistoryAsync(campaign)).Facts.Count);
    }

    [Fact]
    public async Task ShouldCancelCompletionGivenExceptionExpiresBeforeClosure()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId, fixture.ManagerMemberId, "Owner away.",
            fixture.ApproverMemberId);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var botDeploy = AccessReviewCampaignTests.ItemId(campaign, "bot", "deploy");
        foreach (var item in campaign.Items)
            await fixture.SendAsync(fixture.ManagerUserId, fixture.Decision(launched.CampaignId, item.Item.ItemId, 1,
                item.Item.ItemId == botDeploy ? "modify" : "keep", "Reviewed."));
        await fixture.SendAsync(fixture.ApproverUserId, fixture.Exception(launched.CampaignId, botDeploy, 2,
            "Vendor fix pending.", fixture.Clock.GetUtcNow().AddHours(1)));
        var request = fixture.Completion(launched.CampaignId, 1);
        await fixture.SendAsync(fixture.ManagerUserId, request);

        // Act
        fixture.Clock.Advance(TimeSpan.FromHours(2));
        var run = await fixture.RunCompletionAsync(launched.CampaignId, request.CommandId);
        var aggregate = await fixture.CampaignAggregateAsync(launched.CampaignId);

        // Assert
        Assert.Equal(ProcessState.Completed, run.State);
        Assert.False(aggregate.IsCompleted);
        var canceled = Assert.IsType<AccessReviewCampaignCompletionCanceled>((await fixture.CampaignHistoryAsync(aggregate)).Facts[^1]);
        var proof = await fixture.Provider.GetRequiredService<IImmutableContentStore>()
            .ReadJsonAsync<AccessReviewCompletionFailure>(fixture.TenantId.ToString(), canceled.SourceProofs!);
        Assert.Equal(botDeploy, proof.ItemId);
        Assert.Contains("requires verified remediation", proof.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldResumeWithIdenticalCommandsGivenCrashDuringCompletion()
    {
        // Arrange
        var crash = new CrashAfterProcessReplaces();
        await using var fixture = await AccessReviewFixture.CreateAsync(processStore: crash.Wrap);
        var (campaignId, itemIds) = await DecidedCampaignAsync(fixture);
        var request = fixture.Completion(campaignId, 1);
        await fixture.SendAsync(fixture.ManagerUserId, request);
        await fixture.StartCompletionAsync(campaignId, request.CommandId);
        crash.ArmAfterReplaceCount(6);

        // Act
        var crashed = await Record.ExceptionAsync(() => fixture.RunCompletionAsync(campaignId, request.CommandId));
        var held = new List<Uuid>();
        foreach (var itemId in itemIds)
            held.AddRange((await UseFactsAsync(fixture, campaignId, itemId))
                .OfType<AccessReviewItemCampaignCompletionUseAuthorized>().Select(static fact => fact.Metadata!.EventId));
        crash.Disarm();
        var resumed = await fixture.RunCompletionAsync(campaignId, request.CommandId);
        var after = new List<Uuid>();
        foreach (var itemId in itemIds)
            after.AddRange((await UseFactsAsync(fixture, campaignId, itemId))
                .OfType<AccessReviewItemCampaignCompletionUseAuthorized>().Select(static fact => fact.Metadata!.EventId));

        // Assert
        Assert.IsType<InvalidOperationException>(crashed);
        Assert.InRange(held.Count, 1, itemIds.Count - 1);
        Assert.Equal(ProcessState.Completed, resumed.State);
        Assert.Equal(itemIds.Count, after.Count);
        Assert.All(held, eventId => Assert.Contains(eventId, after));
        Assert.True((await fixture.CampaignAggregateAsync(campaignId)).IsCompleted);
    }

    [Fact]
    public async Task ShouldStartCompletionFromCommittedAdmissionGivenRootReactor()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, itemIds) = await DecidedCampaignAsync(fixture);
        var request = fixture.Completion(campaignId, 1);
        await fixture.SendAsync(fixture.ManagerUserId, request);
        var aggregate = await fixture.CampaignAggregateAsync(campaignId);
        var record = await ReadAsync<AccessReviewCampaignCompletionAccepted>(fixture, aggregate.Stream);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var reactor = new AccessReviewCompletionReactor(new InMemoryProjectionCheckpointStore(),
            scope.ServiceProvider.GetRequiredService<ProcessRootReaction>(),
            scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(),
            scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            scope.ServiceProvider.GetRequiredService<AccessReviewManifests>(),
            scope.ServiceProvider.GetRequiredService<ISourceFactResolver>());
        await fixture.BindReactorAsync(reactor);

        // Act
        await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<AccessReviewCampaignCompletionAccepted>(record),
            CancellationToken.None);
        var process = await fixture.ProcessAsync(await fixture.CompletionKey(request.CommandId));

        // Assert
        Assert.Equal(ProcessState.Completed, process!.State);
        Assert.Equal(itemIds.Count, process.Targets.Count);
        Assert.Equal(process.Targets.Order(Bdgrz.Compliance.Features.Kernel.StreamKeyOrder.Instance), process.Targets);
        Assert.True((await fixture.CampaignAggregateAsync(campaignId)).IsCompleted);
    }

    [Fact]
    public async Task ShouldMatchDesignCatalogGivenCompletionAndLaunchDefinitions()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "product", "audits",
            "2026-10-23", "target-reactor-contracts.json");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var catalog = document.RootElement.GetProperty("processes").EnumerateArray()
            .ToDictionary(p => p.GetProperty("process").GetString()!, p => p);

        // Act
        var definitions = fixture.Provider.GetRequiredService<ProcessCatalog>().Definitions
            .Where(definition => definition.Name.StartsWith("AccessReview", StringComparison.Ordinal)).ToArray();

        // Assert
        Assert.Equal(2, definitions.Length);
        foreach (var definition in definitions)
        {
            var entry = Assert.Contains(definition.Name, catalog);
            Assert.Equal(entry.GetProperty("ordered").GetBoolean(), definition.Ordered);
            Assert.Equal(entry.GetProperty("rootSourceEvents").EnumerateArray().Select(e => e.GetString()),
                definition.RootSourceEvents);
            Assert.Equal(entry.GetProperty("stages").EnumerateArray().Select(s => s.GetProperty("stage").GetString()),
                definition.Stages.Select(s => s.Stage));
            Assert.Equal(entry.GetProperty("stages").EnumerateArray()
                    .SelectMany(s => s.GetProperty("receivers").EnumerateArray().Select(r => r.GetString())),
                definition.Stages.SelectMany(s => s.Receivers));
            Assert.Equal(entry.GetProperty("abort").EnumerateArray().Select(a => a.GetString()), definition.Abort);
        }
    }

    static Uuid ItemOf(string target) => Uuid.Parse(target.Split('|')[1],
        System.Globalization.CultureInfo.InvariantCulture);


    /// <summary>Launches a campaign with every item decided keep, ready for completion at campaign revision 1.</summary>
    static async Task<(Uuid CampaignId, IReadOnlyList<Uuid> ItemIds)> DecidedCampaignAsync(AccessReviewFixture fixture)
    {
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId, fixture.ManagerMemberId, "Owner away.",
            fixture.ApproverMemberId);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        foreach (var item in campaign.Items)
            await fixture.SendAsync(fixture.ManagerUserId, fixture.Decision(launched.CampaignId, item.Item.ItemId, 1,
                "keep", "Still needed."));
        return (launched.CampaignId, [.. campaign.Items.Select(static item => item.Item.ItemId)]);
    }

    /// <summary>Runs the settlement reactor on the cancellation fact, as the worker would.</summary>
    static async Task SettleCanceledAsync(AccessReviewFixture fixture, Uuid campaignId)
    {
        var aggregate = await fixture.CampaignAggregateAsync(campaignId);
        var record = await ReadAsync<AccessReviewCampaignCompletionCanceled>(fixture, aggregate.Stream);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var reactor = new AccessReviewCompletionSettlementReactor(new InMemoryProjectionCheckpointStore(),
            scope.ServiceProvider.GetRequiredService<IRequestBus>(),
            scope.ServiceProvider.GetRequiredService<IProcessRecordStore>(),
            scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            scope.ServiceProvider.GetRequiredService<ISourceFactResolver>(),
            scope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
        await fixture.BindReactorAsync(reactor);
        await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<AccessReviewCampaignCompletionCanceled>(record),
            CancellationToken.None);
    }

    static async Task<DomainEventRecord> ReadAsync<TEvent>(AccessReviewFixture fixture, EventStreamAddress stream)
        where TEvent : DomainEvent
    {
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await foreach (var record in events.ReadAsync(stream, 0))
            if (record.Event is TEvent)
                return record;
        throw new InvalidOperationException($"No {typeof(TEvent).Name} was found.");
    }

    /// <summary>The item's completion use facts (grants and settlements), which are not business facts of the item.</summary>
    static async Task<List<DomainEvent>> UseFactsAsync(AccessReviewFixture fixture, Uuid campaignId, Uuid itemId)
    {
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var found = new List<DomainEvent>();
        try
        {
            await foreach (var record in events.ReadAsync(AccessReviewEffects.ItemStream(fixture.TenantId, campaignId,
                               itemId), 0))
                if (record.Event is AccessReviewItemCampaignCompletionUseAuthorized or
                    AccessReviewItemCampaignCompletionUseSettled)
                    found.Add(record.Event);
        }
        catch (InvalidOperationException)
        {
            // The in-memory store reports an unwritten stream this way.
        }

        return found;
    }
}
