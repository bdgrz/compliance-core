using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Handlers;
using Bdgrz.Compliance.Features.Tenants.History;
using Bdgrz.Compliance.Features.Tenants.Queries;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class PlatformOperatorRosterTests
{
    [Fact]
    public async Task ShouldRetainNewGrantGivenExactOldRetriesAcrossPerUserLifecycle()
    {
        // Arrange
        await using var provider = Services();
        var actor = Uuid.CreateVersion4();
        var target = Uuid.CreateVersion4();
        await SeedAsync(provider, actor);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var first = new GrantPlatformOperator(target, "First", Uuid.CreateVersion4(), 0);
        var revoke = new RevokePlatformOperator(target, "Ended", Uuid.CreateVersion4(), 1, first.CommandId);
        var next = new GrantPlatformOperator(target, "Replacement", Uuid.CreateVersion4(), 2);
        Assert.True((await bus.SendAsync(first, Actor(actor))).IsSuccess);
        Assert.True((await bus.SendAsync(revoke, Actor(actor))).IsSuccess);

        // Act
        var firstRetry = await bus.SendAsync(first, Actor(actor));
        var replacement = await bus.SendAsync(next, Actor(actor));
        var revokeRetry = await bus.SendAsync(revoke, Actor(actor));
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new PlatformOperator(target));

        // Assert
        Assert.True(firstRetry.IsSuccess);
        Assert.True(replacement.IsSuccess);
        Assert.True(revokeRetry.IsSuccess);
        Assert.Equal(3UL, owner.Version);
        Assert.True(owner.IsActive);
        Assert.Equal(next.CommandId, owner.GrantEpisodeId);
    }

    [Fact]
    public async Task ShouldDenyRetryBeforeResolvingAnAcceptedOperationGivenRevokedActor()
    {
        // Arrange
        await using var provider = Services();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        var target = Uuid.CreateVersion4();
        var firstGrant = await SeedAsync(provider, first);
        await SeedAsync(provider, second);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var operation = new GrantPlatformOperator(target, "Coverage", Uuid.CreateVersion4(), 0);
        Assert.True((await bus.SendAsync(operation, Actor(first))).IsSuccess);
        Assert.True((await bus.SendAsync(new RevokePlatformOperator(first, "No longer assigned", Uuid.CreateVersion4(), 1,
            firstGrant), Actor(second))).IsSuccess);

        // Act
        var retry = await bus.SendAsync(operation, Actor(first));
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new PlatformOperator(target));

        // Assert
        Assert.False(retry.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, retry.Error.Kind);
        Assert.Equal(1UL, owner.Version);
    }

    [Fact]
    public async Task ShouldReturnSourceRevisionsGivenCurrentOperatorStateQuery()
    {
        // Arrange
        await using var provider = Services();
        var actor = Uuid.CreateVersion4();
        var grant = await SeedAsync(provider, actor);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var operation = Uuid.CreateVersion4();
        var primary = new PrimaryPlatformOperators(operation);
        Assert.True(primary.ReserveInitialExisting(operation, 0, actor, grant, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), actor, new ActorAttribution(actor.ToString(), "bdgrz")).IsSuccess);
        await scope.ServiceProvider.GetRequiredService<IAggregateWriter>().SaveAsync(primary,
            new RequestDispatchContext(RequestActor.System));

        // Act
        var result = await bus.SendAsync(new GetPlatformOperatorState(actor), Actor(actor));
        var absent = await bus.SendAsync(new GetPlatformOperatorState(Uuid.CreateVersion4()), Actor(actor));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsActive);
        Assert.Equal(grant, result.Value.GrantEpisodeId);
        Assert.Equal(1UL, result.Value.OperatorRevision);
        Assert.Equal(1UL, result.Value.PrimaryRevision);
        Assert.Equal(1UL, result.Value.SeatGeneration);
        Assert.Equal("pending", result.Value.SeatStatus);
        Assert.Equal(1, result.Value.OccupiedPrimarySeats);
        Assert.Equal(0, result.Value.ConfirmedPrimarySeats);
        Assert.True(absent.IsSuccess);
        Assert.False(absent.Value.IsActive);
        Assert.Equal(0UL, absent.Value.OperatorRevision);
    }

    static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new PlatformOperatorAuthority([]));
        services.AddSingleton<IPlatformUserDirectoryReader, ExistingUsers>();
        services.AddScoped<IPlatformOperatorAccess, EventSourcedPlatformOperatorAccess>();
        services.AddScoped<PlatformOperatorOperations>();
        services.AddPortia().AddRequestHandler<GrantPlatformOperatorHandler>()
            .AddRequestHandler<RevokePlatformOperatorHandler>()
            .AddRequestHandler<GetPlatformOperatorStateHandler>()
            .AddRequestAuthorizer<PlatformOperatorAuthorizer>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    static async Task<Uuid> SeedAsync(ServiceProvider provider, Uuid user)
    {
        await using var scope = provider.CreateAsyncScope();
        var operation = Uuid.CreateVersion4();
        var owner = new PlatformOperator(user, operation);
        Assert.True(owner.GrantBootstrap(operation, 0, Uuid.CreateVersion4(), new ActorAttribution("portia:system", "Portia")).IsSuccess);
        await scope.ServiceProvider.GetRequiredService<IAggregateWriter>().SaveAsync(owner,
            new RequestDispatchContext(RequestActor.System));
        return operation;
    }

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString(), ClaimValueTypes.String, "bdgrz")], "BdgrzSession"));

    sealed class ExistingUsers : IPlatformUserDirectoryReader
    {
        public ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
}
