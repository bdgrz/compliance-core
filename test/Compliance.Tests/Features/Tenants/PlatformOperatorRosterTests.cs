using System.Globalization;
using System.Security.Claims;
using Bdgrz.Compliance.Tests.Hosting.Persistence;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class PlatformOperatorRosterTests
{
    static readonly Uuid First = Uuid.Parse("852a1468-9236-4808-880a-582e37256b96", CultureInfo.InvariantCulture);
    static readonly Uuid Second = Uuid.Parse("28b82a3b-be8f-4407-aabe-fc270b442ef3", CultureInfo.InvariantCulture);
    static readonly Uuid Third = Uuid.Parse("59ebcdd4-b6c9-4fdc-b36d-1d8abef92160", CultureInfo.InvariantCulture);
    static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorAttribution SystemActor = new("portia:system", "Portia");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldNotRestoreAuthorityGivenAnExactGrantRetryAfterRevocation(bool directoryLag)
    {
        // Arrange
        var users = new ExistingUsers();
        await using var provider = Services(users);
        await SeedAsync(provider, First);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var identity = RequestMetadata.Create();
        var grant = new GrantPlatformOperator(Second, "Cover operations", identity.RequestId, 1);
        Assert.True((await bus.DispatchAsync(grant, bus.CreateContext(Actor(First), identity))).IsSuccess);
        Assert.True((await bus.SendAsync(new RevokePlatformOperator(Second, "Coverage ended",
            Uuid.CreateVersion4(), 2, grant.CommandId), Actor(First))).IsSuccess);
        users.Available = !directoryLag;
        var lookups = users.Lookups;

        // Act
        var result = await bus.DispatchAsync(grant, bus.CreateContext(Actor(First), identity));
        var roster = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new PlatformOperatorRoster());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(roster.IsOperator(Second));
        Assert.Equal(3UL, roster.Version);
        Assert.Equal(lookups, users.Lookups);
    }

    [Fact]
    public void ShouldPreserveLaterAssignmentGivenAnExactOldRevocationRetry()
    {
        // Arrange
        var first = Uuid.CreateVersion4();
        var removal = Uuid.CreateVersion4();
        var next = Uuid.CreateVersion4();
        var scenario = Given(removal, Seeded(First), Granted(first), Revoked(removal, first), Granted(next, 3));

        // Act
        var result = scenario.Aggregate.Revoke(removal, 2, First, Second, first, "Coverage ended", Provenance(First), Now);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(next, scenario.Aggregate.AssignmentId(Second));
        Assert.Empty(scenario.PendingEvents);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("issuer")]
    [InlineData("subject")]
    [InlineData("reason")]
    [InlineData("revision")]
    [InlineData("kind")]
    public void ShouldConflictGivenChangedIntentUnderAnAcceptedGrantIdentity(string change)
    {
        // Arrange
        var first = Uuid.CreateVersion4();
        var scenario = Given(first, Seeded(First), Granted(first), Revoked(Uuid.CreateVersion4(), first), Granted(Uuid.CreateVersion4(), 3));
        var actor = change == "actor" ? Second : First;
        var provenance = change == "issuer" ? new ActorAttribution(actor.ToString(), "another-issuer") : Provenance(actor);

        // Act
        var result = change == "kind"
            ? scenario.Aggregate.Revoke(first, 1, actor, Second, first, "Cover operations", provenance, Now)
            : scenario.Aggregate.Grant(first, change == "revision" ? 2UL : 1, actor,
                change == "subject" ? Third : Second, change == "reason" ? "Changed reason" : "Cover operations", provenance, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("issuer")]
    [InlineData("subject")]
    [InlineData("reason")]
    [InlineData("revision")]
    [InlineData("episode")]
    [InlineData("kind")]
    public void ShouldConflictGivenChangedIntentUnderAnAcceptedRevocationIdentity(string change)
    {
        // Arrange
        var first = Uuid.CreateVersion4();
        var removal = Uuid.CreateVersion4();
        var scenario = Given(removal, Seeded(First), Granted(first), Revoked(removal, first), Granted(Uuid.CreateVersion4(), 3));
        var actor = change == "actor" ? Second : First;
        var provenance = change == "issuer" ? new ActorAttribution(actor.ToString(), "another-issuer") : Provenance(actor);

        // Act
        var result = change == "kind"
            ? scenario.Aggregate.Grant(removal, 2, actor, Second, "Coverage ended", provenance, Now)
            : scenario.Aggregate.Revoke(removal, change == "revision" ? 4UL : 2, actor,
                change == "subject" ? Third : Second, change == "episode" ? Uuid.CreateVersion4() : first,
                change == "reason" ? "Changed reason" : "Coverage ended", provenance, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectLastOperatorRevocationGivenSingleActiveOperator()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var scenario = Given(id, Seeded(First));
        var assignment = scenario.Aggregate.AssignmentId(First)!.Value;

        // Act
        var result = scenario.Aggregate.Revoke(id, 1, First, First, assignment, "Leaving", Provenance(First), Now);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.True(scenario.Aggregate.IsOperator(First));
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectNewGrantIntentGivenAnAlreadyActiveSubject()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var scenario = Given(id, Seeded(First, Second));

        // Act
        var result = scenario.Aggregate.Grant(id, 1, First, Second, "Cover operations", Provenance(First), Now);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldPreserveNewAuthorityGivenDelayedRevocationOfAnOldAssignmentAtTheCurrentRevision()
    {
        // Arrange
        var first = Uuid.CreateVersion4();
        var next = Uuid.CreateVersion4();
        var removal = Uuid.CreateVersion4();
        var scenario = Given(removal, Seeded(First), Granted(first), Revoked(Uuid.CreateVersion4(), first), Granted(next, 3));

        // Act
        var result = scenario.Aggregate.Revoke(removal, 4, First, Second, first, "Coverage ended", Provenance(First), Now);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(next, scenario.Aggregate.AssignmentId(Second));
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRetainOriginalIntentGivenChangedServerClockAndActorDisplay()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var scenario = Given(id, Seeded(First), Granted(id));

        // Act
        var result = scenario.Aggregate.Grant(id, 1, First, Second, " Cover operations ", Provenance(First),
            Now.AddDays(1), "Updated profile");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(id, scenario.Aggregate.AssignmentId(Second));
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRecordActorDisplaySubjectTimeAndReasonGivenANewGrant()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var scenario = Given(id, Seeded(First));

        // Act
        var result = scenario.Aggregate.Grant(id, 1, First, Second, " Cover operations ", Provenance(First),
            Now.AddMinutes(1), " Operator One ");

        // Assert
        Assert.True(result.IsSuccess);
        var fact = Assert.IsType<PlatformOperatorGranted>(Assert.Single(scenario.PendingEvents));
        Assert.Equal((First, Second, "Cover operations", Now.AddMinutes(1)),
            (fact.ActorUserId, fact.SubjectUserId, fact.Reason, fact.OccurredAt));
        Assert.Equal("Operator One", fact.ActorDisplay);
        Assert.Equal(Provenance(First), fact.Actor);
        Assert.Equal(id, fact.CommandId);
    }

    [Fact]
    public async Task ShouldDenyBeforeResolvingAcceptedIntentGivenAnActorWhoLostOperatorAuthority()
    {
        // Arrange
        await using var provider = Services();
        await SeedAsync(provider, First, Third);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var grant = new GrantPlatformOperator(Second, "Cover operations", Uuid.CreateVersion4(), 1);
        Assert.True((await bus.SendAsync(grant, Actor(First))).IsSuccess);
        var roster = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new PlatformOperatorRoster());
        Assert.True((await bus.SendAsync(new RevokePlatformOperator(First, "Handed over", Uuid.CreateVersion4(), 2,
            roster.AssignmentId(First)!.Value), Actor(Third))).IsSuccess);

        // Act
        // Assert
        await RequestScenario.For(provider).GivenActor(Actor(First)).When(grant)
            .ExpectDenied(RequestErrorKind.Forbidden).ExpectNotHandled();
    }

    [Fact]
    public void ShouldNotRestoreRevokedOperatorGivenRepeatedConfigurationSeed()
    {
        // Arrange
        var seed = Seeded(First, Second);
        var original = Given(Uuid.Empty, seed).Aggregate;
        var removed = new PlatformOperatorRevoked(Uuid.CreateVersion4(), 1, Second, First,
            original.AssignmentId(First)!.Value, Provenance(Second), Now, "Handed over");
        var scenario = Given(PlatformOperatorRoster.CreationId, Seeded(First, Second), removed);

        // Act
        var result = scenario.Aggregate.Seed(PlatformOperatorRoster.CreationId, [Second, First, First], SystemActor, Now.AddDays(1));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(scenario.Aggregate.IsOperator(First));
        Assert.True(scenario.Aggregate.IsOperator(Second));
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldConflictGivenDifferentConfigurationUnderTheSameCreationIdentity()
    {
        // Arrange
        var scenario = Given(PlatformOperatorRoster.CreationId, Seeded(First));

        // Act
        var result = scenario.Aggregate.Seed(PlatformOperatorRoster.CreationId, [Second], SystemActor, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public async Task ShouldExposeExactAssignmentsAndRevisionGivenTheCurrentRosterRead()
    {
        // Arrange
        await using var provider = Services();
        await SeedAsync(provider, First);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var id = Uuid.CreateVersion4();
        Assert.True((await bus.SendAsync(new GrantPlatformOperator(Second, "Cover operations", id, 1), Actor(First))).IsSuccess);

        // Act
        var result = await bus.SendAsync(new ListPlatformOperators(), Actor(First));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2UL, result.Value.Revision);
        Assert.Equal(id, Assert.Single(result.Value.Operators, entry => entry.UserId == Second).AssignmentId);
        Assert.NotEqual(Uuid.Empty, Assert.Single(result.Value.Operators, entry => entry.UserId == First).AssignmentId);
    }

    [Fact]
    public async Task ShouldRecognizeOriginalNativeIntentGivenConcreteDurableRoundTrips()
    {
        // Arrange
        await using var provider = EventPersistenceFrameTests.Compose();
        var serializer = provider.GetRequiredService<IDomainEventSerializer>();
        var id = Uuid.CreateVersion4();
        var owner = new PlatformOperatorRoster(id);
        var facts = new DomainEvent[] { Seeded(First), Granted(id), Revoked(Uuid.CreateVersion4(), id) };
        var restored = facts.Select((fact, index) => serializer.Deserialize(serializer.Serialize(
            DomainEventSeed.Attach(fact, owner.Id, (ulong)index + 1)))).ToArray();
        var scenario = new AggregateScenario<PlatformOperatorRoster>(owner).Given(restored);

        // Act
        var result = owner.Grant(id, 1, First, Second, "Cover operations", Provenance(First), Now);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(owner.IsOperator(Second));
        Assert.Equal(3UL, owner.Version);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public async Task ShouldRejectAnEmptyTypedIdentityGivenAnAuthorizedCommand()
    {
        // Arrange
        await using var provider = Services();
        await SeedAsync(provider, First);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
            new GrantPlatformOperator(Second, "Cover operations", Uuid.Empty, 1), Actor(First));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldRetainOneOperatorGivenConcurrentCompetingRevocations()
    {
        // Arrange
        var store = new EventStoreProbe(typeof(PlatformOperatorRevoked)) { WaitForPair = true };
        await using var provider = Services(store: store);
        await SeedAsync(provider, First, Second);
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var initial = await reader.HydrateAsync(new PlatformOperatorRoster());

        // Act
        var results = await Task.WhenAll(RevokeAsync(First, Second), RevokeAsync(Second, First));
        var current = await reader.HydrateAsync(new PlatformOperatorRoster());

        // Assert
        Assert.Single(results, result => result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.Single(results, result => !result.IsSuccess).Error!.Kind);
        Assert.Single(current.Operators);
        Assert.Equal(2UL, current.Version);

        async Task<Result> RevokeAsync(Uuid actor, Uuid target)
        {
            await using var attempt = provider.CreateAsyncScope();
            try
            {
                return await attempt.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
                    new RevokePlatformOperator(target, "Rotate operations", Uuid.CreateVersion4(), 1,
                        initial.AssignmentId(target)!.Value), Actor(actor));
            }
            catch (EventStreamConcurrencyException)
            {
                return Result.Failure(new RequestError(RequestErrorKind.Conflict, "Lost the append race.", isTransient: true));
            }
        }
    }

    [Fact]
    public async Task ShouldInitializeOnceGivenConcurrentHostSeeds()
    {
        // Arrange
        var store = new EventStoreProbe(typeof(PlatformOperatorRosterSeeded)) { WaitForPair = true };
        await using var provider = Services(store: store);
        var scopes = provider.GetRequiredService<IServiceScopeFactory>();
        var firstHost = new PlatformOperatorRosterBootstrap(scopes, new PlatformOperatorAuthority([First]));
        var secondHost = new PlatformOperatorRosterBootstrap(scopes, new PlatformOperatorAuthority([First]));

        // Act
        await Task.WhenAll(firstHost.StartAsync(CancellationToken.None), secondHost.StartAsync(CancellationToken.None));
        await using var scope = provider.CreateAsyncScope();
        var roster = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new PlatformOperatorRoster());

        // Assert
        Assert.Equal(1UL, roster.Version);
        Assert.Equal(First, Assert.Single(roster.Operators).UserId);
    }

    [Fact]
    public async Task ShouldKeepTheInitialSeedGivenChangedLaterStartupConfiguration()
    {
        // Arrange
        await using var provider = Services();
        await SeedAsync(provider, First);
        var host = new PlatformOperatorRosterBootstrap(provider.GetRequiredService<IServiceScopeFactory>(),
            new PlatformOperatorAuthority([Second]));

        // Act
        await host.StartAsync(CancellationToken.None);
        await using var scope = provider.CreateAsyncScope();
        var roster = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new PlatformOperatorRoster());

        // Assert
        Assert.Equal(1UL, roster.Version);
        Assert.True(roster.IsOperator(First));
        Assert.False(roster.IsOperator(Second));
    }

    [Theory]
    [InlineData("seed")]
    [InlineData("grant")]
    [InlineData("revoke")]
    public async Task ShouldRecoverAcceptedIntentGivenAnUnknownOwnerAppendOutcome(string boundary)
    {
        // Arrange
        var eventType = boundary switch
        {
            "seed" => typeof(PlatformOperatorRosterSeeded),
            "grant" => typeof(PlatformOperatorGranted),
            _ => typeof(PlatformOperatorRevoked),
        };
        var store = new EventStoreProbe(eventType) { InterruptAfterCommit = true };
        var users = new ExistingUsers();
        await using var provider = Services(users, store);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var grant = new GrantPlatformOperator(Second, "Cover operations", Uuid.CreateVersion4(), 1);
        var revoke = new RevokePlatformOperator(Second, "Coverage ended", Uuid.CreateVersion4(), 2, grant.CommandId);
        if (boundary != "seed")
            await SeedAsync(provider, First);
        if (boundary == "revoke")
            Assert.True((await bus.SendAsync(grant, Actor(First))).IsSuccess);
        IRequest command = boundary switch
        {
            "seed" => new SeedPlatformOperatorRoster(PlatformOperatorRoster.CreationId, [First]),
            "grant" => grant,
            _ => revoke,
        };
        var actor = boundary == "seed" ? RequestActor.System : Actor(First);
        await Assert.ThrowsAsync<IOException>(async () => await bus.SendAsync(command, actor));
        if (boundary == "grant")
            users.Available = false;
        var next = Uuid.CreateVersion4();
        if (boundary == "revoke")
            Assert.True((await bus.SendAsync(new GrantPlatformOperator(Second, "New coverage", next, 3), Actor(First))).IsSuccess);

        // Act
        var retry = await bus.SendAsync(command, actor);
        var current = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new PlatformOperatorRoster());

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(boundary == "seed" ? 1UL : boundary == "grant" ? 2UL : 4UL, current.Version);
        Assert.Equal(boundary != "seed", current.IsOperator(Second));
        if (boundary == "revoke")
            Assert.Equal(next, current.AssignmentId(Second));
    }

    static ServiceProvider Services(ExistingUsers? users = null, IEventStore? store = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(store ?? new InMemoryEventStore());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new PlatformOperatorAuthority([First]));
        services.AddScoped<IPlatformOperatorAccess, EventSourcedPlatformOperatorAccess>();
        services.AddSingleton<IPlatformUserDirectoryReader>(users ?? new ExistingUsers());
        services.AddPortia().AddRequestHandler<SeedPlatformOperatorRosterHandler>()
            .AddRequestAuthorizer<SeedPlatformOperatorRosterAuthorizer>()
            .AddRequestHandler<GrantPlatformOperatorHandler>().AddRequestHandler<RevokePlatformOperatorHandler>()
            .AddRequestHandler<ListPlatformOperatorsHandler>().AddRequestAuthorizer<PlatformOperatorAuthorizer>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    static async Task SeedAsync(ServiceProvider provider, params Uuid[] users)
    {
        await using var scope = provider.CreateAsyncScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
            new SeedPlatformOperatorRoster(PlatformOperatorRoster.CreationId, users), RequestActor.System)).IsSuccess);
    }

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString(), ClaimValueTypes.String, "bdgrz")], "BdgrzSession"));
    static ActorAttribution Provenance(Uuid userId) => new(userId.ToString(), "bdgrz");
    static PlatformOperatorRosterSeeded Seeded(params Uuid[] users) => new(PlatformOperatorRoster.CreationId,
        users.Distinct().OrderBy(user => user.ToString(), StringComparer.Ordinal).ToArray(), SystemActor, Now);
    static PlatformOperatorGranted Granted(Uuid id, ulong revision = 1) =>
        new(id, revision, First, Second, Provenance(First), Now, "Cover operations", "Operator One");
    static PlatformOperatorRevoked Revoked(Uuid id, Uuid assignment, ulong revision = 2) =>
        new(id, revision, First, Second, assignment, Provenance(First), Now, "Coverage ended", "Operator One");
    static AggregateScenario<PlatformOperatorRoster> Given(Uuid selected, params DomainEvent[] facts)
    {
        var owner = new PlatformOperatorRoster(selected);
        return new AggregateScenario<PlatformOperatorRoster>(owner).Given(facts.Select((fact, index) =>
            DomainEventSeed.Attach(fact, owner.Id, (ulong)index + 1)).ToArray());
    }

    sealed class ExistingUsers : IPlatformUserDirectoryReader
    {
        public bool Available { get; set; } = true;
        public int Lookups { get; private set; }
        public ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default)
        {
            Lookups++;
            return ValueTask.FromResult(Available);
        }
    }

    sealed class EventStoreProbe(Type eventType) : IEventStore
    {
        readonly InMemoryEventStore _inner = new();
        readonly TaskCompletionSource _arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int _attempts;
        public bool WaitForPair { get; init; }
        public bool InterruptAfterCommit { get; init; }
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
            CancellationToken ct) => _inner.ReadAsync(stream, fromOffset, ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            CancellationToken ct) => _inner.ReadAsync(pattern, cursor, ct);
        public async ValueTask AppendAsync(EventStreamAddress stream, ulong expectedStreamPosition,
            IReadOnlyList<DomainEvent> events, CancellationToken ct = default)
        {
            var attempt = events.Any(ev => ev.GetType() == eventType) ? Interlocked.Increment(ref _attempts) : 0;
            if (WaitForPair && attempt is 1 or 2)
            {
                if (attempt == 2)
                    _arrived.TrySetResult();
                await _arrived.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
            }
            await _inner.AppendAsync(stream, expectedStreamPosition, events, ct);
            if (InterruptAfterCommit && attempt == 1)
                throw new IOException("Interrupted after the owner append committed.");
        }
    }
}
