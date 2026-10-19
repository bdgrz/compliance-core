using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ComplianceRecordRestrictedAccessGrantBackfillReactorTests
{
    [Fact]
    public async Task ShouldBackfillRestrictedComplianceRecordAccessForBuiltInLeadsGivenHistoricalRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme"));

        // Act
        await scenario.RunAsync(new ComplianceRecordRestrictedAccessGrantBackfillReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests));

        // Assert
        Assert.Equal(4, scenario.SentRequests.Count);
        Assert.Contains(scenario.SentRequests, request => request is AssignRolePermission
        {
            TenantId: var actualTenant,
            RoleId: var roleId,
            Permission: RbacPermissions.ComplianceRecordRestrictedRead,
        } && actualTenant == tenantId &&
            roleId == BuiltInRbac.TenantAdministrationRoleId(tenantId));
        Assert.Contains(scenario.SentRequests, request => request is AssignRolePermission
        {
            TenantId: var actualTenant,
            RoleId: var roleId,
            Permission: RbacPermissions.ComplianceRecordRestrictedRead,
        } && actualTenant == tenantId &&
            roleId == BuiltInRbac.ComplianceManagementRoleId(tenantId));
        Assert.Contains(scenario.SentRequests, request => request is AssignRolePermission
        {
            TenantId: var actualTenant,
            RoleId: var roleId,
            Permission: RbacPermissions.ComplianceRecordRestrictedWrite,
        } && actualTenant == tenantId &&
            roleId == BuiltInRbac.TenantAdministrationRoleId(tenantId));
        Assert.Contains(scenario.SentRequests, request => request is AssignRolePermission
        {
            TenantId: var actualTenant,
            RoleId: var roleId,
            Permission: RbacPermissions.ComplianceRecordRestrictedWrite,
        } && actualTenant == tenantId &&
            roleId == BuiltInRbac.ComplianceManagementRoleId(tenantId));
    }
}
