using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Remediation.Authorizers;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Contracts;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class WaiverAuthorizationTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid User = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("request", WaiverPermissions.Request)]
    [InlineData("review", WaiverPermissions.Review)]
    [InlineData("revoke", WaiverPermissions.Review)]
    public async Task ShouldRequireOperationPermissionGivenActiveClientMember(string operation, string expectedPermission)
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(true);
        var authorizer = new WaiverManagementAuthorizer(new FixedMembershipDirectory(true), new ActiveTenant(),
            permissions, TimeProvider.System);
        var context = Context(Command(operation), "http");

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(expectedPermission, Assert.Single(permissions.Permissions));
    }

    [Theory]
    [InlineData("review", "mcp")]
    [InlineData("review", "direct")]
    [InlineData("revoke", "mcp")]
    [InlineData("revoke", "direct")]
    public async Task ShouldRefuseWithoutPermissionReadGivenPersonalDecisionOutsideHttp(string operation, string transport)
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(true);
        var authorizer = new WaiverManagementAuthorizer(new FixedMembershipDirectory(true), new ActiveTenant(),
            permissions, TimeProvider.System);

        // Act
        var result = await authorizer.AuthorizeAsync(Context(Command(operation), transport), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(permissions.Permissions);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("suspended")]
    [InlineData("firm_staff")]
    [InlineData("permission")]
    public async Task ShouldRefuseWaiverChangesGivenIneligibleMembershipOrPermission(string fault)
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(fault != "permission");
        var memberships = new FixedMembershipDirectory(fault != "missing",
            fault == "firm_staff" ? "firm_staff" : "client_personnel", isSuspended: fault == "suspended");
        var authorizer = new WaiverManagementAuthorizer(memberships, new ActiveTenant(), permissions, TimeProvider.System);

        // Act
        var result = await authorizer.AuthorizeAsync(Context(Command("request"), "http"), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        if (fault != "permission")
            Assert.Empty(permissions.Permissions);
    }

    static IWaiverManagementRequest Command(string operation) => operation switch
    {
        "review" => new ReviewWaiver(Uuid.CreateVersion4(), Tenant, Uuid.CreateVersion4(), 1, true),
        "revoke" => new RevokeWaiver(Uuid.CreateVersion4(), Tenant, Uuid.CreateVersion4(), 2, "Reason"),
        _ => new RequestWaiver(Uuid.CreateVersion4(), Tenant, "control_occurrence",
            [new WaiverScopeRecord("control_occurrence", Uuid.CreateVersion4(), 1)], "Reason", "Compensation", Now.AddDays(1)),
    };

    static RequestContext<IWaiverManagementRequest> Context(IWaiverManagementRequest request, string transport)
    {
        RequestInvocation invocation = transport switch
        {
            "mcp" => new McpInvocation("waiver"),
            "direct" => new DirectInvocation(),
            _ => new HttpInvocation("POST", "/waivers", "/waivers", "waiver"),
        };
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", User.ToString())], "BdgrzSession"));
        return new RequestContext<IWaiverManagementRequest>(request, new RequestDispatchContext(actor, invocation));
    }
}
