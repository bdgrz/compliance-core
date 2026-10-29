using System.Text.Json;
using Bdgrz.Compliance;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class TenantSlugChangeAttributionTests
{
    [Fact]
    public void ShouldRetainRequesterAndTimeGivenReplayBeforeSlugConfirmation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var ownerId = Uuid.CreateVersion4();
        var tenant = new Tenant(tenantId);
        Assert.True(tenant.Register(ownerId, "Acme", "acme").IsSuccess);
        Assert.True(tenant.ConfirmSlug("acme").IsSuccess);
        var memberId = RbacIds.Member(tenantId, Uuid.CreateVersion4());
        var requestedBy = ActorReference.ForMember(memberId, "Org Admin");
        var requestedAt = new DateTimeOffset(2026, 10, 3, 18, 30, 0, TimeSpan.Zero);
        Assert.True(tenant.RequestSlugChange("acme-next", requestedBy, requestedAt).IsSuccess);
        var history = new AggregateScenario<Tenant>(tenant).PendingEvents.ToArray();
        var requested = Assert.Single(history.OfType<TenantSlugChangeRequested>());
        var encoded = JsonSerializer.Serialize(requested,
            ComplianceCoreJsonContext.Default.TenantSlugChangeRequested);
        var persisted = JsonSerializer.Deserialize(encoded,
            ComplianceCoreJsonContext.Default.TenantSlugChangeRequested);
        Assert.NotNull(persisted);
        var replayHistory = history.Select(domainEvent =>
            domainEvent is TenantSlugChangeRequested ? persisted : domainEvent).ToArray();
        var replayed = new AggregateScenario<Tenant>(new Tenant(tenantId))
            .Given(replayHistory).Aggregate;

        // Act
        var confirmation = replayed.ConfirmSlug("acme-next");
        var confirmed = Assert.Single(new AggregateScenario<Tenant>(replayed).PendingEvents
            .OfType<TenantSlugChanged>());

        // Assert
        Assert.True(confirmation.IsSuccess);
        Assert.Equal("acme-next", replayed.CurrentSlug);
        Assert.Equal(requestedBy, confirmed.RequestedBy);
        Assert.Equal(requestedAt, confirmed.RequestedAt);
    }
}
