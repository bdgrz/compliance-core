using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using System.Diagnostics;
using Bdgrz.Compliance.Tests.E2E;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantInvitationReactorTests
{
    readonly IImmutableContentStore _content = new FitzKvImmutableContentStore(
        new PersistenceKvClient(new InMemoryKvClient()));

    static TenantInvitationAccepted Accepted(Uuid tenantId, Uuid userId, string email, string affiliation,
        bool administrator = false, Uuid? invitationId = null, ImmutableContentRef? scope = null,
        DateOnly? guestExpiresOn = null, DateTimeOffset? acceptedAt = null, Uuid invitedBy = default,
        string? display = null, string? role = null)
    {
        var id = invitationId ?? Uuid.CreateVersion4();
        var invitation = new TenantInvitation(tenantId, id);
        return new TenantInvitationAccepted(tenantId, id, userId, email, affiliation, administrator, role, scope,
            guestExpiresOn, acceptedAt ?? TenantInvitationFacts.At, invitedBy == Uuid.Empty ? Uuid.CreateVersion4() : invitedBy,
            display, TenantInvitationFacts.Provenance(invitation, TenantInvitationIds.AcceptanceOf(id),
                "AcceptTenantInvitation"), null);
    }

    [Fact]
    public async Task ShouldProvisionEveryGrantedProgramGivenGuestAcceptsInvitation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var invitedBy = Uuid.CreateVersion4();
        var invitationId = Uuid.CreateVersion4();
        var programIds = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        var scope = await _content.WriteJsonAsync(tenantId.ToString(), Uuid.CreateVersion4(),
            AcceptTenantInvitationHandler.GuestScopeSchema, (IReadOnlyList<Uuid>)programIds);
        var scenario = new ReactorScenario().Given(Accepted(tenantId, userId, "guest@example.com", "guest",
            invitationId: invitationId, scope: scope, guestExpiresOn: new DateOnly(2026, 11, 30),
            acceptedAt: new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero), invitedBy: invitedBy,
            display: "Org Admin"));
        var reactor = new TenantInvitationReactor(new InMemoryProjectionCheckpointStore(),
            TypedReactionBus.For(scenario), _content, NullLogger<TenantInvitationReactor>.Instance);

        // Act
        await scenario.RunAsync(reactor);

        // Assert
        var registration = Assert.Single(TypedReactionBus.Sent(scenario).OfType<RegisterMember>());
        Assert.Equal("guest", registration.Affiliation);
        Assert.Equal(new DateOnly(2026, 11, 30), registration.MembershipExpiresOn);
        var grants = TypedReactionBus.Sent(scenario).OfType<ProvisionGuestProgramAccess>().ToArray();
        Assert.Equal(programIds.Length, grants.Length);
        Assert.All(grants, grant => Assert.Equal(invitationId, grant.InvitationId));
        Assert.Equal(programIds.OrderBy(id => id.ToString(), StringComparer.Ordinal),
            grants.Select(grant => grant.ProgramId).OrderBy(id => id.ToString(), StringComparer.Ordinal));
        Assert.All(grants, grant =>
        {
            Assert.Equal(tenantId, grant.TenantId);
            Assert.Equal(userId, grant.UserId);
            Assert.Equal(invitedBy, grant.InvitedBy);
            Assert.Equal("Org Admin", grant.InvitedByDisplay);
            Assert.Equal(new DateOnly(2026, 11, 30), grant.ExpiresOn);
            Assert.Equal(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero), grant.AcceptedAt);
        });
        Assert.Empty(TypedReactionBus.Sent(scenario).OfType<AssignTeamMember>());
        Assert.Empty(TypedReactionBus.Sent(scenario).OfType<ActivateTenant>());
    }

    [Fact]
    public async Task ShouldBindTeamAssignmentToTheRegisteredEpisodeGivenAcceptedInvitation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(Accepted(tenantId, userId, "member@example.com", "client_personnel",
            role: BuiltInRbac.ComplianceManagementRole));
        var reactor = new TenantInvitationReactor(new InMemoryProjectionCheckpointStore(),
            TypedReactionBus.For(scenario), _content, NullLogger<TenantInvitationReactor>.Instance);

        // Act
        await scenario.RunAsync(reactor);

        // Assert
        var registration = Assert.Single(TypedReactionBus.Sent(scenario).OfType<RegisterMember>());
        var assignment = Assert.Single(TypedReactionBus.Sent(scenario).OfType<AssignTeamMember>());
        Assert.Equal(registration.CommandId, assignment.MemberEpisodeId);
        Assert.Equal(BuiltInRbac.TeamIdForRole(tenantId, BuiltInRbac.ComplianceManagementRole), assignment.TeamId);
        Assert.Equal(RbacIds.Member(tenantId, userId), assignment.MemberId);
    }

    [Fact]
    public async Task ShouldAdmitAdministratorsTeamAssignmentToTheCurrentEpisodeGivenAlreadyLiveMember()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario()
            .Given(Accepted(tenantId, Uuid.CreateVersion4(), "admin@example.com", "client_personnel", true))
            .RespondTo<RegisterMember>(Result.Failure(Member.LiveEpisodeConflict));
        var reactor = new TenantInvitationReactor(new InMemoryProjectionCheckpointStore(),
            TypedReactionBus.For(scenario), _content, NullLogger<TenantInvitationReactor>.Instance);

        // Act
        await scenario.RunAsync(reactor);

        // Assert
        var assignment = Assert.Single(TypedReactionBus.Sent(scenario).OfType<AssignTeamMember>());
        Assert.Equal(BuiltInRbac.AdministratorsTeamId(tenantId), assignment.TeamId);
        Assert.Equal(Uuid.Empty, assignment.MemberEpisodeId);
    }

    [Fact]
    public async Task ShouldCompleteActivationWithoutReplayingReactionGivenTransientProjectionLag()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario()
            .Given(Accepted(tenantId, userId, "admin@example.com",
                "client_personnel", administrator: true))
            .RespondTo<ActivateTenant>(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The first administrator is still being provisioned.", isTransient: true)));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new TenantInvitationReactor(checkpoints, TypedReactionBus.For(scenario), _content,
            NullLogger<TenantInvitationReactor>.Instance);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var running = scenario.RunAsync(reactor, deadline.Token);
        while (!TypedReactionBus.Sent(scenario).Any(request => request is ActivateTenant) &&
               !running.IsCompleted)
            await Task.Delay(10, deadline.Token);
        var identity = new CheckpointIdentity(reactor.Name, reactor.Pattern);
        var beforeRecovery = await checkpoints.LoadAsync(identity, deadline.Token);
        scenario.RespondTo<ActivateTenant>(Result.Success);
        var remainedPending = !running.IsCompleted;
        if (remainedPending)
            await running;
        var afterRecovery = await checkpoints.LoadAsync(identity);

        // Assert
        Assert.True(remainedPending, "A transient conflict must keep the reactor active for recovery.");
        Assert.Equal(ProjectionCheckpoint.Start, beforeRecovery);
        Assert.NotEqual(ProjectionCheckpoint.Start, afterRecovery);
        Assert.Equal(2, TypedReactionBus.Sent(scenario).Count(request => request is ActivateTenant));
    }

    [Fact]
    public async Task ShouldReplayActivationAfterWorkerCancellationGivenTransientProjectionLag()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario()
            .Given(Accepted(tenantId, userId, "admin@example.com",
                "client_personnel", administrator: true))
            .RespondTo<ActivateTenant>(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The first administrator is still being provisioned.", isTransient: true)));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new TenantInvitationReactor(checkpoints, TypedReactionBus.For(scenario), _content,
            NullLogger<TenantInvitationReactor>.Instance);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var running = scenario.RunAsync(reactor, cancellation.Token);
        while (!TypedReactionBus.Sent(scenario).Any(request => request is ActivateTenant) &&
               !running.IsCompleted)
            await Task.Delay(10, cancellation.Token);
        var identity = new CheckpointIdentity(reactor.Name, reactor.Pattern);
        cancellation.Cancel();
        var canceled = await Record.ExceptionAsync(() => running);
        var checkpointAfterCancellation = await checkpoints.LoadAsync(identity);
        scenario.RespondTo<ActivateTenant>(Result.Success);
        await scenario.RunAsync(reactor);
        var checkpointAfterReplay = await checkpoints.LoadAsync(identity);

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(canceled);
        Assert.Equal(ProjectionCheckpoint.Start, checkpointAfterCancellation);
        Assert.NotEqual(ProjectionCheckpoint.Start, checkpointAfterReplay);
        Assert.Equal(2, TypedReactionBus.Sent(scenario).Count(request => request is ActivateTenant));
    }

    [Fact]
    public async Task ShouldDeferTransientActivationToDurableReplayGivenExtendedProjectionLag()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario()
            .Given(Accepted(tenantId, userId, "admin@example.com",
                "client_personnel", administrator: true))
            .RespondTo<ActivateTenant>(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The first administrator is still being provisioned.", isTransient: true)));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        using var logs = new AuthorizationDenialLogE2ETests.CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var reactor = new TenantInvitationReactor(checkpoints, TypedReactionBus.For(scenario), _content,
            loggerFactory.CreateLogger<TenantInvitationReactor>());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        // Act
        var started = Stopwatch.GetTimestamp();
        await Assert.ThrowsAsync<ReactionCommandFailedException>(async () =>
            await scenario.RunAsync(reactor, deadline.Token));
        var elapsed = Stopwatch.GetElapsedTime(started);
        var identity = new CheckpointIdentity(reactor.Name, reactor.Pattern);
        var checkpointBeforeReplay = await checkpoints.LoadAsync(identity);
        scenario.RespondTo<ActivateTenant>(Result.Success);
        await scenario.RunAsync(reactor);
        var checkpointAfterReplay = await checkpoints.LoadAsync(identity);

        // Assert
        Assert.True(elapsed >= TimeSpan.FromSeconds(8),
            "A transient activation must retry for the bounded local window before durable replay.");
        Assert.Equal(ProjectionCheckpoint.Start, checkpointBeforeReplay);
        Assert.NotEqual(ProjectionCheckpoint.Start, checkpointAfterReplay);
        Assert.True(TypedReactionBus.Sent(scenario).Count(request => request is ActivateTenant) > 1);
        Assert.Contains(logs.Records, record => record.EventName == "LogActivationDeferred" &&
            string.Equals(record.Value("TenantId")?.ToString(), tenantId.ToString(),
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldContinueWithTeamAssignmentGivenAcceptedInvitationForAlreadyLiveMember()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario()
            .Given(Accepted(tenantId, userId, "admin@example.com",
                "client_personnel", administrator: true))
            .RespondTo<RegisterMember>(Result.Failure(Member.LiveEpisodeConflict));
        var reactor = new TenantInvitationReactor(new InMemoryProjectionCheckpointStore(), TypedReactionBus.For(scenario), _content,
            NullLogger<TenantInvitationReactor>.Instance);

        // Act
        await scenario.RunAsync(reactor);

        // Assert
        Assert.Single(TypedReactionBus.Sent(scenario).OfType<RegisterMember>());
        var assignment = Assert.Single(TypedReactionBus.Sent(scenario).OfType<AssignTeamMember>());
        Assert.Equal(BuiltInRbac.AdministratorsTeamId(tenantId), assignment.TeamId);
    }

    [Fact]
    public async Task ShouldReuseAssignmentCommandIdGivenReplayedInvitationAcceptance()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario()
            .Given(Accepted(tenantId, Uuid.CreateVersion4(), "admin@example.com",
                "client_personnel", administrator: true));

        // Act
        await scenario.RunAsync(new TenantInvitationReactor(new InMemoryProjectionCheckpointStore(),
            TypedReactionBus.For(scenario), _content, NullLogger<TenantInvitationReactor>.Instance));
        await scenario.RunAsync(new TenantInvitationReactor(new InMemoryProjectionCheckpointStore(),
            TypedReactionBus.For(scenario), _content, NullLogger<TenantInvitationReactor>.Instance));

        // Assert
        var assignments = TypedReactionBus.Sent(scenario).OfType<AssignTeamMember>().ToArray();
        Assert.Equal(2, assignments.Length);
        Assert.Equal(assignments[0], assignments[1]);
        Assert.NotEqual(Uuid.Empty, assignments[0].CommandId);
    }

    [Fact]
    public async Task ShouldContinueWithActivationGivenAdministratorAlreadyOnTeam()
    {
        // Arrange
        var scenario = new ReactorScenario()
            .Given(Accepted(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "admin@example.com",
                "client_personnel", administrator: true));
        TypedReactionBus.FailWith<AssignTeamMember>(scenario, TeamMember.AlreadyAssignedConflict);
        var reactor = new TenantInvitationReactor(new InMemoryProjectionCheckpointStore(),
            TypedReactionBus.For(scenario), _content, NullLogger<TenantInvitationReactor>.Instance);

        // Act
        await scenario.RunAsync(reactor);

        // Assert
        Assert.Single(TypedReactionBus.Sent(scenario).OfType<AssignTeamMember>());
        Assert.Single(TypedReactionBus.Sent(scenario).OfType<ActivateTenant>());
    }

    [Fact]
    public async Task ShouldFailReactionGivenAssignmentConflictOtherThanAlreadyAssigned()
    {
        // Arrange
        var scenario = new ReactorScenario()
            .Given(Accepted(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "admin@example.com",
                "client_personnel", administrator: true));
        TypedReactionBus.FailWith<AssignTeamMember>(scenario, new RequestError(RequestErrorKind.Conflict,
            "The team assignment changed since it was read.", false));
        var reactor = new TenantInvitationReactor(new InMemoryProjectionCheckpointStore(),
            TypedReactionBus.For(scenario), _content, NullLogger<TenantInvitationReactor>.Instance);

        // Act
        var failure = await Record.ExceptionAsync(async () => await scenario.RunAsync(reactor));

        // Assert
        Assert.IsType<ReactionCommandFailedException>(failure);
        Assert.Empty(TypedReactionBus.Sent(scenario).OfType<ActivateTenant>());
    }

    [Fact]
    public async Task ShouldFailReactionGivenRegistrationConflictOtherThanLiveEpisode()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario()
            .Given(Accepted(tenantId, Uuid.CreateVersion4(), "admin@example.com",
                "client_personnel", administrator: true))
            .RespondTo<RegisterMember>(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Membership authority cleanup must finish before a new invitation can take effect.", false)));
        var reactor = new TenantInvitationReactor(new InMemoryProjectionCheckpointStore(), TypedReactionBus.For(scenario), _content,
            NullLogger<TenantInvitationReactor>.Instance);

        // Act
        var failure = await Record.ExceptionAsync(async () => await scenario.RunAsync(reactor));

        // Assert
        Assert.IsType<ReactionCommandFailedException>(failure);
        Assert.Empty(TypedReactionBus.Sent(scenario).OfType<AssignTeamMember>());
    }

    [Fact]
    public async Task ShouldStopRetryingActivationGivenPermanentFailure()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario()
            .Given(Accepted(tenantId, userId, "admin@example.com",
                "client_personnel", administrator: true))
            .RespondTo<ActivateTenant>(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Membership and permission projections are not ready.")));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new TenantInvitationReactor(checkpoints, TypedReactionBus.For(scenario), _content,
            NullLogger<TenantInvitationReactor>.Instance);

        // Act
        await Assert.ThrowsAsync<ReactionCommandFailedException>(async () => await scenario.RunAsync(reactor));
        var identity = new CheckpointIdentity(reactor.Name, reactor.Pattern);
        var firstPassAttempts = TypedReactionBus.Sent(scenario).Count(request => request is ActivateTenant);
        var checkpointAfterFailure = await checkpoints.LoadAsync(identity);
        scenario.RespondTo<ActivateTenant>(Result.Success);
        await scenario.RunAsync(reactor);
        var checkpointAfterReplay = await checkpoints.LoadAsync(identity);

        // Assert
        Assert.Equal(1, firstPassAttempts);
        Assert.Equal(ProjectionCheckpoint.Start, checkpointAfterFailure);
        Assert.NotEqual(ProjectionCheckpoint.Start, checkpointAfterReplay);
        Assert.Equal(2, TypedReactionBus.Sent(scenario).Count(request => request is RegisterMember));
        Assert.Single(TypedReactionBus.Sent(scenario).OfType<RegisterMember>().Select(request => request.CommandId).Distinct());
        Assert.Equal(2, TypedReactionBus.Sent(scenario).Count(request => request is AssignTeamMember));
        Assert.Equal(2, TypedReactionBus.Sent(scenario).Count(request => request is ActivateTenant));
    }
    [Fact]
    public async Task ShouldAbandonActivationObservablyGivenPermanentFailureBudgetExhausted()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario()
            .Given(Accepted(tenantId, userId, "admin@example.com",
                "client_personnel", administrator: true))
            .RespondTo<ActivateTenant>(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The first administrator must be client personnel.")));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        using var logs = new AuthorizationDenialLogE2ETests.CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var policy = new TenantActivationPolicy(TimeSpan.FromMilliseconds(50), permanentFailureLimit: 3);
        var reactor = new TenantInvitationReactor(checkpoints, TypedReactionBus.For(scenario), _content,
            loggerFactory.CreateLogger<TenantInvitationReactor>(), policy);

        // Act
        await Assert.ThrowsAsync<ReactionCommandFailedException>(async () => await scenario.RunAsync(reactor));
        var identity = new CheckpointIdentity(reactor.Name, reactor.Pattern);
        await Assert.ThrowsAsync<ReactionCommandFailedException>(async () => await scenario.RunAsync(reactor));
        var checkpointBeforeBudget = await checkpoints.LoadAsync(identity);
        await scenario.RunAsync(reactor);
        var checkpointAfterBudget = await checkpoints.LoadAsync(identity);

        // Assert
        Assert.Equal(ProjectionCheckpoint.Start, checkpointBeforeBudget);
        Assert.NotEqual(ProjectionCheckpoint.Start, checkpointAfterBudget);
        Assert.Equal(3, TypedReactionBus.Sent(scenario).Count(request => request is ActivateTenant));
        Assert.Contains(logs.Records, record => record.EventName == "LogActivationAbandoned" &&
            record.Level == LogLevel.Error &&
            string.Equals(record.Value("TenantId")?.ToString(), tenantId.ToString(),
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldReturnTransientActivationToDurableReplayGivenConfiguredLocalWindow()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario()
            .Given(Accepted(tenantId, Uuid.CreateVersion4(), "admin@example.com",
                "client_personnel", administrator: true))
            .RespondTo<ActivateTenant>(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The first administrator is still being provisioned.", isTransient: true)));
        var reactor = new TenantInvitationReactor(new InMemoryProjectionCheckpointStore(),
            TypedReactionBus.For(scenario), _content, NullLogger<TenantInvitationReactor>.Instance,
            new TenantActivationPolicy(TimeSpan.FromMilliseconds(100), permanentFailureLimit: 1));

        // Act
        var started = Stopwatch.GetTimestamp();
        var failures = 0;
        for (var pass = 0; pass < 30; pass++)
        {
            if (await Record.ExceptionAsync(async () => await scenario.RunAsync(reactor)) is
                ReactionCommandFailedException)
                failures++;
        }
        var elapsed = Stopwatch.GetElapsedTime(started);

        // Assert
        Assert.Equal(30, failures);
        Assert.True(elapsed < TimeSpan.FromSeconds(20), $"Thirty short passes took {elapsed}.");
    }

    [Fact]
    public void ShouldNotFaultWorkerHostGivenActivationWorkloadRegistrations()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            })
            .Build();

        // Act
        services.AddCompliance(configuration).AddWorkers();
        var registrations = services
            .Where(descriptor => descriptor.ServiceType == typeof(WorkloadRegistration))
            .Select(descriptor => (WorkloadRegistration)descriptor.ImplementationInstance!)
            .Where(registration => registration.Name is "TenantInvitation" or "TenantSelfServiceActivationV1")
            .ToArray();

        // Assert
        Assert.Equal(2, registrations.Length);
        Assert.All(registrations, registration =>
            Assert.Equal(TenantActivationPolicy.WorkloadFailureAttemptLimit, registration.FailureAttemptLimit));
        Assert.Equal(int.MaxValue, TenantActivationPolicy.WorkloadFailureAttemptLimit);
    }
}
