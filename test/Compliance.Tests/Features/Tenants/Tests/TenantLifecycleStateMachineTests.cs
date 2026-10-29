using System.Globalization;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

/// <summary>The Tenant lifecycle state machine adopted by decision TENANT_Lifecycle (2026-10-29).</summary>
public sealed class TenantLifecycleStateMachineTests
{
    static readonly Uuid TenantId = Uuid.Parse(
        "6b1f3c0e-52f4-4c3b-9a35-0f1f1f6c9a11",
        CultureInfo.InvariantCulture);
    static readonly Uuid OwnerUserId = Uuid.Parse(
        "a7d1c3e2-8f60-4b0d-8d4e-3c2a9b7e5f21",
        CultureInfo.InvariantCulture);
    static readonly DateTimeOffset At = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRefuseOffboardingGivenSlugAwaitingConfirmation()
    {
        // Arrange
        var tenant = new Tenant(TenantId);
        _ = tenant.Register(OwnerUserId, "Acme", "acme");
        var scenario = new AggregateScenario<Tenant>(tenant);
        var before = scenario.PendingEvents.Count;

        // Act
        var result = tenant.StartOffboarding(OwnerUserId, "Too early", At);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(tenant.IsOffboarding);
        Assert.Equal(before, scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldRefuseOffboardingGivenInvitedAdministratorNotYetActivated()
    {
        // Arrange
        var tenant = new Tenant(TenantId);
        _ = tenant.Register(OwnerUserId, "Acme", "acme", "Acme LLC", "admin@example.com");
        _ = tenant.ConfirmSlug("acme");
        var scenario = new AggregateScenario<Tenant>(tenant);
        var before = scenario.PendingEvents.Count;

        // Act
        var result = tenant.StartOffboarding(OwnerUserId, "Not yet in service", At);
        var prepared = tenant.PrepareLifecycle(nameof(OffboardTenant), "Not yet in service", (long)tenant.Revision);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, prepared.Error?.Kind);
        Assert.False(tenant.IsOffboarding);
        Assert.Equal(before, scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldStartOffboardingGivenInvitedAdministratorActivated()
    {
        // Arrange
        var tenant = new Tenant(TenantId);
        _ = tenant.Register(OwnerUserId, "Acme", "acme", "Acme LLC", "admin@example.com");
        _ = tenant.ConfirmSlug("acme");
        var activation = tenant.Activate(OwnerUserId, "admin@example.com");
        var scenario = new AggregateScenario<Tenant>(tenant);

        // Act
        var prepared = tenant.PrepareLifecycle(nameof(OffboardTenant), "Client left", (long)tenant.Revision);
        var result = tenant.StartOffboarding(OwnerUserId, "Client left", At);

        // Assert
        Assert.True(activation.IsSuccess);
        Assert.True(prepared.IsSuccess);
        Assert.True(result.IsSuccess);
        Assert.True(tenant.IsOffboarding);
        Assert.Single(scenario.PendingEvents.OfType<TenantOffboardingStarted>());
    }

    [Fact]
    public void ShouldRefuseOffboardingGivenRejectedSlugRegistration()
    {
        // Arrange
        var tenant = new Tenant(TenantId);
        _ = tenant.Register(OwnerUserId, "Acme", "acme");
        _ = tenant.RejectSlug("acme");
        var scenario = new AggregateScenario<Tenant>(tenant);
        var before = scenario.PendingEvents.Count;

        // Act
        var result = tenant.StartOffboarding(OwnerUserId, "Registration failed", At);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal(before, scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldStartOffboardingGivenSuspendedTenant()
    {
        // Arrange
        var tenant = new Tenant(TenantId);
        _ = tenant.Register(OwnerUserId, "Acme", "acme");
        _ = tenant.ConfirmSlug("acme");
        _ = tenant.Suspend(OwnerUserId, "Security review", At);
        var scenario = new AggregateScenario<Tenant>(tenant);

        // Act
        var result = tenant.StartOffboarding(OwnerUserId, "Client left", At.AddHours(1));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(tenant.IsOffboarding);
        Assert.False(tenant.IsActive);
        Assert.Single(scenario.PendingEvents.OfType<TenantOffboardingStarted>());
    }

    [Fact]
    public void ShouldPermitNativeOffboardingGivenSuspendedTenant()
    {
        // Arrange
        var tenant = new Tenant(TenantId);
        _ = tenant.Register(OwnerUserId, "Acme", "acme");
        _ = tenant.ConfirmSlug("acme");
        _ = tenant.Suspend(OwnerUserId, "Security review", At);

        // Act
        var prepared = tenant.PrepareLifecycle(nameof(OffboardTenant), "Client left", (long)tenant.Revision);

        // Assert
        Assert.True(prepared.IsSuccess);
    }

    [Fact]
    public void ShouldRefuseNativeOffboardingGivenSlugAwaitingConfirmation()
    {
        // Arrange
        var tenant = new Tenant(TenantId);
        _ = tenant.Register(OwnerUserId, "Acme", "acme");

        // Act
        var prepared = tenant.PrepareLifecycle(nameof(OffboardTenant), "Too early", (long)tenant.Revision);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, prepared.Error?.Kind);
    }

    [Fact]
    public void ShouldRefuseLifecycleDecisionGivenStaleRevision()
    {
        // Arrange
        var tenant = new Tenant(TenantId);
        _ = tenant.Register(OwnerUserId, "Acme", "acme");
        _ = tenant.ConfirmSlug("acme");
        var stale = (long)tenant.Revision + 1;

        // Act
        var suspend = tenant.PrepareLifecycle(nameof(SuspendTenant), "Stale read", stale);
        var offboard = tenant.PrepareLifecycle(nameof(OffboardTenant), "Stale read", stale);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, suspend.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, offboard.Error?.Kind);
    }

    [Fact]
    public void ShouldRefuseSuspensionGivenOffboarding()
    {
        // Arrange
        var tenant = new Tenant(TenantId);
        _ = tenant.Register(OwnerUserId, "Acme", "acme");
        _ = tenant.ConfirmSlug("acme");
        _ = tenant.StartOffboarding(OwnerUserId, "Client left", At);
        var scenario = new AggregateScenario<Tenant>(tenant);
        var before = scenario.PendingEvents.Count;

        // Act
        var result = tenant.Suspend(OwnerUserId, "After offboarding", At.AddMinutes(1));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(tenant.IsSuspended);
        Assert.Equal(before, scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldRefuseRegistrationGivenSurrenderedSlug()
    {
        // Arrange
        var tenant = new Tenant(TenantId);
        _ = tenant.Register(OwnerUserId, "Acme", "acme");
        _ = tenant.ConfirmSlug("acme");
        _ = tenant.RequestSlugSurrender("acme");
        _ = tenant.ConfirmSlugSurrender("acme");
        var scenario = new AggregateScenario<Tenant>(tenant);
        var before = scenario.PendingEvents.Count;

        // Act
        var result = tenant.Register(OwnerUserId, "Acme", "acme-again");

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before, scenario.PendingEvents.Count);
        Assert.Single(scenario.PendingEvents.OfType<TenantRegistered>());
    }

    [Fact]
    public void ShouldRestoreLifecycleStateGivenReplayOfValidHistory()
    {
        // Arrange
        var source = new Tenant(TenantId);
        _ = source.Register(OwnerUserId, "Acme", "acme");
        _ = source.ConfirmSlug("acme");
        _ = source.Suspend(OwnerUserId, "Security review", At);
        _ = source.StartOffboarding(OwnerUserId, "Client left", At.AddHours(1));
        var history = new AggregateScenario<Tenant>(source).PendingEvents.ToArray();

        // Act
        var replayed = new AggregateScenario<Tenant>(new Tenant(TenantId)).Given(history).Aggregate;

        // Assert
        Assert.True(replayed.IsSuspended);
        Assert.True(replayed.IsOffboarding);
        Assert.False(replayed.IsActive);
        Assert.Equal(source.Revision, replayed.Revision);
        Assert.Equal(RequestErrorKind.Conflict, replayed.Register(OwnerUserId, "Acme", "acme").Error?.Kind);
        Assert.True(replayed.StartOffboarding(OwnerUserId, "Repeat", At.AddHours(2)).IsSuccess);
    }
}
