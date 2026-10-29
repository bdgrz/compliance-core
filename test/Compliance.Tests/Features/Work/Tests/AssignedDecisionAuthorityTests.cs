using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Boundaries.Commands;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Programs.Authorizers;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Responsibilities.Commands;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Queries;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class AssignedDecisionAuthorityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldExcludeBoundaryDecisionGivenAssignedDutyLacksSourceManagementGrant(bool approval)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var boundaryId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion5(boundaryId, "draft-1");
        var now = DateTimeOffset.UtcNow;
        var reviewId = Uuid.CreateVersion4();
        var content = new BoundaryContent("System boundary", "readiness", ["security"], []);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new BoundaryRules(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.True(boundary.Create(fixture.ProgramId, versionId, content,
                    fixture.LeadMemberId, "Lead", now.AddMinutes(-5)).IsSuccess);
                Assert.Null(boundary.AssignResponsibility(new ResponsibilityScope("boundary",
                        boundaryId, versionId, 1), Uuid.CreateVersion4(), fixture.ReviewerMemberId,
                    approval ? ResponsibilityType.PolicyApprover : ResponsibilityType.AssignedReviewer, fixture.LeadMemberId, "Lead",
                    now.AddMinutes(-4), now.AddMinutes(-4), null, []));
                if (approval)
                    Assert.Null(boundary.Review(versionId, 1, reviewId, "accept", "Reviewed.",
                        fixture.ApproverMemberId, "Reviewer", now.AddMinutes(-3)));
                return Result.Success;
            });
        fixture.Boundaries.Add(new BoundaryView(fixture.TenantId, boundaryId, fixture.ProgramId,
            new BoundaryVersionView(fixture.TenantId, boundaryId, fixture.ProgramId,
                versionId, 1, content, "draft", null, fixture.LeadMemberId, "Lead",
                now.AddMinutes(-5)), null, approval
                    ? new BoundaryDecisionView(fixture.TenantId, boundaryId, reviewId, versionId, 1,
                        "accept", fixture.ApproverMemberId, "Reviewer", "Reviewed.",
                        now.AddMinutes(-3), null, null, null)
                    : null, approval ? 2 : 1));
        var authorizer = new ProgramManagementAuthorizer(
            fixture.Provider.GetRequiredService<ITenantMembershipDirectoryReader>(),
            fixture.Provider.GetRequiredService<ITenantActivity>(),
            fixture.Provider.GetRequiredService<IAccessGrantPermissionAuthorizer>(),
            ProgramManagementServices.ResourceScopes(fixture.ProgramId));
        IProgramManagementRequest request = approval
            ? new ApproveBoundary(Uuid.CreateVersion4(), fixture.TenantId, boundaryId, versionId, 1, reviewId,
                fixture.Today, "Approved.", "impact")
            : new ReviewBoundary(Uuid.CreateVersion4(), fixture.TenantId, boundaryId, versionId, 1, "accept", "Reviewed.");
        var context = new RequestContext<IProgramManagementRequest>(request,
            ProgramManagementServices.Actor(fixture.ReviewerUserId));

        // Act
        var source = await authorizer.AuthorizeAsync(context, CancellationToken.None);
        var reviewer = await fixture.AsAsync(fixture.ReviewerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));
        var oversight = await fixture.AsAsync(fixture.LeadUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "all"));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, source.Error?.Kind);
        Assert.Empty(reviewer.Items);
        Assert.Equal(new WorkCountsView(0, 0, 0, 0), reviewer.Counts);
        Assert.Null(Assert.Single(oversight.Items).AssigneeMemberId);

        // Act: a current grant permits the same named source duty.
        fixture.Permissions.Managers.Add(fixture.ReviewerMemberId);
        var grantedSource = await authorizer.AuthorizeAsync(context, CancellationToken.None);
        var granted = await fixture.AsAsync(fixture.ReviewerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));

        // Assert
        Assert.True(grantedSource.IsSuccess);
        var item = Assert.Single(granted.Items);
        Assert.Equal(fixture.ReviewerMemberId, item.AssigneeMemberId);

        // Act: revocation removes action eligibility without erasing the duty.
        fixture.Permissions.Managers.Remove(fixture.ReviewerMemberId);
        var revokedSource = await authorizer.AuthorizeAsync(context, CancellationToken.None);
        var revoked = await fixture.AsAsync(fixture.ReviewerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));
        fixture.Permissions.Managers.Add(fixture.ReviewerMemberId);
        var restored = await fixture.AsAsync(fixture.ReviewerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, revokedSource.Error?.Kind);
        Assert.Empty(revoked.Items);
        Assert.Equal(item.WorkItemId, Assert.Single(restored.Items).WorkItemId);
    }
}
