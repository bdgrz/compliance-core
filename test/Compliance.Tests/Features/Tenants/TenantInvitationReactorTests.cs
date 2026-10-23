using Bdgrz.Compliance.Features.AccessControl;
using System.Diagnostics;
using Bdgrz.Compliance.Tests.E2E;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantInvitationReactorTests
{
    [Fact]
    public async Task ShouldProvisionEveryGrantedProgramGivenGuestAcceptsInvitation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var invitedBy = Uuid.CreateVersion4();
        var programIds = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        var scenario = new ReactorScenario().Given(new TenantInvitationAccepted(tenantId,
            userId, "guest@example.com", "guest", false,
            GuestProgramIds: programIds, GuestExpiresOn: new DateOnly(2026, 11, 30),
            AcceptedAt: new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero),
            InvitedBy: invitedBy, InvitedByDisplay: "Org Admin"));
        var reactor = new TenantInvitationReactor(new InMemoryProjectionCheckpointStore(),
            scenario.Requests, NullLogger<TenantInvitationReactor>.Instance);

        // Act
        await scenario.RunAsync(reactor);

        // Assert
        var registration = Assert.Single(scenario.SentRequests.OfType<RegisterMember>());
        Assert.Equal("guest", registration.Affiliation);
        Assert.Equal(new DateOnly(2026, 11, 30), registration.MembershipExpiresOn);
        var grants = scenario.SentRequests.OfType<ProvisionGuestProgramAccess>().ToArray();
        Assert.Equal(programIds.Length, grants.Length);
        Assert.All(grants, grant => Assert.Equal(
            TenantInvitation.IdFor(tenantId, "guest@example.com"), grant.InvitationId));
        Assert.Equal(programIds.OrderBy(id => id.ToString(), StringComparer.Ordinal),
            grants.Select(grant => grant.ProgramId).OrderBy(id => id.ToString(), StringComparer.Ordinal));
        Assert.All(grants, grant =>
        {
            Assert.Equal(tenantId, grant.TenantId);
            Assert.Equal(userId, grant.UserId);
            Assert.Equal(invitedBy, grant.InvitedBy);
            Assert.Equal("Org Admin", grant.InvitedByDisplay);
            Assert.Equal(new DateOnly(2026, 11, 30), grant.ExpiresOn);
            Assert.Equal(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero),
                grant.AcceptedAt);
        });
        Assert.Empty(scenario.SentRequests.OfType<AssignTeamMember>());
        Assert.Empty(scenario.SentRequests.OfType<ActivateTenant>());
    }

    [Fact]
    public async Task ShouldCompleteActivationWithoutReplayingReactionGivenTransientProjectionLag()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario()
            .Given(new TenantInvitationAccepted(tenantId, userId, "admin@example.com",
                "client_personnel", Administrator: true))
            .RespondTo<ActivateTenant>(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The first administrator is still being provisioned.", isTransient: true)));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new TenantInvitationReactor(checkpoints, scenario.Requests,
            NullLogger<TenantInvitationReactor>.Instance);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var running = scenario.RunAsync(reactor, deadline.Token);
        while (!scenario.SentRequests.Any(request => request is ActivateTenant) &&
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
        Assert.Equal(2, scenario.SentRequests.Count(request => request is ActivateTenant));
    }

    [Fact]
    public async Task ShouldReplayActivationAfterWorkerCancellationGivenTransientProjectionLag()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario()
            .Given(new TenantInvitationAccepted(tenantId, userId, "admin@example.com",
                "client_personnel", Administrator: true))
            .RespondTo<ActivateTenant>(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The first administrator is still being provisioned.", isTransient: true)));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new TenantInvitationReactor(checkpoints, scenario.Requests,
            NullLogger<TenantInvitationReactor>.Instance);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var running = scenario.RunAsync(reactor, cancellation.Token);
        while (!scenario.SentRequests.Any(request => request is ActivateTenant) &&
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
        Assert.Equal(2, scenario.SentRequests.Count(request => request is ActivateTenant));
    }

    [Fact]
    public async Task ShouldDeferTransientActivationToDurableReplayGivenExtendedProjectionLag()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario()
            .Given(new TenantInvitationAccepted(tenantId, userId, "admin@example.com",
                "client_personnel", Administrator: true))
            .RespondTo<ActivateTenant>(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The first administrator is still being provisioned.", isTransient: true)));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        using var logs = new AuthorizationDenialLogE2ETests.CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var reactor = new TenantInvitationReactor(checkpoints, scenario.Requests,
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
        Assert.True(scenario.SentRequests.Count(request => request is ActivateTenant) > 1);
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
            .Given(new TenantInvitationAccepted(tenantId, userId, "admin@example.com",
                "client_personnel", Administrator: true))
            .RespondTo<RegisterMember>(Result.Failure(Member.LiveEpisodeConflict));
        var reactor = new TenantInvitationReactor(new InMemoryProjectionCheckpointStore(), scenario.Requests,
            NullLogger<TenantInvitationReactor>.Instance);

        // Act
        await scenario.RunAsync(reactor);

        // Assert
        Assert.Single(scenario.SentRequests.OfType<RegisterMember>());
        var assignment = Assert.Single(scenario.SentRequests.OfType<AssignTeamMember>());
        Assert.Equal(BuiltInRbac.AdministratorsTeamId(tenantId), assignment.TeamId);
    }

    [Fact]
    public async Task ShouldFailReactionGivenRegistrationConflictOtherThanLiveEpisode()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario()
            .Given(new TenantInvitationAccepted(tenantId, Uuid.CreateVersion4(), "admin@example.com",
                "client_personnel", Administrator: true))
            .RespondTo<RegisterMember>(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Membership authority cleanup must finish before a new invitation can take effect.", false)));
        var reactor = new TenantInvitationReactor(new InMemoryProjectionCheckpointStore(), scenario.Requests,
            NullLogger<TenantInvitationReactor>.Instance);

        // Act
        var failure = await Record.ExceptionAsync(async () => await scenario.RunAsync(reactor));

        // Assert
        Assert.IsType<ReactionCommandFailedException>(failure);
        Assert.Empty(scenario.SentRequests.OfType<AssignTeamMember>());
    }

    [Fact]
    public async Task ShouldStopRetryingActivationGivenPermanentFailure()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario()
            .Given(new TenantInvitationAccepted(tenantId, userId, "admin@example.com",
                "client_personnel", Administrator: true))
            .RespondTo<ActivateTenant>(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Membership and permission projections are not ready.")));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new TenantInvitationReactor(checkpoints, scenario.Requests,
            NullLogger<TenantInvitationReactor>.Instance);

        // Act
        await Assert.ThrowsAsync<ReactionCommandFailedException>(async () => await scenario.RunAsync(reactor));
        var identity = new CheckpointIdentity(reactor.Name, reactor.Pattern);
        var firstPassAttempts = scenario.SentRequests.Count(request => request is ActivateTenant);
        var checkpointAfterFailure = await checkpoints.LoadAsync(identity);
        scenario.RespondTo<ActivateTenant>(Result.Success);
        await scenario.RunAsync(reactor);
        var checkpointAfterReplay = await checkpoints.LoadAsync(identity);

        // Assert
        Assert.Equal(1, firstPassAttempts);
        Assert.Equal(ProjectionCheckpoint.Start, checkpointAfterFailure);
        Assert.NotEqual(ProjectionCheckpoint.Start, checkpointAfterReplay);
        Assert.Equal(2, scenario.SentRequests.Count(request => request is RegisterMember));
        Assert.Single(scenario.SentRequests.OfType<RegisterMember>().Select(request => request.CommandId).Distinct());
        Assert.Equal(2, scenario.SentRequests.Count(request => request is AssignTeamMember));
        Assert.Equal(2, scenario.SentRequests.Count(request => request is ActivateTenant));
    }
    [Fact]
    public async Task ShouldAbandonActivationObservablyGivenPermanentFailureBudgetExhausted()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario()
            .Given(new TenantInvitationAccepted(tenantId, userId, "admin@example.com",
                "client_personnel", Administrator: true))
            .RespondTo<ActivateTenant>(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The first administrator must be client personnel.")));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        using var logs = new AuthorizationDenialLogE2ETests.CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var policy = new TenantActivationPolicy(TimeSpan.FromMilliseconds(50), permanentFailureLimit: 3);
        var reactor = new TenantInvitationReactor(checkpoints, scenario.Requests,
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
        Assert.Equal(3, scenario.SentRequests.Count(request => request is ActivateTenant));
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
            .Given(new TenantInvitationAccepted(tenantId, Uuid.CreateVersion4(), "admin@example.com",
                "client_personnel", Administrator: true))
            .RespondTo<ActivateTenant>(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The first administrator is still being provisioned.", isTransient: true)));
        var reactor = new TenantInvitationReactor(new InMemoryProjectionCheckpointStore(),
            scenario.Requests, NullLogger<TenantInvitationReactor>.Instance,
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
