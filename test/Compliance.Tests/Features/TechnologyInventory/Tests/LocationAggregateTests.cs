using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;

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
