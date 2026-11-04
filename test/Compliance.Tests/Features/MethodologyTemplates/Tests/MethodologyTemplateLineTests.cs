using System.Text.Json;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class MethodologyTemplateLineTests
{
    static readonly Uuid Template = Uuid.CreateVersion4();
    static readonly Uuid Operator = Uuid.CreateVersion4();

    [Fact]
    public void ShouldCreateIdleWithoutDraftGivenNewLine()
    {
        // Arrange
        var scenario = New();

        // Act
        var created = scenario.Aggregate.CreateLine("Firm controls", "Reusable firm methodology", Operator);

        // Assert
        Assert.True(created.IsSuccess);
        Assert.Equal("idle", scenario.Aggregate.LineStatus);
        Assert.Null(scenario.Aggregate.OpenDraftId);
        Assert.Equal(0, scenario.Aggregate.PublishedVersionNumber);
        Assert.IsType<MethodologyTemplateCreatedV4>(Assert.Single(scenario.PendingEvents));
    }

    [Fact]
    public void ShouldKeepOneOpenDraftGivenCompetingStarts()
    {
        // Arrange
        var scenario = Created();
        var draft = Uuid.CreateVersion4();
        var content = Content([]);

        // Act
        var first = scenario.Aggregate.StartDraft(draft, 0, [], content, Operator);
        var repeated = scenario.Aggregate.StartDraft(draft, 0, [], content, Operator);
        var competing = scenario.Aggregate.StartDraft(Uuid.CreateVersion4(), 0, [], content, Operator);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.False(competing.IsSuccess);
        Assert.Equal(draft, scenario.Aggregate.OpenDraftId);
        Assert.Equal("drafting", scenario.Aggregate.LineStatus);
        Assert.Single(scenario.PendingEvents.OfType<MethodologyTemplateDraftStarted>());
    }

    [Fact]
    public void ShouldPublishOnlyOpenDraftAndKeepExactRetryFactFreeGivenApprovedSnapshot()
    {
        // Arrange
        var scenario = Started(out var draft);
        IReadOnlyList<MethodologyTemplateRecordContent> entries = [Entry()];
        var content = Content(entries);
        var approval = Uuid.CreateVersion4();

        // Act
        var wrongDraft = scenario.Aggregate.PublishDraft(Uuid.CreateVersion4(), approval, 1, 0, entries, content,
            ["access-review"], Operator);
        var published = scenario.Aggregate.PublishDraft(draft, approval, 1, 0, entries, content, ["access-review"], Operator);
        var repeated = scenario.Aggregate.PublishDraft(draft, approval, 1, 0, entries, content, ["access-review"], Operator);
        var changed = scenario.Aggregate.PublishDraft(draft, Uuid.CreateVersion4(), 1, 0, entries, content, ["access-review"], Operator);

        // Assert
        Assert.False(wrongDraft.IsSuccess);
        Assert.True(published.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Equal(1, scenario.Aggregate.PublishedVersionNumber);
        Assert.Equal(content, scenario.Aggregate.PublishedEntriesContent);
        Assert.Null(scenario.Aggregate.OpenDraftId);
        var fact = Assert.Single(scenario.PendingEvents.OfType<MethodologyTemplateVersionPublishedV4>());
        Assert.Equal(draft, fact.DraftId);
        Assert.Equal(approval, fact.ApprovalEventId);
        Assert.Equal(["access-review"], fact.ChangedEntryKeys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldStartOnlyFromExactLatestSnapshotGivenPublishedVersion(bool wrongVersion)
    {
        // Arrange
        var scenario = Published(out _, out var entries, out var content);
        var count = scenario.PendingEvents.Count;

        // Act
        var stale = scenario.Aggregate.StartDraft(Uuid.CreateVersion4(), wrongVersion ? 0 : 1, entries,
            wrongVersion ? content : Content(entries), Operator);
        var next = Uuid.CreateVersion4();
        var started = scenario.Aggregate.StartDraft(next, 1, entries, content, Operator);

        // Assert
        Assert.False(stale.IsSuccess);
        Assert.True(started.IsSuccess);
        Assert.Equal(count + 1, scenario.PendingEvents.Count);
        Assert.Equal(next, scenario.Aggregate.OpenDraftId);
        Assert.Equal(1, Assert.IsType<MethodologyTemplateDraftStarted>(scenario.PendingEvents[^1]).BasedOnVersion);
    }

    [Fact]
    public void ShouldCloseOnlyMatchingDraftWithoutChangingPublishedVersionGivenDiscard()
    {
        // Arrange
        var scenario = Published(out _, out var entries, out var content);
        var draft = Uuid.CreateVersion4();
        Assert.True(scenario.Aggregate.StartDraft(draft, 1, entries, content, Operator).IsSuccess);
        var discard = Uuid.CreateVersion4();

        // Act
        var wrong = scenario.Aggregate.CloseDraft(Uuid.CreateVersion4(), discard, "Superseded");
        var closed = scenario.Aggregate.CloseDraft(draft, discard, "Superseded");
        var repeated = scenario.Aggregate.CloseDraft(draft, discard, "Superseded");
        var changed = scenario.Aggregate.CloseDraft(draft, discard, "Changed reason");

        // Assert
        Assert.False(wrong.IsSuccess);
        Assert.True(closed.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Equal(1, scenario.Aggregate.PublishedVersionNumber);
        Assert.Equal(content, scenario.Aggregate.PublishedEntriesContent);
        Assert.Null(scenario.Aggregate.OpenDraftId);
        Assert.Equal("idle", scenario.Aggregate.LineStatus);
        Assert.Single(scenario.PendingEvents.OfType<MethodologyTemplateDraftClosed>());
    }

    [Fact]
    public void ShouldRefuseRetirementWhileDraftOpenAndRefuseNewDraftAfterRetirementGivenLineLifecycle()
    {
        // Arrange
        var scenario = Started(out var draft);

        // Act
        var whileOpen = scenario.Aggregate.RetireLine(Operator, "Methodology superseded");
        Assert.True(scenario.Aggregate.CloseDraft(draft, Uuid.CreateVersion4(), "Not needed").IsSuccess);
        var blank = scenario.Aggregate.RetireLine(Operator, " ");
        var retired = scenario.Aggregate.RetireLine(Operator, "Methodology superseded");
        var repeated = scenario.Aggregate.RetireLine(Operator, "Methodology superseded");
        var started = scenario.Aggregate.StartDraft(Uuid.CreateVersion4(), 0, [], Content([]), Operator);

        // Assert
        Assert.False(whileOpen.IsSuccess);
        Assert.False(blank.IsSuccess);
        Assert.True(retired.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.False(started.IsSuccess);
        Assert.Equal("retired", scenario.Aggregate.LineStatus);
        Assert.Single(scenario.PendingEvents.OfType<MethodologyTemplateRetiredV4>());
    }

    [Fact]
    public void ShouldKeepOldPublicationRetryFactFreeGivenNextDraftAlreadyOpened()
    {
        // Arrange
        var scenario = Published(out var firstDraft, out var entries, out var content);
        var publication = Assert.Single(scenario.PendingEvents.OfType<MethodologyTemplateVersionPublishedV4>());
        var next = Uuid.CreateVersion4();
        Assert.True(scenario.Aggregate.StartDraft(next, 1, entries, content, Operator).IsSuccess);
        var count = scenario.PendingEvents.Count;

        // Act
        var replay = scenario.Aggregate.PublishDraft(firstDraft, publication.ApprovalEventId, 1, 0, entries, content,
            ["access-review"], Operator);

        // Assert
        Assert.True(replay.IsSuccess);
        Assert.Equal(count, scenario.PendingEvents.Count);
        Assert.Equal(next, scenario.Aggregate.OpenDraftId);
        Assert.Equal(1, scenario.Aggregate.PublishedVersionNumber);
    }

    [Fact]
    public void ShouldPreserveOpenDraftAndImmutableVersionGivenRetainedFacts()
    {
        // Arrange
        var scenario = Published(out _, out var entries, out var content);
        var next = Uuid.CreateVersion4();
        Assert.True(scenario.Aggregate.StartDraft(next, 1, entries, content, Operator).IsSuccess);

        // Act
        var replay = New().Given(scenario.PendingEvents.ToArray());
        var reused = replay.Aggregate.StartDraft(next, 1, entries, content, Operator);

        // Assert
        Assert.True(reused.IsSuccess);
        Assert.Empty(replay.PendingEvents);
        Assert.Equal(next, replay.Aggregate.OpenDraftId);
        Assert.Equal(content, replay.Aggregate.PublishedEntriesContent);
        Assert.Equal(1, replay.Aggregate.PublishedVersionNumber);
    }

    static AggregateScenario<MethodologyTemplate> New() => new(new MethodologyTemplate(Template));
    static AggregateScenario<MethodologyTemplate> Created()
    {
        var scenario = New();
        Assert.True(scenario.Aggregate.CreateLine("Firm controls", "Reusable firm methodology", Operator).IsSuccess);
        return scenario;
    }
    static AggregateScenario<MethodologyTemplate> Started(out Uuid draft)
    {
        var scenario = Created();
        draft = Uuid.CreateVersion4();
        Assert.True(scenario.Aggregate.StartDraft(draft, 0, [], Content([]), Operator).IsSuccess);
        return scenario;
    }
    static AggregateScenario<MethodologyTemplate> Published(out Uuid draft,
        out IReadOnlyList<MethodologyTemplateRecordContent> entries, out ImmutableContentRef content)
    {
        var scenario = Started(out draft);
        entries = [Entry()];
        content = Content(entries);
        Assert.True(scenario.Aggregate.PublishDraft(draft, Uuid.CreateVersion4(), 1, 0, entries, content,
            ["access-review"], Operator).IsSuccess);
        return scenario;
    }
    static MethodologyTemplateRecordContent Entry() => new("access-review", "control", "Review access", "Review quarterly.", []);
    static ImmutableContentRef Content(IReadOnlyList<MethodologyTemplateRecordContent> entries)
    {
        var info = ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(IReadOnlyList<MethodologyTemplateRecordContent>))!;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(entries, info);
        return new ImmutableContentRef(Uuid.CreateVersion4(), ContentDigest.Sha256(bytes), bytes.LongLength,
            "application/json", MethodologyTemplateDraft.EntriesSchema);
    }
}
