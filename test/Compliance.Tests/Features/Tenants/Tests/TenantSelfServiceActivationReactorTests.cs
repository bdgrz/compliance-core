using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Reactors;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class TenantSelfServiceActivationReactorTests
{
    [Fact]
    public async Task ShouldActivateVerifiedCreatorAfterAdministratorAssignmentGivenProjectionRecovery()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var creatorId = Uuid.CreateVersion4();
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        await SeedTenantAsync(scope.ServiceProvider, tenantId, creatorId,
            creatorIsAdministrator: true);
        var scenario = new ReactorScenario()
            .Given(TeamMemberFacts.Assigned(tenantId, BuiltInRbac.AdministratorsTeamId(tenantId),
                RbacIds.Member(tenantId, creatorId)))
            .RespondTo<ActivateTenant>(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The first administrator is still being provisioned.", isTransient: true)));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new TenantSelfServiceActivationReactor(checkpoints,
            scope.ServiceProvider.GetRequiredService<IAggregateReader>(), scenario.Requests,
            NullLogger<TenantSelfServiceActivationReactor>.Instance);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var running = scenario.RunAsync(reactor, deadline.Token);
        while (!scenario.SentRequests.Any(request => request is ActivateTenant) &&
               !running.IsCompleted)
            await Task.Delay(10, deadline.Token);
        var identity = new CheckpointIdentity(reactor.Name, reactor.Pattern);
        var checkpointWhilePending = await checkpoints.LoadAsync(identity, deadline.Token);
        scenario.RespondTo<ActivateTenant>(Result.Success);
        await running;

        // Assert
        Assert.Equal(ProjectionCheckpoint.Start, checkpointWhilePending);
        Assert.NotEqual(ProjectionCheckpoint.Start, await checkpoints.LoadAsync(identity));
        Assert.Equal(2, scenario.SentRequests.Count(request => request is ActivateTenant
        {
            TenantId: var targetTenantId,
            FirstAdministratorUserId: var userId,
        } && targetTenantId == tenantId && userId == creatorId));
    }

    [Fact]
    public async Task ShouldReplayCreatorActivationAfterWorkerCancellationGivenTransientProjectionLag()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var creatorId = Uuid.CreateVersion4();
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        await SeedTenantAsync(scope.ServiceProvider, tenantId, creatorId,
            creatorIsAdministrator: true);
        var scenario = new ReactorScenario()
            .Given(TeamMemberFacts.Assigned(tenantId, BuiltInRbac.AdministratorsTeamId(tenantId),
                RbacIds.Member(tenantId, creatorId)))
            .RespondTo<ActivateTenant>(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The first administrator is still being provisioned.", isTransient: true)));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new TenantSelfServiceActivationReactor(checkpoints,
            scope.ServiceProvider.GetRequiredService<IAggregateReader>(), scenario.Requests,
            NullLogger<TenantSelfServiceActivationReactor>.Instance);
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

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(canceled);
        Assert.Equal(ProjectionCheckpoint.Start, checkpointAfterCancellation);
        Assert.NotEqual(ProjectionCheckpoint.Start, await checkpoints.LoadAsync(identity));
        Assert.Equal(2, scenario.SentRequests.Count(request => request is ActivateTenant));
    }

    [Fact]
    public async Task ShouldIgnoreOtherAdministratorGivenCreatorActivationAssignment()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var creatorId = Uuid.CreateVersion4();
        var otherUserId = Uuid.CreateVersion4();
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        await SeedTenantAsync(scope.ServiceProvider, tenantId, creatorId,
            creatorIsAdministrator: true);
        var scenario = new ReactorScenario()
            .Given(TeamMemberFacts.Assigned(tenantId, BuiltInRbac.AdministratorsTeamId(tenantId),
                RbacIds.Member(tenantId, otherUserId)));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new TenantSelfServiceActivationReactor(checkpoints,
            scope.ServiceProvider.GetRequiredService<IAggregateReader>(), scenario.Requests,
            NullLogger<TenantSelfServiceActivationReactor>.Instance);

        // Act
        await scenario.RunAsync(reactor);

        // Assert
        Assert.DoesNotContain(scenario.SentRequests, request => request is ActivateTenant);
        Assert.NotEqual(ProjectionCheckpoint.Start,
            await checkpoints.LoadAsync(new CheckpointIdentity(reactor.Name, reactor.Pattern)));
    }

    [Fact]
    public async Task ShouldIgnoreOwnerAssignmentGivenOperatorRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var operatorId = Uuid.CreateVersion4();
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        await SeedTenantAsync(scope.ServiceProvider, tenantId, operatorId,
            creatorIsAdministrator: false);
        var scenario = new ReactorScenario()
            .Given(TeamMemberFacts.Assigned(tenantId, BuiltInRbac.AdministratorsTeamId(tenantId),
                RbacIds.Member(tenantId, operatorId)));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new TenantSelfServiceActivationReactor(checkpoints,
            scope.ServiceProvider.GetRequiredService<IAggregateReader>(), scenario.Requests,
            NullLogger<TenantSelfServiceActivationReactor>.Instance);

        // Act
        await scenario.RunAsync(reactor);

        // Assert
        Assert.DoesNotContain(scenario.SentRequests, request => request is ActivateTenant);
        Assert.NotEqual(ProjectionCheckpoint.Start,
            await checkpoints.LoadAsync(new CheckpointIdentity(reactor.Name, reactor.Pattern)));
    }

    [Fact]
    public async Task ShouldSkipHistoricalCreatorAssignmentGivenAlreadyActivatedTenant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var creatorId = Uuid.CreateVersion4();
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        await SeedTenantAsync(scope.ServiceProvider, tenantId, creatorId,
            creatorIsAdministrator: true, alreadyActivated: true);
        var scenario = new ReactorScenario()
            .Given(TeamMemberFacts.Assigned(tenantId, BuiltInRbac.AdministratorsTeamId(tenantId),
                RbacIds.Member(tenantId, creatorId)))
            .RespondTo<ActivateTenant>(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The historical creator is no longer authorized.")));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new TenantSelfServiceActivationReactor(checkpoints,
            scope.ServiceProvider.GetRequiredService<IAggregateReader>(), scenario.Requests,
            NullLogger<TenantSelfServiceActivationReactor>.Instance);

        // Act
        await scenario.RunAsync(reactor);

        // Assert
        Assert.DoesNotContain(scenario.SentRequests, request => request is ActivateTenant);
        Assert.NotEqual(ProjectionCheckpoint.Start,
            await checkpoints.LoadAsync(new CheckpointIdentity(reactor.Name, reactor.Pattern)));
    }

    static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia();
        return services.BuildServiceProvider();
    }

    static async Task SeedTenantAsync(IServiceProvider services, Uuid tenantId, Uuid ownerId,
        bool creatorIsAdministrator, bool alreadyActivated = false)
    {
        var tenant = new Tenant(tenantId);
        Assert.True(tenant.Register(ownerId, "Acme", "acme",
            creatorIsAdministrator: creatorIsAdministrator).IsSuccess);
        if (alreadyActivated)
        {
            Assert.True(tenant.ConfirmSlug("acme").IsSuccess);
            Assert.True(tenant.Activate(ownerId, null).IsSuccess);
        }
        await services.GetRequiredService<IAggregateWriter>()
            .SaveAsync(tenant, new RequestDispatchContext(RequestActor.System));
    }
}
