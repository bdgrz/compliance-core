using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class RemoveRolePermissionRequestScenarioTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();
    const string Permission = "controls.read";

    [Fact]
    public async Task ShouldRejectUserPermissionMutationGivenTheFixedRoleCatalog()
    {
        // Arrange
        await using var provider = RbacManagementServices.Build(allowed: true,
            portia => portia.AddRequestHandler<RemoveRolePermissionHandler>());
        await Seed(provider);

        await RequestScenario.For(provider)
            .GivenActor(RbacManagementServices.Actor())
            // Act
            .When(new RemoveRolePermission(TenantId, RoleId, Permission, Uuid.CreateVersion4(), 1, RbacPermissionFacts.AssignmentId(TenantId, RoleId, Permission)))
            // Assert
            .ExpectDenied(RequestErrorKind.Forbidden)
            .ExpectNotHandled();
    }

    [Fact]
    public async Task ShouldDenyGivenAnActorWithoutTheTenantRbacManagePermission()
    {
        // Arrange
        await using var provider = RbacManagementServices.Build(allowed: false,
            portia => portia.AddRequestHandler<RemoveRolePermissionHandler>());
        await Seed(provider);

        await RequestScenario.For(provider)
            .GivenActor(RbacManagementServices.Actor())
            // Act
            .When(new RemoveRolePermission(TenantId, RoleId, Permission, Uuid.CreateVersion4(), 1, RbacPermissionFacts.AssignmentId(TenantId, RoleId, Permission)))
            // Assert
            .ExpectDenied(RequestErrorKind.Forbidden)
            .ExpectNotHandled();
    }

    static Task Seed(ServiceProvider provider)
    {
        var commandId = RbacPermissionFacts.AssignmentId(TenantId, RoleId, Permission);
        return RbacManagementServices.SeedAsync(provider,
            new RolePermission(TenantId, RoleId, Permission, commandId),
            permission => permission.Assign(commandId, 0, RbacPermissionFacts.Actor));
    }
}
