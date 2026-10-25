using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Commitments;

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
