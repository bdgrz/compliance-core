using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Reactors;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class MethodologyTemplateDraftOpeningTests
{
    [Fact]
    public async Task ShouldOpenExactImmutableDraftOnceGivenCommittedLineStart()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();

        // Act
        var opened = await fixture.Handler.HandleAsync(System(fixture.Request), CancellationToken.None);
        var repeated = await fixture.Handler.HandleAsync(System(fixture.Request), CancellationToken.None);
        var owner = await fixture.OwnerAsync();
        var facts = new List<DomainEvent>();
        await foreach (var record in fixture.Scenario.Events.ReadAsync(owner.Stream, 0, CancellationToken.None))
            facts.Add(record.Event);

        // Assert
        Assert.True(opened.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.Equal("drafting", owner.Status);
        Assert.Equal(0, owner.Revision);
        Assert.Equal(fixture.Started.EntriesContent, owner.EntriesContent);
        var fact = Assert.IsType<MethodologyTemplateDraftOpened>(Assert.Single(facts));
        Assert.Equal(fixture.Started.Metadata.EventId, fact.StartEventId);
        Assert.Equal(1, fact.BasedOnVersion);
        Assert.Equal(["access-review"], fact.EntryKeys);
    }

    [Theory]
    [InlineData("template", false)]
    [InlineData("draft", false)]
    [InlineData("realm", false)]
    [InlineData("source", true)]
    [InlineData("position", true)]
    public async Task ShouldRefuseWithoutOpeningGivenForgedStartBinding(string fault, bool proofFailure)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var request = fault switch
        {
            "template" => fixture.Request with { TemplateId = Uuid.CreateVersion4() },
            "draft" => fixture.Request with { DraftId = Uuid.CreateVersion4() },
            "realm" => fixture.Request with
            {
                Source = fixture.Request.Source with
                {
                    Stream = new EventStreamAddress("another-realm", MethodologyTemplate.Area, fixture.Template.ToString()),
                },
            },
            "source" => fixture.Request with { Source = fixture.Request.Source with { EventId = Uuid.CreateVersion4() } },
            "position" => fixture.Request with { SourcePosition = fixture.Request.SourcePosition + 100 },
            _ => throw new ArgumentOutOfRangeException(nameof(fault)),
        };
        request = request with { CommandId = Uuid.CreateVersion5(request.Source.EventId, nameof(OpenMethodologyTemplateDraft)) };

        // Act
        if (proofFailure)
            await Assert.ThrowsAnyAsync<SourceFactVerificationException>(() =>
                fixture.Handler.HandleAsync(System(request), CancellationToken.None).AsTask());
        else
        {
            var refused = await fixture.Handler.HandleAsync(System(request), CancellationToken.None);
            Assert.False(refused.IsSuccess);
        }
        var owner = await fixture.OwnerAsync();
        var requested = await fixture.Scenario.Reader.HydrateAsync(new MethodologyTemplateDraft(request.TemplateId, request.DraftId));

        // Assert
        Assert.Equal("unopened", owner.Status);
        Assert.Equal("unopened", requested.Status);
        Assert.Equal(0UL, owner.Version);
        Assert.Equal(0UL, requested.Version);
    }

    [Fact]
    public async Task ShouldRefuseWithoutOpeningGivenPersonalCaller()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();

        // Act
        var result = await fixture.Handler.HandleAsync(MethodologyTemplateScenario.Context(fixture.Request,
            Uuid.CreateVersion4()), CancellationToken.None);
        var owner = await fixture.OwnerAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
        Assert.Equal("unopened", owner.Status);
        Assert.Equal(0UL, owner.Version);
    }

    [Fact]
    public async Task ShouldKeepCheckpointAndRepeatSameCommandGivenOpeningRefusedThenReplayed()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var failure = new RequestError(RequestErrorKind.Conflict, "Opening source is pending.", true);
        var scenario = new ReactorScenario().Given(fixture.Started)
            .RespondTo<OpenMethodologyTemplateDraft>(Result.Failure(failure));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new MethodologyTemplateDraftOpeningReactor(checkpoints, scenario.Requests);
        var identity = new CheckpointIdentity(reactor.Name, reactor.Pattern);

        // Act
        var rejected = await Assert.ThrowsAsync<ProcessorFailureException>(() => scenario.RunAsync(reactor));
        var held = await checkpoints.LoadAsync(identity);
        var first = Assert.Single(scenario.SentRequests.OfType<OpenMethodologyTemplateDraft>());
        scenario.RespondTo<OpenMethodologyTemplateDraft>(Result.Success);
        await scenario.RunAsync(reactor);
        var sent = scenario.SentRequests.OfType<OpenMethodologyTemplateDraft>().ToArray();
        var completed = await checkpoints.LoadAsync(identity);

        // Assert
        Assert.Same(failure, rejected.Error);
        Assert.Equal(ProjectionCheckpoint.Start, held);
        Assert.NotEqual(ProjectionCheckpoint.Start, completed);
        Assert.Equal(2, sent.Length);
        Assert.Equal(first, sent[1]);
        Assert.Equal(Uuid.CreateVersion5(fixture.Started.Metadata.EventId, nameof(OpenMethodologyTemplateDraft)), first.CommandId);
        Assert.Equal(fixture.Template, first.TemplateId);
        Assert.Equal(fixture.Draft, first.DraftId);
    }

    static RequestContext<T> System<T>(T request) where T : IRequestBase => new(request, RequestActor.System);

    sealed class Fixture : IAsyncDisposable
    {
        public MethodologyTemplateScenario Scenario { get; } = new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));
        public Uuid Template { get; } = Uuid.CreateVersion4();
        public Uuid Draft { get; } = Uuid.CreateVersion4();
        public MethodologyTemplateDraftStarted Started { get; private set; } = null!;
        public OpenMethodologyTemplateDraft Request => new(Template, Draft,
            NativeFactReference.Of(new MethodologyTemplate(Template).Stream, Started), 0,
            Uuid.CreateVersion5(Started.Metadata.EventId, nameof(OpenMethodologyTemplateDraft)));
        public OpenMethodologyTemplateDraftHandler Handler => new(Scenario.Executor,
            new EventStoreSourceFactResolver(Scenario.Events), Scenario.Content);

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            IReadOnlyList<MethodologyTemplateRecordContent> entries =
                [new("access-review", "control", "Review access", "Review access quarterly.", [])];
            var content = await fixture.Scenario.Content.WriteJsonAsync(MethodologyTemplate.Realm,
                Uuid.CreateVersion5(fixture.Draft, MethodologyTemplateDraft.EntriesSchema),
                MethodologyTemplateDraft.EntriesSchema, entries, CancellationToken.None);
            fixture.Started = new MethodologyTemplateDraftStarted(fixture.Template, fixture.Draft, 1, content,
                Uuid.CreateVersion4());
            fixture.Started.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), fixture.Template, 1,
                fixture.Scenario.Clock.Now));
            await fixture.Scenario.Events.AppendAsync(new MethodologyTemplate(fixture.Template).Stream, 0,
                [fixture.Started], CancellationToken.None);
            return fixture;
        }

        public ValueTask<MethodologyTemplateDraft> OwnerAsync() => Scenario.Reader.HydrateAsync(
            new MethodologyTemplateDraft(Template, Draft));
        public ValueTask DisposeAsync() => Scenario.DisposeAsync();
    }
}
