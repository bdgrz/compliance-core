using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class TenantRbacBootstrapReactorTests
{
    [Fact]
    public async Task ShouldLeaveBuiltInRoleCatalogAndFeaturePermissionsToDedicatedReactorsGivenRegisteredTenant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme"));

        // Act
        await scenario.RunAsync(new TenantRbacBootstrapReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests));

        // Assert
        Assert.DoesNotContain(scenario.SentRequests, request => request is DefineRole or RenameRole);
        Assert.DoesNotContain(scenario.SentRequests, request => request is DefineTeam
        {
            TeamId: var teamId,
        } && teamId == BuiltInRbac.ViewersTeamId(tenantId));
        Assert.DoesNotContain(scenario.SentRequests, request => request is AssignTeamRole
        {
            TeamId: var teamId,
            RoleId: var roleId,
        } && teamId == BuiltInRbac.ViewersTeamId(tenantId) &&
            roleId == BuiltInRbac.ViewerRoleId(tenantId));
        Assert.DoesNotContain(scenario.SentRequests, request => request is AssignRolePermission
        {
            RoleId: var roleId,
        } && roleId == BuiltInRbac.ViewerRoleId(tenantId));

        var featurePermissions = new HashSet<string>(StringComparer.Ordinal)
        {
            RbacPermissions.ApplicationRestrictedRead,
            RbacPermissions.ComplianceRecordRestrictedRead,
            RbacPermissions.ComplianceRecordRestrictedWrite,
            RbacPermissions.TechnologyInventoryRestrictedRead,
            RbacPermissions.EvidenceRedactionPrepare,
            RbacPermissions.EvidenceRedactionApprove,
            RbacPermissions.EvidenceArtifactRead,
        };
        Assert.DoesNotContain(scenario.SentRequests, request => request is AssignRolePermission
        {
            Permission: var permission,
        } && featurePermissions.Contains(permission));
    }

    [Fact]
    public async Task ShouldKeepCoreAccessGrantsInBootstrapGivenRegisteredTenant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var ownerId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, ownerId, "Acme", "acme"));

        // Act
        await scenario.RunAsync(new TenantRbacBootstrapReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests));

        // Assert
        Assert.Contains(scenario.SentRequests, request => request is AssignRolePermission
        {
            RoleId: var roleId,
            Permission: RbacPermissions.TenantAccess,
        } && roleId == BuiltInRbac.TenantAdministrationRoleId(tenantId));
        Assert.Contains(scenario.SentRequests, request => request is AssignRolePermission
        {
            RoleId: var roleId,
            Permission: RbacPermissions.TenantRbacManage,
        } && roleId == BuiltInRbac.TenantAdministrationRoleId(tenantId));
        Assert.Contains(scenario.SentRequests, request => request is AssignRolePermission
        {
            RoleId: var roleId,
            Permission: RbacPermissions.ProgramManage,
        } && roleId == BuiltInRbac.TenantAdministrationRoleId(tenantId));
        Assert.Contains(scenario.SentRequests, request => request is AssignRolePermission
        {
            RoleId: var roleId,
            Permission: RbacPermissions.TenantAccess,
        } && roleId == BuiltInRbac.ComplianceManagementRoleId(tenantId));
        Assert.Contains(scenario.SentRequests, request => request is AssignRolePermission
        {
            RoleId: var roleId,
            Permission: RbacPermissions.ProgramManage,
        } && roleId == BuiltInRbac.ComplianceManagementRoleId(tenantId));
        Assert.Contains(scenario.SentRequests, request => request is AssignRolePermission
        {
            RoleId: var roleId,
            Permission: RbacPermissions.TenantAccess,
        } && roleId == BuiltInRbac.ComplianceParticipationRoleId(tenantId));
    }

    [Fact]
    public async Task ShouldBootstrapCreatorGivenVerifiedSelfServiceRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var creatorId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, creatorId, "Acme", "acme",
            "Acme LLC", CreatorIsAdministrator: true, ActivationRequired: true));

        // Act
        await scenario.RunAsync(new TenantRbacBootstrapReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests));

        // Assert
        Assert.Contains(scenario.SentRequests, request => request is RegisterMember
        {
            UserId: var userId, Affiliation: "client_personnel",
        } && userId == creatorId);
        Assert.Contains(scenario.SentRequests, request => request is AssignTeamMember
        {
            TeamId: var teamId, MemberId: var memberId,
        } && teamId == BuiltInRbac.AdministratorsTeamId(tenantId) &&
            memberId == RbacIds.Member(tenantId, creatorId));
        Assert.DoesNotContain(scenario.SentRequests, request => request is ActivateTenant);
    }

    [Fact]
    public async Task ShouldProvisionCreatorWorkspaceBeforeNonCriticalDefaultsGivenTenantRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var creatorId = Uuid.CreateVersion4();
        var adminTeamId = BuiltInRbac.AdministratorsTeamId(tenantId);
        var adminRoleId = BuiltInRbac.TenantAdministrationRoleId(tenantId);
        var memberId = RbacIds.Member(tenantId, creatorId);
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, creatorId, "Acme", "acme",
                "Acme LLC", CreatorIsAdministrator: true, ActivationRequired: true));

        // Act
        await scenario.RunAsync(new TenantRbacBootstrapReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests));

        // Assert
        // These commands all descend from this TenantRegistered source event. Membership starts
        // projecting before the core admin grants, and activation is triggered only after those
        // grants and the admin team-role link have been sent.
        Assert.Collection(scenario.SentRequests.Take(7),
            request => Assert.Equal(new DefineTeam(tenantId, adminTeamId,
                BuiltInRbac.AdministratorsTeamName), request),
            request => Assert.Equal(new RegisterMember(tenantId, creatorId), request),
            request => Assert.Equal(new AssignTeamRole(tenantId, adminTeamId, adminRoleId), request),
            request => Assert.Equal(new AssignRolePermission(tenantId, adminRoleId,
                RbacPermissions.TenantAccess), request),
            request => Assert.Equal(new AssignRolePermission(tenantId, adminRoleId,
                RbacPermissions.TenantRbacManage), request),
            request => Assert.Equal(new AssignRolePermission(tenantId, adminRoleId,
                RbacPermissions.ProgramManage), request),
            request => Assert.Equal(new AssignTeamMember(tenantId, adminTeamId, memberId), request));
    }

    [Fact]
    public async Task ShouldBootstrapLaterTenantGivenEarlierSelfServiceActivationIsTransient()
    {
        // Arrange
        var firstTenantId = Uuid.CreateVersion4();
        var firstCreatorId = Uuid.CreateVersion4();
        var laterTenantId = Uuid.CreateVersion4();
        var laterCreatorId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(firstTenantId, firstCreatorId, "First", "first",
                "First LLC", CreatorIsAdministrator: true, ActivationRequired: true),
            new TenantRegistered(laterTenantId, laterCreatorId, "Later", "later",
                "Later LLC", CreatorIsAdministrator: true, ActivationRequired: true))
            .RespondTo<ActivateTenant>(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The first administrator is still being provisioned.", isTransient: true)));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new TenantRbacBootstrapReactor(checkpoints, scenario.Requests);

        // Act
        await scenario.RunAsync(reactor);

        // Assert
        Assert.Contains(scenario.SentRequests, request => request is AssignRolePermission
        {
            TenantId: var tenantId,
            Permission: RbacPermissions.TenantRbacManage,
        } && tenantId == laterTenantId);
        Assert.DoesNotContain(scenario.SentRequests, request => request is ActivateTenant);
        Assert.NotEqual(ProjectionCheckpoint.Start,
            await checkpoints.LoadAsync(new CheckpointIdentity(reactor.Name, reactor.Pattern)));
    }

    [Fact]
    public async Task ShouldPreserveInvitationBootstrapGivenLegacyRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var operatorId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, operatorId, "Acme", "acme",
            "Acme LLC", "admin@example.com"));

        // Act
        await scenario.RunAsync(new TenantRbacBootstrapReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests));

        // Assert
        Assert.Contains(scenario.SentRequests, request => request is DefineTeam);
        Assert.DoesNotContain(scenario.SentRequests, request => request is RegisterMember);
        Assert.DoesNotContain(scenario.SentRequests, request => request is AssignTeamMember);
    }

}
