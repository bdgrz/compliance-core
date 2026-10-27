using System.Globalization;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Handlers;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class ActivateTenantReadinessTests
{
    [Fact]
    public async Task ShouldRejectActivationPermanentlyGivenFirmStaffAdministrator()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var creatorId = Uuid.CreateVersion4();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships { Ready = true });
        services.AddSingleton<IPermissionAuthorizer>(new Permissions { Ready = true });
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<Bdgrz.Compliance.Features.Content.Contracts.IImmutableContentStore>(
            new Bdgrz.Compliance.Features.Content.Persistence.FitzKvImmutableContentStore(new Cntryl.Fitz.Testing.InMemoryKvClient()));
        services.AddScoped<Bdgrz.Compliance.Features.NativeOperations.Models.NativeOperationExecutor>();
        services.AddPortia()
            .AddRequestHandler<ActivateTenantHandler>()
            .AddRequestAuthorizer<ActivateTenantAuthorizer>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var tenant = new Tenant(tenantId);
        Assert.True(tenant.Register(creatorId, "Acme", "acme", creatorIsAdministrator: true).IsSuccess);
        Assert.True(tenant.ConfirmSlug("acme").IsSuccess);
        await writer.SaveAsync(tenant, new RequestDispatchContext(RequestActor.System));
        var member = new Member(tenantId, creatorId);
        Assert.True(member.Register("firm_staff").IsSuccess);
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        var assignment = new TeamMember(tenantId, BuiltInRbac.AdministratorsTeamId(tenantId),
            RbacIds.Member(tenantId, creatorId));
        Assert.True(assignment.Assign().IsSuccess);
        await writer.SaveAsync(assignment, new RequestDispatchContext(RequestActor.System));

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>()
            .SendAsync(new ActivateTenant(Uuid.CreateVersion4(), tenantId, creatorId), RequestActor.System);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.False(result.Error.IsTransient);
        Assert.False((await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new Tenant(tenantId))).IsActive);
    }

    [Fact]
    public async Task ShouldCheckpointLaterAdministratorActivationGivenAlreadyActiveTenant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var creatorId = Uuid.CreateVersion4();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships());
        services.AddSingleton<IPermissionAuthorizer>(new Permissions());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<Bdgrz.Compliance.Features.Content.Contracts.IImmutableContentStore>(
            new Bdgrz.Compliance.Features.Content.Persistence.FitzKvImmutableContentStore(new Cntryl.Fitz.Testing.InMemoryKvClient()));
        services.AddScoped<Bdgrz.Compliance.Features.NativeOperations.Models.NativeOperationExecutor>();
        services.AddPortia()
            .AddRequestHandler<ActivateTenantHandler>()
            .AddRequestAuthorizer<ActivateTenantAuthorizer>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var tenant = new Tenant(tenantId);
        Assert.True(tenant.Register(creatorId, "Active", "active-tenant",
            creatorIsAdministrator: true).IsSuccess);
        Assert.True(tenant.ConfirmSlug("active-tenant").IsSuccess);
        Assert.True(tenant.Activate(creatorId, null).IsSuccess);
        await writer.SaveAsync(tenant, new RequestDispatchContext(RequestActor.System));

        // Act
        var recovery = await scope.ServiceProvider.GetRequiredService<IRequestBus>()
            .SendAsync(new ActivateTenant(Uuid.CreateVersion4(), tenantId, Uuid.CreateVersion4(), "recovery@example.com"),
                RequestActor.System);

        // Assert
        Assert.True(recovery.IsSuccess);
        Assert.True((await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new Tenant(tenantId))).IsActive);
    }

    [Fact]
    public async Task ShouldCheckpointActivationGivenRejectedSlugWithoutAdministratorProjection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var creatorId = Uuid.CreateVersion4();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships());
        services.AddSingleton<IPermissionAuthorizer>(new Permissions());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<Bdgrz.Compliance.Features.Content.Contracts.IImmutableContentStore>(
            new Bdgrz.Compliance.Features.Content.Persistence.FitzKvImmutableContentStore(new Cntryl.Fitz.Testing.InMemoryKvClient()));
        services.AddScoped<Bdgrz.Compliance.Features.NativeOperations.Models.NativeOperationExecutor>();
        services.AddPortia()
            .AddRequestHandler<ActivateTenantHandler>()
            .AddRequestAuthorizer<ActivateTenantAuthorizer>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var tenant = new Tenant(tenantId);
        Assert.True(tenant.Register(creatorId, "Rejected", "rejected",
            creatorIsAdministrator: true).IsSuccess);
        Assert.True(tenant.RejectSlug("rejected").IsSuccess);
        await writer.SaveAsync(tenant, new RequestDispatchContext(RequestActor.System));

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>()
            .SendAsync(new ActivateTenant(Uuid.CreateVersion4(), tenantId, creatorId), RequestActor.System);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False((await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new Tenant(tenantId))).IsActive);
    }

    [Fact]
    public async Task ShouldKeepCreatorProvisioningGivenUnmaterializedAdministratorGrant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var creatorId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenantId, creatorId);
        var memberships = new Memberships();
        var permissions = new Permissions();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton<ITenantMembershipDirectoryReader>(memberships);
        services.AddSingleton<IPermissionAuthorizer>(permissions);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<Bdgrz.Compliance.Features.Content.Contracts.IImmutableContentStore>(
            new Bdgrz.Compliance.Features.Content.Persistence.FitzKvImmutableContentStore(new Cntryl.Fitz.Testing.InMemoryKvClient()));
        services.AddScoped<Bdgrz.Compliance.Features.NativeOperations.Models.NativeOperationExecutor>();
        services.AddPortia()
            .AddRequestHandler<ActivateTenantHandler>()
            .AddRequestAuthorizer<ActivateTenantAuthorizer>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var tenant = new Tenant(tenantId);
        Assert.True(tenant.Register(creatorId, "Acme", "acme",
            creatorIsAdministrator: true).IsSuccess);
        Assert.True(tenant.ConfirmSlug("acme").IsSuccess);
        await writer.SaveAsync(tenant, new RequestDispatchContext(RequestActor.System));
        var member = new Member(tenantId, creatorId);
        Assert.True(member.Register().IsSuccess);
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        var teamMember = new TeamMember(tenantId, BuiltInRbac.AdministratorsTeamId(tenantId), memberId);
        Assert.True(teamMember.Assign(member.MembershipEpisodeId).IsSuccess);
        await writer.SaveAsync(teamMember, new RequestDispatchContext(RequestActor.System));

        // Act: event streams exist, but the two authorization projections have not caught up.
        var before = await bus.SendAsync(new ActivateTenant(Uuid.CreateVersion4(), tenantId, creatorId), RequestActor.System);
        memberships.Ready = true;
        permissions.Ready = true;
        var after = await bus.SendAsync(new ActivateTenant(Uuid.CreateVersion4(), tenantId, creatorId), RequestActor.System);

        // Assert
        Assert.False(before.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, before.Error.Kind);
        Assert.True(before.Error.IsTransient);
        Assert.True(after.IsSuccess);
        var hydrated = await reader.HydrateAsync(new Tenant(tenantId));
        Assert.True(hydrated.IsActive);
    }

    sealed class Memberships : ITenantMembershipDirectoryReader
    {
        public bool Ready { get; set; }

        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult<TenantMembershipView?>(
                Ready ? new TenantMembershipView(userId,
                    Uuid.Parse(tenantId, CultureInfo.InvariantCulture), "client_personnel") : null);

        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult(Ready);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }

    sealed class Permissions : IPermissionAuthorizer
    {
        public bool Ready { get; set; }

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission,
            CancellationToken ct = default) => ValueTask.FromResult(Ready);
    }
}
