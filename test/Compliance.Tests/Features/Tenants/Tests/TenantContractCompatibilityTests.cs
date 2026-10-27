using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bdgrz.Compliance;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Models;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class TenantContractCompatibilityTests
{
    [Fact]
    public void ShouldReadSelfServiceAndLegacyRegistrationBodiesGivenOptionalInvitationField()
    {
        // Arrange
        const string selfService = """{"name":"Acme","slug":"acme","legal_name":"Acme LLC"}""";
        const string legacy = """{"name":"Acme","slug":"acme","first_administrator_email":"admin@example.com"}""";

        // Act
        var newRequest = JsonSerializer.Deserialize(selfService,
            ComplianceCoreJsonContext.Default.RegisterTenant);
        var oldRequest = JsonSerializer.Deserialize(legacy,
            ComplianceCoreJsonContext.Default.RegisterTenant);

        // Assert
        Assert.Null(newRequest?.FirstAdministratorEmail);
        Assert.Equal("admin@example.com", oldRequest?.FirstAdministratorEmail);
    }

    [Fact]
    public void ShouldRemainActiveGivenLegacyRegistrationWithoutInvitation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var operatorId = Uuid.CreateVersion4();
        var json = $$"""{"tenant_id":"{{tenantId}}","owner_user_id":"{{operatorId}}","name":"Acme","slug":"acme"}""";

        // Act
        var registered = JsonSerializer.Deserialize(json,
            (JsonTypeInfo)ComplianceCoreJsonContext.Default.TenantRegistered);

        // Assert
        Assert.NotNull(registered);
        Assert.Equal("TenantRegistered", registered.GetType().Name);
        Assert.Null(registered.GetType().GetProperty("FirstAdministratorEmail")?.GetValue(registered));
        Assert.False((bool)registered.GetType().GetProperty("CreatorIsAdministrator")!
            .GetValue(registered)!);
    }

    [Fact]
    public void ShouldRemainActiveGivenSelfServiceRegistrationBeforeActivationFence()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var creatorId = Uuid.CreateVersion4();
        var json = $$"""{"tenant_id":"{{tenantId}}","owner_user_id":"{{creatorId}}","name":"Acme","slug":"acme","creator_is_administrator":true}""";
        var registered = JsonSerializer.Deserialize(json,
            ComplianceCoreJsonContext.Default.TenantRegistered);
        Assert.NotNull(registered);
        var tenant = new Tenant(tenantId);

        // Act
        new AggregateScenario<Tenant>(tenant).Given(
            DomainEventSeed.Attach(registered, tenantId, 1),
            DomainEventSeed.Attach(TenantFacts.Confirmed(tenantId, "acme"), tenantId, 2));

        // Assert
        Assert.True(registered.CreatorIsAdministrator);
        Assert.False(registered.ActivationRequired);
        Assert.True(tenant.IsActive);
    }

    [Fact]
    public void ShouldDefaultToActiveGivenLegacyDirectoryRow()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var json = $$"""{"tenant_id":"{{tenantId}}","name":"Acme","slug":"acme"}""";

        // Act
        var view = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.TenantView);

        // Assert
        Assert.Equal("active", view?.Status);
    }

    [Fact]
    public void ShouldReadLegacyLifecycleEventsGivenReasonAndOccurrenceTimeWereNotStored()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var operatorId = Uuid.CreateVersion4();
        var json = $$"""{"tenant_id":"{{tenantId}}","operator_user_id":"{{operatorId}}"}""";

        // Act
        var suspended = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.TenantSuspended);
        var reactivated = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.TenantReactivated);

        // Assert
        Assert.NotNull(suspended);
        Assert.Null(suspended.Reason);
        Assert.Equal(default, suspended.OccurredAt);
        Assert.NotNull(reactivated);
        Assert.Null(reactivated.Reason);
        Assert.Equal(default, reactivated.OccurredAt);
    }
}
