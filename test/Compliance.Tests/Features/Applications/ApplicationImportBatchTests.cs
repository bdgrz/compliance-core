using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportBatchTests
{
    [Fact]
    public async Task ShouldReplayOriginalRegistrationGivenIdenticalSubmissionAfterLaterChange()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var submission = Uuid.CreateVersion4();
        var first = await host.StageAsync(submission);
        var batch = await host.BatchAsync(first.Value!.BatchId);
        var rowId = (await host.RowsAsync(batch)).Single().RowId;
        Assert.True((await host.CorrelateAsync(batch.Id, rowId, 1, "create_new")).IsSuccess);

        // Act
        var replay = await host.StageAsync(submission);
        var after = await host.BatchAsync(batch.Id);

        // Assert: the original registration returns, never the current revision, and nothing was appended.
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(1, replay.Value!.Revision);
        Assert.Equal(2, after.Revision);
        Assert.Single(await host.EventsAsync<ApplicationImportStaged>(batch.Stream));
    }

    [Fact]
    public async Task ShouldConflictGivenSameSubmissionWithChangedContent()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var submission = Uuid.CreateVersion4();
        Assert.True((await host.StageAsync(submission)).IsSuccess);

        // Act
        var changed = await host.StageAsync(submission, rows: new ApplicationImportInputRow("record-1", "Changed",
            "Pay staff", null));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
    }

    [Fact]
    public async Task ShouldReportInvalidAndDuplicateRowsGivenStagedObservation()
    {
        // Arrange
        await using var host = new ApplicationImportHost();

        // Act
        var batch = await host.StageBatchAsync("partial", "manual", "applications",
            new ApplicationImportInputRow("dup", "One", "Purpose", null),
            new ApplicationImportInputRow("dup", "Two", "Purpose", null),
            new ApplicationImportInputRow(" ", "", "", null),
            new ApplicationImportInputRow("fine", "Fine", "Purpose", null));
        var rows = await host.RowsAsync(batch);

        // Assert
        Assert.Equal(4, batch.Staged!.RowCount);
        Assert.Equal(3, batch.Staged.InvalidCount);
        Assert.Equal(["duplicate_source_record_id"], rows[0].ValidationFindings);
        Assert.Equal(["duplicate_source_record_id"], rows[1].ValidationFindings);
        Assert.Equal(["source_record_id_required", "name_required", "purpose_required"],
            rows[2].ValidationFindings);
        Assert.Empty(rows[3].ValidationFindings);
        Assert.Equal(Enumerable.Range(1, 4), rows.Select(row => row.RowNumber));
    }

    [Theory]
    [InlineData("bad", "applications", "partial", 1)]
    [InlineData("manual", " applications", "partial", 1)]
    [InlineData("manual", "applications", "unknown", 1)]
    [InlineData("manual", "applications", "partial", 0)]
    public async Task ShouldRejectWithoutEventGivenInvalidObservationShape(string key, string ns, string coverage,
        int rowCount)
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var rows = Enumerable.Range(0, rowCount).Select(index => new ApplicationImportInputRow($"r{index}", "Name",
            "Purpose", null)).ToArray();
        var request = new StageApplicationImport(host.TenantId, Uuid.CreateVersion4(), key == "bad" ? "" : key, ns,
            coverage, rows);

        // Act
        var result = await host.StageHandler.HandleAsync(host.Context(request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(await host.EventsAsync<ApplicationImportStaged>(
            host.NewBatch(ApplicationImportBatch.BatchIdFor(request)).Stream));
    }

    [Fact]
    public async Task ShouldStageEveryRowGivenObservationLargerThanTwoHundredRowsAndOneFrame()
    {
        // Arrange: 1,000 rows serialize far beyond one 64 KiB frame and the former 200-row cap.
        await using var host = new ApplicationImportHost();
        var rows = Enumerable.Range(1, 1000).Select(index => new ApplicationImportInputRow($"record-{index}",
            $"Application {index}", new string('p', 200), null)).ToArray();

        // Act
        var batch = await host.StageBatchAsync("partial", "manual", "applications", rows);
        var staged = await host.RowsAsync(batch);

        // Assert
        Assert.True(batch.Staged!.RowsManifest.Bytes > 64 * 1024);
        Assert.Equal(1000, batch.Staged.RowCount);
        Assert.Equal(1000, staged.Count);
        Assert.Equal(Enumerable.Range(1, 1000), staged.Select(row => row.RowNumber));
        Assert.All(staged, row => Assert.Empty(row.ValidationFindings));
    }

    [Fact]
    public async Task ShouldKeepBatchIdentityAndDigestGivenCultureChange()
    {
        // Arrange
        var previous = CultureInfo.CurrentCulture;
        var request = new StageApplicationImport(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "manual", "applications",
            "partial", [new("record-1", "Payroll", "Pay staff", null)]);
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            var invariantId = ApplicationImportBatch.BatchIdFor(request);
            var invariantDigest = ApplicationImportBatch.ObservationSha256(request);

            // Act
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            var turkishId = ApplicationImportBatch.BatchIdFor(request);
            var turkishDigest = ApplicationImportBatch.ObservationSha256(request);

            // Assert
            Assert.Equal(invariantId, turkishId);
            Assert.Equal(invariantDigest, turkishDigest);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public async Task ShouldRecordAttributedChoiceGivenValidStagedRow()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        var rowId = (await host.RowsAsync(batch)).Single().RowId;

        // Act
        var result = await host.CorrelateAsync(batch.Id, rowId, 1, "create_new");
        var after = await host.BatchAsync(batch.Id);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var choice = (await host.BatchHistoryAsync(after)).GetCorrelation(rowId)!;
        Assert.Equal("create_new", choice.Decision);
        Assert.Equal(host.ActorMemberId, choice.ActorMemberId);
        Assert.Equal("Reviewed identity", choice.Reason);
        Assert.Equal(2, choice.Revision);
        Assert.Equal("record-1", choice.SourceRecordId);
        Assert.Equal(ApplicationImportEffects.CreatedApplicationId(batch.Id, rowId), choice.ApplicationId);
        Assert.Equal(2, after.Revision);
    }

    [Fact]
    public async Task ShouldReturnOriginalResultGivenExactCorrelationRetryAfterLaterChange()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        var rowId = (await host.RowsAsync(batch)).Single().RowId;
        var command = Uuid.CreateVersion4();
        Assert.True((await host.CorrelateAsync(batch.Id, rowId, 1, "create_new", commandId: command)).IsSuccess);
        var target = await host.SeedApplicationAsync();
        Assert.True((await host.CorrelateAsync(batch.Id, rowId, 2, "link_existing", target, 1)).IsSuccess);

        // Act: the exact retry of the first command and a retry that changed its decision.
        var retry = await host.CorrelateAsync(batch.Id, rowId, 1, "create_new", commandId: command);
        var changed = await host.CorrelateAsync(batch.Id, rowId, 1, "link_existing", target, 1, command);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(3, (await host.BatchAsync(batch.Id)).Revision);
    }

    [Fact]
    public async Task ShouldRejectChoiceGivenInvalidBatchRowOrDecision()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync("partial", "manual", "applications",
            new ApplicationImportInputRow("good", "Good", "Purpose", null),
            new ApplicationImportInputRow("", "Bad", "Purpose", null));
        var rows = await host.RowsAsync(batch);
        var target = await host.SeedApplicationAsync();

        // Act
        var missingBatch = await host.CorrelateAsync(Uuid.CreateVersion4(), rows[0].RowId, 1, "create_new");
        var missingRow = await host.CorrelateAsync(batch.Id, Uuid.CreateVersion4(), 1, "create_new");
        var invalidRow = await host.CorrelateAsync(batch.Id, rows[1].RowId, 1, "create_new");
        var badDecision = await host.CorrelateAsync(batch.Id, rows[0].RowId, 1, "accept");
        var linkWithoutTarget = await host.CorrelateAsync(batch.Id, rows[0].RowId, 1, "link_existing");
        var createWithTarget = await host.CorrelateAsync(batch.Id, rows[0].RowId, 1, "create_new", target, 1);
        var stale = await host.CorrelateAsync(batch.Id, rows[0].RowId, 7, "create_new");

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, missingBatch.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, missingRow.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, invalidRow.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, badDecision.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, linkWithoutTarget.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, createWithTarget.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(1, (await host.BatchAsync(batch.Id)).Revision);
    }

    [Fact]
    public async Task ShouldRejectLinkGivenUnavailableRetiredOrChangedTarget()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        var rowId = (await host.RowsAsync(batch)).Single().RowId;
        var current = await host.SeedApplicationAsync(2);

        // Act
        var unknown = await host.CorrelateAsync(batch.Id, rowId, 1, "link_existing", Uuid.CreateVersion4(), 1);
        var stale = await host.CorrelateAsync(batch.Id, rowId, 1, "link_existing", current, 1);
        var accepted = await host.CorrelateAsync(batch.Id, rowId, 1, "link_existing", current, 2);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, unknown.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
    }

    [Fact]
    public async Task ShouldRejectCorrelationGivenFrozenPlanOrCancellation()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var sealedBatch = await host.StageBatchAsync();
        var canceled = await host.StageBatchAsync(rows: new ApplicationImportInputRow("other", "Other", "Purpose", null));
        var sealedRow = (await host.RowsAsync(sealedBatch)).Single().RowId;
        var canceledRow = (await host.RowsAsync(canceled)).Single().RowId;
        Assert.True((await host.CorrelateAsync(sealedBatch.Id, sealedRow, 1, "create_new")).IsSuccess);
        Assert.True((await host.AcceptAsync(sealedBatch.Id, 2)).IsSuccess);
        Assert.True((await host.CancelAsync(canceled.Id, 1)).IsSuccess);

        // Act
        var afterSeal = await host.CorrelateAsync(sealedBatch.Id, sealedRow, 3, "create_new");
        var afterCancel = await host.CorrelateAsync(canceled.Id, canceledRow, 2, "create_new");

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, afterSeal.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, afterCancel.Error?.Kind);
    }

    [Fact]
    public async Task ShouldFreezeEveryRowGivenExplicitNewTargetChoices()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync("partial", "manual", "applications",
            new ApplicationImportInputRow("a", "Alpha", "Purpose a", "Owner a"),
            new ApplicationImportInputRow("b", "Beta", "Purpose b", null),
            new ApplicationImportInputRow("c", "Gamma", "Purpose c", null));
        var rows = await host.RowsAsync(batch);
        var revision = 1L;
        foreach (var row in rows)
            Assert.True((await host.CorrelateAsync(batch.Id, row.RowId, revision++, "create_new")).IsSuccess);

        // Act
        var accepted = await host.AcceptAsync(batch.Id, revision);
        var sealedBatch = await host.BatchAsync(batch.Id);
        var plan = await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(host.Content, host.TenantId,
            sealedBatch.Plan!.FrozenPlan);
        var planned = await ApplicationImportContent.ReadSelectionAsync<ApplicationImportPlannedRow>(host.Content,
            host.TenantId, plan.Rows);
        var groups = await ApplicationImportContent.ReadSelectionAsync<ImportTargetGroupIntent>(host.Content,
            host.TenantId, plan.TargetGroups);

        // Assert: row order, explicit decisions, every initial baseline and the frozen actors.
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Equal(rows.Select(row => row.RowId), planned.Select(row => row.RowId));
        Assert.All(planned, row => Assert.Equal("create_new", row.Decision));
        Assert.Equal(["Alpha", "Beta", "Gamma"], planned.Select(row => row.Name));
        Assert.Equal("Owner a", planned[0].OwnerReference);
        Assert.Equal(3, groups.Count);
        Assert.All(groups, group => Assert.Equal(0UL, group.InitialApplicationRevision));
        Assert.All(groups, group => Assert.Equal(1UL, group.CandidateContentRevision));
        Assert.Equal(host.ActorMemberId, plan.SubmitterMemberId);
        Assert.Equal(host.ActorMemberId, plan.ApproverMemberId);
        Assert.Equal(accepted.Value!.OperationId, plan.Header.RootOperationId);
        Assert.Equal("accepting", sealedBatch.State);
    }

    [Fact]
    public async Task ShouldRejectWholePlanGivenUnreadyOrStaleBatch()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync("partial", "manual", "applications",
            new ApplicationImportInputRow("a", "Alpha", "Purpose", null),
            new ApplicationImportInputRow("b", "", "Purpose", null));
        var rows = await host.RowsAsync(batch);

        // Act
        var invalid = await host.AcceptAsync(batch.Id, 1);
        var stale = await host.AcceptAsync(batch.Id, 9);
        var missingBatch = await host.AcceptAsync(Uuid.CreateVersion4(), 1);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, invalid.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, missingBatch.Error?.Kind);
        Assert.Null((await host.BatchAsync(batch.Id)).Plan);
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task ShouldRecheckLinkedTargetGivenAcceptanceAfterCorrelation()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        var rowId = (await host.RowsAsync(batch)).Single().RowId;
        var target = await host.SeedApplicationAsync();
        Assert.True((await host.CorrelateAsync(batch.Id, rowId, 1, "link_existing", target, 1)).IsSuccess);
        var revised = await host.ApplicationAsync(target);
        Assert.Null(revised.Revise(1, "Seeded", "Edited elsewhere", null, host.ActorMemberId, "Import manager",
            host.Clock.GetUtcNow()));
        await host.Writer.SaveAsync(revised, new RequestDispatchContext(RequestActor.System));

        // Act
        var accepted = await host.AcceptAsync(batch.Id, 2);

        // Assert: the plan never freezes a baseline the target no longer has.
        Assert.Equal(RequestErrorKind.Conflict, accepted.Error?.Kind);
        Assert.Null((await host.BatchAsync(batch.Id)).Plan);
    }

    [Fact]
    public async Task ShouldRefuseAcceptanceGivenAnotherBatchHoldsTheSourceAndFreezeNothing()
    {
        // Arrange: the first batch is sealed and its source claim granted.
        await using var host = new ApplicationImportHost();
        var first = await host.StageBatchAsync();
        var firstRow = (await host.RowsAsync(first)).Single().RowId;
        Assert.True((await host.CorrelateAsync(first.Id, firstRow, 1, "create_new")).IsSuccess);
        Assert.True((await host.AcceptAsync(first.Id, 2)).IsSuccess);
        var record = await host.StartProcessAsync(first.Id);
        Assert.True((await host.SendAsync((await host.StepAsync(record, "claim", nameof(ClaimApplicationImportSource),
            record.Targets[0]))!)).IsSuccess);
        var second = await host.StageBatchAsync(rows: new ApplicationImportInputRow("record-2", "Other", "Purpose", null));
        var secondRow = (await host.RowsAsync(second)).Single().RowId;
        Assert.True((await host.CorrelateAsync(second.Id, secondRow, 1, "create_new")).IsSuccess);

        // Act
        var refused = await host.AcceptAsync(second.Id, 2);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.True(refused.Error!.IsTransient);
        Assert.Null((await host.BatchAsync(second.Id)).Plan);
    }

    [Fact]
    public async Task ShouldRejectRebindingGivenCommittedSourceClaim()
    {
        // Arrange: a published batch claims record-1 for a created application.
        await using var host = new ApplicationImportHost();
        var first = await host.StageBatchAsync();
        var firstRow = (await host.RowsAsync(first)).Single().RowId;
        Assert.True((await host.CorrelateAsync(first.Id, firstRow, 1, "create_new")).IsSuccess);
        Assert.True((await host.AcceptAsync(first.Id, 2)).IsSuccess);
        Assert.Equal(Bdgrz.Compliance.Features.Processes.ProcessState.Completed, (await host.RunProcessAsync(first.Id)).State);
        var second = await host.StageBatchAsync();
        var secondRow = (await host.RowsAsync(second)).Single().RowId;
        var other = await host.SeedApplicationAsync();

        // Act
        var createAgain = await host.CorrelateAsync(second.Id, secondRow, 1, "create_new");
        var linkOther = await host.CorrelateAsync(second.Id, secondRow, 1, "link_existing", other, 1);
        var claimed = ApplicationImportEffects.CreatedApplicationId(first.Id, firstRow);
        var linkClaimed = await host.CorrelateAsync(second.Id, secondRow, 1, "link_existing", claimed, 1);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, createAgain.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, linkOther.Error?.Kind);
        Assert.True(linkClaimed.IsSuccess, linkClaimed.Error?.Message);
    }

    [Fact]
    public async Task ShouldFreezeProposalGivenCompleteSourceOmissionAndInvalidateItOnLaterChange()
    {
        // Arrange: record-omitted was published by an earlier batch; the complete source no longer observes it.
        await using var host = new ApplicationImportHost();
        var original = await host.StageBatchAsync("partial", "manual", "applications",
            new ApplicationImportInputRow("record-omitted", "Omitted", "Purpose", null));
        var originalRow = (await host.RowsAsync(original)).Single().RowId;
        Assert.True((await host.CorrelateAsync(original.Id, originalRow, 1, "create_new")).IsSuccess);
        Assert.True((await host.AcceptAsync(original.Id, 2)).IsSuccess);
        Assert.Equal(Bdgrz.Compliance.Features.Processes.ProcessState.Completed, (await host.RunProcessAsync(original.Id)).State);
        var batch = await host.StageBatchAsync("declared_complete", "manual", "applications",
            new ApplicationImportInputRow("record-present", "Present", "Purpose", null));
        var source = await host.SourceAsync();

        // Act
        var frozen = await host.FreezeHandler.HandleAsync(host.Context(new FreezeApplicationImportOmissionProposal(
            Uuid.CreateVersion4(), host.TenantId, batch.Id, 1, source.CommittedStreamPosition, batch.ContentDigest!,
            "Reviewed omission")), CancellationToken.None);
        var sealedProposal = await host.BatchAsync(batch.Id);
        var proposal = await ApplicationImportContent.ReadAsync<ApplicationImportOmissionProposalContent>(host.Content,
            host.TenantId, sealedProposal.Proposal!.Proposal);
        var row = (await host.RowsAsync(batch)).Single().RowId;
        Assert.True((await host.CorrelateAsync(batch.Id, row, 2, "create_new")).IsSuccess);
        var afterChange = await host.BatchAsync(batch.Id);

        // Assert
        Assert.True(frozen.IsSuccess, frozen.Error?.Message);
        Assert.Equal(2, sealedProposal.Revision);
        Assert.Equal(sealedProposal.Proposal.Proposal.Sha256, sealedProposal.Proposal.ProposalSha256);
        var omitted = Assert.Single(proposal.Rows);
        Assert.Equal("record-omitted", omitted.SourceRecordId);
        Assert.Equal(original.Id, omitted.LastObservedBatchId);
        Assert.Equal(source.CommittedStreamPosition, proposal.SourcePosition);
        Assert.Equal(["record-present"], proposal.PresentSourceRecordIds);
        Assert.Equal("Reviewed omission", proposal.Reason);
        Assert.False(afterChange.HasCurrentProposal);
    }

    [Fact]
    public async Task ShouldRejectProposalGivenChangedDigestPartialCoverageOrSourcePosition()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var partial = await host.StageBatchAsync();
        var complete = await host.StageBatchAsync("declared_complete", rows: new ApplicationImportInputRow("x", "X",
            "Purpose", null));
        var source = await host.SourceAsync();
        Task<Result> Freeze(Uuid batch, string digest, ulong position) => host.FreezeHandler.HandleAsync(
            host.Context(new FreezeApplicationImportOmissionProposal(Uuid.CreateVersion4(), host.TenantId, batch, 1,
                position, digest, "Reason")), CancellationToken.None).AsTask();

        // Act
        var digest = await Freeze(complete.Id, "0000", source.CommittedStreamPosition);
        var coverage = await Freeze(partial.Id, partial.ContentDigest!, source.CommittedStreamPosition);
        var position = await Freeze(complete.Id, complete.ContentDigest!, source.CommittedStreamPosition + 5);
        var noOmission = await Freeze(complete.Id, complete.ContentDigest!, source.CommittedStreamPosition);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, digest.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, coverage.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, position.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, noOmission.Error?.Kind);
    }
}
