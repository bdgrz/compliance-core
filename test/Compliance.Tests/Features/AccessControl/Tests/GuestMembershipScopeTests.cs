using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class GuestMembershipScopeTests
{
    [Fact]
    public void ShouldRefuseGuestWithoutFactGivenMissingProgramCap()
    {
        // Arrange
        var member = new Member(Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        var result = member.PrepareRegister("guest", new DateOnly(2027, 1, 1));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.False(member.IsRegistered);
    }

    [Fact]
    public async Task ShouldRetainGuestProgramCapGivenRegistrationReplay()
    {
        // Arrange
        await using var provider = RbacManagementServices.Build(true, portia => portia.AddRequestHandler<RegisterMemberHandler>());
        await using var scope = provider.CreateAsyncScope();
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var programs = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        var request = new RegisterMember(Uuid.CreateVersion4(), tenant, user, "guest", new DateOnly(2027, 1, 1), programs);
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();

        // Act
        var result = await bus.SendAsync(request, RequestActor.System);
        var restored = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new Member(tenant, user));
        var retry = await bus.SendAsync(request, RequestActor.System);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(retry.IsSuccess);
        Assert.True(programs.ToHashSet().SetEquals(restored.GuestProgramIds));
        Assert.Equal(1UL, restored.Version);
    }
}
