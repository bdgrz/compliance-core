using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.ControlMappings.Aggregates;
using Bdgrz.Compliance.Features.ControlMappings.Commands;
using Bdgrz.Compliance.Features.ControlMappings.Events;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class FitzCriterionApplicabilityWorkItemDirectoryTests
{
    [Fact]
    public async Task ShouldRejectSkippedDecisionRevisionGivenProjectedHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var decisionId = Uuid.CreateVersion4();
        var editionId = Uuid.CreateVersion4();
        var proposerId = Uuid.CreateVersion4();
        var proposer = ActorReference.ForMember(proposerId, "Proposer");
        var proposedAt = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var directory = new FitzCriterionApplicabilityWorkItemDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(
            FitzCriterionApplicabilityWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "criterion-applicability"));
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        await directory.ApplyAsync(Proposed(tenantId, programId, decisionId, editionId,
            proposerId, proposer, 1, 1, proposedAt));

        // Act
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await directory.ApplyAsync(Reviewed(tenantId, programId, editionId, decisionId,
                ActorReference.ForMember(Uuid.CreateVersion4(), "Reviewer"), 3, 1, "reject",
                proposedAt.AddMinutes(1))));

        // Assert
        Assert.Contains("advance its existing source revision", failure.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldProjectOneReviewPerPendingVersionGivenDecisionLifecycle()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var decisionId = Uuid.CreateVersion4();
        var secondId = Uuid.CreateVersion4();
        var editionId = Uuid.CreateVersion4();
        var proposerId = Uuid.CreateVersion4();
        var reviewerId = Uuid.CreateVersion4();
        var proposer = ActorReference.ForMember(proposerId, "Proposer");
        var reviewer = ActorReference.ForMember(reviewerId, "Reviewer");
        var proposedAt = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var directory = new FitzCriterionApplicabilityWorkItemDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(
            FitzCriterionApplicabilityWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "criterion-applicability"));
        var firstCheckpoint = new ProjectionCheckpoint(new EventCursor("applicability-version-one"));
        var secondCheckpoint = new ProjectionCheckpoint(new EventCursor("applicability-version-two"));
        var reviewedCheckpoint = new ProjectionCheckpoint(new EventCursor("applicability-reviewed"));
        var withdrawnCheckpoint = new ProjectionCheckpoint(new EventCursor("applicability-withdrawn"));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(Proposed(tenantId, programId, decisionId, editionId,
                proposerId, proposer, 1, 1, proposedAt));
            await batch.CommitAsync(firstCheckpoint);
        }
        var firstProposal = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         firstCheckpoint)))
        {
            await directory.ApplyAsync(Reviewed(tenantId, programId, editionId, decisionId, reviewer,
                2, 1, "reject", proposedAt.AddMinutes(1)));
            await directory.ApplyAsync(Proposed(tenantId, programId, secondId, editionId,
                proposerId, proposer, 3, 2, proposedAt.AddMinutes(2)));
            await batch.CommitAsync(secondCheckpoint);
        }
        var secondProposal = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         secondCheckpoint)))
        {
            await directory.ApplyAsync(Reviewed(tenantId, programId, editionId, secondId, reviewer,
                4, 2, "accept", proposedAt.AddMinutes(3)));
            await batch.CommitAsync(reviewedCheckpoint);
        }
        var accepted = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         reviewedCheckpoint)))
        {
            await directory.ApplyAsync(new CriterionNotApplicableWithdrawn(tenantId, programId, secondId, 5, 2, proposer, proposedAt.AddMinutes(4), editionId, "AC-2.4", NativeFacts.Ref(MappingLineContent.Schema), new MappingLineHeader(5, 2, secondId, null, null, true), NativeFacts.Root(new CriterionApplicability(tenantId, programId, editionId, "AC-2.4"),
                    Uuid.CreateVersion4(), "WithdrawCriterionNotApplicable", 4, proposerId, proposedAt.AddMinutes(4)), NativeFacts.Ref(), NativeFacts.Ref()));
            await batch.CommitAsync(withdrawnCheckpoint);
        }
        var withdrawn = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);

        // Assert
        Assert.True(firstProposal.IsSuccess);
        var first = Assert.Single(firstProposal.Value);
        Assert.Equal(WorkSource.CriterionApplicabilityReview, first.Kind);
        Assert.Equal(decisionId, first.SourceId);
        Assert.Equal("Review not-applicable proposal for AC-2.4", first.Summary);
        Assert.Equal("A criterion not-applicable proposal is awaiting independent review.",
            first.Reason);
        Assert.Equal("review", first.NextAction);
        Assert.Equal($"/api/v1/tenants/{tenantId}/programs/{programId}/" +
            $"criterion-applicability/{decisionId}/reviews", first.ActionPath);
        Assert.Equal(new OperatingHolder(OperatingAuthority.ProgramManagerHolder, programId),
            first.Responsible);
        Assert.Contains(proposerId, first.Excluded);
        Assert.Equal(proposedAt, first.CreatedAt);
        Assert.Equal(WorkSource.CriterionApplicabilityReviewWorkItemId(CriterionApplicability.IdentityOf(tenantId, programId,
            editionId, "AC-2.4"), decisionId, 1),
            first.WorkItemId);

        Assert.True(secondProposal.IsSuccess);
        var second = Assert.Single(secondProposal.Value);
        Assert.NotEqual(first.WorkItemId, second.WorkItemId);
        Assert.Equal(secondId, second.SourceId);
        Assert.Contains(proposerId, second.Excluded);
        Assert.Empty(accepted.Value);
        Assert.Empty(withdrawn.Value);
        Assert.Equal(5, await directory.LoadRevisionAsync(tenantId));
        Assert.Equal(withdrawnCheckpoint, await directory.LoadCheckpointAsync(tenantId));
    }

    static CriterionNotApplicableProposed Proposed(Uuid tenantId, Uuid programId,
        Uuid decisionId, Uuid editionId, Uuid proposerId, ActorReference proposer,
        long revision, int versionNumber, DateTimeOffset proposedAt) =>
        new(tenantId, programId, decisionId, revision, versionNumber, editionId, "AC-2.4", proposerId, proposer, proposedAt, NativeFacts.Ref(MappingLineContent.Schema), new MappingLineHeader(revision, versionNumber, decisionId, new MappingLineSlot(decisionId, versionNumber, proposerId), null, false), NativeFacts.Root(new CriterionApplicability(tenantId, programId, editionId, "AC-2.4"), decisionId,
                "ProposeCriterionNotApplicable", revision - 1, proposerId, proposedAt), NativeFacts.Ref(), NativeFacts.Ref());

    static CriterionApplicabilityReviewed Reviewed(Uuid tenantId, Uuid programId, Uuid editionId,
        Uuid decisionId, ActorReference reviewer, long revision, int versionNumber,
        string outcome, DateTimeOffset reviewedAt)
    {
        var reviewId = Uuid.CreateVersion4();
        return new CriterionApplicabilityReviewed(tenantId, programId, decisionId, revision, versionNumber, reviewId, outcome, reviewer, reviewedAt, null, editionId, "AC-2.4", NativeFacts.Ref(MappingLineContent.Schema), new MappingLineHeader(revision, versionNumber, decisionId, null, (outcome == "accept" ? new MappingLineSlot(decisionId, versionNumber, Uuid.Parse(reviewer.Id, System.Globalization.CultureInfo.InvariantCulture)) : null), false), NativeFacts.Root(new CriterionApplicability(tenantId, programId, editionId, "AC-2.4"), reviewId,
                "ReviewCriterionApplicability", revision - 1, Uuid.Parse(reviewer.Id,
                    System.Globalization.CultureInfo.InvariantCulture), reviewedAt), NativeFacts.Ref(), NativeFacts.Ref());
    }
}
