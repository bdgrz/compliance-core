using System.Collections;
using System.Reflection;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

public sealed class AccessExpectationNativeBoundaryTests
{
    [Fact]
    public async Task ShouldKeepBoundedExpectationAuthorityGivenManyApprovedSuccessorsOnOneLine()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var parameters = new AccessExpectationParameters(PrincipalKind: "access_token");
        var current = await fixture.ApprovedExpectationAsync(AccessReviewVocabulary.ForbiddenPrincipalKind, parameters);
        var lineId = current.ExpectationId;
        for (var index = 0; index < 48; index++)
        {
            var proposal = await fixture.SendAsync(fixture.ManagerUserId, fixture.Propose(
                AccessReviewVocabulary.ForbiddenPrincipalKind, parameters, revision: current.Revision,
                supersedes: current.ExpectationId, rationale: $"Reviewed successor {index + 1}.",
                effectiveFrom: AccessReviewFixture.Observed));
            current = await fixture.SendAsync(fixture.ApproverUserId,
                fixture.Approve(proposal.ExpectationId, proposal.Revision));
        }

        // Act
        var owner = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new AccessExpectation(fixture.TenantId, lineId));
        var retained = typeof(AccessExpectation).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.GetValue(owner) is ICollection { Count: > 1 }).ToArray();

        // Assert
        Assert.Empty(retained);
        Assert.Equal(98, owner.Revision);
        Assert.Equal(fixture.InstanceId, owner.SystemInstanceId);
        Assert.Equal(AccessExpectation.Approved, current.Status);
    }
}
