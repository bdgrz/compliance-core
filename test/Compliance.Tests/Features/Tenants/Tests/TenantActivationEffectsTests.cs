using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Services;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class TenantActivationEffectsTests
{
    [Fact]
    public void ShouldKeepOneActivationIdentityGivenTheSameCommittedSourceAndTarget()
    {
        // Arrange
        var source = Uuid.CreateVersion4();
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();

        // Act
        var first = TenantActivationEffects.Activate(source, tenant, user);
        var replay = TenantActivationEffects.Activate(source, tenant, user);
        var otherSource = TenantActivationEffects.Activate(Uuid.CreateVersion4(), tenant, user);
        var otherTarget = TenantActivationEffects.Activate(source, tenant, Uuid.CreateVersion4());

        // Assert
        Assert.NotEqual(Uuid.Empty, first.CommandId);
        Assert.Equal(first, replay);
        Assert.NotEqual(first.CommandId, otherSource.CommandId);
        Assert.NotEqual(first.CommandId, otherTarget.CommandId);
    }
}
