using System.Globalization;
using System.Security.Claims;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class TeamMemberTests
{
    static readonly Uuid TenantId = Uuid.Parse("11f455d2-fb10-4f28-a157-23e18e706e70", CultureInfo.InvariantCulture);
    static readonly Uuid TeamId = Uuid.Parse("2cc8e854-a49a-42a1-9581-d0622de3d5c3", CultureInfo.InvariantCulture);
    static readonly Uuid MemberId = Uuid.Parse("0862062f-97e9-45de-a312-0f884c48180d", CultureInfo.InvariantCulture);
    static readonly Uuid UserId = Uuid.Parse("a1f455d2-fb10-4f28-a157-23e18e706e71", CultureInfo.InvariantCulture);
    static readonly DateTimeOffset ActionAt = new(2026, 9, 29, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRejectReplacingActiveAuthorityGivenAnotherMembershipEpisode()
    {
        // Arrange
        var originalEpisode = Uuid.CreateVersion4();
        var owner = new TeamMember(TenantId, TeamId, MemberId);
        var scenario = new AggregateScenario<TeamMember>(owner).Given(DomainEventSeed.Attach(
            TeamMemberFacts.Assigned(TenantId, TeamId, MemberId, originalEpisode), owner.Id, 1));

        // Act
        var result = owner.Assign(Uuid.CreateVersion4());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.Equal(originalEpisode, owner.MembershipEpisodeId);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldReportAlreadyAssignedGivenSecondAssignmentForTheSameEpisode()
    {
        // Arrange
        var episode = Uuid.CreateVersion4();
        var teamMember = new TeamMember(TenantId, TeamId, MemberId);
        var scenario = new AggregateScenario<TeamMember>(teamMember);
        var first = scenario.Aggregate.Assign(
            TeamMemberFacts.Provenance(teamMember, Uuid.CreateVersion4(), "AssignTeamMember"),
            TeamMemberFacts.FirstResponse(), episode, 0, 0);

        // Act
        var second = scenario.Aggregate.Assign(
            TeamMemberFacts.Provenance(teamMember, Uuid.CreateVersion4(), "AssignTeamMember"),
            TeamMemberFacts.FirstResponse(), episode, -1, -1);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.Equal(TeamMember.AlreadyAssignedConflict, second.Error);
        var assigned = Assert.Single(scenario.PendingEvents);
        Assert.Equal("TeamMemberAssigned", assigned.GetType().Name);
    }

    [Fact]
    public void ShouldRemoveMemberGivenExistingAssignment()
    {
        // Arrange
        var teamMember = new TeamMember(TenantId, TeamId, MemberId);
        var scenario = new AggregateScenario<TeamMember>(teamMember)
            .Given(DomainEventSeed.Attach(TeamMemberFacts.Assigned(TenantId, TeamId, MemberId, Uuid.CreateVersion4()), teamMember.Id, 1));

        // Act
        var result = scenario.Aggregate.Remove();

        // Assert
        Assert.True(result.IsSuccess);
        var removed = Assert.Single(scenario.PendingEvents);
        Assert.Equal("TeamMemberRemoved", removed.GetType().Name);
    }

    [Fact]
    public void ShouldReturnNotFoundGivenAnUnassignedMember()
    {
        // Arrange
        var teamMember = new TeamMember(TenantId, TeamId, MemberId);

        // Act
        var result = teamMember.Remove();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error.Kind);
    }

    [Fact]
    public void ShouldReturnNotFoundGivenARepeatedRemoval()
    {
        // Arrange
        var teamMember = new TeamMember(TenantId, TeamId, MemberId);
        var assignment = DomainEventSeed.Attach(TeamMemberFacts.Assigned(TenantId, TeamId, MemberId, Uuid.CreateVersion4()), teamMember.Id, 1);
        var scenario = new AggregateScenario<TeamMember>(teamMember)
            .Given(assignment,
                DomainEventSeed.Attach(TeamMemberFacts.Removed(TenantId, TeamId, MemberId,
                    assignment.MembershipEpisodeId, assignment.Metadata.EventId), teamMember.Id, 2));

        // Act
        var result = scenario.Aggregate.Remove();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error.Kind);
    }

    [Fact]
    public void ShouldAllowRejoiningGivenPriorDeparture()
    {
        // Arrange
        var teamMember = new TeamMember(TenantId, TeamId, MemberId);
        var assignment = DomainEventSeed.Attach(TeamMemberFacts.Assigned(TenantId, TeamId, MemberId, Uuid.CreateVersion4()), teamMember.Id, 1);
        var scenario = new AggregateScenario<TeamMember>(teamMember)
            .Given(assignment,
                DomainEventSeed.Attach(TeamMemberFacts.Removed(TenantId, TeamId, MemberId,
                    assignment.MembershipEpisodeId, assignment.Metadata.EventId), teamMember.Id, 2));

        // Act
        var result = scenario.Aggregate.Assign();

        // Assert
        Assert.True(result.IsSuccess);
        var rejoined = Assert.Single(scenario.PendingEvents);
        Assert.Equal("TeamMemberAssigned", rejoined.GetType().Name);
    }

    [Fact]
    public void ShouldRecordNewEpisodeGivenAssignmentFromPriorMembershipEpisode()
    {
        // Arrange
        var firstEpisodeId = Uuid.CreateVersion4();
        var nextEpisodeId = Uuid.CreateVersion4();
        var teamMember = new TeamMember(TenantId, TeamId, MemberId);
        var assigned = DomainEventSeed.Attach(TeamMemberFacts.Assigned(TenantId, TeamId, MemberId,
            firstEpisodeId), teamMember.Id, 1);
        var scenario = new AggregateScenario<TeamMember>(teamMember).Given(assigned,
            DomainEventSeed.Attach(TeamMemberFacts.Removed(TenantId, TeamId, MemberId, firstEpisodeId,
                assigned.Metadata.EventId), teamMember.Id, 2));

        // Act
        var result = scenario.Aggregate.Assign(nextEpisodeId);

        // Assert
        Assert.True(result.IsSuccess);
        var reassigned = Assert.IsType<TeamMemberAssigned>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(nextEpisodeId, reassigned.MembershipEpisodeId);
        Assert.Equal(nextEpisodeId, scenario.Aggregate.MembershipEpisodeId);
    }

    [Fact]
    public async Task ShouldAssignDirectlyAndRespondCompletedGivenNonAdministratorTeam()
    {
        // Arrange
        await using var provider = Services();
        var episode = await RegisterAsync(provider);
        var request = Assignment(Uuid.CreateVersion4(), episode);

        // Act
        var result = await AssignAsync(provider, request);
        var owner = await provider.GetRequiredService<IAggregateReader>().HydrateAsync(Owner(request.CommandId));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(request.CommandId, result.Value!.OperationId);
        Assert.Equal(AdministratorTransitionResponseState.Completed, result.Value.State);
        Assert.Null(result.Value.StatusUrl);
        Assert.True(owner.IsAssigned);
        Assert.Equal(episode, owner.MembershipEpisodeId);
        Assert.Equal(1UL, owner.AssignmentGeneration);
        Assert.Equal(1UL, owner.AssignmentRevision);
    }

    [Fact]
    public async Task ShouldAppendNothingAndRepeatResponseGivenExactAssignmentRetry()
    {
        // Arrange
        await using var provider = Services();
        var episode = await RegisterAsync(provider);
        var request = Assignment(Uuid.CreateVersion4(), episode);
        var first = await AssignAsync(provider, request);

        // Act
        var retry = await AssignAsync(provider, request);
        var history = await HistoryAsync(provider);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(first.Value, retry.Value);
        Assert.IsType<TeamMemberAssigned>(Assert.Single(history));
    }

    [Theory]
    [InlineData("revision")]
    [InlineData("generation")]
    [InlineData("episode")]
    [InlineData("actor")]
    public async Task ShouldConflictGivenSameAssignmentIdWithChangedIntent(string change)
    {
        // Arrange
        await using var provider = Services();
        var episode = await RegisterAsync(provider);
        var original = Assignment(Uuid.CreateVersion4(), episode);
        Assert.True((await AssignAsync(provider, original)).IsSuccess);
        var changed = change switch
        {
            "revision" => original with { ExpectedAssignmentRevision = 5 },
            "generation" => original with { ExpectedAssignmentGeneration = 5 },
            "episode" => original with { MemberEpisodeId = Uuid.CreateVersion4() },
            _ => original,
        };
        var actor = change == "actor" ? Caller(Uuid.CreateVersion4()) : null;

        // Act
        var result = await AssignAsync(provider, changed, actor);
        var history = await HistoryAsync(provider);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Single(history);
    }

    [Fact]
    public async Task ShouldNotAssignNewEpisodeGivenExactOldAssignmentRetryAfterReRegistration()
    {
        // Arrange
        await using var provider = Services();
        var oldEpisode = await RegisterAsync(provider);
        var request = Assignment(Uuid.CreateVersion4(), oldEpisode);
        var first = await AssignAsync(provider, request);
        var newEpisode = await ReRegisterAsync(provider, oldEpisode);

        // Act
        var retry = await AssignAsync(provider, request);
        var owner = await provider.GetRequiredService<IAggregateReader>().HydrateAsync(Owner());

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(first.Value, retry.Value);
        Assert.NotEqual(newEpisode, oldEpisode);
        Assert.False(owner.IsAssigned);
        Assert.DoesNotContain(await HistoryAsync(provider), fact => fact is TeamMemberAssigned assigned &&
            assigned.MembershipEpisodeId == newEpisode);
    }

    [Fact]
    public async Task ShouldConflictGivenUnacceptedOldEpisodeAssignmentAfterReRegistration()
    {
        // Arrange
        await using var provider = Services();
        var oldEpisode = await RegisterAsync(provider);
        var newEpisode = await ReRegisterAsync(provider, oldEpisode);
        var oldRetry = Assignment(Uuid.CreateVersion4(), oldEpisode);

        // Act
        var result = await AssignAsync(provider, oldRetry);
        var history = await HistoryAsync(provider);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(history);
        Assert.NotEqual(newEpisode, oldEpisode);
    }

    [Fact]
    public async Task ShouldConflictGivenStaleExpectedAssignmentGeneration()
    {
        // Arrange
        await using var provider = Services();
        var episode = await RegisterAsync(provider);
        Assert.True((await AssignAsync(provider, Assignment(Uuid.CreateVersion4(), episode))).IsSuccess);
        Assert.True((await RemoveAsync(provider, Removal(Uuid.CreateVersion4(), episode, 1, 1))).IsSuccess);

        // Act
        var stale = await AssignAsync(provider, Assignment(Uuid.CreateVersion4(), episode));
        var current = await AssignAsync(provider, Assignment(Uuid.CreateVersion4(), episode, 2, 1));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(stale.Error).Kind);
        Assert.True(current.IsSuccess);
    }

    [Fact]
    public async Task ShouldAppendDirectlyWithoutGuardReadGivenNonAdministratorTeamRemoval()
    {
        // Arrange
        await using var provider = Services();
        var episode = await RegisterAsync(provider);
        Assert.True((await AssignAsync(provider, Assignment(Uuid.CreateVersion4(), episode))).IsSuccess);
        var request = Removal(Uuid.CreateVersion4(), episode, 1, 1);

        // Act
        var result = await RemoveAsync(provider, request);
        var owner = await provider.GetRequiredService<IAggregateReader>().HydrateAsync(Owner(request.CommandId));
        var history = await HistoryAsync(provider);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(AdministratorTransitionResponseState.Completed, result.Value!.State);
        Assert.False(owner.IsAssigned);
        Assert.IsType<TeamMemberRemoved>(history[^1]);
        Assert.Equal(request.CommandId, owner.Acceptance.SelectedOperation!.Operation.LogicalId);
    }

    [Fact]
    public async Task ShouldAppendNothingGivenExactRemovalRetry()
    {
        // Arrange
        await using var provider = Services();
        var episode = await RegisterAsync(provider);
        Assert.True((await AssignAsync(provider, Assignment(Uuid.CreateVersion4(), episode))).IsSuccess);
        var request = Removal(Uuid.CreateVersion4(), episode, 1, 1);
        var first = await RemoveAsync(provider, request);

        // Act
        var retry = await RemoveAsync(provider, request);
        var changed = await RemoveAsync(provider, request with { ExpectedAssignmentRevision = 9 });

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(first.Value, retry.Value);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changed.Error).Kind);
        Assert.Equal(2, (await HistoryAsync(provider)).Count);
    }

    [Fact]
    public async Task ShouldRejectRemovalGivenAnotherEpisodeOrStaleExpectation()
    {
        // Arrange
        await using var provider = Services();
        var episode = await RegisterAsync(provider);
        Assert.True((await AssignAsync(provider, Assignment(Uuid.CreateVersion4(), episode))).IsSuccess);

        // Act
        var otherEpisode = await RemoveAsync(provider, Removal(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, 1));
        var staleRevision = await RemoveAsync(provider, Removal(Uuid.CreateVersion4(), episode, 0, 1));
        var staleGeneration = await RemoveAsync(provider, Removal(Uuid.CreateVersion4(), episode, 1, 0));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(otherEpisode.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(staleRevision.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(staleGeneration.Error).Kind);
        Assert.Single(await HistoryAsync(provider));
    }

    [Theory]
    [InlineData("command")]
    [InlineData("tenant")]
    [InlineData("team")]
    [InlineData("member")]
    [InlineData("episode")]
    public async Task ShouldRejectBeforeAnythingGivenEmptyIdentifier(string empty)
    {
        // Arrange
        await using var provider = Services();
        var episode = await RegisterAsync(provider);
        var assign = Assignment(Uuid.CreateVersion4(), episode);
        var remove = Removal(Uuid.CreateVersion4(), episode, 0, 0);
        var user = Caller(Uuid.CreateVersion4());
        (assign, remove) = empty switch
        {
            "command" => (assign with { CommandId = Uuid.Empty }, remove with { CommandId = Uuid.Empty }),
            "tenant" => (assign with { TenantId = Uuid.Empty }, remove with { TenantId = Uuid.Empty }),
            "team" => (assign with { TeamId = Uuid.Empty }, remove with { TeamId = Uuid.Empty }),
            "member" => (assign with { MemberId = Uuid.Empty }, remove with { MemberId = Uuid.Empty }),
            _ => (assign with { MemberEpisodeId = Uuid.Empty }, remove with { MemberEpisodeId = Uuid.Empty }),
        };

        // Act
        var assigned = await AssignAsync(provider, assign, user);
        var removed = await RemoveAsync(provider, remove, user);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(assigned.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(removed.Error).Kind);
        Assert.Empty(await HistoryAsync(provider));
    }

    [Fact]
    public async Task ShouldRejectNegativeExpectationsGivenUserCaller()
    {
        // Arrange
        await using var provider = Services();
        var episode = await RegisterAsync(provider);

        // Act
        var result = await AssignAsync(provider, Assignment(Uuid.CreateVersion4(), episode, -1, -1),
            Caller(Uuid.CreateVersion4()));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldBindToCurrentEpisodeGivenSystemAssignmentWithoutEpisode()
    {
        // Arrange
        await using var provider = Services();
        var episode = await RegisterAsync(provider);
        var effect = TeamMemberEffects.Assign(Uuid.CreateVersion4(), TenantId, TeamId, Member());

        // Act
        var result = await AssignAsync(provider, effect);
        var retry = await AssignAsync(provider, effect);
        var owner = await provider.GetRequiredService<IAggregateReader>().HydrateAsync(Owner());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(result.Value, retry.Value);
        Assert.Equal(episode, owner.MembershipEpisodeId);
        Assert.Single(await HistoryAsync(provider));
    }

    [Fact]
    public async Task ShouldRecognizeAcceptedIntentGivenDurableRoundTrip()
    {
        // Arrange
        await using var provider = Services();
        var episode = await RegisterAsync(provider);
        var request = Assignment(Uuid.CreateVersion4(), episode);
        Assert.True((await AssignAsync(provider, request)).IsSuccess);
        var original = Assert.IsType<TeamMemberAssigned>(Assert.Single(await HistoryAsync(provider)));
        await using var persistence = EventPersistenceFrameTests.Compose();
        var serializer = persistence.GetRequiredService<IDomainEventSerializer>();
        var owner = Owner(request.CommandId);
        var restored = serializer.Deserialize(serializer.Serialize(original));
        var scenario = new AggregateScenario<TeamMember>(owner).Given(restored);

        // Act
        var decision = owner.Acceptance.Decide(AcceptedOperationBinding.Of(AssignTeamMemberHandler.Provenance(
            request, ActorReference.ForSystemProcess("system", "system"), ActionAt)));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Replayed, decision);
        Assert.Empty(scenario.PendingEvents);
        Assert.Equal(episode, owner.MembershipEpisodeId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldResolveAcceptanceWithoutWritingContentGivenTeamOperationRetry(bool remove, bool changed)
    {
        // Arrange
        TeamContentStore content = null!;
        await using var provider = Services(inner => content = new TeamContentStore(inner));
        var episode = await RegisterAsync(provider);
        var assign = Assignment(Uuid.CreateVersion4(), episode);
        var assignment = await AssignAsync(provider, assign);
        Assert.True(assignment.IsSuccess);
        var removal = Removal(Uuid.CreateVersion4(), episode, 1, 1);
        var first = remove ? await RemoveAsync(provider, removal) : assignment;
        Assert.True(first.IsSuccess);
        var writes = content.WriteAttempts;
        content.RejectWrites = true;

        // Act
        var retry = remove
            ? await RemoveAsync(provider, changed ? removal with { ExpectedAssignmentRevision = 9 } : removal)
            : await AssignAsync(provider, changed ? assign with { ExpectedAssignmentRevision = 9 } : assign);

        // Assert
        if (changed)
            Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(retry.Error).Kind);
        else
        {
            Assert.True(retry.IsSuccess);
            Assert.Equal(first.Value, retry.Value);
        }
        Assert.Equal(writes, content.WriteAttempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectBeforeWritingContentGivenStaleTeamOperation(bool remove)
    {
        // Arrange
        TeamContentStore content = null!;
        await using var provider = Services(inner => content = new TeamContentStore(inner));
        var episode = await RegisterAsync(provider);
        content.RejectWrites = true;

        // Act
        var result = remove
            ? await RemoveAsync(provider, Removal(Uuid.CreateVersion4(), episode, 1, 1))
            : await AssignAsync(provider, Assignment(Uuid.CreateVersion4(), episode, 1, 1));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(0, content.WriteAttempts);
        Assert.Empty(await HistoryAsync(provider));
    }

    [Fact]
    public async Task ShouldRepeatFrozenResponseGivenMemberRetirementAfterAssignmentAppend()
    {
        // Arrange
        await using var provider = Services();
        var episode = await RegisterAsync(provider);
        var request = Assignment(Uuid.CreateVersion4(), episode);
        var executor = provider.GetRequiredService<IAggregateExecutor>();

        // Act
        var first = await AssignAsync(provider, request, configureExecutor: inner => new AfterCommitExecutor(inner,
            async () => Assert.True((await executor.ExecuteAsync(new Member(TenantId, UserId), member =>
                AggregateOutcome.CommitOnSuccess(member.Deprovision(MemberId, "Alex Admin", ActionAt,
                    "Access ended.")), new RequestDispatchContext(RequestActor.System), CancellationToken.None)).IsSuccess)));
        var retry = await AssignAsync(provider, request);
        var assignment = await provider.GetRequiredService<IAggregateReader>().HydrateAsync(Owner());

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(first.Value, retry.Value);
        Assert.False(assignment.IsAssigned);
        Assert.Collection(await HistoryAsync(provider),
            fact => Assert.IsType<TeamMemberAssigned>(fact),
            fact => Assert.IsType<TeamMemberRemoved>(fact));
    }

    static Uuid Member() => RbacIds.Member(TenantId, UserId);

    static TeamMember Owner(Uuid selected = default) => new(TenantId, TeamId, Member(), selected);

    static AssignTeamMember Assignment(Uuid commandId, Uuid episode, long revision = 0, long generation = 0) =>
        new(commandId, TenantId, TeamId, Member(), episode, revision, generation);

    static RemoveTeamMember Removal(Uuid commandId, Uuid episode, long revision, long generation) =>
        new(commandId, TenantId, TeamId, Member(), episode, revision, generation);

    static ServiceProvider Services(Func<IImmutableContentStore, IImmutableContentStore>? configureContent = null)
    {
        var services = new ServiceCollection();
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IProjectionCheckpointStore, InMemoryProjectionCheckpointStore>();
        IImmutableContentStore content = new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
        services.AddSingleton(configureContent?.Invoke(content) ?? content);
        services.AddPortia();
        return services.BuildServiceProvider();
    }

    static async Task<Uuid> RegisterAsync(IServiceProvider provider, Uuid commandId = default)
    {
        var id = commandId == Uuid.Empty ? Uuid.CreateVersion4() : commandId;
        Assert.True((await provider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(new Member(TenantId, UserId),
            member => AggregateOutcome.CommitOnSuccess(member.Register(commandId: id)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None)).IsSuccess);
        return id;
    }

    /// <summary>Retires the episode, closes its team assignment as retirement does, and registers a new episode.</summary>
    static async Task<Uuid> ReRegisterAsync(IServiceProvider provider, Uuid episode)
    {
        var reader = provider.GetRequiredService<IAggregateReader>();
        var executor = provider.GetRequiredService<IAggregateExecutor>();
        var system = new RequestDispatchContext(RequestActor.System);
        Assert.True((await executor.ExecuteAsync(new Member(TenantId, UserId), member =>
            AggregateOutcome.CommitOnSuccess(member.Deprovision(MemberId, "Alex Admin", ActionAt, "Access ended.")),
            system, CancellationToken.None)).IsSuccess);
        var proof = Assert.IsType<MembershipTerminationProof>(
            (await reader.HydrateAsync(new Member(TenantId, UserId))).TerminationProof);
        var assignment = await reader.HydrateAsync(Owner());
        if (assignment.IsAssigned)
            Assert.True((await executor.ExecuteAsync(Owner(), owner => AggregateOutcome.CommitOnSuccess(
                owner.RemoveForRetirement(proof, assignment.AssignmentEventId, Uuid.CreateVersion4(), ActionAt)),
                system, CancellationToken.None)).IsSuccess);
        Assert.True((await executor.ExecuteAsync(new Member(TenantId, UserId, proof.MembershipEpisodeId), member =>
            AggregateOutcome.CommitOnSuccess(member.CompleteDeprovisionCleanup(proof,
                MemberRetirementEffects.Complete(proof).CommandId, ActionAt.AddMinutes(1))),
            system, CancellationToken.None)).IsSuccess);
        return await RegisterAsync(provider);
    }

    static async Task<Result<AdministratorTransitionResponse>> AssignAsync(IServiceProvider provider,
        AssignTeamMember request, ClaimsPrincipal? actor = null,
        Func<IAggregateExecutor, IAggregateExecutor>? configureExecutor = null)
    {
        var reader = provider.GetRequiredService<IAggregateReader>();
        var inner = provider.GetRequiredService<IAggregateExecutor>();
        var executor = configureExecutor?.Invoke(inner) ?? inner;
        var content = provider.GetRequiredService<IImmutableContentStore>();
        var cleanup = new MemberAuthorityCleanup(reader, executor, provider.GetRequiredService<IDomainEventReader>(),
            provider.GetRequiredService<IProjectionCheckpointStore>(), TimeProvider.System);
        // The guard is deliberately absent: a non-administrator team must never reach it.
        var handler = new AssignTeamMemberHandler(new NativeOperationExecutor(reader, executor, content),
            null!, reader, cleanup, content, TimeProvider.System);
        return await handler.HandleAsync(new RequestContext<AssignTeamMember>(request, actor ?? RequestActor.System),
            CancellationToken.None);
    }

    static async Task<Result<AdministratorTransitionResponse>> RemoveAsync(IServiceProvider provider,
        RemoveTeamMember request, ClaimsPrincipal? actor = null)
    {
        var reader = provider.GetRequiredService<IAggregateReader>();
        var executor = provider.GetRequiredService<IAggregateExecutor>();
        var content = provider.GetRequiredService<IImmutableContentStore>();
        // The guard is deliberately absent: a non-administrator team must never reach it.
        var handler = new RemoveTeamMemberHandler(new NativeOperationExecutor(reader, executor, content),
            null!, reader, content, TimeProvider.System);
        return await handler.HandleAsync(new RequestContext<RemoveTeamMember>(request, actor ?? RequestActor.System),
            CancellationToken.None);
    }

    static async Task<List<DomainEvent>> HistoryAsync(IServiceProvider provider)
    {
        var history = new List<DomainEvent>();
        try
        {
            await foreach (var record in provider.GetRequiredService<IDomainEventReader>()
                               .ReadAsync(Owner().Stream, 0))
                history.Add(record.Event);
        }
        catch (InvalidOperationException)
        {
            // An unknown stream reads as empty.
        }

        return history;
    }

    static ClaimsPrincipal Caller(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

    sealed class TeamContentStore(IImmutableContentStore inner) : IImmutableContentStore
    {
        public bool RejectWrites { get; set; }
        public int WriteAttempts { get; private set; }

        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            WriteAttempts++;
            if (RejectWrites)
                throw new InvalidOperationException("Content writes are unavailable.");
            return inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        }

        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => inner.ReadVerifiedAsync(realm, reference, ct);
    }

    sealed class AfterCommitExecutor(IAggregateExecutor inner, Func<Task> competitor) : IAggregateExecutor
    {
        int _calls;

        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate => inner.ExecuteAsync(aggregate, operation, context, ct);

        public async ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate
        {
            var result = await inner.ExecuteAsync(aggregate, operation, context, ct);
            if (Interlocked.Increment(ref _calls) == 1)
                await competitor();
            return result;
        }
    }

}
