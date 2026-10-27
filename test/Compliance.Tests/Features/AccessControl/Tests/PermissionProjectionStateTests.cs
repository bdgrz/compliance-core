using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

/// <summary>
///     Regression coverage for a real bug: <see cref="PermissionProjectionState" /> previously keyed
///     <c>RolePermissions</c> by <see cref="Uuid" /> (a <c>Dictionary&lt;Uuid, HashSet&lt;string&gt;&gt;</c>),
///     which System.Text.Json's source-generated <c>UuidJsonConverter</c> cannot serialize as a
///     dictionary key. Every real write to <c>FitzPermissionProjection</c> against a live Fitz
///     broker threw mid-transaction, so no permission grant was ever durably written — every
///     tenant-scoped request was silently forbidden. Fixed by matching the edge-record pattern its
///     siblings (<c>TeamMembers</c>, <c>TeamRoles</c>) already used instead of a dictionary:
///     <c>RolePermissions</c> is now a flat <c>HashSet&lt;RolePermissionEdge&gt;</c>.
///     <see cref="Cntryl.Fitz.Testing.InMemoryKvClient" />-backed tests never exercise real
///     <see cref="JsonSerializer" /> round-trips, so nothing caught this until a manual end-to-end
///     run against a real broker did.
/// </summary>
public sealed class PermissionProjectionStateTests
{
    [Fact]
    public void ShouldAdvanceProjectionRevisionGivenAppliedAccessEvents()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var state = new PermissionProjectionState();
        var memberId = Uuid.CreateVersion4();
        var roleId = BuiltInRbac.ViewerRoleId(tenantId);

        // Act
        state.Apply(MemberFacts.Registered(tenantId, memberId, Uuid.CreateVersion4()));
        state.Apply(RbacFacts.RoleDefinedFact(tenantId, roleId, BuiltInRbac.ViewerRoleName));
        state.Apply(RbacFacts.RoleRenamedFact(tenantId, roleId, "Read only"));

        // Assert
        Assert.Equal(3, state.Revision);
        var restored = JsonSerializer.Deserialize(JsonSerializer.SerializeToUtf8Bytes(state,
            ComplianceCoreJsonContext.Default.PermissionProjectionState),
            ComplianceCoreJsonContext.Default.PermissionProjectionState);
        Assert.NotNull(restored);
        Assert.Equal(state.Revision, restored.Revision);
    }

    [Fact]
    public void ShouldRoundTripGivenRealJsonSerialization()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var teamId = Uuid.CreateVersion4();
        var roleId = BuiltInRbac.ComplianceManagementRoleId(tenantId);
        var state = new PermissionProjectionState();
        state.Apply(MemberFacts.Registered(tenantId, memberId, Uuid.CreateVersion4()));
        state.Apply(RbacFacts.TeamDefinedFact(tenantId, teamId, "Reviewers"));
        state.Apply(TeamMemberFacts.Assigned(tenantId, teamId, memberId));
        state.Apply(RbacFacts.RoleDefinedFact(tenantId, roleId, "Reviewer"));
        state.Apply(RbacPermissionFacts.Assigned(tenantId, roleId, "tenant:access"));
        state.Apply(RbacTeamRoleFacts.Assigned(tenantId, teamId, roleId));

        var json = JsonSerializer.SerializeToUtf8Bytes(state, ComplianceCoreJsonContext.Default.PermissionProjectionState);

        // Act
        var restored = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.PermissionProjectionState);

        // Assert
        Assert.NotNull(restored);
        var grant = Assert.Single(restored.Materialize());
        Assert.Equal(memberId, grant.MemberId);
        Assert.Equal("tenant:access", grant.Permission);
    }

    [Fact]
    public void ShouldMaterializePermissionGivenActiveMemberAssignedRole()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var teamId = Uuid.CreateVersion4();
        var roleId = BuiltInRbac.ComplianceManagementRoleId(tenantId);
        var state = new PermissionProjectionState();
        state.Apply(MemberFacts.Registered(tenantId, memberId, Uuid.CreateVersion4()));
        state.Apply(RbacFacts.TeamDefinedFact(tenantId, teamId, "Reviewers"));
        state.Apply(TeamMemberFacts.Assigned(tenantId, teamId, memberId));
        state.Apply(RbacFacts.RoleDefinedFact(tenantId, roleId, "Reviewer"));
        state.Apply(RbacPermissionFacts.Assigned(tenantId, roleId, "tenant:access"));

        // Act
        state.Apply(RbacTeamRoleFacts.Assigned(tenantId, teamId, roleId));

        // Assert
        var grant = Assert.Single(state.Materialize());

        Assert.Equal(memberId, grant.MemberId);
        Assert.Equal("tenant:access", grant.Permission);
    }

    [Fact]
    public void ShouldNotMaterializePermissionGivenFirmStaffTeamAssignment()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var teamId = Uuid.CreateVersion4();
        var roleId = BuiltInRbac.ComplianceManagementRoleId(tenantId);
        var state = new PermissionProjectionState();
        state.Apply(MemberFacts.Registered(tenantId, memberId, Uuid.CreateVersion4(), "firm_staff"));
        state.Apply(RbacFacts.TeamDefinedFact(tenantId, teamId, "Administrators"));
        state.Apply(RbacFacts.RoleDefinedFact(tenantId, roleId, "Tenant Administration"));
        state.Apply(RbacTeamRoleFacts.Assigned(tenantId, teamId, roleId));
        state.Apply(RbacPermissionFacts.Assigned(tenantId, roleId, RbacPermissions.TenantAccess));

        // Act
        state.Apply(TeamMemberFacts.Assigned(tenantId, teamId, memberId));

        // Assert
        Assert.Empty(state.Materialize());
        Assert.Empty(state.Explain(memberId));
    }

    [Fact]
    public void ShouldNotAuthorizeHistoricalCustomRoleGivenExistingTeamAssignment()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var teamId = Uuid.CreateVersion4();
        var legacyRoleId = Uuid.CreateVersion4();
        var state = new PermissionProjectionState();
        state.Apply(MemberFacts.Registered(tenantId, memberId, Uuid.CreateVersion4()));
        state.Apply(RbacFacts.TeamDefinedFact(tenantId, teamId, "Administrators"));
        state.Apply(TeamMemberFacts.Assigned(tenantId, teamId, memberId));
        state.Apply(RbacFacts.RoleDefinedFact(tenantId, legacyRoleId, "Legacy custom administrator"));
        state.Apply(RbacPermissionFacts.Assigned(tenantId, legacyRoleId, RbacPermissions.TenantRbacManage));
        state.Apply(RbacTeamRoleFacts.Assigned(tenantId, teamId, legacyRoleId));

        // Act
        var grants = state.Materialize();
        var paths = state.Explain(memberId);

        // Assert
        Assert.Empty(grants);
        Assert.Contains(paths, path => path.RoleId == legacyRoleId);
    }

    [Fact]
    public void ShouldRevokeAndRestorePermissionGivenMemberSuspensionAndReinstatement()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenantId, userId);
        var actorMemberId = Uuid.CreateVersion4();
        var teamId = BuiltInRbac.AdministratorsTeamId(tenantId);
        var roleId = BuiltInRbac.TenantAdministrationRoleId(tenantId);
        var at = DateTimeOffset.UtcNow;
        var state = new PermissionProjectionState();
        state.Apply(MemberFacts.Registered(tenantId, memberId, userId));
        state.Apply(RbacFacts.TeamDefinedFact(tenantId, teamId, "Administrators"));
        state.Apply(TeamMemberFacts.Assigned(tenantId, teamId, memberId));
        state.Apply(RbacFacts.RoleDefinedFact(tenantId, roleId, "Tenant Administration"));
        state.Apply(RbacTeamRoleFacts.Assigned(tenantId, teamId, roleId));
        state.Apply(RbacPermissionFacts.Assigned(tenantId, roleId, RbacPermissions.TenantAccess));

        // Act
        state.Apply(MemberFacts.Suspended(tenantId, memberId, userId, actorMemberId,
            "Alex Admin", at, "Employment ended."));
        var grantsWhileSuspended = state.Materialize();
        state.Apply(MemberFacts.Reinstated(tenantId, memberId, userId, "client_personnel",
            actorMemberId, "Alex Admin", at.AddHours(1)));
        var grantsAfterReinstatement = state.Materialize();

        // Assert
        Assert.Empty(grantsWhileSuspended);
        Assert.Collection(grantsAfterReinstatement, grant =>
        {
            Assert.Equal(memberId, grant.MemberId);
            Assert.Equal(RbacPermissions.TenantAccess, grant.Permission);
        });
    }

    [Fact]
    public void ShouldNotRestoreOldTeamPermissionGivenFreshMembershipEpisode()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenantId, userId);
        var teamId = Uuid.CreateVersion4();
        var roleId = BuiltInRbac.ComplianceManagementRoleId(tenantId);
        var actorMemberId = Uuid.CreateVersion4();
        var oldEpisodeId = Uuid.CreateVersion4();
        var newEpisodeId = Uuid.CreateVersion4();
        var at = DateTimeOffset.UtcNow;
        var state = new PermissionProjectionState();
        state.Apply(MemberFacts.Registered(tenantId, memberId, userId,
            membershipEpisodeId: oldEpisodeId));
        state.Apply(RbacFacts.TeamDefinedFact(tenantId, teamId, "Reviewers"));
        state.Apply(TeamMemberFacts.Assigned(tenantId, teamId, memberId, oldEpisodeId));
        state.Apply(RbacFacts.RoleDefinedFact(tenantId, roleId, "Reviewer"));
        state.Apply(RbacTeamRoleFacts.Assigned(tenantId, teamId, roleId));
        state.Apply(RbacPermissionFacts.Assigned(tenantId, roleId, "program:read"));

        // Act
        state.Apply(MemberFacts.Deprovisioned(tenantId, memberId, userId, actorMemberId,
            "Alex Admin", at, "Access is no longer required.", oldEpisodeId));
        state.Apply(MemberFacts.Registered(tenantId, memberId, userId,
            membershipEpisodeId: newEpisodeId));
        // A team assignment that was already in flight can project after the fresh invitation.
        state.Apply(TeamMemberFacts.Assigned(tenantId, teamId, memberId, oldEpisodeId));

        // Assert
        Assert.Empty(state.Materialize());
        Assert.Empty(state.Explain(memberId));
    }

    [Fact]
    public void ShouldRemovePermissionGivenTeamMemberRemoval()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var teamId = Uuid.CreateVersion4();
        var roleId = BuiltInRbac.ComplianceManagementRoleId(tenantId);
        var state = new PermissionProjectionState();
        state.Apply(MemberFacts.Registered(tenantId, memberId, Uuid.CreateVersion4()));
        state.Apply(RbacFacts.TeamDefinedFact(tenantId, teamId, "Reviewers"));
        state.Apply(TeamMemberFacts.Assigned(tenantId, teamId, memberId));
        state.Apply(RbacFacts.RoleDefinedFact(tenantId, roleId, "Reviewer"));
        state.Apply(RbacPermissionFacts.Assigned(tenantId, roleId, "tenant:access"));
        state.Apply(RbacTeamRoleFacts.Assigned(tenantId, teamId, roleId));

        // Act
        state.Apply(TeamMemberFacts.Removed(tenantId, teamId, memberId, Uuid.Empty, Uuid.CreateVersion4()));

        // Assert
        Assert.Empty(state.Materialize());
    }

    [Fact]
    public void ShouldExplainCurrentRolePathGivenPermissionChanges()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var teamId = Uuid.CreateVersion4();
        var roleId = BuiltInRbac.ComplianceManagementRoleId(tenantId);
        var state = new PermissionProjectionState();
        state.Apply(MemberFacts.Registered(tenantId, memberId, Uuid.CreateVersion4()));
        state.Apply(RbacFacts.TeamDefinedFact(tenantId, teamId, "Reviewers"));
        state.Apply(RbacFacts.RoleDefinedFact(tenantId, roleId, "Reviewer"));
        state.Apply(TeamMemberFacts.Assigned(tenantId, teamId, memberId));
        state.Apply(RbacTeamRoleFacts.Assigned(tenantId, teamId, roleId));
        state.Apply(RbacPermissionFacts.Assigned(tenantId, roleId, "tenant.access"));
        state.Apply(RbacPermissionFacts.Assigned(tenantId, roleId, "program.manage"));

        // Act
        var before = Assert.Single(state.Explain(memberId));
        state.Apply(RbacPermissionFacts.Removed(tenantId, roleId, "program.manage"));
        var after = Assert.Single(state.Explain(memberId));
        state.Apply(TeamMemberFacts.Removed(tenantId, teamId, memberId, Uuid.Empty, Uuid.CreateVersion4()));

        // Assert
        Assert.Equal(teamId, before.TeamId);
        Assert.Equal(roleId, before.RoleId);
        Assert.Equal(["program.manage", "tenant.access"], before.Permissions);
        Assert.Equal(["tenant.access"], after.Permissions);
        Assert.Empty(state.Explain(memberId));
        Assert.Empty(state.Explain(Uuid.CreateVersion4()));
    }
}
