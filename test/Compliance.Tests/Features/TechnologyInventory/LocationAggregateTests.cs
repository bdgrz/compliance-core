using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory;

public sealed class LocationAggregateTests
{
    [Fact]
    public void ShouldRejectRevisionFromAnotherTenantGivenLocationReplay()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var locationId = Uuid.CreateVersion4();
        var source = NamedRecordFacts.RecordedLocation(tenantId, locationId);
        var history = new AggregateScenario<Location>(source).PendingEvents.ToArray();
        var revision = Assert.IsType<LocationRevisionRecorded>(history[1]);
        history[1] = revision with { TenantId = Uuid.CreateVersion4() };

        // Act
        var error = Record.Exception(() => NativeFacts.Replay(new Location(tenantId, locationId), history));

        // Assert
        Assert.IsType<InvalidOperationException>(error);
    }
}
