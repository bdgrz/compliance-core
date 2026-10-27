using System.Globalization;
using System.Security.Claims;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Bdgrz.Compliance.Tests.Hosting.Persistence;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.History;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Tenants.Rules;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Handlers;
using Bdgrz.Compliance.Features.Tenants.Queries;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

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
        var roster = await ReadRosterAsync(scope.ServiceProvider);

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
        var roster = await ReadRosterAsync(scope.ServiceProvider);
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
        var native = Given(id, Seeded(First), Granted(id), Revoked(Uuid.CreateVersion4(), id));
        var restored = native.Facts.Select(fact => serializer.Deserialize(serializer.Serialize(fact))).ToArray();
        var scenario = RosterFixture.Restore(id, restored, native.State);
        var owner = scenario.Aggregate;

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
        var initial = await ReadRosterAsync(scope.ServiceProvider);

        // Act
        var results = await Task.WhenAll(RevokeAsync(First, Second), RevokeAsync(Second, First));
        var current = await ReadRosterAsync(scope.ServiceProvider);

        // Assert
        Assert.Single(results, result => result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, Assert.Single(results, result => !result.IsSuccess).Error!.Kind);
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
        var roster = await ReadRosterAsync(scope.ServiceProvider);

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
        var roster = await ReadRosterAsync(scope.ServiceProvider);

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
        var current = await ReadRosterAsync(scope.ServiceProvider);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(boundary == "seed" ? 1UL : boundary == "grant" ? 2UL : 4UL, current.Version);
        Assert.Equal(boundary != "seed", current.IsOperator(Second));
        if (boundary == "revoke")
            Assert.Equal(next, current.AssignmentId(Second));
    }

    [Fact]
    public async Task ShouldKeepAcceptingFactBoundedGivenLargeLegalBootstrapRoster()
    {
        // Arrange
        await using var provider = Services();
        var users = Enumerable.Range(0, 4096).Select(_ => Uuid.CreateVersion4()).ToArray();
        await using var scope = provider.CreateAsyncScope();

        // Act
        var accepted = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
            new SeedPlatformOperatorRoster(PlatformOperatorRoster.CreationId, users), RequestActor.System);
        var facts = new List<DomainEvent>();
        var stream = new PlatformOperatorRoster().Stream;
        await foreach (var record in provider.GetRequiredService<IEventStore>().ReadAsync(stream, 0))
            facts.Add(record.Event);

        // Assert
        Assert.True(accepted.IsSuccess);
        var fact = Assert.IsType<PlatformOperatorRosterSeeded>(Assert.Single(facts));
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(fact, ComplianceCoreJsonContext.Default.PlatformOperatorRosterSeeded);
        Assert.True(bytes.Length <= 48 * 1024, $"The legal bootstrap roster emitted {bytes.Length} event payload bytes.");
    }

    [Fact]
    public async Task ShouldRetainOriginalNativeUnitResultGivenExactRetryAfterLaterProgress()
    {
        // Arrange
        await using var provider = Services();
        await SeedAsync(provider, First);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var request = new GrantPlatformOperator(Second, " Cover operations ", Uuid.CreateVersion4(), 1);
        Assert.True((await bus.SendAsync(request, Actor(First))).IsSuccess);
        var original = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new PlatformOperatorRoster(request.CommandId));
        var accepted = Assert.IsType<PlatformOperatorGranted>(original.Acceptance.SelectedFact);
        var content = provider.GetRequiredService<IImmutableContentStore>();
        var descriptor = await FirstResponses.ReplayAsync(content, "bdgrz", accepted.FirstResponse!);
        var body = Assert.IsType<StandardResponseBody>(descriptor.Body);
        var bytes = await content.ReadVerifiedAsync("bdgrz", body.Content);
        Assert.True((await bus.SendAsync(new RevokePlatformOperator(Second, "Ended", Uuid.CreateVersion4(), 2,
            request.CommandId), Actor(First))).IsSuccess);
        var displayChanged = Actor(First);
        ((ClaimsIdentity)displayChanged.Identity!).AddClaim(new Claim("email", "updated@example.test"));

        // Act
        var retry = await bus.SendAsync(request with { Reason = "Cover operations" }, displayChanged);
        var current = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new PlatformOperatorRoster(request.CommandId));
        var history = await PlatformOperatorRosterContent.ReadHistoryAsync(content,
            provider.GetRequiredService<IEventStore>(), current);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(accepted.FirstResponse, ((IPlatformOperatorFact)current.Acceptance.SelectedFact!).FirstResponse);
        Assert.Equal(200, descriptor.HttpStatus);
        Assert.Equal(UnitResult.Schema, descriptor.ResultSchema);
        Assert.Equal(accepted.OperationResult, body.Content);
        Assert.Equal("{}", System.Text.Encoding.UTF8.GetString(bytes.Span));
        Assert.Equal(3, history.Count);
        Assert.Equal(accepted.Operation.Actor, history[1].Actor);
        Assert.False((await ReadRosterAsync(scope.ServiceProvider)).IsOperator(Second));
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("issuer")]
    [InlineData("subject")]
    [InlineData("reason")]
    [InlineData("revision")]
    [InlineData("kind")]
    public async Task ShouldConflictGivenChangedNativeBindingUnderAcceptedGrantIdentity(string change)
    {
        // Arrange
        await using var provider = Services();
        await SeedAsync(provider, First, Third);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var request = new GrantPlatformOperator(Second, "Cover operations", Uuid.CreateVersion4(), 1);
        Assert.True((await bus.SendAsync(request, Actor(First))).IsSuccess);
        var actor = change == "actor" ? Actor(Third) : change == "issuer" ? new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", First.ToString(), ClaimValueTypes.String, "other")], "BdgrzSession")) : Actor(First);
        IRequest candidate = change == "kind" ? new RevokePlatformOperator(Second, "Cover operations", request.CommandId, 1, request.CommandId) :
            request with
            {
                UserId = change == "subject" ? Uuid.CreateVersion4() : Second,
                Reason = change == "reason" ? "Changed" : request.Reason,
                ExpectedRevision = change == "revision" ? 2UL : 1
            };

        // Act
        var result = await bus.SendAsync(candidate, actor);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(2UL, (await ReadRosterAsync(scope.ServiceProvider)).Version);
    }

    [Theory]
    [InlineData(PlatformOperatorRosterContent.AuthoritySchema)]
    [InlineData(FirstResponses.Schema)]
    [InlineData(UnitResult.Schema)]
    public async Task ShouldFailClosedGivenUnavailableOriginalAuthorityOrResponseOnRetry(string schema)
    {
        // Arrange
        var content = new RejectingContent(new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient())));
        await using var provider = Services(content: content);
        await SeedAsync(provider, First);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var request = new GrantPlatformOperator(Second, "Cover operations", Uuid.CreateVersion4(), 1);
        Assert.True((await bus.SendAsync(request, Actor(First))).IsSuccess);
        content.RejectedSchema = schema;

        // Act
        // Assert
        await Assert.ThrowsAsync<ImmutableContentIntegrityException>(async () => await bus.SendAsync(request, Actor(First)));
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new PlatformOperatorRoster());
        Assert.Equal(2UL, owner.Revision);
    }

    [Fact]
    public async Task ShouldRejectChangedPreparedAuthorityGivenAnOtherwiseValidNativeAppend()
    {
        // Arrange
        await using var provider = Services();
        await SeedAsync(provider, First, Third);
        await using var scope = provider.CreateAsyncScope();
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new PlatformOperatorRoster());
        var content = provider.GetRequiredService<IImmutableContentStore>();
        var before = await PlatformOperatorRosterContent.ReadAsync(content, owner);
        var id = Uuid.CreateVersion4();
        var mutation = new PlatformOperatorMutationContent(nameof(GrantPlatformOperator), id, 1, First, Second, Uuid.Empty, [],
            "Cover operations", Provenance(First), ActorReference.ForPlatformOperator(First, "One"), Now);
        var after = PlatformOperatorRosterRules.Apply(before, mutation).Value!;
        var authority = await content.WriteJsonAsync("bdgrz", Uuid.CreateVersion4(), PlatformOperatorRosterContent.AuthoritySchema, after);
        var change = await content.WriteJsonAsync("bdgrz", Uuid.CreateVersion4(), PlatformOperatorRosterContent.ChangeSchema, mutation);
        var response = await UnitResponses.FreezeAsync(content, "bdgrz");
        var first = await FirstResponses.FreezeAsync(content, "bdgrz", response);
        var operation = OwnerProvenance.For(owner, id, mutation.CommandKind, mutation.Actor, owner.Id.ToString(), 1,
            PlatformOperatorRosterContent.IntentDigest(mutation), Now);
        var fact = PlatformOperatorRosterContent.Fact(mutation, operation, change, authority, first,
            ((StandardResponseBody)response.Body).Content, after);
        var changedBefore = before with { Operators = [before.Operators[0]] };

        // Act
        var result = owner.Append(fact, owner.Revision, owner.CommittedStreamPosition, mutation, changedBefore, after, response);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(1UL, owner.Revision);
    }

    sealed class RejectingContent(IImmutableContentStore inner) : IImmutableContentStore
    {
        public string? RejectedSchema { get; set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            reference.Schema == RejectedSchema ? throw new ImmutableContentIntegrityException("Unavailable or corrupt immutable source.") :
                inner.ReadVerifiedAsync(realm, reference, ct);
    }

    [Fact]
    public void ShouldFailClosedGivenRetainedLegacyRosterWithoutTrustedNativeAuthority()
    {
        // Arrange
        var owner = new PlatformOperatorRoster();
        var legacy = DomainEventSeed.Attach(Seeded(First), owner.Id, 1);

        // Act
        // Assert
        Assert.Throws<InvalidOperationException>(() => new AggregateScenario<PlatformOperatorRoster>(owner).Given(legacy));
    }

    static ServiceProvider Services(ExistingUsers? users = null, IEventStore? store = null, IImmutableContentStore? content = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(store ?? new InMemoryEventStore());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IImmutableContentStore>(content ?? new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient())));
        services.AddScoped<NativeOperationExecutor>();
        services.AddScoped<PlatformOperatorRosterOperations>();
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
    static RosterFixture Given(Uuid selected, params DomainEvent[] facts) => RosterFixture.Create(selected, facts);

    static async Task<RosterFixture> ReadRosterAsync(IServiceProvider services)
    {
        var owner = await services.GetRequiredService<IAggregateReader>().HydrateAsync(new PlatformOperatorRoster());
        var state = await PlatformOperatorRosterContent.ReadAsync(services.GetRequiredService<IImmutableContentStore>(), owner);
        return RosterFixture.Restore(Uuid.Empty, [], state, owner);
    }

    /// <summary>Transient test fold: every fixture fact is a real native receipt before the runtime owner sees it.</summary>
    sealed class RosterFixture
    {
        readonly PlatformOperatorRoster _owner;
        readonly AggregateScenario<PlatformOperatorRoster> _scenario;
        public PlatformOperatorAuthorityContent State { get; private set; }
        public RosterFixture Aggregate => this;
        public DomainEvent[] Facts { get; }
        public IReadOnlyList<DomainEvent> PendingEvents => _scenario.PendingEvents.ToArray();
        public ulong Version => _owner.Version;
        public IReadOnlyList<PlatformOperatorAssignmentView> Operators => State.Operators;
        public bool IsOperator(Uuid user) => State.Operators.Any(item => item.UserId == user);
        public Uuid? AssignmentId(Uuid user) => State.Operators.FirstOrDefault(item => item.UserId == user)?.AssignmentId;

        RosterFixture(PlatformOperatorRoster owner, AggregateScenario<PlatformOperatorRoster> scenario,
            PlatformOperatorAuthorityContent state, DomainEvent[] facts)
        {
            _owner = owner;
            _scenario = scenario;
            State = state;
            Facts = facts;
        }

        public static RosterFixture Restore(Uuid selected, DomainEvent[] facts, PlatformOperatorAuthorityContent state,
            PlatformOperatorRoster? hydrated = null)
        {
            var owner = hydrated ?? new PlatformOperatorRoster(selected);
            var scenario = new AggregateScenario<PlatformOperatorRoster>(owner);
            if (hydrated is null)
                scenario.Given(facts);
            return new(owner, scenario, state, facts);
        }

        public static RosterFixture Create(Uuid selected, DomainEvent[] inputs)
        {
            var state = PlatformOperatorRosterContent.Empty;
            var facts = new List<DomainEvent>();
            var owner = new PlatformOperatorRoster(selected);
            foreach (var input in inputs)
            {
                var mutation = input switch
                {
                    PlatformOperatorRosterSeeded seed => SeedMutation(seed.RosterId, seed.UserIds, seed.Actor, seed.OccurredAt),
                    PlatformOperatorGranted grant => Change(nameof(GrantPlatformOperator), grant.CommandId, grant.ExpectedRevision,
                        grant.ActorUserId, grant.SubjectUserId, Uuid.Empty, grant.Reason, grant.Actor, grant.OccurredAt, grant.ActorDisplay),
                    PlatformOperatorRevoked revoke => Change(nameof(RevokePlatformOperator), revoke.CommandId, revoke.ExpectedRevision,
                        revoke.ActorUserId, revoke.SubjectUserId, revoke.AssignmentId, revoke.Reason, revoke.Actor, revoke.OccurredAt, revoke.ActorDisplay),
                    _ => throw new InvalidOperationException()
                };
                var next = PlatformOperatorRosterRules.Apply(state, mutation);
                Assert.True(next.IsSuccess);
                state = next.Value!;
                facts.Add(DomainEventSeed.Attach(Fact(owner, mutation, state), owner.Id, (ulong)facts.Count + 1));
            }
            return Restore(selected, facts.ToArray(), state);
        }

        public Result Seed(Uuid id, IEnumerable<Uuid> users, ActorAttribution actor, DateTimeOffset at) =>
            Admit(SeedMutation(id, users, actor, at));
        public Result Grant(Uuid id, ulong revision, Uuid actorUserId, Uuid subject, string reason,
            ActorAttribution actor, DateTimeOffset at, string? display = null) => Admit(Change(nameof(GrantPlatformOperator),
            id, revision, actorUserId, subject, Uuid.Empty, reason, actor, at, display));
        public Result Revoke(Uuid id, ulong revision, Uuid actorUserId, Uuid subject, Uuid assignment, string reason,
            ActorAttribution actor, DateTimeOffset at, string? display = null) => Admit(Change(nameof(RevokePlatformOperator),
            id, revision, actorUserId, subject, assignment, reason, actor, at, display));

        Result Admit(PlatformOperatorMutationContent mutation)
        {
            var operation = Provenance(_owner, mutation);
            var binding = _owner.Acceptance.Decide(AcceptedOperationBinding.Of(operation));
            if (binding == NativeAcceptanceDecision.Replayed)
                return Result.Success;
            if (binding == NativeAcceptanceDecision.Conflict)
                return Result.Failure(AcceptedAppendResult.ConflictError);
            var next = PlatformOperatorRosterRules.Apply(State, mutation);
            if (!next.IsSuccess)
                return Result.Failure(next.Error!);
            var result = _owner.Append(Fact(_owner, mutation, next.Value!), _owner.Revision,
                _owner.CommittedStreamPosition, mutation, State, next.Value!, Response());
            if (result.IsSuccess)
                State = next.Value!;
            return result;
        }

        static PlatformOperatorMutationContent SeedMutation(Uuid id, IEnumerable<Uuid> users, ActorAttribution actor,
            DateTimeOffset at) => new(nameof(SeedPlatformOperatorRoster), id, null, Uuid.Empty, Uuid.Empty, Uuid.Empty,
            PlatformOperatorRosterRules.CanonicalUsers(users), string.Empty, actor,
            ActorReference.ForSystemProcess("platform-operator-bootstrap", "Platform operator bootstrap"), at);
        static PlatformOperatorMutationContent Change(string kind, Uuid id, ulong revision, Uuid actorUserId,
            Uuid subject, Uuid assignment, string reason, ActorAttribution actor, DateTimeOffset at, string? display) =>
            new(kind, id, revision, actorUserId, subject, assignment, [], reason.Trim(), actor,
                ActorReference.ForPlatformOperator(actorUserId, string.IsNullOrWhiteSpace(display) ? actorUserId.ToString() : display.Trim()), at);
        static OperationProvenance Provenance(PlatformOperatorRoster owner, PlatformOperatorMutationContent mutation) =>
            OwnerProvenance.For(owner, mutation.CommandId, mutation.CommandKind, mutation.Actor, owner.Id.ToString(),
                mutation.ExpectedRevision, PlatformOperatorRosterContent.IntentDigest(mutation), mutation.AcceptedAt);
        static ImmutableContentRef Reference<T>(T value, string schema) where T : notnull =>
            new(Uuid.CreateVersion4(), ContentDigest.OfJson(value),
                System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value, ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))!).Length,
                "application/json", schema);
        static readonly ImmutableContentRef UnitReference = Reference(new UnitResult(), UnitResult.Schema);
        static FirstResponseDescriptor Response() => new(200, "application/json", UnitResult.Schema, new StandardResponseBody(UnitReference));
        static DomainEvent Fact(PlatformOperatorRoster owner, PlatformOperatorMutationContent mutation,
            PlatformOperatorAuthorityContent state) => PlatformOperatorRosterContent.Fact(mutation, Provenance(owner, mutation),
                Reference(mutation, PlatformOperatorRosterContent.ChangeSchema), Reference(state, PlatformOperatorRosterContent.AuthoritySchema),
                Reference(Response(), FirstResponses.Schema), UnitReference, state);
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
