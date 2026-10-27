using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Queries;
namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class WorkforceAuthorizerTests
{
    [Theory]
    [InlineData(false, true, "client_personnel", RequestErrorKind.NotFound)]
    [InlineData(true, false, "client_personnel", RequestErrorKind.Forbidden)]
    [InlineData(true, true, "firm_staff", RequestErrorKind.Forbidden)]
    public async Task ShouldDenyRosterGivenMissingMembershipGrantOrEngagement(bool member,
        bool permitted, string affiliation, RequestErrorKind expected)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var authorizer = new WorkforceAuthorizer(new FixedMembershipDirectory(member, affiliation),
            new ActiveTenant(), new RecordingPermissionAuthorizer(permitted));
        var context = new RequestContext<IWorkforceRequest>(new ListPeople(tenantId),
            BdgrzActor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(expected, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldRequireWorkforcePermissionGivenActiveTenantMember()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(true);
        var authorizer = new WorkforceAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), permissions);
        var context = new RequestContext<IWorkforceRequest>(
            new RecordPerson(Uuid.CreateVersion4(), tenantId, "Ada Lovelace"), BdgrzActor(userId));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(RbacPermissions.WorkforceManage, Assert.Single(permissions.Permissions));
        Assert.Equal(RbacIds.Member(tenantId, userId), Assert.Single(permissions.MemberIds));
    }

    [Fact]
    public async Task ShouldDenySuspendedMemberGivenHistoricalWorkforceGrant()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(true);
        var authorizer = new WorkforceAuthorizer(new FixedMembershipDirectory(true, isSuspended: true),
            new ActiveTenant(), permissions);
        var context = new RequestContext<IWorkforceRequest>(new ListPeople(Uuid.CreateVersion4()),
            BdgrzActor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(permissions.Permissions);
    }

    static ClaimsPrincipal BdgrzActor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));
}
