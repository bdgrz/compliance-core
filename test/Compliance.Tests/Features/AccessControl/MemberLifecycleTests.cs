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

public sealed class MemberLifecycleTests
{
    static readonly Uuid TenantId = Uuid.Parse("11f455d2-fb10-4f28-a157-23e18e706e70", CultureInfo.InvariantCulture);
    static readonly Uuid UserId = Uuid.Parse("0862062f-97e9-45de-a312-0f884c48180d", CultureInfo.InvariantCulture);
    static readonly Uuid ActorMemberId = Uuid.Parse("2cc8e854-a49a-42a1-9581-d0622de3d5c3", CultureInfo.InvariantCulture);
    static readonly DateTimeOffset ActionAt = new(2026, 9, 29, 14, 0, 0, TimeSpan.Zero);
    static readonly DateOnly GuestExpiresOn = new(2026, 11, 30);

    [Theory]
    [InlineData(false, "tenant")]
    [InlineData(false, "member")]
    [InlineData(false, "user")]
    [InlineData(true, "tenant")]
    [InlineData(true, "member")]
    [InlineData(true, "user")]
    public void ShouldRejectLifecycleFactGivenDifferentMemberIdentityOnReplay(bool reinstating, string field)
    {
        // Arrange
        var member = new Member(TenantId, UserId);
        var facts = new List<DomainEvent>
        {
            DomainEventSeed.Attach(MemberFacts.Registered(TenantId, member.Id, UserId,
                membershipEpisodeId: Uuid.CreateVersion4()), member.Id, 1),
        };
        var suspension = MemberFacts.Suspended(TenantId, member.Id, UserId, ActorMemberId, "Admin", ActionAt, "Reason");
        DomainEvent foreign;
        if (reinstating)
        {
            facts.Add(DomainEventSeed.Attach(suspension, member.Id, 2));
            var reinstatement = MemberFacts.Reinstated(TenantId, member.Id, UserId, "client_personnel", ActorMemberId, "Admin", ActionAt);
            foreign = field switch
            {
                "tenant" => reinstatement with { TenantId = Uuid.CreateVersion4() },
                "member" => reinstatement with { MemberId = Uuid.CreateVersion4() },
                _ => reinstatement with { UserId = Uuid.CreateVersion4() },
            };
        }
        else
            foreign = field switch
            {
                "tenant" => suspension with { TenantId = Uuid.CreateVersion4() },
                "member" => suspension with { MemberId = Uuid.CreateVersion4() },
                _ => suspension with { UserId = Uuid.CreateVersion4() },
            };
        facts.Add(DomainEventSeed.Attach(foreign, member.Id, (ulong)facts.Count + 1));

        // Act
        var error = Record.Exception(() => new AggregateScenario<Member>(member).Given(facts.ToArray()));

        // Assert
        Assert.IsType<InvalidOperationException>(error);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("member")]
    [InlineData("user")]
    public void ShouldRejectRegistrationGivenDifferentMemberIdentityOnReplay(string field)
    {
        // Arrange
        var member = new Member(TenantId, UserId);
        var fact = MemberFacts.Registered(TenantId, member.Id, UserId, membershipEpisodeId: Uuid.CreateVersion4());
        fact = field switch
        {
            "tenant" => fact with { TenantId = Uuid.CreateVersion4() },
            "member" => fact with { MemberId = Uuid.CreateVersion4() },
            _ => fact with { UserId = Uuid.CreateVersion4() },
        };
        var stored = DomainEventSeed.Attach(fact, member.Id, 1);

        // Act
        var error = Record.Exception(() => new AggregateScenario<Member>(member).Given(stored));

        // Assert
        Assert.IsType<InvalidOperationException>(error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldRejectAnotherMembershipEpisodeGivenLiveOrUncleanedEpisodeOnReplay(bool deprovisioned)
    {
        // Arrange
        var member = new Member(TenantId, UserId);
        var firstEpisode = Uuid.CreateVersion4();
        var facts = new List<DomainEvent>
        {
            DomainEventSeed.Attach(MemberFacts.Registered(TenantId, member.Id, UserId,
                membershipEpisodeId: firstEpisode), member.Id, 1),
        };
        if (deprovisioned)
            facts.Add(DomainEventSeed.Attach(MemberFacts.Deprovisioned(TenantId, member.Id, UserId,
                ActorMemberId, "Alex Admin", ActionAt, "Access ended", firstEpisode), member.Id, 2));
        facts.Add(DomainEventSeed.Attach(MemberFacts.Registered(TenantId, member.Id, UserId,
            membershipEpisodeId: Uuid.CreateVersion4()), member.Id, (ulong)facts.Count + 1));

        // Act
        var error = Record.Exception(() => new AggregateScenario<Member>(member).Given(facts.ToArray()));

        // Assert
        Assert.IsType<InvalidOperationException>(error);
    }

    [Fact]
    public void ShouldRegisterGuestGivenExternalConsultantMembership()
    {
        // Arrange
        var member = new Member(TenantId, UserId);
        var scenario = new AggregateScenario<Member>(member);

        // Act
        var result = MemberFacts.Register(scenario.Aggregate, "guest", GuestExpiresOn);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("guest", scenario.Aggregate.Affiliation);
        Assert.Equal(GuestExpiresOn, scenario.Aggregate.MembershipExpiresOn);
        var registered = Assert.IsType<MemberRegistered>(Assert.Single(scenario.PendingEvents));
        Assert.Equal("guest", registered.Affiliation);
        Assert.Equal(GuestExpiresOn, registered.MembershipExpiresOn);
    }

    [Fact]
    public void ShouldRequireEndDateGivenGuestMembership()
    {
        // Arrange
        var member = new Member(TenantId, UserId);
        var scenario = new AggregateScenario<Member>(member);

        // Act
        var result = MemberFacts.Register(scenario.Aggregate, "guest");

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRecordAttributedSuspensionGivenRegisteredMember()
    {
        // Arrange
        var member = new Member(TenantId, UserId);
        var scenario = new AggregateScenario<Member>(member)
            .Given(DomainEventSeed.Attach(MemberFacts.Registered(TenantId, member.Id, UserId,
                membershipEpisodeId: Uuid.CreateVersion4()), member.Id, 1));

        // Act
        var result = scenario.Aggregate.Suspend(ActorMemberId, "Alex Admin", ActionAt,
            "Employment ended.");

        // Assert
        Assert.True(result.IsSuccess);
        var suspended = Assert.IsType<MemberSuspended>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(TenantId, suspended.TenantId);
        Assert.Equal(member.Id, suspended.MemberId);
        Assert.Equal(UserId, suspended.UserId);
        Assert.Equal(ActorMemberId, suspended.SuspendedByMemberId);
        Assert.Equal("Alex Admin", suspended.SuspendedByDisplay);
        Assert.Equal(ActionAt, suspended.SuspendedAt);
        Assert.Equal("Employment ended.", suspended.Reason);
    }

    [Fact]
    public void ShouldRecordAttributedReinstatementGivenSuspendedMember()
    {
        // Arrange
        var member = new Member(TenantId, UserId);
        var scenario = new AggregateScenario<Member>(member).Given(
            DomainEventSeed.Attach(MemberFacts.Registered(TenantId, member.Id, UserId,
                membershipEpisodeId: Uuid.CreateVersion4()), member.Id, 1),
            DomainEventSeed.Attach(MemberFacts.Suspended(TenantId, member.Id, UserId, ActorMemberId,
                "Alex Admin", ActionAt, "Employment ended."), member.Id, 2));

        // Act
        var result = scenario.Aggregate.Reinstate(ActorMemberId, "Alex Admin", ActionAt.AddHours(1));

        // Assert
        Assert.True(result.IsSuccess);
        var reinstated = Assert.IsType<MemberReinstated>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(TenantId, reinstated.TenantId);
        Assert.Equal(member.Id, reinstated.MemberId);
        Assert.Equal(UserId, reinstated.UserId);
        Assert.Equal("client_personnel", reinstated.Affiliation);
        Assert.Equal(ActorMemberId, reinstated.ReinstatedByMemberId);
        Assert.Equal("Alex Admin", reinstated.ReinstatedByDisplay);
        Assert.Equal(ActionAt.AddHours(1), reinstated.ReinstatedAt);
    }

    [Fact]
    public void ShouldTerminateMembershipEpisodeGivenRegisteredMember()
    {
        // Arrange
        var member = new Member(TenantId, UserId);
        var scenario = new AggregateScenario<Member>(member)
            .Given(DomainEventSeed.Attach(MemberFacts.Registered(TenantId, member.Id, UserId,
                membershipEpisodeId: Uuid.CreateVersion4()), member.Id, 1));

        // Act
        var result = scenario.Aggregate.Deprovision(ActorMemberId, "Alex Admin", ActionAt,
            "Access is no longer required.");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(scenario.Aggregate.IsRegistered);
        Assert.True(scenario.Aggregate.IsDeprovisioned);
        var deprovisioned = Assert.IsType<MemberDeprovisioned>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(TenantId, deprovisioned.TenantId);
        Assert.Equal(member.Id, deprovisioned.MemberId);
        Assert.Equal(UserId, deprovisioned.UserId);
        Assert.Equal(ActorMemberId, deprovisioned.DeprovisionedByMemberId);
        Assert.Equal("Alex Admin", deprovisioned.DeprovisionedByDisplay);
        Assert.Equal(ActionAt, deprovisioned.DeprovisionedAt);
        Assert.Equal("Access is no longer required.", deprovisioned.Reason);
    }

    [Fact]
    public void ShouldStartFreshMembershipEpisodeGivenDeprovisionedMember()
    {
        // Arrange
        var member = new Member(TenantId, UserId);
        var priorEpisodeId = Uuid.CreateVersion4();
        var termination = DomainEventSeed.Attach(MemberFacts.Deprovisioned(TenantId, member.Id, UserId,
            ActorMemberId, "Alex Admin", ActionAt, "Access is no longer required.", priorEpisodeId), member.Id, 2);
        var scenario = new AggregateScenario<Member>(member).Given(
            DomainEventSeed.Attach(MemberFacts.Registered(TenantId, member.Id, UserId,
                "client_personnel", priorEpisodeId), member.Id, 1),
            termination,
            DomainEventSeed.Attach(new MemberDeprovisionCleanupCompleted(TenantId, member.Id,
                priorEpisodeId, termination.Metadata.EventId,
                MemberRetirementEffects.CompletionId(termination.Metadata.EventId, member.Id, priorEpisodeId),
                ActionAt.AddMinutes(1)), member.Id, 3));

        // Act
        var result = MemberFacts.Register(scenario.Aggregate, "firm_staff");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(scenario.Aggregate.IsRegistered);
        Assert.False(scenario.Aggregate.IsSuspended);
        Assert.False(scenario.Aggregate.IsDeprovisioned);
        Assert.Equal("firm_staff", scenario.Aggregate.Affiliation);
        Assert.NotEqual(priorEpisodeId, scenario.Aggregate.MembershipEpisodeId);
        Assert.IsType<MemberRegistered>(Assert.Single(scenario.PendingEvents));
    }

    [Fact]
    public void ShouldNotReinstateGivenDeprovisionedMember()
    {
        // Arrange
        var member = new Member(TenantId, UserId);
        var episodeId = Uuid.CreateVersion4();
        var scenario = new AggregateScenario<Member>(member).Given(
            DomainEventSeed.Attach(MemberFacts.Registered(TenantId, member.Id, UserId,
                membershipEpisodeId: episodeId), member.Id, 1),
            DomainEventSeed.Attach(MemberFacts.Deprovisioned(TenantId, member.Id, UserId,
                ActorMemberId, "Alex Admin", ActionAt, "Access is no longer required.", episodeId), member.Id, 2));

        // Act
        var result = scenario.Aggregate.Reinstate(ActorMemberId, "Alex Admin", ActionAt.AddHours(1));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public async Task ShouldRetainEveryAttributedLifecycleEventGivenRepeatedSuspensionCycles()
    {
        // Arrange
        var services = new ServiceCollection();
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var member = new Member(TenantId, UserId);
        Assert.True(MemberFacts.Register(member).IsSuccess);
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));

        // Act
        member = await reader.HydrateAsync(new Member(TenantId, UserId));
        Assert.True(member.Suspend(ActorMemberId, "Alex Admin", ActionAt,
            "First access review.").IsSuccess);
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        member = await reader.HydrateAsync(new Member(TenantId, UserId));
        Assert.True(member.Reinstate(ActorMemberId, "Alex Admin", ActionAt.AddHours(1)).IsSuccess);
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        member = await reader.HydrateAsync(new Member(TenantId, UserId));
        Assert.True(member.Suspend(ActorMemberId, "Alex Admin", ActionAt.AddHours(2),
            "Second access review.").IsSuccess);
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        var history = new List<DomainEvent>();
        await foreach (var record in events.ReadAsync(new Member(TenantId, UserId).Stream, 0))
            history.Add(record.Event);

        // Assert
        Assert.Collection(history,
            registration => Assert.IsType<MemberRegistered>(registration),
            first =>
            {
                var suspension = Assert.IsType<MemberSuspended>(first);
                Assert.Equal("First access review.", suspension.Reason);
                Assert.Equal(ActorMemberId, suspension.SuspendedByMemberId);
            },
            reinstatement =>
            {
                var restored = Assert.IsType<MemberReinstated>(reinstatement);
                Assert.Equal(ActorMemberId, restored.ReinstatedByMemberId);
            },
            second =>
            {
                var suspension = Assert.IsType<MemberSuspended>(second);
                Assert.Equal("Second access review.", suspension.Reason);
                Assert.Equal(ActionAt.AddHours(2), suspension.SuspendedAt);
            });
        Assert.True((await reader.HydrateAsync(new Member(TenantId, UserId))).IsSuspended);
    }

    [Fact]
    public async Task ShouldPersistOneSuspensionGivenConcurrentMemberWriters()
    {
        // Arrange
        var services = new ServiceCollection();
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var member = new Member(TenantId, UserId);
        Assert.True(MemberFacts.Register(member).IsSuccess);
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        var first = await reader.HydrateAsync(new Member(TenantId, UserId));
        var second = await reader.HydrateAsync(new Member(TenantId, UserId));
        Assert.True(first.Suspend(ActorMemberId, "Alex Admin", ActionAt,
            "First concurrent decision.").IsSuccess);
        Assert.True(second.Suspend(ActorMemberId, "Alex Admin", ActionAt,
            "Second concurrent decision.").IsSuccess);

        // Act
        await writer.SaveAsync(first, new RequestDispatchContext(RequestActor.System));
        await Assert.ThrowsAsync<EventStreamConcurrencyException>(async () =>
            await writer.SaveAsync(second, new RequestDispatchContext(RequestActor.System)));
        var suspensionEvents = new List<MemberSuspended>();
        await foreach (var record in events.ReadAsync(new Member(TenantId, UserId).Stream, 0))
        {
            if (record.Event is MemberSuspended suspended)
                suspensionEvents.Add(suspended);
        }

        // Assert
        Assert.Equal("First concurrent decision.", Assert.Single(suspensionEvents).Reason);
        Assert.True((await reader.HydrateAsync(new Member(TenantId, UserId))).IsSuspended);
    }

    [Fact]
    public async Task ShouldUseCommandIdAsMembershipEpisodeGivenRegistration()
    {
        // Arrange
        await using var provider = RegistrationServices();
        var commandId = Uuid.CreateVersion4();

        // Act
        var result = await HandleRegistrationAsync(provider, new RegisterMember(commandId, TenantId, UserId));
        var history = await HistoryAsync(provider);

        // Assert
        Assert.True(result.IsSuccess);
        var registered = Assert.IsType<MemberRegistered>(Assert.Single(history));
        Assert.Equal(commandId, registered.CommandId);
        Assert.Equal(commandId, registered.MembershipEpisodeId);
        Assert.Equal(commandId, registered.Operation.LogicalId);
        Assert.NotNull(registered.FirstResponse);
        var member = await provider.GetRequiredService<IAggregateReader>().HydrateAsync(new Member(TenantId, UserId));
        Assert.Equal(commandId, member.MembershipEpisodeId);
    }

    [Fact]
    public async Task ShouldReturnValidationWithoutWritingContentGivenInvalidRegistration()
    {
        // Arrange
        RegistrationContentStore? content = null;
        await using var provider = RegistrationServices(store => content = new RegistrationContentStore(store));
        content!.RejectWrites = true;
        var request = new RegisterMember(Uuid.CreateVersion4(), TenantId, UserId, "guest");

        // Act
        var result = await HandleRegistrationAsync(provider, request);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(0, content.WriteAttempts);
        Assert.Empty(await HistoryAsync(provider));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldResolveAcceptedIdentityWithoutWritingContentGivenRegistrationRetry(bool changed)
    {
        // Arrange
        RegistrationContentStore? content = null;
        await using var provider = RegistrationServices(store => content = new RegistrationContentStore(store));
        var request = new RegisterMember(Uuid.CreateVersion4(), TenantId, UserId);
        Assert.True((await HandleRegistrationAsync(provider, request)).IsSuccess);
        var writes = content!.WriteAttempts;
        content.RejectWrites = true;

        // Act
        var retry = await HandleRegistrationAsync(provider, changed ? request with { Affiliation = "firm_staff" } : request);

        // Assert
        if (changed)
            Assert.Equal(RequestErrorKind.Conflict, retry.Error!.Kind);
        else
            Assert.True(retry.IsSuccess);
        Assert.Equal(writes, content.WriteAttempts);
        Assert.Single(await HistoryAsync(provider));
    }

    [Fact]
    public async Task ShouldAppendNothingGivenExactRegisterMemberRetry()
    {
        // Arrange
        await using var provider = RegistrationServices();
        var request = new RegisterMember(Uuid.CreateVersion4(), TenantId, UserId);
        Assert.True((await HandleRegistrationAsync(provider, request)).IsSuccess);

        // Act
        var retry = await HandleRegistrationAsync(provider, request);
        var history = await HistoryAsync(provider);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.IsType<MemberRegistered>(Assert.Single(history));
    }

    [Fact]
    public async Task ShouldReturnOriginalEpisodeGivenExactRegisterMemberRetryAfterDeprovisionAndCleanup()
    {
        // Arrange
        await using var provider = RegistrationServices();
        var reader = provider.GetRequiredService<IAggregateReader>();
        var executor = provider.GetRequiredService<IAggregateExecutor>();
        var request = new RegisterMember(Uuid.CreateVersion4(), TenantId, UserId);
        Assert.True((await HandleRegistrationAsync(provider, request)).IsSuccess);
        Assert.True((await executor.ExecuteAsync(new Member(TenantId, UserId), member =>
            AggregateOutcome.CommitOnSuccess(member.Deprovision(ActorMemberId, "Alex Admin", ActionAt,
                "Access ended.")), new RequestDispatchContext(RequestActor.System), CancellationToken.None)).IsSuccess);
        var proof = Assert.IsType<MembershipTerminationProof>(
            (await reader.HydrateAsync(new Member(TenantId, UserId))).TerminationProof);
        Assert.True((await executor.ExecuteAsync(new Member(TenantId, UserId, proof.MembershipEpisodeId), member =>
            AggregateOutcome.CommitOnSuccess(member.CompleteDeprovisionCleanup(proof,
                MemberRetirementEffects.Complete(proof).CommandId, ActionAt.AddMinutes(1))),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None)).IsSuccess);

        // Act
        var retry = await HandleRegistrationAsync(provider, request);
        var history = await HistoryAsync(provider);
        var member = await reader.HydrateAsync(new Member(TenantId, UserId, Uuid.Empty, request.CommandId));

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(3, history.Count);
        Assert.Single(history.OfType<MemberRegistered>());
        Assert.False(member.IsRegistered);
        Assert.Equal(request.CommandId, member.MembershipEpisodeId);
        Assert.Equal(request.CommandId, member.Acceptance.SelectedOperation!.Operation.LogicalId);
    }

    [Theory]
    [InlineData("affiliation")]
    [InlineData("actor")]
    public async Task ShouldConflictGivenSameCommandIdWithChangedAffiliationOrActor(string change)
    {
        // Arrange
        await using var provider = RegistrationServices();
        var commandId = Uuid.CreateVersion4();
        Assert.True((await HandleRegistrationAsync(provider, new RegisterMember(commandId, TenantId, UserId))).IsSuccess);
        var changed = change == "affiliation"
            ? new RegisterMember(commandId, TenantId, UserId, "firm_staff")
            : new RegisterMember(commandId, TenantId, UserId);
        var actor = change == "actor" ? Caller(Uuid.CreateVersion4()) : RequestActor.System;

        // Act
        var result = await HandleRegistrationAsync(provider, changed, actor);
        var history = await HistoryAsync(provider);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Single(history);
    }

    [Fact]
    public async Task ShouldConflictGivenSameCommandIdWithChangedUserOrActor()
    {
        // Arrange
        await using var provider = RegistrationServices();
        var commandId = Uuid.CreateVersion4();
        var original = new RegisterMember(commandId, TenantId, UserId);
        Assert.True((await HandleRegistrationAsync(provider, original)).IsSuccess);
        var member = await provider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new Member(TenantId, UserId, Uuid.Empty, commandId));
        var system = ActorReference.ForSystemProcess("system", "system");

        // Act
        var userDecision = member.Acceptance.Decide(AcceptedOperationBinding.Of(RegisterMemberHandler.Provenance(
            new RegisterMember(commandId, TenantId, Uuid.CreateVersion4()), system, ActionAt)));
        var actorDecision = member.Acceptance.Decide(AcceptedOperationBinding.Of(RegisterMemberHandler.Provenance(
            original, ActorReference.ForMember(ActorMemberId, "Alex Admin"), ActionAt)));
        var exactDecision = member.Acceptance.Decide(AcceptedOperationBinding.Of(
            RegisterMemberHandler.Provenance(original, system, ActionAt)));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Conflict, userDecision);
        Assert.Equal(NativeAcceptanceDecision.Conflict, actorDecision);
        Assert.Equal(NativeAcceptanceDecision.Replayed, exactDecision);
    }

    [Fact]
    public async Task ShouldRecognizeAcceptedIntentGivenDurableRoundTrip()
    {
        // Arrange
        await using var provider = RegistrationServices();
        var request = new RegisterMember(Uuid.CreateVersion4(), TenantId, UserId);
        Assert.True((await HandleRegistrationAsync(provider, request)).IsSuccess);
        var original = Assert.IsType<MemberRegistered>(Assert.Single(await HistoryAsync(provider)));
        await using var persistence = EventPersistenceFrameTests.Compose();
        var serializer = persistence.GetRequiredService<IDomainEventSerializer>();
        var owner = new Member(TenantId, UserId, Uuid.Empty, request.CommandId);
        var restored = serializer.Deserialize(serializer.Serialize(original));
        var scenario = new AggregateScenario<Member>(owner).Given(restored);

        // Act
        var decision = owner.Acceptance.Decide(AcceptedOperationBinding.Of(RegisterMemberHandler.Provenance(
            request, ActorReference.ForSystemProcess("system", "system"), ActionAt)));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Replayed, decision);
        Assert.Empty(scenario.PendingEvents);
        Assert.Equal(request.CommandId, owner.MembershipEpisodeId);
    }

    static ServiceProvider RegistrationServices(Func<IImmutableContentStore, IImmutableContentStore>? configureContent = null)
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

    static async Task<Result> HandleRegistrationAsync(IServiceProvider provider, RegisterMember request,
        ClaimsPrincipal? actor = null)
    {
        var reader = provider.GetRequiredService<IAggregateReader>();
        var content = provider.GetRequiredService<IImmutableContentStore>();
        var cleanup = new MemberAuthorityCleanup(reader, provider.GetRequiredService<IAggregateExecutor>(),
            provider.GetRequiredService<IDomainEventReader>(), provider.GetRequiredService<IProjectionCheckpointStore>(),
            TimeProvider.System);
        var handler = new RegisterMemberHandler(new NativeOperationExecutor(reader,
            provider.GetRequiredService<IAggregateExecutor>(), content), reader, cleanup, content, TimeProvider.System);
        return await handler.HandleAsync(new RequestContext<RegisterMember>(request, actor ?? RequestActor.System),
            CancellationToken.None);
    }

    static async Task<List<DomainEvent>> HistoryAsync(IServiceProvider provider)
    {
        var history = new List<DomainEvent>();
        try
        {
            await foreach (var record in provider.GetRequiredService<IDomainEventReader>()
                               .ReadAsync(new Member(TenantId, UserId).Stream, 0))
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

    sealed class RegistrationContentStore(IImmutableContentStore inner) : IImmutableContentStore
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
}
