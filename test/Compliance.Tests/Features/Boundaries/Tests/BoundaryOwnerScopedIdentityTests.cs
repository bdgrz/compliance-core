using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Readers;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
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
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Boundaries.Tests;

public sealed class BoundaryOwnerScopedIdentityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldKeepBothOwnersDiscoverableGivenReusedLocalDecisionOrSuccessorIdentity(bool sharedVersion)
    {
        // Arrange
        await using var native = new BoundaryProjectionScenario();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var boundaries = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        var command = Uuid.CreateVersion4();
        var shared = sharedVersion ? Uuid.CreateVersion5(command, "draft") : command;
        var author = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var approver = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        foreach (var boundary in boundaries)
        {
            var draft = Uuid.CreateVersion5(boundary, "draft-1");
            var rules = new BoundaryRules(tenant, boundary);
            Assert.True(rules.Create(program, draft, new BoundaryContent("Scope", "readiness", ["security"], []),
                author, "Author", now).IsSuccess);
            var review = sharedVersion ? Uuid.CreateVersion4() : shared;
            Assert.Null(rules.Review(draft, 1, review, "accept", "Independent review", reviewer, "Reviewer", now.AddMinutes(1)));
            if (sharedVersion)
            {
                Assert.Null(rules.Approve(draft, 1, Uuid.CreateVersion4(), review, new DateOnly(2027, 1, 1),
                    "Approved", "digest", approver, "Approver", now.AddMinutes(2)));
                Assert.Null(rules.ProposeSuccessor(draft, shared, new BoundaryContent("Successor scope", "readiness", ["security"], []),
                    author, "Author", now.AddMinutes(3), command));
                review = Uuid.CreateVersion4();
                Assert.Null(rules.Review(shared, 1, review, "accept", "Independent successor review", reviewer, "Reviewer", now.AddMinutes(4)));
                Assert.Null(rules.Approve(shared, 1, Uuid.CreateVersion4(), review, new DateOnly(2027, 2, 1),
                    "Approved", "digest", approver, "Approver", now.AddMinutes(5)));
            }
            await native.PublishAsync(rules);
        }
        var directory = new FitzBoundaryDirectory(native.Kv, native.Boundaries);

        // Act
        await native.ProjectAsync(directory, directory.ApplyAsync, FitzBoundaryDirectory.ProjectorName, tenant);

        // Assert
        foreach (var boundary in boundaries)
        {
            if (sharedVersion)
                Assert.Equal(boundary, (await directory.GetVersionAsync(tenant, boundary, shared))?.BoundaryId);
            else
                Assert.Equal(boundary, (await directory.GetDecisionAsync(tenant, boundary, shared))?.BoundaryId);
        }
    }
}
