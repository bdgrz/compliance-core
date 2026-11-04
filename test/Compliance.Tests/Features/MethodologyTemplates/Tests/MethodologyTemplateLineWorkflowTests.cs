using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class MethodologyTemplateLineWorkflowTests
{
    [Fact]
    public async Task ShouldPublishExactApprovedRevisionOnceGivenRealLineAndSeparateDraft()
    {
        // Arrange
        await using var fixture = await Fixture.ApprovedAsync();
        var request = await fixture.PublicationAsync();

        // Act
        var published = await fixture.Publish.HandleAsync(System(request), CancellationToken.None);
        var repeated = await fixture.Publish.HandleAsync(System(request), CancellationToken.None);
        var owner = await fixture.LineAsync();
        var facts = await fixture.LineFactsAsync();

        // Assert
        Assert.True(published.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.Null(owner.OpenDraftId);
        Assert.Equal(1, owner.PublishedVersionNumber);
        var publication = Assert.Single(facts.OfType<MethodologyTemplateVersionPublishedV4>());
        Assert.Equal(fixture.Draft, publication.DraftId);
        Assert.Equal(request.Source.EventId, publication.ApprovalEventId);
        Assert.Equal(fixture.Approver, publication.ApprovedBy);
        Assert.Equal(["access-review"], publication.ChangedEntryKeys);
        var entries = await fixture.Scenario.Content.ReadJsonAsync<IReadOnlyList<MethodologyTemplateRecordContent>>(
            MethodologyTemplate.Realm, owner.PublishedEntriesContent!, CancellationToken.None);
        Assert.Equal("Quarterly access review", Assert.Single(entries).Title);
    }

    [Fact]
    public async Task ShouldStartNextDraftFromPublishedSnapshotGivenCurrentLineVersion()
    {
        // Arrange
        await using var fixture = await Fixture.ApprovedAsync();
        Assert.True((await fixture.Publish.HandleAsync(System(await fixture.PublicationAsync()), CancellationToken.None)).IsSuccess);
        var original = (await fixture.LineAsync()).PublishedEntriesContent;
        var next = Uuid.CreateVersion4();
        var request = new StartMethodologyTemplateDraft(Uuid.CreateVersion4(), fixture.Template, next);

        // Act
        var started = await fixture.Start.HandleAsync(MethodologyTemplateScenario.Context(request, fixture.Author), CancellationToken.None);
        var repeated = await fixture.Start.HandleAsync(MethodologyTemplateScenario.Context(request, fixture.Author), CancellationToken.None);
        var owner = await fixture.LineAsync();
        var facts = await fixture.LineFactsAsync();

        // Assert
        Assert.True(started.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.Equal(next, owner.OpenDraftId);
        var fact = Assert.Single(facts.OfType<MethodologyTemplateDraftStarted>().Where(value => value.DraftId == next));
        Assert.Equal(1, fact.BasedOnVersion);
        Assert.Equal(original, fact.EntriesContent);
    }

    [Theory]
    [InlineData("template")]
    [InlineData("draft")]
    [InlineData("stream")]
    [InlineData("system")]
    public async Task ShouldRefuseWithoutPublicationGivenForgedApprovalTargetOrPersonalCaller(string fault)
    {
        // Arrange
        await using var fixture = await Fixture.ApprovedAsync();
        var request = await fixture.PublicationAsync();
        request = fault switch
        {
            "template" => request with { TemplateId = Uuid.CreateVersion4() },
            "draft" => request with { DraftId = Uuid.CreateVersion4() },
            "stream" => request with { Source = request.Source with { Stream = new MethodologyTemplate(fixture.Template).Stream } },
            _ => request,
        };
        var before = (await fixture.LineAsync()).Version;
        var context = fault == "system" ? MethodologyTemplateScenario.Context(request, fixture.Author) : System(request);

        // Act
        var refused = await fixture.Publish.HandleAsync(context, CancellationToken.None);
        var owner = await fixture.LineAsync();

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.Equal(before, owner.Version);
        Assert.Equal(0, owner.PublishedVersionNumber);
        Assert.Equal(fixture.Draft, owner.OpenDraftId);
    }

    [Fact]
    public async Task ShouldCloseOpenDraftWithoutPublicationGivenCommittedDiscard()
    {
        // Arrange
        await using var fixture = await Fixture.StartedAsync();
        var draft = await fixture.DraftAsync();
        Assert.True(draft.Discard(fixture.Author, "Use a different methodology").IsSuccess);
        await fixture.Scenario.Writer.SaveAsync(draft, new RequestDispatchContext(RequestActor.System));
        var (discarded, position) = await fixture.DraftFactAsync<MethodologyTemplateDraftDiscarded>();
        var request = new CloseMethodologyTemplateDraft(fixture.Template, fixture.Draft,
            NativeFactReference.Of(draft.Stream, discarded), position,
            Uuid.CreateVersion5(discarded.Metadata.EventId, nameof(CloseMethodologyTemplateDraft)));

        // Act
        var closed = await fixture.Close.HandleAsync(System(request), CancellationToken.None);
        var repeated = await fixture.Close.HandleAsync(System(request), CancellationToken.None);
        var owner = await fixture.LineAsync();
        var facts = await fixture.LineFactsAsync();

        // Assert
        Assert.True(closed.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.Null(owner.OpenDraftId);
        Assert.Equal(0, owner.PublishedVersionNumber);
        Assert.Equal("Use a different methodology", Assert.Single(facts.OfType<MethodologyTemplateDraftClosed>()).Reason);
        Assert.Empty(facts.OfType<MethodologyTemplateVersionPublishedV4>());
    }

    static RequestContext<T> System<T>(T request) where T : IRequestBase => new(request, RequestActor.System);

    sealed class Fixture : IAsyncDisposable
    {
        public MethodologyTemplateScenario Scenario { get; } = new(DateTimeOffset.UnixEpoch);
        public Uuid Template { get; } = Uuid.CreateVersion4();
        public Uuid Draft { get; } = Uuid.CreateVersion4();
        public Uuid Author { get; } = Uuid.CreateVersion4();
        public Uuid Reviewer { get; } = Uuid.CreateVersion4();
        public Uuid Approver { get; } = Uuid.CreateVersion4();
        EventStoreSourceFactResolver Sources => new(Scenario.Events);
        public StartMethodologyTemplateDraftHandler Start => new(Scenario.Executor, Scenario.Reader, Sources, Scenario.Content);
        public PublishMethodologyTemplateVersionHandler Publish => new(Scenario.Executor, Scenario.Reader, Sources, Scenario.Content);
        public CloseMethodologyTemplateDraftHandler Close => new(Scenario.Executor, Scenario.Reader, Sources, Scenario.Content);

        public static async Task<Fixture> StartedAsync()
        {
            var fixture = new Fixture();
            var line = new MethodologyTemplate(fixture.Template);
            Assert.True(line.CreateLine("Firm methodology", "Original reusable firm entries", fixture.Author).IsSuccess);
            await fixture.Scenario.Writer.SaveAsync(line, new RequestDispatchContext(RequestActor.System));
            var start = new StartMethodologyTemplateDraft(Uuid.CreateVersion4(), fixture.Template, fixture.Draft);
            Assert.True((await fixture.Start.HandleAsync(MethodologyTemplateScenario.Context(start, fixture.Author), CancellationToken.None)).IsSuccess);
            MethodologyTemplateDraftStarted? started = null;
            ulong position = 0;
            await foreach (var record in fixture.Scenario.Events.ReadAsync(line.Stream, 0))
                if (record.Event is MethodologyTemplateDraftStarted fact)
                    (started, position) = (fact, record.ResourceOffset);
            Assert.NotNull(started);
            var open = new OpenMethodologyTemplateDraft(fixture.Template, fixture.Draft,
                NativeFactReference.Of(line.Stream, started), position,
                Uuid.CreateVersion5(started.Metadata.EventId, nameof(OpenMethodologyTemplateDraft)));
            Assert.True((await new OpenMethodologyTemplateDraftHandler(fixture.Scenario.Executor, fixture.Sources,
                fixture.Scenario.Content).HandleAsync(System(open), CancellationToken.None)).IsSuccess);
            return fixture;
        }

        public static async Task<Fixture> ApprovedAsync()
        {
            var fixture = await StartedAsync();
            var draft = await fixture.DraftAsync();
            IReadOnlyList<MethodologyTemplateRecordContent> entries =
                [new("access-review", "control", "Quarterly access review", "Review access quarterly.", [])];
            var content = await fixture.Scenario.Content.WriteJsonAsync(MethodologyTemplate.Realm, Uuid.CreateVersion4(),
                MethodologyTemplateDraft.EntriesSchema, entries, CancellationToken.None);
            Assert.True(draft.Revise(0, entries, content, fixture.Author).IsSuccess);
            Assert.True(draft.Review(1, fixture.Reviewer, true, "Reviewed").IsSuccess);
            Assert.True(draft.Approve(1, fixture.Approver).IsSuccess);
            await fixture.Scenario.Writer.SaveAsync(draft, new RequestDispatchContext(RequestActor.System));
            return fixture;
        }

        public async Task<PublishMethodologyTemplateVersion> PublicationAsync()
        {
            var (approved, position) = await DraftFactAsync<MethodologyTemplateDraftApproved>();
            return new PublishMethodologyTemplateVersion(Template, Draft,
                NativeFactReference.Of(new MethodologyTemplateDraft(Template, Draft).Stream, approved), position,
                Uuid.CreateVersion5(approved.Metadata.EventId, nameof(PublishMethodologyTemplateVersion)));
        }
        public ValueTask<MethodologyTemplate> LineAsync() => Scenario.Reader.HydrateAsync(new MethodologyTemplate(Template));
        public ValueTask<MethodologyTemplateDraft> DraftAsync() => Scenario.Reader.HydrateAsync(new MethodologyTemplateDraft(Template, Draft));
        public async Task<IReadOnlyList<DomainEvent>> LineFactsAsync()
        {
            var facts = new List<DomainEvent>();
            await foreach (var record in Scenario.Events.ReadAsync(new MethodologyTemplate(Template).Stream, 0))
                facts.Add(record.Event);
            return facts;
        }
        public async Task<(T Fact, ulong Position)> DraftFactAsync<T>() where T : DomainEvent
        {
            await foreach (var record in Scenario.Events.ReadAsync(new MethodologyTemplateDraft(Template, Draft).Stream, 0))
                if (record.Event is T fact)
                    return (fact, record.ResourceOffset);
            throw new InvalidOperationException("The fixture's committed terminal draft fact is missing.");
        }
        public ValueTask DisposeAsync() => Scenario.DisposeAsync();
    }
}
