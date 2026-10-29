using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Commitments.Readers;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
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
using Bdgrz.Compliance.Features.Commitments.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Commitments.Tests;

public sealed class CommitmentOwnerScopedIdentityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldKeepBothDraftDecisionsDiscoverableGivenReusedLocalReviewOrApprovalId(bool sharedApproval)
    {
        // Arrange
        await using var native = new CommitmentProjectionScenario();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var shared = Uuid.CreateVersion4();
        var author = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var approver = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        var drafts = new List<Uuid>();
        foreach (var identifier in new[] { "SC-ONE", "SC-TWO" })
        {
            var draft = CommitmentDraft.IdFor(tenant, program, "service_commitment", identifier);
            drafts.Add(draft);
            var rules = new CommitmentRules(tenant, draft);
            Assert.True(rules.Create(program, Uuid.CreateVersion4(), Uuid.CreateVersion4(), "service_commitment", identifier,
                "Protect customer data", "Security commitment", "MSA 4.1", author, "Author", now).IsSuccess);
            var review = sharedApproval ? Uuid.CreateVersion4() : shared;
            Assert.Null(rules.Review(program, 1, review, "accept", "Security owner", "applicable", "supported",
                null, "Verified source", reviewer, "Reviewer", now.AddMinutes(1),
                sourceVerifiedReference: "MSA 4.1", sourceEvidence: "Checked the signed MSA"));
            Assert.Null(rules.Approve(program, 1, sharedApproval ? shared : Uuid.CreateVersion4(), review,
                new DateOnly(2027, 1, 1), "Independent approval", "impact", approver, "Approver", now.AddMinutes(2)));
            await native.PublishAsync(rules);
        }
        var directory = new FitzCommitmentDraftDirectory(native.Kv, native.Commitments);

        // Act
        await native.ProjectAsync(directory, directory.ApplyAsync, FitzCommitmentDraftDirectory.ProjectorName, tenant);

        // Assert
        foreach (var draft in drafts)
        {
            var decisions = await directory.ListDecisionsAsync(tenant, draft, 10, null);
            Assert.Equal(2, decisions.Items.Count);
            Assert.All(decisions.Items, decision => Assert.Equal(draft, decision.DraftId));
            Assert.Single(decisions.Items, decision => decision.DecisionId == shared);
            Assert.Equal(draft, (await directory.GetVersionAsync(tenant, draft, 1))?.DraftId);
            Assert.Equal(draft, (await directory.GetDecisionAsync(tenant, draft, shared))?.DraftId);
        }
        Assert.Null(await directory.GetDecisionAsync(tenant, Uuid.CreateVersion4(), shared));
        Assert.Null(await directory.GetDecisionAsync(Uuid.CreateVersion4(), drafts[0], shared));
    }
}
