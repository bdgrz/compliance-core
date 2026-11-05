using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class PrimaryOrgAdminsReadTests
{
    [Fact]
    public async Task ShouldSeparatePendingAndDesignatedGivenCurrentSourceSeats()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var firstUser = Uuid.CreateVersion4();
        var firstEpisode = Uuid.CreateVersion4();
        var firstDesignation = Uuid.CreateVersion4();
        var secondUser = Uuid.CreateVersion4();
        var secondEpisode = Uuid.CreateVersion4();
        var secondDesignation = Uuid.CreateVersion4();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var primaries = new PrimaryOrgAdmins(tenant);
        Assert.True(primaries.Designate(firstDesignation, firstUser, firstEpisode).IsSuccess);
        Assert.True(primaries.RecordOutcome(firstDesignation, firstUser, firstEpisode, null).IsSuccess);
        Assert.True(primaries.Designate(secondDesignation, secondUser, secondEpisode).IsSuccess);
        await writer.SaveAsync(primaries, new RequestDispatchContext(RequestActor.System));
        var handler = new GetPrimaryOrgAdminsHandler(reader);

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetPrimaryOrgAdmins>(
            new GetPrimaryOrgAdmins(tenant), RequestActor.System), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(tenant, result.Value.TenantId);
        Assert.Equal(1, result.Value.DesignatedCount);
        Assert.Equal(1, result.Value.PendingCount);
        var first = Assert.Single(result.Value.Seats, seat => seat.IsDesignated);
        Assert.Equal(firstUser, first.UserId);
        Assert.Equal(firstEpisode, first.MembershipEpisodeId);
        Assert.Equal(firstDesignation, first.DesignationId);
        var pending = Assert.Single(result.Value.Seats, seat => !seat.IsDesignated);
        Assert.Equal(secondUser, pending.UserId);
        Assert.Equal(secondEpisode, pending.MembershipEpisodeId);
    }

    [Fact]
    public async Task ShouldReadOnlyRequestedTenantGivenAnotherTenantsPrimarySeats()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var primaries = new PrimaryOrgAdmins(tenant);
        Assert.True(primaries.Designate(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4()).IsSuccess);
        await writer.SaveAsync(primaries, new RequestDispatchContext(RequestActor.System));
        var handler = new GetPrimaryOrgAdminsHandler(reader);

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetPrimaryOrgAdmins>(
            new GetPrimaryOrgAdmins(Uuid.CreateVersion4()), RequestActor.System), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Seats);
        Assert.Equal(0, result.Value.DesignatedCount);
        Assert.Equal(0, result.Value.PendingCount);
    }
}
