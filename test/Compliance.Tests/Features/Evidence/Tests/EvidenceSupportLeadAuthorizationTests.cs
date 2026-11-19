using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Programs.Authorizers;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.Evidence.Tests;

public sealed class EvidenceSupportLeadAuthorizationTests
{
    [Theory]
    [InlineData("lead", true)]
    [InlineData("admin", false)]
    [InlineData("custom", false)]
    [InlineData("revoked_lead", false)]
    [InlineData("other_program_lead", false)]
    public async Task ShouldRequireCurrentExactProgramLeadGivenEvidenceSupportWithdrawal(string authority, bool allowed)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var permissions = new Permissions(tenant, program, authority);
        var authorizer = new ProgramManagementAuthorizer(new Membership(tenant, user), new ActiveTenant(),
            permissions, ProgramManagementServices.ResourceScopes(), rolePermissions: permissions);
        var command = new WithdrawEvidenceSupport(Uuid.CreateVersion4(), tenant, program,
            Uuid.CreateVersion4(), 1, "Mistaken target");
        var context = new RequestContext<IProgramManagementRequest>(command,
            ProgramManagementServices.Actor(user));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(allowed, result.IsSuccess);
        if (!allowed)
            Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(0, permissions.GenericCalls);
        Assert.Equal(1, permissions.RoleCalls);
        Assert.Equal(tenant, permissions.Tenant);
        Assert.Equal(program, permissions.Program);
        Assert.Equal(user, permissions.User);
        Assert.Equal(RbacIds.Member(tenant, user), permissions.Member);
        Assert.Equal(BuiltInRbac.ComplianceManagementRoleId(tenant), permissions.Role);
        Assert.Equal(IProgramScopedRequest.ManagementPermission, permissions.Permission);
    }

    sealed class Permissions(Uuid tenant, Uuid program, string authority)
        : IAccessGrantPermissionAuthorizer, IAccessGrantRolePermissionAuthorizer
    {
        public int GenericCalls { get; private set; }
        public int RoleCalls { get; private set; }
        public Uuid Tenant { get; private set; }
        public Uuid Program { get; private set; }
        public Uuid User { get; private set; }
        public Uuid Member { get; private set; }
        public Uuid Role { get; private set; }
        public string? Permission { get; private set; }

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, Uuid programId,
            string permission, CancellationToken ct = default)
        {
            GenericCalls++;
            // Admin/custom role has both generic management and named withdrawal permission;
            // neither substitutes for the required current built-in Lead identity.
            return ValueTask.FromResult(true);
        }

        public ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId, Uuid userId,
            Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(new ProgramAccessVisibility(true, new HashSet<Uuid>()));

        public ValueTask<bool> IsAllowedAsRoleAsync(Uuid tenantId, Uuid userId, Uuid memberId, Uuid programId,
            Uuid roleId, string permission, CancellationToken ct = default)
        {
            RoleCalls++;
            (Tenant, Program, User, Member, Role, Permission) = (tenantId, programId, userId, memberId, roleId, permission);
            return ValueTask.FromResult(authority == "lead" && tenantId == tenant && programId == program &&
                memberId == RbacIds.Member(tenant, userId) && roleId == BuiltInRbac.ComplianceManagementRoleId(tenant));
        }
    }

    sealed class Membership(Uuid tenant, Uuid user) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenantId == tenant.ToString() && userId == user ? new(user, tenant) : null);
        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenantId == tenant.ToString() && userId == user);
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }

    sealed class ActiveTenant : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
}
