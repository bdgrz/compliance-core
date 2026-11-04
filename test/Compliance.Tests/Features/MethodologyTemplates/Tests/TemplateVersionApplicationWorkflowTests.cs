using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Reactors;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class TemplateVersionApplicationWorkflowTests
{
    [Fact]
    public async Task ShouldOpenOnceAndCompleteOwningContextOutcomeGivenCommittedApplication()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.OpenRequest;
        var handler = fixture.OpenHandler;

        // Act
        var opened = await handler.HandleAsync(System(request), CancellationToken.None);
        var repeated = await handler.HandleAsync(System(request), CancellationToken.None);
        var beforeOutcome = await fixture.OwnerAsync();
        var outcome = new TemplateEntryOutcome("cc6-1-access-review", "created", Uuid.CreateVersion4(), null);
        var receipt = fixture.Receipt(outcome);
        var recorded = await fixture.RecordHandler.HandleAsync(System(receipt), CancellationToken.None);
        var repeatedOutcome = await fixture.RecordHandler.HandleAsync(System(receipt), CancellationToken.None);
        var changedOutcome = await fixture.RecordHandler.HandleAsync(System(receipt with
        {
            Outcome = outcome with { RecordId = Uuid.CreateVersion4() },
        }), CancellationToken.None);
        var after = await fixture.OwnerAsync();
        var facts = await fixture.FactsAsync();

        // Assert
        Assert.True(opened.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.True(beforeOutcome.IsOpened);
        Assert.False(beforeOutcome.IsCompleted);
        Assert.True(recorded.IsSuccess);
        Assert.True(repeatedOutcome.IsSuccess);
        Assert.False(changedOutcome.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changedOutcome.Error.Kind);
        Assert.True(after.IsCompleted);
        Assert.Equal(new TemplateApplicationOutcomeCounts(1, 0, 0), after.Counts);
        Assert.Collection(facts,
            fact => Assert.IsType<TemplateVersionApplicationOpened>(fact),
            fact => Assert.Equal(outcome, Assert.IsType<TemplateEntryOutcomeRecorded>(fact).Outcome),
            fact => Assert.Equal(after.Counts, Assert.IsType<TemplateVersionApplicationCompleted>(fact).Counts));
    }

    [Theory]
    [InlineData("tenant", true)]
    [InlineData("program", false)]
    [InlineData("template", false)]
    [InlineData("version", false)]
    [InlineData("source", true)]
    [InlineData("position", true)]
    public async Task ShouldRefuseWithoutOpeningGivenForgedApplicationBinding(string fault, bool proofFailure)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var request = fault switch
        {
            "tenant" => fixture.OpenRequest with { TenantId = Uuid.CreateVersion4() },
            "program" => fixture.OpenRequest with { ProgramId = Uuid.CreateVersion4() },
            "template" => fixture.OpenRequest with { TemplateId = Uuid.CreateVersion4() },
            "version" => fixture.OpenRequest with { TemplateVersion = 2 },
            "source" => fixture.OpenRequest with
            {
                Source = fixture.OpenRequest.Source with { EventId = Uuid.CreateVersion4() },
            },
            "position" => fixture.OpenRequest with { SourcePosition = fixture.OpenRequest.SourcePosition + 100 },
            _ => throw new ArgumentOutOfRangeException(nameof(fault)),
        };
        request = request with { CommandId = Uuid.CreateVersion5(request.Source.EventId, nameof(OpenTemplateVersionApplication)) };

        // Act
        if (proofFailure)
            await Assert.ThrowsAnyAsync<SourceFactVerificationException>(() =>
                fixture.OpenHandler.HandleAsync(System(request), CancellationToken.None).AsTask());
        else
        {
            var refusal = await fixture.OpenHandler.HandleAsync(System(request), CancellationToken.None);
            Assert.False(refusal.IsSuccess);
            Assert.Equal(RequestErrorKind.Validation, refusal.Error.Kind);
        }
        var original = await fixture.OwnerAsync();
        var requested = await fixture.Scenario.Reader.HydrateAsync(new TemplateVersionApplication(
            request.TenantId, request.ProgramId, request.TemplateId, request.TemplateVersion));

        // Assert
        Assert.False(original.IsOpened);
        Assert.False(requested.IsOpened);
        Assert.Equal(0UL, original.Version);
        Assert.Equal(0UL, requested.Version);
    }

    [Fact]
    public async Task ShouldKeepOutcomeRetryableAndRejectPersonalActorsGivenVersionNotOpened()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var outcome = new TemplateEntryOutcome("cc6-1-access-review", "refused", null, "identifier_in_use");
        var receipt = fixture.Receipt(outcome);

        // Act
        var pending = await fixture.RecordHandler.HandleAsync(System(receipt), CancellationToken.None);
        var personalOutcome = await fixture.RecordHandler.HandleAsync(
            MethodologyTemplateScenario.Context(receipt, Uuid.CreateVersion4()), CancellationToken.None);
        var personalOpen = await fixture.OpenHandler.HandleAsync(
            MethodologyTemplateScenario.Context(fixture.OpenRequest, Uuid.CreateVersion4()), CancellationToken.None);
        var owner = await fixture.OwnerAsync();

        // Assert
        Assert.False(pending.IsSuccess);
        Assert.True(pending.Error.IsTransient);
        Assert.False(personalOutcome.IsSuccess);
        Assert.False(personalOpen.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, personalOutcome.Error.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, personalOpen.Error.Kind);
        Assert.False(owner.IsOpened);
        Assert.Equal(0UL, owner.Version);
    }

    [Fact]
    public async Task ShouldKeepCheckpointAndStableCommandGivenOpeningSendRefusedThenReplayed()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var source = Assert.IsType<MethodologyTemplateApplied>(fixture.Source.Event);
        var error = new RequestError(RequestErrorKind.Conflict, "Opening is not visible yet.", isTransient: true);
        var scenario = new ReactorScenario(new TenantId(fixture.Tenant.ToString())).Given(source)
            .RespondTo<OpenTemplateVersionApplication>(Result.Failure(error));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new TemplateVersionApplicationOpeningReactor(checkpoints, scenario.Requests);
        var identity = new CheckpointIdentity(reactor.Name, EventStreamPattern.ForPattern(
            fixture.Tenant.ToString(), TemplateApplication.Area));

        // Act
        var failure = await Assert.ThrowsAsync<ProcessorFailureException>(() => scenario.RunAsync(reactor));
        var held = await checkpoints.LoadAsync(identity);
        var first = Assert.Single(scenario.SentRequests.OfType<OpenTemplateVersionApplication>());
        scenario.RespondTo<OpenTemplateVersionApplication>(Result.Success);
        await scenario.RunAsync(reactor);
        var sent = scenario.SentRequests.OfType<OpenTemplateVersionApplication>().ToArray();
        var completed = await checkpoints.LoadAsync(identity);

        // Assert
        Assert.Same(error, failure.Error);
        Assert.Equal(ProjectionCheckpoint.Start, held);
        Assert.NotEqual(ProjectionCheckpoint.Start, completed);
        Assert.Equal(2, sent.Length);
        Assert.Equal(first, sent[1]);
        Assert.Equal(Uuid.CreateVersion5(source.Metadata.EventId, nameof(OpenTemplateVersionApplication)), first.CommandId);
        Assert.Equal(fixture.Tenant, first.TenantId);
        Assert.Equal(fixture.Program, first.ProgramId);
        Assert.Equal(fixture.Template, first.TemplateId);
        Assert.Equal(1, first.TemplateVersion);
    }

    static RequestContext<T> System<T>(T request) where T : IRequestBase => new(request, RequestActor.System);

    sealed class Fixture : IAsyncDisposable
    {
        const string ApplicationKey = "methodology-outcomes";
        static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
        public MethodologyTemplateScenario Scenario { get; } = new(Now);
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Template { get; } = Uuid.CreateVersion4();
        public DomainEventRecord Source { get; private set; } = null!;
        public OpenTemplateVersionApplication OpenRequest => new(Tenant, Program, Template, 1,
            NativeFactReference.Of(Source.Stream, Source.Event), Source.ResourceOffset,
            Uuid.CreateVersion5(Source.Event.Metadata.EventId, nameof(OpenTemplateVersionApplication)));
        public OpenTemplateVersionApplicationHandler OpenHandler => new(Scenario.Executor,
            new EventStoreSourceFactResolver(Scenario.Events), Scenario.Content);
        public RecordTemplateEntryOutcomeHandler RecordHandler => new(Scenario.Executor);

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            var author = Uuid.CreateVersion4();
            var reviewer = Uuid.CreateVersion4();
            var approver = Uuid.CreateVersion4();
            var program = new ProgramRules(fixture.Tenant, fixture.Program);
            Assert.Null(program.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null),
                Uuid.CreateVersion4(), "Lead", Now));
            await ProgramSeeds.WriteAsync(fixture.Scenario.Reader, fixture.Scenario.Writer,
                fixture.Scenario.Events, fixture.Scenario.Content, program);
            Assert.True((await fixture.Scenario.CreateAsync(new CreateMethodologyTemplate(fixture.Template,
                MethodologyTemplateTests.Content("Review access quarterly."), "Initial"), author)).IsSuccess);
            Assert.True((await fixture.Scenario.ReviewAsync(new ReviewMethodologyTemplate(Uuid.CreateVersion4(),
                fixture.Template, 1, "approved", "Reviewed"), reviewer)).IsSuccess);
            Assert.True((await fixture.Scenario.PublishAsync(new ApproveMethodologyTemplateVersion(Uuid.CreateVersion4(),
                fixture.Template, 1, "Approved"), approver)).IsSuccess);
            var application = TemplateApplicationId.For(fixture.Program, fixture.Template, ApplicationKey);
            var request = new ApplyMethodologyTemplate(fixture.Tenant, fixture.Program, fixture.Template, 1, ApplicationKey);
            Assert.True((await new ApplyMethodologyTemplateHandler(fixture.Scenario.ApplicationOperations(),
                fixture.Scenario.Views).HandleAsync(MethodologyTemplateScenario.Context(request, Uuid.CreateVersion4()),
                CancellationToken.None)).IsSuccess);
            await foreach (var record in fixture.Scenario.Events.ReadAsync(new EventStreamAddress(fixture.Tenant.ToString(),
                               TemplateApplication.Area, application.ToString()), 0, CancellationToken.None))
                if (record.Event is MethodologyTemplateApplied)
                    fixture.Source = record;
            Assert.NotNull(fixture.Source);
            return fixture;
        }

        public RecordTemplateEntryOutcome Receipt(TemplateEntryOutcome outcome) => new(Tenant, Program, Template, 1,
            Source.Event.Metadata.EventId, outcome,
            Uuid.CreateVersion5(Source.Event.Metadata.EventId, "template-entry-outcome:" + outcome.EntryKey));

        public ValueTask<TemplateVersionApplication> OwnerAsync() => Scenario.Reader.HydrateAsync(
            new TemplateVersionApplication(Tenant, Program, Template, 1));

        public async Task<IReadOnlyList<DomainEvent>> FactsAsync()
        {
            var facts = new List<DomainEvent>();
            var owner = new TemplateVersionApplication(Tenant, Program, Template, 1);
            await foreach (var record in Scenario.Events.ReadAsync(owner.Stream, 0, CancellationToken.None))
                facts.Add(record.Event);
            return facts;
        }

        public ValueTask DisposeAsync() => Scenario.DisposeAsync();
    }
}
