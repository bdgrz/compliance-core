using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class RoleRenameTests
{
    [Fact]
    public void ShouldRecordRenameGivenAnExistingRole()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var role = new Role(tenantId, roleId);
        _ = role.Define("Old role name");
        var scenario = new AggregateScenario<Role>(role);

        // Act
        var result = role.Rename("New role name");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains(scenario.PendingEvents, ev => ev is RoleRenamed
        {
            RoleId: var renamedId, Name: "New role name",
        } && renamedId == roleId);
    }

    [Fact]
    public void ShouldRefuseBuiltinCreationGivenRenameOnUninitializedTenant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var roleId = BuiltInRbac.TenantAdministrationRoleId(tenantId);
        var role = new Role(tenantId, roleId);
        var scenario = new AggregateScenario<Role>(role);

        // Act
        var result = role.Rename(BuiltInRbac.TenantAdministrationRoleName);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectBuiltInRenameHistoryGivenImmutableRoleIdentity()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var roleId = BuiltInRbac.ComplianceManagementRoleId(tenantId);
        var history = new AggregateScenario<Role>(new Role(tenantId, roleId))
            .Given(DomainEventSeed.Attach(RbacFacts.RoleDefinedFact(tenantId, roleId, "Compliance Manager"), roleId, 1));
        var renamed = DomainEventSeed.Attach(RbacFacts.RoleRenamedFact(tenantId, roleId,
            BuiltInRbac.ComplianceManagementRoleName), roleId, 2);

        // Act
        // Assert
        Assert.Throws<InvalidOperationException>(() => history.Given(renamed));
        Assert.Empty(history.PendingEvents);
    }
}
