using Bdgrz.Compliance.Features.Boundaries.Aggregates;
using Bdgrz.Compliance.Features.Boundaries.Commands;
using Bdgrz.Compliance.Features.Boundaries.Events;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Boundaries.Queries;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.Independence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Boundaries;

public sealed class FitzBoundaryDirectoryTests
{
    [Fact]
    public async Task ShouldProjectWaiverEvidenceGivenBoundaryReview()
    {
        // Arrange
        await using var native = new BoundaryProjectionScenario();
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var draftId = Uuid.CreateVersion5(boundaryId, "draft-1");
        var reviewer = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var rules = new BoundaryRules(tenantId, boundaryId);
        Assert.True(rules.Create(programId, draftId, new BoundaryContent("Scope", "readiness", ["security"], []),
            reviewer, "Author", now).IsSuccess);
        var waiver = new SeparationOfDutiesWaiver(tenantId, Uuid.CreateVersion4());
        Assert.Null(waiver.Record(new SeparationOfDutiesWaiverScope("boundary", boundaryId, draftId, 1,
            SeparationOfDutiesActions.Review), reviewer, Uuid.CreateVersion4(), "Requester", "Exact independent exception",
            now.AddMinutes(-2), now.AddHours(1)));
        Assert.Null(waiver.Approve(Uuid.CreateVersion4(), "Independent approver", now.AddMinutes(-1)));
        await native.Writer.SaveAsync(waiver, new RequestDispatchContext(RequestActor.System));
        var resolver = new SeparationOfDutiesWaiverProofResolver(native.Reader, new EventStoreSourceFactResolver(native.Events));
        var proof = await resolver.ResolveAsync(new SeparationOfDutiesWaiverProofScope(tenantId, waiver.Id, reviewer,
            reviewer, SeparationOfDutiesActions.Review, "boundary", boundaryId, draftId, 1), now.AddMinutes(1), CancellationToken.None);
        Assert.True(proof.IsSuccess, proof.Error?.Message);
        Assert.Null(rules.Review(draftId, 1, Uuid.CreateVersion4(), "accept", "Self-reviewed under approved waiver.",
            reviewer, "Reviewer", now.AddMinutes(1), proof.Value));
        var facts = await native.PublishAsync(rules);
        var directory = new FitzBoundaryDirectory(native.Kv, native.Boundaries);

        // Act
        await native.ProjectAsync(directory, directory.ApplyAsync, FitzBoundaryDirectory.ProjectorName, tenantId, facts);

        // Assert
        var decision = await directory.GetDecisionAsync(tenantId, boundaryId,
            (await directory.GetAsync(tenantId, boundaryId))!.LatestDecision!.DecisionId);
        Assert.Equal(waiver.Id, decision?.SeparationOfDutiesWaiverId);
        Assert.True(decision?.SeparationOfDutiesWaived);
    }

    [Fact]
    public async Task ShouldWaitForExactProjectedRevisionGivenImpactPreview()
    {
        // Arrange
        await using var native = new BoundaryProjectionScenario();
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var draftId = Uuid.CreateVersion5(boundaryId, "draft-1");
        var authorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var content = new BoundaryContent("Initial", "readiness", ["security"], []);
        var directory = new FitzBoundaryDirectory(native.Kv, native.Boundaries);
        var impact = new BoundaryImpactService(directory, native.Boundaries,
            [new ProgramBoundaryImpactContributor()]);
        var identity = new CheckpointIdentity(FitzBoundaryDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString()));

        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await PublishAndProjectAsync(native, directory, new BoundaryDraftCreatedContent(tenantId, boundaryId,
                programId, draftId, content, authorId, "Author", now));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        var requested = new PreviewBoundaryImpact(tenantId, boundaryId, draftId, 2);

        // Act
        var lagged = await impact.PreviewAsync(requested, CancellationToken.None);

        // Assert
        Assert.False(lagged.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, lagged.Error.Kind);

        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await PublishAndProjectAsync(native, directory, new BoundaryDraftRevisedContent(tenantId, boundaryId,
                draftId, 2, content with { Statement = "Revised" }, authorId,
                "Author", now.AddMinutes(1)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        var caughtUp = await impact.PreviewAsync(requested, CancellationToken.None);
        Assert.True(caughtUp.IsSuccess);
        Assert.True(caughtUp.Value.Complete);
        Assert.Contains(caughtUp.Value.Changes, change =>
            change.Field == "statement" && change.ProposedValue == "Revised");
    }

    [Fact]
    public async Task ShouldRollBackAndRetryGivenFailedProjectionBatch()
    {
        // Arrange
        await using var native = new BoundaryProjectionScenario();
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var draftId = Uuid.CreateVersion5(boundaryId, "draft-1");
        var authorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var content = new BoundaryContent("Original", "readiness", ["security"], []);
        var directory = new FitzBoundaryDirectory(native.Kv, native.Boundaries);

        // Act
        var identity = new CheckpointIdentity(FitzBoundaryDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString()));


        // Assert
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await PublishAndProjectAsync(native, directory, new BoundaryDraftCreatedContent(tenantId, boundaryId,
                programId, draftId, content, authorId, "Author", now));
            var malformedRules = await native.Boundaries.RulesAsync(tenantId, boundaryId);
            var validRevision = new BoundaryDraftRevisedContent(tenantId, boundaryId, draftId, 2,
                content with { Statement = "Revised" }, authorId, "Author", now.AddMinutes(1));
            var root = await FreezeProjectionAsync(native, malformedRules, validRevision, 2);
            var malformed = Assert.IsType<BoundaryDraftRevised>(root) with { DraftVersionId = Uuid.CreateVersion4() };
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await directory.ApplyAsync(malformed));
        }

        Assert.Null(await directory.GetAsync(tenantId, boundaryId));

        await using (var retry = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await PublishAndProjectAsync(native, directory, new BoundaryDraftCreatedContent(tenantId, boundaryId,
                programId, draftId, content, authorId, "Author", now));
            await PublishAndProjectAsync(native, directory, new BoundaryDraftRevisedContent(tenantId, boundaryId,
                draftId, 2, content with { Statement = "Revised" }, authorId,
                "Author", now.AddMinutes(1)));
            await retry.CommitAsync(ProjectionCheckpoint.Start);
        }

        Assert.Equal("Revised", (await directory.GetAsync(tenantId, boundaryId))?
            .Draft?.Content.Statement);
    }

    [Fact]
    public async Task ShouldRemoveCurrentDraftGivenDiscardOfNeverReviewedVersion()
    {
        // Arrange
        await using var native = new BoundaryProjectionScenario();
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var draftId = Uuid.CreateVersion5(boundaryId, "draft-1");
        var authorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var content = new BoundaryContent("Unused", "readiness", ["security"], []);
        var directory = new FitzBoundaryDirectory(native.Kv, native.Boundaries);
        var identity = new CheckpointIdentity(FitzBoundaryDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString()));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await PublishAndProjectAsync(native, directory, new BoundaryDraftCreatedContent(tenantId, boundaryId,
                programId, draftId, content, authorId, "Author", now));
            await PublishAndProjectAsync(native, directory, new BoundaryDraftDiscardedContent(tenantId, boundaryId,
                draftId, 1, authorId, "Author", "Wrong scope", now.AddMinutes(1)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        Assert.Null(await directory.GetAsync(tenantId, boundaryId));
        Assert.Empty((await directory.ListProgramAsync(tenantId, programId, 20, null)).Items);
    }

    [Fact]
    public async Task ShouldPreserveEffectiveHistoryGivenApprovedSuccessor()
    {
        // Arrange
        await using var native = new BoundaryProjectionScenario();
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var originalId = Uuid.CreateVersion5(boundaryId, "draft-1");
        var successorId = native.NewSuccessorVersion();
        var authorId = Uuid.CreateVersion4();
        var reviewerId = Uuid.CreateVersion4();
        var reviewId = Uuid.CreateVersion4();
        var approvalId = Uuid.CreateVersion4();
        var nextReviewId = Uuid.CreateVersion4();
        var nextApprovalId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var original = new BoundaryContent("Original scope", "readiness", ["security"],
            [new BoundaryScopeEntry(Uuid.CreateVersion4(), "inclusion", "service",
                "Service A", null, "Compliance lead", "Core service", true)]);
        var successor = original with { Statement = "Scope with provider B" };
        var directory = new FitzBoundaryDirectory(native.Kv, native.Boundaries);
        var identity = new CheckpointIdentity(FitzBoundaryDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString()));


        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await PublishAndProjectAsync(native, directory, new BoundaryDraftCreatedContent(tenantId, boundaryId,
                programId, originalId, original, authorId, "Author", now));
            await PublishAndProjectAsync(native, directory, new BoundaryReviewedContent(tenantId, boundaryId,
                originalId, 1, reviewId, "accept", reviewerId,
                "Reviewer", "Reviewed", now.AddMinutes(1)));
            await PublishAndProjectAsync(native, directory, new BoundaryApprovedContent(tenantId, boundaryId,
                originalId, 1, approvalId, reviewId, reviewerId, "Reviewer",
                "Approved", new DateOnly(2027, 1, 1), now.AddMinutes(2), "digest-1"));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var predecessorBefore = await directory.GetVersionAsync(tenantId, boundaryId,
            originalId);
        var predecessorHash = SnapshotContentIdentity.ApprovedBoundaryVersion(
            Assert.IsType<BoundaryVersionView>(predecessorBefore));

        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await PublishAndProjectAsync(native, directory, new BoundarySuccessorProposedContent(tenantId, boundaryId,
                successorId, originalId, successor, authorId, "Author", now.AddDays(1)));
            await PublishAndProjectAsync(native, directory, new BoundaryReviewedContent(tenantId, boundaryId,
                successorId, 1, nextReviewId, "accept", reviewerId,
                "Reviewer", "Reviewed", now.AddDays(1).AddMinutes(1)));
            await PublishAndProjectAsync(native, directory, new BoundaryApprovedContent(tenantId, boundaryId,
                successorId, 1, nextApprovalId, nextReviewId, reviewerId, "Reviewer",
                "Approved", new DateOnly(2027, 2, 1), now.AddDays(1).AddMinutes(2), "digest-2"));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        Assert.Null(await directory.GetEffectiveVersionAsync(tenantId, boundaryId,
            new DateOnly(2026, 12, 31)));
        Assert.Equal(originalId, (await directory.GetEffectiveVersionAsync(tenantId,
            boundaryId, new DateOnly(2027, 1, 31)))?.VersionId);
        Assert.Equal(successorId, (await directory.GetEffectiveVersionAsync(tenantId,
            boundaryId, new DateOnly(2027, 2, 1)))?.VersionId);
        Assert.Equal(successorId, (await directory.GetEffectiveVersionAsync(tenantId,
            boundaryId, new DateOnly(2028, 1, 1)))?.VersionId);
        Assert.Equal(successorId, (await directory.GetEffectiveVersionAsync(tenantId,
            boundaryId, DateOnly.MaxValue))?.VersionId);
        var predecessorAfter = await directory.GetVersionAsync(tenantId, boundaryId,
            originalId);
        Assert.Equal(predecessorHash, SnapshotContentIdentity.ApprovedBoundaryVersion(
            Assert.IsType<BoundaryVersionView>(predecessorAfter)));
        Assert.Equal([originalId, successorId],
            (await directory.ListVersionsAsync(tenantId, boundaryId, 20, null))?
            .Items.Select(item => item.VersionId));
        Assert.Equal("digest-1", (await directory.GetDecisionAsync(tenantId,
            boundaryId, approvalId))?.ImpactDigest);
        Assert.Equal(reviewId, (await directory.GetDecisionAsync(tenantId,
            boundaryId, approvalId))?.ReliesOnDecisionId);
        Assert.Null(await directory.GetVersionAsync(Uuid.CreateVersion4(),
            boundaryId, originalId));
        Assert.Null(await directory.GetDecisionAsync(Uuid.CreateVersion4(),
            boundaryId, approvalId));
    }

    [Fact]
    public async Task ShouldRejectOverlappingEffectiveDateGivenReplayedSuccessor()
    {
        // Arrange
        await using var native = new BoundaryProjectionScenario();
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var originalId = Uuid.CreateVersion5(boundaryId, "draft-1");
        var successorId = native.NewSuccessorVersion();
        var authorId = Uuid.CreateVersion4();
        var reviewerId = Uuid.CreateVersion4();
        var originalReviewId = Uuid.CreateVersion4();
        var successorReviewId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var content = new BoundaryContent("Original scope", "readiness", ["security"], []);
        var effectiveFrom = new DateOnly(2027, 1, 1);
        var directory = new FitzBoundaryDirectory(native.Kv, native.Boundaries);
        var identity = new CheckpointIdentity(FitzBoundaryDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString()));
        var originalCheckpoint = new ProjectionCheckpoint(new EventCursor("original-approved"));
        await using (var first = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await PublishAndProjectAsync(native, directory, new BoundaryDraftCreatedContent(tenantId, boundaryId,
                programId, originalId, content, authorId, "Author", now));
            await PublishAndProjectAsync(native, directory, new BoundaryReviewedContent(tenantId, boundaryId,
                originalId, 1, originalReviewId, "accept", reviewerId, "Reviewer",
                "Reviewed", now));
            await PublishAndProjectAsync(native, directory, new BoundaryApprovedContent(tenantId, boundaryId,
                originalId, 1, Uuid.CreateVersion4(), originalReviewId, reviewerId,
                "Reviewer", "Approved", effectiveFrom, now, "digest"));
            await first.CommitAsync(originalCheckpoint);
        }

        // Act: only the projection dependency receives the malformed replay; no invalid native owner is written.
        var replayRules = await native.Boundaries.RulesAsync(tenantId, boundaryId);
        var replaySequence = (await native.ReadAsync(tenantId, boundaryId)).Count;
        await using (var successor = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, originalCheckpoint)))
        {
            await directory.ApplyAsync(await FreezeProjectionAsync(native, replayRules,
                new BoundarySuccessorProposedContent(tenantId, boundaryId, successorId, originalId, content,
                    authorId, "Author", now), ++replaySequence));
            await directory.ApplyAsync(await FreezeProjectionAsync(native, replayRules,
                new BoundaryReviewedContent(tenantId, boundaryId, successorId, 1, successorReviewId, "accept",
                    reviewerId, "Reviewer", "Reviewed", now), ++replaySequence));
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(await FreezeProjectionAsync(native, replayRules,
                    new BoundaryApprovedContent(tenantId, boundaryId, successorId, 1, Uuid.CreateVersion4(), successorReviewId,
                        reviewerId, "Reviewer", "Approved", effectiveFrom, now, "digest"), ++replaySequence)));
        }

        // Assert
        var current = await directory.GetAsync(tenantId, boundaryId);
        Assert.Equal(originalId, current?.LatestApprovedVersion?.VersionId);
        Assert.Null(current?.Draft);
        Assert.Null(await directory.GetDecisionAsync(tenantId, boundaryId,
            successorReviewId));
        Assert.Null(await directory.GetVersionAsync(tenantId, boundaryId, successorId));
        Assert.Equal(originalId, (await directory.GetEffectiveVersionAsync(tenantId,
            boundaryId, effectiveFrom))?.VersionId);
        Assert.Equal(originalCheckpoint, await directory.LoadCheckpointAsync(identity));
    }

    [Fact]
    public async Task ShouldSelectEffectiveVersionGivenHistoryAcrossProjectionPages()
    {
        // Arrange
        await using var native = new BoundaryProjectionScenario();
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var authorId = Uuid.CreateVersion4();
        var reviewerId = Uuid.CreateVersion4();
        var firstDate = new DateOnly(2027, 1, 1);
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var content = new BoundaryContent("Scope", "readiness", ["security"], []);
        var directory = new FitzBoundaryDirectory(native.Kv, native.Boundaries);
        var identity = new CheckpointIdentity(FitzBoundaryDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString()));
        var versionIds = new List<Uuid>();

        var rules = new BoundaryRules(tenantId, boundaryId);

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            Uuid? predecessorId = null;
            for (var index = 0; index < 201; index++)
            {
                var versionId = index == 0 ? Uuid.CreateVersion5(boundaryId, "draft-1") : native.NewSuccessorVersion();
                var reviewId = Uuid.CreateVersion4();
                versionIds.Add(versionId);
                if (predecessorId is { } prior)
                    Assert.Null(rules.ProposeSuccessor(prior, versionId, content, authorId, "Author", now,
                        commandId: native.SuccessorCommand(versionId)));
                else
                    Assert.True(rules.Create(programId, versionId, content, authorId, "Author", now).IsSuccess);
                Assert.Null(rules.Review(versionId, 1, reviewId, "accept", "Reviewed", reviewerId, "Reviewer", now));
                Assert.Null(rules.Approve(versionId, 1, Uuid.CreateVersion4(), reviewId, firstDate.AddDays(index),
                    "Approved", "digest", reviewerId, "Reviewer", now));
                foreach (var fact in await native.PublishAsync(rules))
                    await directory.ApplyAsync(fact);
                predecessorId = versionId;
            }
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var firstPage = await directory.ListVersionsAsync(tenantId, boundaryId, 200, null);
        var nextPage = await directory.ListVersionsAsync(tenantId, boundaryId, 200,
            firstPage?.NextCursor);

        // Assert
        Assert.Null(await directory.GetEffectiveVersionAsync(tenantId, boundaryId,
            firstDate.AddDays(-1)));
        Assert.Equal(versionIds[199], (await directory.GetEffectiveVersionAsync(tenantId,
            boundaryId, firstDate.AddDays(199)))?.VersionId);
        Assert.Equal(versionIds[200], (await directory.GetEffectiveVersionAsync(tenantId,
            boundaryId, firstDate.AddDays(200)))?.VersionId);
        Assert.Equal(200, firstPage?.Items.Count);
        Assert.Single(Assert.IsType<Page<BoundaryVersionView>>(nextPage).Items);
    }
    static async Task<DomainEvent> FreezeProjectionAsync(BoundaryProjectionScenario native, BoundaryRules rules,
        IBoundaryMutationContent change, long sequence)
    {
        var data = Metadata(change);
        var owner = await native.Reader.HydrateAsync(new SystemBoundary(data.Tenant, data.Boundary));
        var previousRevision = rules.Revision;
        rules.Apply(change);
        var command = change switch
        {
            BoundaryDraftCreatedContent => data.Boundary,
            BoundaryReviewedContent reviewed => reviewed.DecisionId,
            BoundaryApprovedContent approved => approved.ApprovalDecisionId,
            BoundarySuccessorProposedContent proposed => native.SuccessorCommand(proposed.DraftVersionId),
            _ => Uuid.CreateVersion4(),
        };
        ulong? gate = change switch
        {
            BoundaryDraftRevisedContent revised => (ulong)(revised.Revision - 1),
            BoundaryReviewedContent reviewed => (ulong)reviewed.Revision,
            BoundaryApprovedContent approved => (ulong)approved.Revision,
            BoundaryDraftDiscardedContent discarded => (ulong)discarded.Revision,
            BoundarySuccessorProposedContent => (ulong)previousRevision,
            _ => null,
        };
        var operation = OwnerProvenance.For(owner, command, data.Kind,
            ActorReference.ForMember(data.Member, data.Display), owner.Id.ToString(), gate, AuthoredIntentDigest.Compute(change), data.At);
        return await BoundarySeeds.FreezeAsync(native.Content, rules, [change], operation, sequence);
    }

    static async Task PublishAndProjectAsync(BoundaryProjectionScenario native, FitzBoundaryDirectory directory,
        IBoundaryMutationContent change)
    {
        var data = Metadata(change);
        if (change is BoundaryDraftCreatedContent && (await native.ReadAsync(data.Tenant, data.Boundary)).Count > 0)
        {
            await directory.ApplyAsync((await native.ReadAsync(data.Tenant, data.Boundary))[0]);
            return;
        }
        var rules = await native.Boundaries.RulesAsync(data.Tenant, data.Boundary);
        rules.Apply(change);
        var command = change switch
        {
            BoundaryDraftCreatedContent => data.Boundary,
            BoundaryReviewedContent reviewed => reviewed.DecisionId,
            BoundaryApprovedContent approved => approved.ApprovalDecisionId,
            BoundarySuccessorProposedContent proposed => native.SuccessorCommand(proposed.DraftVersionId),
            _ => Uuid.CreateVersion4(),
        };
        var fact = await native.PublishExplicitAsync(rules, change, data.Kind, command, data.Member, data.Display, data.At);
        await directory.ApplyAsync(fact);
    }

    static (Uuid Tenant, Uuid Boundary, Uuid Member, string Display, DateTimeOffset At, string Kind) Metadata(
        IBoundaryMutationContent change) => change switch
        {
            BoundaryDraftCreatedContent item => (item.TenantId, item.BoundaryId, item.AuthorMemberId, item.AuthorDisplay, item.ChangedAt, nameof(CreateBoundary)),
            BoundaryDraftRevisedContent item => (item.TenantId, item.BoundaryId, item.AuthorMemberId, item.AuthorDisplay, item.ChangedAt, nameof(ReviseBoundaryDraft)),
            BoundaryReviewedContent item => (item.TenantId, item.BoundaryId, item.ActorMemberId, item.ActorDisplay, item.DecidedAt, nameof(ReviewBoundary)),
            BoundaryApprovedContent item => (item.TenantId, item.BoundaryId, item.ActorMemberId, item.ActorDisplay, item.DecidedAt, nameof(ApproveBoundary)),
            BoundarySuccessorProposedContent item => (item.TenantId, item.BoundaryId, item.AuthorMemberId, item.AuthorDisplay, item.ChangedAt, nameof(ProposeBoundarySuccessor)),
            BoundaryDraftDiscardedContent item => (item.TenantId, item.BoundaryId, item.ActorMemberId, item.ActorDisplay, item.DiscardedAt, nameof(DiscardBoundaryDraft)),
            _ => throw new InvalidOperationException("The directory fixture has another business change."),
        };
}
