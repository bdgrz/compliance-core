using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class FitzControlMappingWorkItemDirectoryTests
{
    [Fact]
    public async Task ShouldRejectSkippedSourceRevisionGivenMappingEvent()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var controlVersionId = Uuid.CreateVersion4();
        var editionId = Uuid.CreateVersion4();
        var mappingId = Uuid.CreateVersion4();
        var proposerId = Uuid.CreateVersion4();
        var directory = new FitzControlMappingWorkItemDirectory(new InMemoryKvClient());

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity(FitzControlMappingWorkItemDirectory.ProjectorName,
                EventStreamPattern.ForPattern(tenantId.ToString(), "control-criterion-mappings")),
            ProjectionCheckpoint.Start));
        await directory.ApplyAsync(Proposed(tenantId, programId, controlId, mappingId,
            controlVersionId, editionId, proposerId, 1, 1,
            new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero)));

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await directory.ApplyAsync(Proposed(tenantId, programId, controlId, mappingId,
                controlVersionId, editionId, proposerId, 3, 2,
                new DateTimeOffset(2026, 10, 5, 12, 2, 0, TimeSpan.Zero))));
    }

    [Fact]
    public async Task ShouldRejectProposalGivenOperationIdentityDifferentFromMappingId()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var controlVersionId = Uuid.CreateVersion4();
        var editionId = Uuid.CreateVersion4();
        var proposerId = Uuid.CreateVersion4();
        var proposedAt = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var mappingId = Uuid.CreateVersion4();
        var directory = new FitzControlMappingWorkItemDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzControlMappingWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "control-criterion-mappings"));

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        var proposal = Proposed(tenantId, programId, controlId, mappingId, controlVersionId,
            editionId, proposerId, 1, 1, proposedAt);
        proposal = proposal with { Operation = proposal.Operation with { LogicalId = Uuid.CreateVersion4() } };

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await directory.ApplyAsync(proposal));
    }

    [Fact]
    public async Task ShouldProjectOneReviewPerPendingVersionGivenMappingLifecycle()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var controlVersionId = Uuid.CreateVersion4();
        var editionId = Uuid.CreateVersion4();
        var mappingId = Uuid.CreateVersion4();
        var secondId = Uuid.CreateVersion4();
        var proposerId = Uuid.CreateVersion4();
        var reviewerId = Uuid.CreateVersion4();
        var proposedAt = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var directory = new FitzControlMappingWorkItemDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzControlMappingWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "control-criterion-mappings"));
        var firstCheckpoint = new ProjectionCheckpoint(new EventCursor("mapping-version-one"));
        var secondCheckpoint = new ProjectionCheckpoint(new EventCursor("mapping-version-two"));
        var reviewedCheckpoint = new ProjectionCheckpoint(new EventCursor("mapping-reviewed"));
        var retiredCheckpoint = new ProjectionCheckpoint(new EventCursor("mapping-retired"));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(Proposed(tenantId, programId, controlId, mappingId,
                controlVersionId, editionId, proposerId, 1, 1, proposedAt));
            await batch.CommitAsync(firstCheckpoint);
        }
        var firstProposal = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         firstCheckpoint)))
        {
            await directory.ApplyAsync(Reviewed(tenantId, programId, controlId, editionId, mappingId, reviewerId,
                2, 1, "accept", proposedAt.AddMinutes(1)));
            await directory.ApplyAsync(Proposed(tenantId, programId, controlId, secondId,
                controlVersionId, editionId, proposerId, 3, 2, proposedAt.AddMinutes(2)));
            await batch.CommitAsync(secondCheckpoint);
        }
        var secondProposal = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         secondCheckpoint)))
        {
            await directory.ApplyAsync(Reviewed(tenantId, programId, controlId, editionId, secondId, reviewerId,
                4, 2, "reject", proposedAt.AddMinutes(3)));
            await batch.CommitAsync(reviewedCheckpoint);
        }
        var resolved = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         reviewedCheckpoint)))
        {
            await directory.ApplyAsync(new ControlCriterionMappingRetired(tenantId, programId,
                mappingId, 5, 1, "Mapping is no longer needed.", reviewerId, "Reviewer",
                proposedAt.AddMinutes(4), controlId, editionId, "AC-2.4",
                NativeFacts.Root(new ControlCriterionMapping(tenantId, programId, controlId, editionId, "AC-2.4"),
                    Uuid.CreateVersion4(), "RetireControlCriterionMapping", 4, reviewerId, proposedAt.AddMinutes(4)),
                NativeFacts.Ref(), NativeFacts.Ref()));
            await batch.CommitAsync(retiredCheckpoint);
        }
        var retired = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);

        // Assert
        Assert.True(firstProposal.IsSuccess);
        var first = Assert.Single(firstProposal.Value);
        Assert.Equal(WorkSource.ControlCriterionMappingReview, first.Kind);
        Assert.Equal(mappingId, first.SourceId);
        Assert.Equal(controlId, first.ControlId);
        Assert.Equal("Review control mapping for AC-2.4", first.Summary);
        Assert.Equal("A control-to-criteria mapping proposal is awaiting independent review.",
            first.Reason);
        Assert.Equal("review", first.NextAction);
        Assert.Equal($"/api/v1/tenants/{tenantId}/programs/{programId}/" +
            $"control-mappings/{mappingId}/reviews", first.ActionPath);
        Assert.Equal(new OperatingHolder(OperatingAuthority.ProgramManagerHolder, programId),
            first.Responsible);
        Assert.Contains(proposerId, first.Excluded);
        Assert.Equal(proposedAt, first.CreatedAt);
        Assert.Equal(WorkSource.ControlCriterionMappingReviewWorkItemId(ControlCriterionMapping.IdentityOf(tenantId, programId,
            controlId, editionId, "AC-2.4"), mappingId, 1),
            first.WorkItemId);

        Assert.True(secondProposal.IsSuccess);
        var second = Assert.Single(secondProposal.Value);
        Assert.NotEqual(first.WorkItemId, second.WorkItemId);
        Assert.Equal(secondId, second.SourceId);
        Assert.Contains(proposerId, second.Excluded);
        Assert.Empty(resolved.Value);
        Assert.Empty(retired.Value);
        Assert.Equal(5, await directory.LoadRevisionAsync(tenantId));
        Assert.Equal(retiredCheckpoint, await directory.LoadCheckpointAsync(tenantId));
    }

    [Fact]
    public async Task ShouldIsolateMappingReviewsByTenantAndProgramGivenScopedRead()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var otherProgramId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var controlVersionId = Uuid.CreateVersion4();
        var editionId = Uuid.CreateVersion4();
        var proposerId = Uuid.CreateVersion4();
        var mappingId = Uuid.CreateVersion4();
        var proposedAt = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var directory = new FitzControlMappingWorkItemDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzControlMappingWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "control-criterion-mappings"));
        var checkpoint = new ProjectionCheckpoint(new EventCursor("mapping-proposed"));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(Proposed(tenantId, programId, controlId, mappingId,
                controlVersionId, editionId, proposerId, 1, 1, proposedAt));
            await batch.CommitAsync(checkpoint);
        }
        var correctScope = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);
        var otherProgram = await directory.LoadProgramAsync(tenantId, otherProgramId,
            CancellationToken.None);
        var otherTenant = await directory.LoadProgramAsync(otherTenantId, programId,
            CancellationToken.None);

        // Assert
        Assert.True(correctScope.IsSuccess);
        Assert.Single(correctScope.Value);
        Assert.True(otherProgram.IsSuccess);
        Assert.Empty(otherProgram.Value);
        Assert.True(otherTenant.IsSuccess);
        Assert.Empty(otherTenant.Value);
    }

    static ControlCriterionMappingProposed Proposed(Uuid tenantId, Uuid programId, Uuid controlId,
        Uuid mappingId, Uuid controlVersionId, Uuid editionId, Uuid proposerId, long revision,
        int versionNumber, DateTimeOffset proposedAt) => new(tenantId, programId, mappingId,
        revision, versionNumber, controlId, controlVersionId, editionId, "AC-2.4", "security",
        "Map the control to the criterion.", "The control enforces the requirement.", proposerId,
        "Proposer", proposedAt,
        NativeFacts.Root(new ControlCriterionMapping(tenantId, programId, controlId, editionId, "AC-2.4"), mappingId,
            "ProposeControlCriterionMapping", revision - 1, proposerId, proposedAt), NativeFacts.Ref(),
        NativeFacts.Ref());

    static ControlCriterionMappingReviewed Reviewed(Uuid tenantId, Uuid programId, Uuid controlId, Uuid editionId,
        Uuid mappingId, Uuid reviewerId, long revision, int versionNumber, string outcome,
        DateTimeOffset reviewedAt)
    {
        var decisionId = Uuid.CreateVersion4();
        return new ControlCriterionMappingReviewed(tenantId, programId, mappingId, revision, versionNumber, decisionId,
            outcome, "Reviewed the mapping rationale.", reviewerId, "Reviewer", reviewedAt, null, controlId, editionId,
            "AC-2.4",
            NativeFacts.Root(new ControlCriterionMapping(tenantId, programId, controlId, editionId, "AC-2.4"),
                decisionId, "ReviewControlCriterionMapping", revision - 1, reviewerId, reviewedAt), NativeFacts.Ref(),
            NativeFacts.Ref());
    }
}
