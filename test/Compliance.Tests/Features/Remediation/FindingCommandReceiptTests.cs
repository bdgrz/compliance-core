using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Remediation;

public sealed class FindingCommandReceiptTests
{
    [Theory]
    [InlineData("raise")]
    [InlineData("complete")]
    [InlineData("close")]
    public void ShouldRejectNullCollectionWithoutAppendGivenRecordedRequestRetry(string operation)
    {
        // Arrange
        var fixture = new Fixture(operation);
        var requestId = Uuid.CreateVersion4();
        var first = fixture.Root(requestId);
        Assert.Null(fixture.Execute(first, requestId));
        fixture.Events.AddRange(new AggregateScenario<FindingAggregate>(first).PendingEvents);
        fixture.AppendLaterFact();
        var retry = fixture.Root(requestId);

        // Act
        var failure = operation switch
        {
            "raise" => retry.Raise(requestId, new FindingSource("manual", null, null, "Exact source wording"),
                "Gap", "Description", "medium", "Scope", fixture.Owner, fixture.Due, null!, fixture.Actor, Fixture.Now),
            "complete" => retry.CompleteAction(requestId, 2, retry.CorrectiveActions[0].ActionId,
                "Repair", null!, Uuid.Parse(fixture.Actor.Id, null), fixture.Actor, Fixture.Now),
            "close" => retry.Close(requestId, 3, requestId, "Verified", null!, "Repair",
                Uuid.Parse(fixture.Actor.Id, null), fixture.Actor, Fixture.Now, null),
            _ => throw new InvalidOperationException(),
        };

        // Assert
        Assert.NotNull(failure);
        Assert.Empty(new AggregateScenario<FindingAggregate>(retry).PendingEvents);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("triage")]
    [InlineData("link")]
    [InlineData("request")]
    [InlineData("time")]
    [InlineData("payload")]
    public void ShouldRejectMalformedNativePayloadGivenReplay(string mutation)
    {
        // Arrange
        var fixture = new Fixture("raise");
        var requestId = Uuid.CreateVersion4();
        var root = fixture.Root(requestId);
        Assert.Null(fixture.Execute(root, requestId));
        var original = Assert.IsType<FindingRaisedV2>(Assert.Single(new AggregateScenario<FindingAggregate>(root).PendingEvents));
        var malformed = mutation switch
        {
            "source" => original with { Source = original.Source with { SourceText = "" } },
            "triage" => original with { Severity = "unknown" },
            "link" => original with { Links = [new FindingLink("unknown", "REF")] },
            "request" => original with { RequestId = Uuid.Empty },
            "time" => original with { RaisedAt = Fixture.Now.ToOffset(TimeSpan.FromHours(1)) },
            "payload" => original with
            {
                Source = original.Source with { SourceText = new string('é', 8000) },
                Description = new string('x', 8000),
                Links = Enumerable.Range(0, 50).Select(static _ => new FindingLink("control", new string('x', 500))).ToArray(),
            },
            _ => throw new InvalidOperationException(),
        };
        malformed.AttachMetadata(original.Metadata);

        // Act
        var replay = () => new AggregateScenario<FindingAggregate>(new FindingAggregate(
            fixture.Tenant, fixture.Program, fixture.Finding)).Given(malformed);

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    [Fact]
    public void ShouldRejectExistingActionIdentityWithoutAppendGivenDifferentRequest()
    {
        // Arrange
        var fixture = new Fixture("complete");
        var requestId = Uuid.CreateVersion4();
        var root = fixture.Root(requestId);

        // Act
        var failure = root.AddAction(requestId, 2, root.CorrectiveActions[0].ActionId, "Changed",
            fixture.Owner, fixture.Due, fixture.Actor, Fixture.Now);

        // Assert
        Assert.NotNull(failure);
        Assert.Empty(new AggregateScenario<FindingAggregate>(root).PendingEvents);
    }

    [Fact]
    public void ShouldRejectNativeMutationOfClosedFindingGivenMalformedReplay()
    {
        // Arrange
        var fixture = new Fixture("reopen");
        var requestId = Uuid.CreateVersion4();
        var root = fixture.Root(requestId);
        var fact = DomainEventSeed.Attach(new FindingRevisedV2(requestId, fixture.Tenant,
            fixture.Program, fixture.Finding, 5, "low", fixture.Owner, fixture.Due, "Changed",
            null, "Changed", fixture.Actor, Fixture.Now), root.Id, 5);

        // Act
        var replay = () => new AggregateScenario<FindingAggregate>(root).Given(fact);

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    [Fact]
    public void ShouldFreezeAcceptedInputAndReceiptGivenMutableSourceLinks()
    {
        // Arrange
        var fixture = new Fixture("raise");
        var requestId = Uuid.CreateVersion4();
        var links = new List<FindingLink> { new("control", "ORIGINAL") };
        var fact = DomainEventSeed.Attach(new FindingRaisedV2(requestId, fixture.Tenant,
            fixture.Program, fixture.Finding, 1, new FindingSource("manual", null, null, "Source"),
            "Gap", "Description", "medium", "All", fixture.Owner, fixture.Due, links,
            fixture.Actor, Fixture.Now), FindingAggregate.StreamId(fixture.Program, fixture.Finding), 1);
        var root = new AggregateScenario<FindingAggregate>(fixture.Root(requestId)).Given(fact).Aggregate;
        var before = JsonSerializer.Serialize(fact, ComplianceCoreJsonContext.Default.FindingRaisedV2);

        // Act
        links[0] = new FindingLink("control", "CHANGED");
        var view = root.Read(new FindingHistoryFold(root.Version, [], [], []), Fixture.Now)!;
        var exposed = Assert.IsAssignableFrom<IList<FindingLink>>(view.Links);
        var exact = root.Raise(requestId, fact.Source, fact.Title, fact.Description, fact.Severity,
            fact.AffectedScope, fact.OwnerMemberId, fact.DueOn, [new FindingLink("control", "ORIGINAL")],
            fixture.Actor, Fixture.Now.AddDays(1));

        // Assert
        Assert.Equal("ORIGINAL", Assert.Single(view.Links).Reference);
        Assert.Throws<NotSupportedException>(() => exposed[0] = new FindingLink("control", "CHANGED"));
        Assert.Equal(before, JsonSerializer.Serialize(fact, ComplianceCoreJsonContext.Default.FindingRaisedV2));
        Assert.Null(exact);
        Assert.Empty(new AggregateScenario<FindingAggregate>(root).PendingEvents);
    }

    [Theory]
    [InlineData("kind")]
    [InlineData("id")]
    [InlineData("display")]
    public void ShouldReturnValidationFailureWithoutAppendGivenInvalidNativeActor(string mutation)
    {
        // Arrange
        var fixture = new Fixture("raise");
        var requestId = Uuid.CreateVersion4();
        var root = fixture.Root(requestId);

        // Act
        var actor = mutation switch
        {
            "kind" => fixture.Actor with { Kind = "invalid" },
            "id" => fixture.Actor with { Id = "" },
            _ => fixture.Actor with { Display = " " },
        };
        var failure = fixture.Execute(root, requestId, actor: actor);

        // Assert
        Assert.NotNull(failure);
        Assert.Empty(new AggregateScenario<FindingAggregate>(root).PendingEvents);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("close")]
    public void ShouldRejectMismatchedMemberAttributionGivenNativeReplay(string operation)
    {
        // Arrange
        var fixture = new Fixture(operation);
        var requestId = Uuid.CreateVersion4();
        var root = fixture.Root(requestId);
        Assert.Null(fixture.Execute(root, requestId));
        var fact = Assert.Single(new AggregateScenario<FindingAggregate>(root).PendingEvents);
        DomainEvent malformed = fact switch
        {
            CorrectiveActionCompletedV2 x => x with { CompletedByMemberId = Uuid.CreateVersion4() },
            FindingClosedV2 x => x with { Closure = x.Closure with { CloserMemberId = Uuid.CreateVersion4() } },
            _ => throw new InvalidOperationException(),
        };
        malformed.AttachMetadata(fact.Metadata);

        // Act
        var replay = () => new AggregateScenario<FindingAggregate>(fixture.Root(Uuid.CreateVersion4())).Given(malformed);

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    [Fact]
    public async Task ShouldRejectTruncatedRoutedPairGivenHistoryFold()
    {
        // Arrange
        var fixture = new Fixture("raise");
        var requestId = Uuid.CreateVersion4();
        var root = fixture.Root(requestId);
        Assert.Null(root.RaiseRouted(requestId, new FindingSource("manual", null, null, "Source"),
            "Gap", "All", fixture.Owner, fixture.Due, [], "Repair", fixture.Actor, Fixture.Now));
        var store = new InMemoryEventStore();
        await store.AppendAsync(root.Stream, 0, [new AggregateScenario<FindingAggregate>(root).PendingEvents[0]]);

        // Act
        var read = () => FindingHistoryFold.ReadAsync(store, fixture.Tenant, fixture.Program,
            fixture.Finding, CancellationToken.None).AsTask();

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(read);
    }

    [Fact]
    public void ShouldValidateRoutedPairGivenUnselectedReplayAndTruncation()
    {
        // Arrange
        var fixture = new Fixture("raise");
        var requestId = Uuid.CreateVersion4();
        var root = fixture.Root(requestId);
        var source = new FindingSource("evaluation_deviation", Uuid.CreateVersion4(), "round1", "Exact source");
        Assert.Null(root.RaiseRouted(requestId, source, "Gap", "Scope", fixture.Owner, fixture.Due,
            [], "Repair", fixture.Actor, Fixture.Now));
        var facts = new AggregateScenario<FindingAggregate>(root).PendingEvents.ToArray();

        // Act
        var replay = new AggregateScenario<FindingAggregate>(new FindingAggregate(fixture.Tenant,
            fixture.Program, fixture.Finding)).Given(facts).Aggregate;
        var orphan = new AggregateScenario<FindingAggregate>(new FindingAggregate(fixture.Tenant,
            fixture.Program, fixture.Finding)).Given(facts[0]).Aggregate;
        var retry = new AggregateScenario<FindingAggregate>(new FindingAggregate(fixture.Tenant,
            fixture.Program, fixture.Finding, requestId)).Given(facts).Aggregate;
        var exact = retry.RaiseRouted(requestId, source, "Gap", "Scope", fixture.Owner, fixture.Due.AddDays(1),
            [], "Repair", fixture.Actor, Fixture.Now.AddDays(1));
        var changed = retry.RaiseRouted(requestId, source with { Version = "round2" }, "Gap", "Scope",
            fixture.Owner, fixture.Due, [], "Repair", fixture.Actor, Fixture.Now.AddDays(1));

        // Assert
        replay.EnsureImportComplete();
        Assert.Throws<InvalidOperationException>(orphan.EnsureImportComplete);
        Assert.Null(exact);
        Assert.NotNull(changed);
        Assert.Equal(2, facts.Length);
        Assert.Empty(new AggregateScenario<FindingAggregate>(retry).PendingEvents);
    }

    [Theory]
    [InlineData("raise")]
    [InlineData("revise")]
    [InlineData("add")]
    [InlineData("complete")]
    [InlineData("accept")]
    [InlineData("close")]
    [InlineData("reopen")]
    public void ShouldRecognizeExactOlderRequestAndRejectChangedActorOrIntentGivenLaterFacts(string operation)
    {
        // Arrange
        var fixture = new Fixture(operation);
        var requestId = Uuid.CreateVersion4();
        var first = fixture.Root(requestId);
        Assert.Null(fixture.Execute(first, requestId));
        var emitted = new AggregateScenario<FindingAggregate>(first).PendingEvents.ToArray();
        var fact = Assert.Single(emitted);
        Assert.EndsWith("V2", fact.GetType().Name, StringComparison.Ordinal);
        var json = JsonSerializer.Serialize(fact, fact.GetType(), ComplianceCoreJsonContext.Default);
        Assert.Contains(requestId.ToString(), json, StringComparison.Ordinal);
        var copy = (DomainEvent)JsonSerializer.Deserialize(json, fact.GetType(), ComplianceCoreJsonContext.Default)!;
        copy.AttachMetadata(fact.Metadata);
        fixture.Events.Add(copy);
        fixture.AppendLaterFact();
        var retry = fixture.Root(requestId);
        var changed = fixture.Root(requestId);
        var changedActor = fixture.Root(requestId);

        // Act
        var exact = fixture.Execute(retry, requestId, actor: fixture.Actor with { Display = "New display" });
        var conflict = fixture.Execute(changed, requestId, changeIntent: true);
        var actorConflict = fixture.Execute(changedActor, requestId,
            actor: fixture.Actor with { Kind = "system_process" });

        // Assert
        Assert.Null(exact);
        Assert.NotNull(conflict);
        Assert.NotNull(actorConflict);
        Assert.Empty(new AggregateScenario<FindingAggregate>(retry).PendingEvents);
        Assert.Empty(new AggregateScenario<FindingAggregate>(changed).PendingEvents);
        Assert.Empty(new AggregateScenario<FindingAggregate>(changedActor).PendingEvents);
    }

    [Fact]
    public void ShouldRejectEmptyActionIdentityWithoutAppendGivenRecordedFinding()
    {
        // Arrange
        var fixture = new Fixture("add");
        var requestId = Uuid.CreateVersion4();
        var root = fixture.Root(requestId);

        // Act
        var failure = root.AddAction(requestId, 1, Uuid.Empty, "Repair", fixture.Owner,
            fixture.Due, fixture.Actor, Fixture.Now);

        // Assert
        Assert.NotNull(failure);
        Assert.Empty(new AggregateScenario<FindingAggregate>(root).PendingEvents);
    }

    [Fact]
    public void ShouldRejectUnselectedMutationGivenReadOnlyHydration()
    {
        // Arrange
        var fixture = new Fixture("raise");
        var root = new FindingAggregate(fixture.Tenant, fixture.Program, fixture.Finding);

        // Act
        var failure = fixture.Execute(root, Uuid.CreateVersion4());

        // Assert
        Assert.NotNull(failure);
        Assert.Empty(new AggregateScenario<FindingAggregate>(root).PendingEvents);
    }

    sealed class Fixture
    {
        public static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        readonly string _operation;
        readonly long _expected;
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Finding { get; } = Uuid.CreateVersion4();
        public Uuid Owner { get; } = Uuid.CreateVersion4();
        readonly Uuid _action = Uuid.CreateVersion4();
        readonly Uuid _acceptance = Uuid.CreateVersion4();
        public ActorReference Actor { get; } = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        public DateOnly Due { get; } = new(2026, 11, 20);
        public List<DomainEvent> Events { get; } = [];

        public Fixture(string operation)
        {
            _operation = operation;
            if (operation != "raise")
                Seed(new FindingRaised(Tenant, Program, Finding, 1, Source(), "Gap", "Description",
                    "medium", "Scope", Owner, Due, [], Actor, Now));
            if (operation is "complete" or "close" or "reopen")
                Seed(new CorrectiveActionAdded(Tenant, Program, Finding, 2,
                    new CorrectiveActionView(_action, "Repair", Owner, Due, "open", Actor, Now)));
            if (operation is "close" or "reopen")
                Seed(new CorrectiveActionCompleted(Tenant, Program, Finding, 3, _action,
                    "Resolved", Evidence(), Owner, Actor, Now));
            if (operation == "reopen")
                Seed(new FindingClosed(Tenant, Program, Finding, 4,
                    new FindingClosureView(Uuid.CreateVersion4(), "Verified", Evidence(), "Done",
                        Uuid.CreateVersion4(), Actor, Now)));
            _expected = Events.Count;
        }

        public FindingAggregate Root(Uuid requestId) => new AggregateScenario<FindingAggregate>(
            new FindingAggregate(Tenant, Program, Finding, requestId)).Given(Events.ToArray()).Aggregate;

        public CommandFailure? Execute(FindingAggregate root, Uuid requestId, bool changeIntent = false,
            ActorReference? actor = null)
        {
            actor ??= Actor;
            var text = changeIntent ? "Changed" : "Repair";
            return _operation switch
            {
                "raise" => root.Raise(requestId, Source(), "Gap", changeIntent ? "Changed" : "Description",
                    "medium", "Scope", Owner, Due, [], actor, Now.AddDays(1)),
                "revise" => root.Revise(requestId, _expected, "high", Owner, Due, "Revised scope", null,
                    text, actor, Now.AddDays(1)),
                "add" => root.AddAction(requestId, _expected, _action, text, Owner, Due, actor, Now.AddDays(1)),
                "complete" => root.CompleteAction(requestId, _expected, _action, text, Evidence(), Uuid.Parse(Actor.Id, null),
                    actor, Now.AddDays(1)),
                "accept" => root.LinkAcceptance(requestId,
                    new FindingHistoryFold(root.Version, [], [], []), _expected, "waiver",
                    changeIntent ? Uuid.CreateVersion4() : _acceptance,
                    null, Now.AddDays(40), actor, Now.AddDays(1)),
                "close" => root.Close(requestId, _expected, requestId, "Verified", Evidence(), text,
                    Uuid.Parse(Actor.Id, null), actor, Now.AddDays(1), null),
                "reopen" => root.Reopen(requestId, _expected, text, actor, Now.AddDays(1)),
                _ => throw new InvalidOperationException(),
            };
        }

        public void AppendLaterFact()
        {
            if (_operation == "close")
                Seed(new FindingReopened(Tenant, Program, Finding, Events.Count + 1, "Returned", Actor, Now.AddDays(2)));
            else
                Seed(new FindingRevised(Tenant, Program, Finding, Events.Count + 1, "low", Owner, Due,
                    "Later scope", null, "Later triage", Actor, Now.AddDays(2)));
        }

        void Seed(DomainEvent fact) => Events.Add(DomainEventSeed.Attach(fact,
            FindingAggregate.StreamId(Program, Finding), (ulong)Events.Count + 1,
            occurredOn: DateTimeOffset.UtcNow.AddSeconds(Events.Count)));

        static FindingSource Source() => new("manual", null, null, "Exact source wording");
        static EvidenceReference[] Evidence() => [new(null, "record", "SUPPORT", "Evidence", "ignored-client-resolution")];
    }
}
