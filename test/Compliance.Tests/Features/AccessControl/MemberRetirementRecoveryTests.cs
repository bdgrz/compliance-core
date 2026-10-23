using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Tests.Hosting.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class MemberRetirementRecoveryTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid UserId = Uuid.CreateVersion4();
    static readonly Uuid ActorId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 23, 1, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldAvoidCleanupCommandGivenAnActiveMembershipWithoutRetirement(bool directGrant)
    {
        // Arrange
        await using var fixture = new Fixture();
        var episode = await fixture.RegisterAsync();
        if (directGrant)
            await fixture.GrantAsync(Uuid.CreateVersion4(), episode);
        else
            await fixture.AssignAsync(Uuid.CreateVersion4(), episode);
        var source = Assert.Single(await fixture.RecordsAsync(directGrant ? "access-grants" : "rbac-team-members"));
        var scenario = new ReactorScenario(new TenantId(TenantId.ToString())).Given(source.Event);
        Reactor reactor = directGrant
            ? new RetiredMemberGrantReactor(fixture.Cuts, scenario.Requests, fixture.Reader)
            : new RetiredTeamAssignmentReactor(fixture.Cuts, scenario.Requests, fixture.Reader);

        // Act
        await scenario.RunAsync(reactor);

        // Assert
        Assert.Empty(scenario.SentRequests);
    }

    [Fact]
    public async Task ShouldRecoverAuthorityWithoutCallerRetryGivenACommittedRetirement()
    {
        // Arrange
        await using var fixture = new Fixture();
        var episode = await fixture.RegisterAsync();
        var team = Uuid.CreateVersion4();
        var grant = Uuid.CreateVersion4();
        await fixture.AssignAsync(team, episode);
        await fixture.GrantAsync(grant, episode);
        await fixture.RetireAsync();

        // Act
        await fixture.RunAsync(new MemberRetirementReactor(fixture.Cuts, fixture.Bus));
        var member = await fixture.MemberAsync(episode);
        var removed = Assert.Single((await fixture.RecordsAsync("rbac-team-members"))
            .Select(record => record.Event).OfType<TeamMemberRemoved>());
        var revoked = Assert.Single((await fixture.RecordsAsync("access-grants"))
            .Select(record => record.Event).OfType<AccessGrantRevoked>());

        // Assert
        Assert.True(member.SelectedCleanupCompleted);
        Assert.True(member.IsDeprovisionCleanupComplete);
        Assert.False((await fixture.AssignmentAsync(team)).IsAssigned);
        Assert.True((await fixture.Reader.HydrateAsync(new AccessGrant(TenantId, grant))).IsRevoked);
        Assert.Equal(member.TerminationProof!.EventId, removed.TerminationEventId);
        Assert.Equal(episode, removed.MembershipEpisodeId);
        Assert.Equal(ActorId.ToString(), revoked.RevokedBy.Id);
        Assert.Equal("Original administrator", revoked.RevokedBy.Display);
        Assert.Equal(MemberRetirementEffects.RelationshipId(removed.AssignmentEventId,
            new TeamMember(TenantId, team, RbacIds.Member(TenantId, UserId)).Stream, episode, 0,
                member.TerminationProof.EventId),
            removed.Metadata.CausationId);
        var completed = Assert.Single((await fixture.RecordsAsync("rbac-members"))
            .Select(record => record.Event).OfType<MemberDeprovisionCleanupCompleted>());
        Assert.Equal(completed.CommandId, completed.Metadata.CausationId);
    }

    [Theory]
    [InlineData("TeamMemberRemoved", false)]
    [InlineData("TeamMemberRemoved", true)]
    [InlineData("AccessGrantRevoked", false)]
    [InlineData("AccessGrantRevoked", true)]
    [InlineData("MemberDeprovisionCleanupCompleted", false)]
    [InlineData("MemberDeprovisionCleanupCompleted", true)]
    public async Task ShouldRecoverExactNativeEffectsGivenUnknownAppendOutcome(string eventType, bool committed)
    {
        // Arrange
        var store = new InterruptedStore(eventType, committed);
        await using var fixture = new Fixture(store);
        var episode = await fixture.RegisterAsync();
        var team = Uuid.CreateVersion4();
        var grant = Uuid.CreateVersion4();
        await fixture.AssignAsync(team, episode);
        await fixture.GrantAsync(grant, episode);
        await fixture.RetireAsync();
        store.Armed = true;
        var reactor = new MemberRetirementReactor(fixture.Cuts, fixture.Bus);

        // Act
        await Assert.ThrowsAsync<IOException>(() => fixture.RunAsync(reactor));
        await fixture.RunAsync(reactor);
        await fixture.RunAsync(reactor);
        var member = await fixture.MemberAsync(episode);
        var relationships = await fixture.RecordsAsync("rbac-team-members");
        var grants = await fixture.RecordsAsync("access-grants");
        var members = await fixture.RecordsAsync("rbac-members");

        // Assert
        Assert.True(member.SelectedCleanupCompleted);
        Assert.Single(relationships, record => record.Event is TeamMemberRemoved);
        Assert.Single(grants, record => record.Event is AccessGrantRevoked);
        Assert.Single(members, record => record.Event is MemberDeprovisionCleanupCompleted);
        Assert.False((await fixture.AssignmentAsync(team)).IsAssigned);
    }

    [Fact]
    public async Task ShouldReconcileLateOldFactsGivenANewerMembershipAndCompletedCleanup()
    {
        // Arrange
        await using var fixture = new Fixture();
        var oldEpisode = await fixture.RegisterAsync();
        await fixture.RetireAsync();
        await fixture.RunAsync(new MemberRetirementReactor(fixture.Cuts, fixture.Bus));
        var newEpisode = await fixture.RegisterAsync();
        var currentTeam = Uuid.CreateVersion4();
        var lateTeam = Uuid.CreateVersion4();
        var oldGrant = Uuid.CreateVersion4();
        var currentGrant = Uuid.CreateVersion4();
        await fixture.AssignAsync(currentTeam, newEpisode);
        await fixture.AssignAsync(lateTeam, oldEpisode);
        await fixture.GrantAsync(oldGrant, oldEpisode);
        await fixture.GrantAsync(currentGrant, newEpisode);

        // Act
        await fixture.RunAsync(new RetiredTeamAssignmentReactor(fixture.Cuts, fixture.Bus, fixture.Reader));
        await fixture.RunAsync(new RetiredMemberGrantReactor(fixture.Cuts, fixture.Bus, fixture.Reader));
        var current = await fixture.MemberAsync(newEpisode);

        // Assert
        Assert.True(current.IsRegistered);
        Assert.Equal(newEpisode, current.MembershipEpisodeId);
        Assert.True((await fixture.AssignmentAsync(currentTeam)).IsAssigned);
        Assert.False((await fixture.AssignmentAsync(lateTeam)).IsAssigned);
        Assert.True((await fixture.Reader.HydrateAsync(new AccessGrant(TenantId, oldGrant))).IsRevoked);
        Assert.False((await fixture.Reader.HydrateAsync(new AccessGrant(TenantId, currentGrant))).IsRevoked);
    }

    [Fact]
    public async Task ShouldPreserveLaterAssignmentGivenAnExactOldRelationshipRetry()
    {
        // Arrange
        await using var fixture = new Fixture();
        var episode = await fixture.RegisterAsync();
        var team = Uuid.CreateVersion4();
        await fixture.AssignAsync(team, episode);
        var first = Assert.Single(await fixture.RecordsAsync("rbac-team-members"));
        await fixture.RetireAsync();
        var terminationId = (await fixture.MemberAsync(episode)).TerminationProof!.EventId;
        var request = MemberRetirementEffects.Team(Assert.IsType<TeamMemberAssigned>(first.Event), first.ResourceOffset,
            terminationId);
        Assert.True((await fixture.Bus.SendAsync(request, RequestActor.System)).IsSuccess);
        await fixture.AssignAsync(team, episode);
        var later = await fixture.AssignmentAsync(team);
        var count = (await fixture.RecordsAsync("rbac-team-members")).Count;

        // Act
        var retried = await fixture.Bus.SendAsync(request, RequestActor.System);
        var current = await fixture.AssignmentAsync(team);
        var records = await fixture.RecordsAsync("rbac-team-members");

        // Assert
        Assert.True(retried.IsSuccess);
        Assert.True(current.IsAssigned);
        Assert.Equal(later.AssignmentEventId, current.AssignmentEventId);
        Assert.Equal(count, records.Count);
        var latest = Assert.IsType<TeamMemberAssigned>(records[^1].Event);
        Assert.True((await fixture.Bus.SendAsync(MemberRetirementEffects.Team(latest,
            records[^1].ResourceOffset, terminationId), RequestActor.System)).IsSuccess);
        Assert.False((await fixture.AssignmentAsync(team)).IsAssigned);
    }

    [Fact]
    public async Task ShouldRejectChangedSourceIntentGivenAValidCleanupIdentity()
    {
        // Arrange
        await using var fixture = new Fixture();
        var episode = await fixture.RegisterAsync();
        await fixture.AssignAsync(Uuid.CreateVersion4(), episode);
        var source = Assert.Single(await fixture.RecordsAsync("rbac-team-members"));
        await fixture.RetireAsync();
        var request = MemberRetirementEffects.Team(Assert.IsType<TeamMemberAssigned>(source.Event), source.ResourceOffset,
            (await fixture.MemberAsync(episode)).TerminationProof!.EventId);
        var changes = new[]
        {
            request with { SourcePosition = source.ResourceOffset + 1 },
            request with { MembershipEpisodeId = Uuid.CreateVersion4() },
            request with { TargetId = Uuid.CreateVersion4() },
            request with { SourceEventId = Uuid.CreateVersion4() },
            request with { TerminationEventId = Uuid.CreateVersion4() },
            request with { TenantId = Uuid.CreateVersion4() },
            request with { MemberId = Uuid.CreateVersion4() },
            request with { Kind = RetiredMembershipRelationshipKind.DirectGrant },
        };

        // Act
        foreach (var changed in changes)
        {
            var result = await fixture.Bus.SendAsync(changed, RequestActor.System);
            // Assert
            Assert.False(result.IsSuccess);
        }
        Assert.True((await fixture.AssignmentAsync(request.TargetId)).IsAssigned);
    }

    [Fact]
    public async Task ShouldRejectUserAuthorityGivenAnInternalCleanupCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        var episode = await fixture.RegisterAsync();
        await fixture.RetireAsync();
        var member = await fixture.MemberAsync(episode);
        var request = MemberRetirementEffects.Complete(member.TerminationProof!);

        // Act
        var result = await fixture.Bus.SendAsync(request, RequestActor.Anonymous);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error!.Kind);
        Assert.False((await fixture.MemberAsync(episode)).SelectedCleanupCompleted);
    }

    [Fact]
    public async Task ShouldRecoverWithoutOpeningANewEpisodeGivenAnUnknownCoverageCheckpointOutcome()
    {
        // Arrange
        var cuts = new InterruptedCoverageStore();
        await using var fixture = new Fixture(cuts: cuts);
        var episode = await fixture.RegisterAsync();
        await fixture.AssignAsync(Uuid.CreateVersion4(), episode);
        await fixture.RetireAsync();
        var reactor = new MemberRetirementReactor(cuts, fixture.Bus);

        // Act
        await Assert.ThrowsAsync<IOException>(() => fixture.RunAsync(reactor));
        var interrupted = await fixture.MemberAsync(episode);
        var admission = interrupted.Register();
        await fixture.RunAsync(reactor);

        // Assert
        Assert.False(admission.IsSuccess);
        Assert.False(interrupted.SelectedCleanupCompleted);
        Assert.True((await fixture.MemberAsync(episode)).SelectedCleanupCompleted);
        Assert.Single(await fixture.RecordsAsync("rbac-team-members"), record => record.Event is TeamMemberRemoved);
    }

    [Fact]
    public async Task ShouldReconcileAnAssignmentBeyondTheSavedCutGivenItCommitsDuringDiscovery()
    {
        // Arrange
        var store = new AppendAfterSnapshotStore();
        await using var fixture = new Fixture(store);
        var episode = await fixture.RegisterAsync();
        await fixture.RetireAsync();
        var lateTeam = Uuid.CreateVersion4();
        store.AppendAfterSnapshot = () => fixture.AssignAsync(lateTeam, episode);

        // Act
        await fixture.RunAsync(new MemberRetirementReactor(fixture.Cuts, fixture.Bus));
        var pending = await fixture.AssignmentAsync(lateTeam);
        await fixture.RunAsync(new RetiredTeamAssignmentReactor(fixture.Cuts, fixture.Bus, fixture.Reader));

        // Assert
        Assert.True(pending.IsAssigned);
        Assert.True((await fixture.MemberAsync(episode)).SelectedCleanupCompleted);
        Assert.False((await fixture.AssignmentAsync(lateTeam)).IsAssigned);
    }

    [Fact]
    public async Task ShouldRejectForgedProofAndPreserveNativeProvenanceGivenConcreteSerialization()
    {
        // Arrange
        await using var fixture = new Fixture();
        await using var serializers = EventPersistenceFrameTests.Compose();
        var serializer = serializers.GetRequiredService<IDomainEventSerializer>();
        var episode = await fixture.RegisterAsync();
        await fixture.RetireAsync();
        await fixture.RunAsync(new MemberRetirementReactor(fixture.Cuts, fixture.Bus));
        var original = await fixture.MemberAsync(episode);
        var restored = (await fixture.RecordsAsync("rbac-members"))
            .Select(record => serializer.Deserialize(serializer.Serialize(record.Event))).ToArray();
        var owner = new Member(TenantId, UserId, episode);
        var scenario = new AggregateScenario<Member>(owner).Given(restored);
        var proof = Assert.IsType<MembershipTerminationProof>(owner.TerminationProof);

        // Act
        var altered = owner.CompleteDeprovisionCleanup(proof with { Sha256 = new string('0', 64) },
            MemberRetirementEffects.Complete(proof).CommandId, Now);
        var exact = owner.CompleteDeprovisionCleanup(proof, MemberRetirementEffects.Complete(proof).CommandId,
            Now.AddHours(1));

        // Assert
        Assert.Equal(original.TerminationProof, proof);
        Assert.False(altered.IsSuccess);
        Assert.True(exact.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public async Task ShouldRetryWithoutRemovalGivenSourceRelationshipHasNoAvailableMembershipAuthority()
    {
        // Arrange
        await using var fixture = new Fixture();
        var episode = Uuid.CreateVersion4();
        var team = Uuid.CreateVersion4();
        await fixture.AssignAsync(team, episode);
        var record = Assert.Single(await fixture.RecordsAsync("rbac-team-members"));
        var request = MemberRetirementEffects.Team(Assert.IsType<TeamMemberAssigned>(record.Event), record.ResourceOffset,
            Uuid.CreateVersion4());

        // Act
        var result = await fixture.Bus.SendAsync(request, RequestActor.System);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.IsTransient);
        Assert.True((await fixture.AssignmentAsync(team)).IsAssigned);
        Assert.Single(await fixture.RecordsAsync("rbac-team-members"));
    }

    [Fact]
    public async Task ShouldRetryWithoutReleaseGivenAProvenReceivingAssignmentIsUnavailable()
    {
        // Arrange
        await using var fixture = new Fixture();
        var episode = await fixture.RegisterAsync();
        await fixture.RetireAsync();
        var proof = (await fixture.MemberAsync(episode)).TerminationProof!;
        var owner = new TeamMember(TenantId, Uuid.CreateVersion4(), proof.MemberId);
        var scenario = new AggregateScenario<TeamMember>(owner);

        // Act
        var result = owner.RemoveForRetirement(proof, Uuid.CreateVersion4());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.IsTransient);
        Assert.Empty(scenario.PendingEvents);
        Assert.False((await fixture.MemberAsync(episode)).SelectedCleanupCompleted);
    }

    [Fact]
    public async Task ShouldCleanEverySourceRelationshipGivenMoreThanOneThousandUnprojectedAssignments()
    {
        // Arrange
        await using var fixture = new Fixture();
        var episode = await fixture.RegisterAsync();
        for (var index = 0; index < 1001; index++)
            await fixture.AssignAsync(Uuid.CreateVersion4(), episode);
        await fixture.RetireAsync();

        // Act
        await fixture.RunAsync(new MemberRetirementReactor(fixture.Cuts, fixture.Bus));
        var records = await fixture.RecordsAsync("rbac-team-members");

        // Assert
        Assert.Equal(1001, records.Count(record => record.Event is TeamMemberRemoved));
        Assert.True((await fixture.MemberAsync(episode)).SelectedCleanupCompleted);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Fixture(IEventStore? store = null, IProjectionCheckpointStore? cuts = null)
        {
            Store = store ?? new InMemoryEventStore();
            Cuts = cuts ?? new InMemoryProjectionCheckpointStore();
            var services = new ServiceCollection();
            services.AddSingleton(Store);
            services.AddSingleton<IDomainEventReader>(Store);
            services.AddSingleton(Cuts);
            services.AddSingleton(TimeProvider.System);
            services.AddScoped<MemberAuthorityCleanup>();
            services.AddPortia().AddRequestHandler<CompleteMemberRetirementHandler>()
                .AddRequestHandler<CleanupRetiredMembershipRelationshipHandler>()
                .AddRequestAuthorizer<MembershipRetirementAuthorizer>();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Bus = _scope.ServiceProvider.GetRequiredService<IRequestBus>();
        }
        public IEventStore Store { get; }
        public IProjectionCheckpointStore Cuts { get; }
        public IAggregateReader Reader { get; }
        public IRequestBus Bus { get; }
        public ValueTask<Member> MemberAsync(Uuid episode) => Reader.HydrateAsync(new Member(TenantId, UserId, episode));
        public ValueTask<TeamMember> AssignmentAsync(Uuid team) => Reader.HydrateAsync(new TeamMember(TenantId, team,
            RbacIds.Member(TenantId, UserId)));
        public async Task<Uuid> RegisterAsync()
        {
            await SeedAsync(new Member(TenantId, UserId), member => member.Register());
            return (await Reader.HydrateAsync(new Member(TenantId, UserId))).MembershipEpisodeId;
        }
        public Task AssignAsync(Uuid team, Uuid episode) => SeedAsync(new TeamMember(TenantId, team,
            RbacIds.Member(TenantId, UserId)), assignment => assignment.Assign(episode));
        public Task GrantAsync(Uuid grant, Uuid episode) => SeedAsync(new AccessGrant(TenantId, grant),
            current => current.Issue(new AccessGrantTerms(new AccessGrantPrincipal(AccessGrantPrincipalKind.Member,
                RbacIds.Member(TenantId, UserId)), Uuid.CreateVersion4(),
                new AccessGrantScope(AccessGrantScopeKind.Organization, TenantId), new AccessGrantSource("manual", "retirement"),
                ActorReference.ForMember(ActorId, "Original administrator"), Now, null), episode));
        public Task RetireAsync() => SeedAsync(new Member(TenantId, UserId),
            member => member.Deprovision(ActorId, "Original administrator", Now, "Membership ended."));
        async Task SeedAsync<T>(T owner, Func<T, Result> operation) where T : Aggregate
        {
            var result = await _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(owner,
                current => AggregateOutcome.CommitOnSuccess(operation(current)), new RequestDispatchContext(RequestActor.System));
            Assert.True(result.IsSuccess, result.Error?.Message);
        }
        public async Task<List<DomainEventRecord>> RecordsAsync(string area)
        {
            var records = new List<DomainEventRecord>();
            await foreach (var record in Store.ReadAsync(EventStreamPattern.ForPattern(TenantId.ToString(), area),
                               EventCursor.Start, CancellationToken.None))
                records.Add(record);
            return records;
        }
        public async Task RunAsync(Reactor reactor)
        {
            await new ReactorScenario(new TenantId(TenantId.ToString())).RunAsync(reactor);
            var identity = new CheckpointIdentity(reactor.Name, reactor.Pattern);
            var checkpoint = await Cuts.LoadAsync(identity);
            var runner = new ReactorRunner(Store);
            while (true)
            {
                var next = await runner.RunAsync(reactor, checkpoint);
                if (next == checkpoint)
                    return;
                checkpoint = next;
            }
        }
        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class InterruptedStore(string eventType, bool committed) : IEventStore
    {
        readonly InMemoryEventStore _inner = new();
        public bool Armed { get; set; }
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong offset, CancellationToken ct) =>
            _inner.ReadAsync(stream, offset, ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor, CancellationToken ct) =>
            _inner.ReadAsync(pattern, cursor, ct);
        public async ValueTask AppendAsync(EventStreamAddress stream, ulong expectedStreamPosition,
            IReadOnlyList<DomainEvent> events, CancellationToken ct = default)
        {
            if (Armed && events.Any(ev => ev.GetType().Name == eventType))
            {
                Armed = false;
                if (committed)
                    await _inner.AppendAsync(stream, expectedStreamPosition, events, ct);
                throw new IOException("The membership cleanup append outcome is unknown.");
            }
            await _inner.AppendAsync(stream, expectedStreamPosition, events, ct);
        }
    }

    sealed class InterruptedCoverageStore : IProjectionCheckpointStore
    {
        readonly InMemoryProjectionCheckpointStore _inner = new();
        bool _failed;
        public ValueTask<ProjectionCheckpoint> LoadAsync(CheckpointIdentity identity, CancellationToken ct = default) =>
            _inner.LoadAsync(identity, ct);
        public async ValueTask SaveAsync(CheckpointIdentity identity, ProjectionCheckpoint checkpoint,
            CancellationToken ct = default)
        {
            await _inner.SaveAsync(identity, checkpoint, ct);
            if (!_failed && identity.ComponentName.StartsWith("MemberRetirementCoverage:", StringComparison.Ordinal))
            {
                _failed = true;
                throw new IOException("The coverage checkpoint outcome is unknown.");
            }
        }
    }

    sealed class AppendAfterSnapshotStore : IEventStore
    {
        readonly InMemoryEventStore _inner = new();
        public Func<Task>? AppendAfterSnapshot { get; set; }
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong offset, CancellationToken ct) =>
            _inner.ReadAsync(stream, offset, ct);
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var record in _inner.ReadAsync(pattern, cursor, ct))
                yield return record;
            if (pattern == EventStreamPattern.ForPattern(TenantId.ToString(), "rbac-team-members") &&
                AppendAfterSnapshot is { } append)
            {
                AppendAfterSnapshot = null;
                await append();
            }
        }
        public ValueTask AppendAsync(EventStreamAddress stream, ulong expectedStreamPosition,
            IReadOnlyList<DomainEvent> events, CancellationToken ct = default) =>
            _inner.AppendAsync(stream, expectedStreamPosition, events, ct);
    }
}
