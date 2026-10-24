using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using TypeIIPlanAggregate = Bdgrz.Compliance.Features.Examinations.TypeIIPlan;
using TypeIIPeriodAggregate = Bdgrz.Compliance.Features.Operations.TypeIIPeriod;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.Operations;

public sealed class TypeIIPeriodTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ExaminationId = Uuid.CreateVersion4();
    static readonly Uuid PlanId = Uuid.CreateVersion4();
    static readonly Uuid PeriodId = Uuid.CreateVersion4();
    static readonly Uuid ProposerId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly Uuid ControlOwnerId = Uuid.CreateVersion4();
    static readonly Uuid GapId = Uuid.CreateVersion4();
    static readonly Uuid ValidatorId = Uuid.CreateVersion4();
    static readonly Uuid CEOId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly PeriodStart = new(2026, 10, 1);
    static readonly DateOnly PeriodEnd = new(2027, 9, 30);

    [Fact]
    public void ShouldBindOpeningSnapshotToApprovedPlanAndExpectedPopulationGivenPeriodStart()
    {
        // Arrange
        var period = NewPeriod();
        var plan = ApprovedPlan();

        // Act
        var started = period.Start(plan.Current!, Opening(), ProposerId, "Compliance lead", Now);

        // Assert
        Assert.Null(started);
        Assert.Equal("active", period.Current!.Status);
        Assert.Equal(PlanId, period.Current.TypeIIPlanId);
        Assert.Equal(1, period.Current.TypeIIPlanVersion);
        Assert.Equal(plan.Current!.Current!.ContentSha256, period.Current.TypeIIPlanContentSha256);
        Assert.Equal(Opening().OpeningSnapshotSha256, period.Current.OpeningSnapshotSha256);
        Assert.Equal(4, period.Current.ExpectedOccurrences.Count);
        Assert.Equal(GapId, Assert.Single(period.Current.AcknowledgedOpeningGapIds));
    }

    [Fact]
    public void ShouldRejectPeriodStartWithoutApprovedPlanGivenProposedPlan()
    {
        // Arrange
        var period = NewPeriod();
        var plan = ProposedPlan();

        // Act
        var failure = period.Start(plan.Current!, Opening(), ProposerId, "Compliance lead", Now);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(0, period.Revision);
    }

    [Fact]
    public void ShouldRequireEveryOpeningGapAcknowledgementGivenApprovedPlan()
    {
        // Arrange
        var period = NewPeriod();
        var opening = Opening() with { AcknowledgedOpeningGapIds = [] };

        // Act
        var failure = period.Start(ApprovedPlan().Current!, opening, ProposerId,
            "Compliance lead", Now);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(0, period.Revision);
    }

    [Fact]
    public void ShouldRetainEffectiveDateAmendmentWithoutChangingOpeningPlanGivenActivePeriod()
    {
        // Arrange
        var period = StartedPeriod();
        var originalPlanHash = period.Current!.TypeIIPlanContentSha256;

        // Act
        var amended = period.Amend(period.Revision, PeriodStart, PeriodEnd.AddDays(7),
            "Extend observation by one week", ProposerId, "Compliance lead", Now.AddMonths(2));

        // Assert
        Assert.Null(amended);
        Assert.Equal(PeriodEnd.AddDays(7), period.Current!.PeriodEnd);
        Assert.Equal(originalPlanHash, period.Current.TypeIIPlanContentSha256);
        Assert.Equal(PeriodEnd, Assert.Single(period.Current.Amendments).PriorPeriodEnd);
        Assert.Equal("Extend observation by one week", period.Current.Amendments[0].Reason);
    }

    [Fact]
    public void ShouldRecordEachGenerationAsNativeFactAndRejectKeyReuseGivenActivePeriod()
    {
        // Arrange
        var period = StartedPeriod();
        var throughDate = new DateOnly(2027, 3, 1);
        var expectedDue = period.Current!.ExpectedOccurrences.Where(item => item.DueOn <= throughDate)
            .Select(item => item.OccurrenceId).OrderBy(static id => id.ToString()).ToArray();

        // Act
        var due = period.DueExpectedOccurrences(throughDate, new HashSet<Uuid>());
        var afterSelection = period.DueExpectedOccurrences(throughDate, expectedDue.ToHashSet());
        var first = period.Generate("period-generation-001", throughDate, ProposerId, "Compliance lead",
            Now.AddMonths(1));
        var reuse = period.PrepareGenerateDueOccurrences("period-generation-001", throughDate, ProposerId,
            "Compliance lead", Now.AddMonths(1).AddMinutes(1));
        var next = period.Generate("period-generation-002", throughDate, ProposerId, "Compliance lead",
            Now.AddMonths(1).AddMinutes(2));

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(next.IsSuccess, next.Error?.Message);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(reuse).Code);
        Assert.Equal(expectedDue, due.Select(item => item.OccurrenceId));
        Assert.Empty(afterSelection);
        Assert.Equal(3, period.Revision);
        Assert.Equal(["period-generation-001", "period-generation-002"],
            period.OccurrenceGenerations.Select(generation => generation.GenerationKey));
        Assert.All(period.OccurrenceGenerations, generation => Assert.Equal(throughDate, generation.ThroughDate));
        Assert.Equal(Now.AddMonths(1), period.OccurrenceGenerations[0].OccurredAt);

        var events = new AggregateScenario<TypeIIPeriodAggregate>(period).PendingEvents.ToArray();
        var replay = new AggregateScenario<TypeIIPeriodAggregate>(NewPeriod()).Given(events).Aggregate;
        Assert.Equal(period.OccurrenceGenerations.Select(generation => generation.GenerationKey),
            replay.OccurrenceGenerations.Select(generation => generation.GenerationKey));
        Assert.Equal(period.Revision, replay.Revision);
    }

    [Fact]
    public void ShouldRejectOccurrenceGenerationOutsideActivePeriodGivenGenerationRequest()
    {
        // Arrange
        var period = StartedPeriod();

        // Act
        var before = period.PrepareGenerateDueOccurrences("before-period", PeriodStart.AddDays(-1),
            ProposerId, "Compliance lead", Now.AddMonths(1));
        var after = period.PrepareGenerateDueOccurrences("after-period", PeriodEnd.AddDays(1),
            ProposerId, "Compliance lead", Now.AddMonths(1));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(before).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(after).Code);
        Assert.Equal(1, period.Revision);
        Assert.Empty(period.OccurrenceGenerations);
    }

    [Fact]
    public void ShouldBlockCloseApprovalGivenMissingExpectedOccurrencesWhenCloseValidationRecorded()
    {
        // Arrange
        var period = StartedPeriod();
        var expected = period.Current!.ExpectedOccurrences.Select(item => item.OccurrenceId).ToArray();
        var missing = expected[^1];
        var actual = expected[..^1];
        Assert.Null(period.RecordCloseValidation(period.Revision,
            new TypeIIPeriodCloseValidationContent(SourceReferences(), expected, actual,
                [], [missing], [GapId]), ValidatorId, "Close validator", Now.AddMonths(12)));

        // Act
        var failure = period.ApproveClose(period.Revision, [GapId], "Reviewed known exception",
            CEOId, "CEO", "Chief Executive", Now.AddMonths(12).AddHours(1));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal("blocked", period.Current!.CloseValidation!.Status);
        Assert.Empty(period.Current.CloseApprovals);
    }

    [Fact]
    public void ShouldRequireExactUnresolvedItemAcknowledgementsBeforeCloseApprovalGivenReconciledPeriod()
    {
        // Arrange
        var period = StartedPeriod();
        var occurrences = period.Current!.ExpectedOccurrences.Select(item => item.OccurrenceId).ToArray();
        Assert.Null(period.RecordCloseValidation(period.Revision,
            new TypeIIPeriodCloseValidationContent(SourceReferences(), occurrences, occurrences,
                [], [], [GapId]), ValidatorId, "Close validator", Now.AddMonths(12)));

        // Act
        var missingAcknowledgement = period.ApproveClose(period.Revision, [], "Reviewed known exception",
            CEOId, "CEO", "Chief Executive", Now.AddMonths(12).AddHours(1));
        var approved = period.ApproveClose(period.Revision, [GapId], "Reviewed known exception",
            CEOId, "CEO", "Chief Executive", Now.AddMonths(12).AddHours(1));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(missingAcknowledgement).Code);
        Assert.Null(approved);
        Assert.Equal("close_approved", period.Current!.Status);
        Assert.Equal(GapId, Assert.Single(period.Current.CloseApprovals).AcknowledgedMaterialItemIds.Single());
    }

    [Fact]
    public void ShouldReplaySameCloseApprovalIntentWithoutChangingFirstTimestampGivenApprovedClose()
    {
        // Arrange
        var period = StartedPeriod();
        var occurrences = period.Current!.ExpectedOccurrences.Select(item => item.OccurrenceId).ToArray();
        Assert.Null(period.RecordCloseValidation(period.Revision,
            new TypeIIPeriodCloseValidationContent(SourceReferences(), occurrences, occurrences,
                [], [], [GapId]), ValidatorId, "Close validator", Now.AddMonths(12)));
        Assert.Null(period.ApproveClose(period.Revision, [GapId], "Reviewed close",
            CEOId, "CEO", "Chief Executive", Now.AddMonths(12).AddHours(1)));

        // Act
        var retry = period.ApproveClose(period.Revision, [GapId], "Reviewed close",
            CEOId, "CEO", "Chief Executive", Now.AddMonths(12).AddHours(4));
        var conflictingRetry = period.ApproveClose(period.Revision, [GapId], "Changed rationale",
            CEOId, "CEO", "Chief Executive", Now.AddMonths(12).AddHours(5));

        // Assert
        Assert.Null(retry);
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(conflictingRetry).Code);
        Assert.Equal(3, period.Revision);
        var approval = Assert.Single(period.Current.CloseApprovals);
        Assert.Equal(Now.AddMonths(12).AddHours(1), approval.ApprovedAt);
        Assert.Equal("Reviewed close", approval.Rationale);
    }

    [Fact]
    public void ShouldReplayFrozenSnapshotGivenLaterServerClockAndSameOriginalRevision()
    {
        // Arrange
        var period = ReadyToClosePeriod();
        var expectedRevision = period.Revision;
        var snapshot = Snapshot(SourceReferences(),
            period.Current!.ExpectedOccurrences.Select(item => item.OccurrenceId).ToArray());
        var firstAt = Now.AddMonths(12).AddHours(2);
        Assert.Null(period.FreezeClose(expectedRevision, snapshot, CEOId, "Chief Executive", firstAt));
        var original = Assert.Single(period.Current.CloseSnapshots);
        var revision = period.Revision;

        // Act
        var replay = period.FreezeClose(expectedRevision, snapshot, CEOId, "Chief Executive", firstAt.AddDays(1));
        var changed = period.FreezeClose(expectedRevision + 1, snapshot, CEOId, "Chief Executive", firstAt.AddDays(1));

        // Assert
        Assert.Null(replay);
        Assert.Equal(CommandFailureCode.StateConflict, changed?.Code);
        Assert.Equal(revision, period.Revision);
        Assert.Same(original, Assert.Single(period.Current.CloseSnapshots));
        Assert.Equal(firstAt, original.RecordedAt);
    }

    [Fact]
    public void ShouldFreezeExactReconciledSnapshotAndPreserveAmendmentLineageGivenApprovedClose()
    {
        // Arrange
        var period = ReadyToClosePeriod();
        var originalSources = SourceReferences();
        var originalSnapshot = Snapshot(originalSources,
            period.Current!.ExpectedOccurrences.Select(item => item.OccurrenceId).ToArray());
        Assert.Null(period.FreezeClose(period.Revision, originalSnapshot, CEOId,
            "Chief Executive", Now.AddMonths(12).AddHours(2)));
        var originalSnapshotHash = period.Current!.CloseSnapshots[^1].ContentSha256;
        var successorSources = originalSources.ToArray();
        successorSources[0] = successorSources[0] with
        {
            VersionId = Uuid.CreateVersion5(successorSources[0].VersionId, "next"),
            Version = successorSources[0].Version + 1,
            ContentSha256 = new string('e', 64),
        };
        var successorSnapshot = Snapshot(successorSources,
            period.Current!.ExpectedOccurrences.Select(item => item.OccurrenceId).ToArray(), Uuid.CreateVersion4());

        // Act
        var amended = period.AmendClose(period.Revision, successorSnapshot,
            "Correct one approved source record", [originalSources[0].RecordId],
            [Uuid.CreateVersion4()], CEOId, "CEO", "Chief Executive", Now.AddMonths(12).AddDays(1));

        // Assert
        Assert.Null(amended);
        Assert.Equal("closed", period.Current!.Status);
        Assert.Equal(2, period.Current.CloseSnapshots.Count);
        Assert.Equal(originalSnapshot.CloseSnapshotId, period.Current.CloseSnapshots[0].CloseSnapshotId);
        Assert.Equal(originalSnapshotHash, period.Current.CloseSnapshots[0].ContentSha256);
        Assert.Equal(originalSnapshot.CloseSnapshotId, period.Current.CloseSnapshots[1].AmendsCloseSnapshotId);
        Assert.NotEqual(period.Current.CloseSnapshots[0].ContentSha256,
            period.Current.CloseSnapshots[1].ContentSha256);
    }

    [Fact]
    public void ShouldReplayOpeningAmendmentsCloseDecisionAndSnapshotHistoryGivenEvents()
    {
        // Arrange
        var period = StartedPeriod();
        Assert.Null(period.Amend(period.Revision, PeriodStart, PeriodEnd.AddDays(7),
            "Approved date change", ProposerId, "Compliance lead", Now.AddMonths(2)));
        var expected = period.Current!.ExpectedOccurrences.Select(item => item.OccurrenceId).ToArray();
        Assert.Null(period.RecordCloseValidation(period.Revision,
            new TypeIIPeriodCloseValidationContent(SourceReferences(), expected, expected,
                [], [], [GapId]), ValidatorId, "Close validator", Now.AddMonths(12)));
        Assert.Null(period.ApproveClose(period.Revision, [GapId], "Reviewed close",
            CEOId, "CEO", "Chief Executive", Now.AddMonths(12).AddHours(1)));
        Assert.Null(period.FreezeClose(period.Revision,
            Snapshot(SourceReferences(), expected), CEOId, "Chief Executive", Now.AddMonths(12).AddHours(2)));
        var events = new AggregateScenario<TypeIIPeriodAggregate>(period).PendingEvents.ToArray();

        // Act
        var replay = new AggregateScenario<TypeIIPeriodAggregate>(NewPeriod()).Given(events).Aggregate;

        // Assert
        Assert.Equal("closed", replay.Current!.Status);
        Assert.Equal(period.Revision, replay.Current.Revision);
        Assert.Equal(period.Current!.OpeningSnapshotSha256, replay.Current.OpeningSnapshotSha256);
        Assert.Equal(period.Current.CloseSnapshots.Select(item => item.ContentSha256).ToArray(),
            replay.Current.CloseSnapshots.Select(item => item.ContentSha256).ToArray());
        Assert.Single(replay.Current.CloseApprovals);
    }

    [Fact]
    public void ShouldRefuseSecondOpeningGivenOpeningAlreadyAcceptedOnPeriod()
    {
        // Arrange
        var period = NewPeriod();
        var plan = ApprovedPlan();
        Assert.Null(period.PrepareOpening(plan.Current!, Opening(), ProposerId, "Compliance lead", Now, out _));
        var synthetic = Synthetic();
        Assert.True(period.AcceptOpening(Provenance(period, PeriodId), synthetic, synthetic).IsSuccess);

        // Act
        var prepared = period.PrepareOpening(plan.Current!, Opening(), ProposerId, "Compliance lead", Now, out var initial);
        var second = period.AcceptOpening(Provenance(period, PeriodId), synthetic, synthetic);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(prepared).Code);
        Assert.Null(initial);
        Assert.False(second.IsSuccess);
        Assert.Equal(0, period.Revision);
        Assert.NotNull(period.Pending);
    }

    [Fact]
    public void ShouldRefuseDelayedApplyGivenAmendmentCanceledBeforeApply()
    {
        // Arrange
        var period = StartedPeriod();
        var synthetic = Synthetic();
        var amendmentId = Uuid.CreateVersion4();
        var change = new TypeIIPeriodAmendmentChange(PeriodStart, PeriodEnd.AddDays(7), "Extend the window.", ProposerId,
            "Compliance lead", Now.AddMonths(1));
        Assert.True(period.AcceptAmendment(period.Revision, Provenance(period, amendmentId), synthetic, synthetic)
            .IsSuccess);
        var canceled = period.CancelAmendment(amendmentId, Provenance(period, Uuid.CreateVersion4(), amendmentId),
            synthetic);

        // Act
        var delayed = period.ApplyAmendment(Provenance(period, Uuid.CreateVersion4(), amendmentId), change,
            new TypeIIPeriodCalendarAuthorization(amendmentId, 2), synthetic);
        var cancelAgain = period.PrepareCancel(amendmentId, period.Revision);

        // Assert
        Assert.True(canceled.IsSuccess, canceled.Error?.Message);
        Assert.False(delayed.IsSuccess);
        Assert.True(period.IsFenced(amendmentId));
        Assert.False(cancelAgain.IsSuccess);
        Assert.Equal(PeriodEnd, period.Current!.PeriodEnd);
        Assert.Equal(new TypeIIPeriodCalendarAuthorization(PeriodId, 1), period.CalendarAuthorization);
    }

    [Fact]
    public void ShouldKeepCloseBlockedGivenPendingAmendmentUntilItEnds()
    {
        // Arrange
        var period = StartedPeriod();
        var synthetic = Synthetic();
        var amendmentId = Uuid.CreateVersion4();
        Assert.True(period.AcceptAmendment(period.Revision, Provenance(period, amendmentId), synthetic, synthetic)
            .IsSuccess);
        var occurrences = period.Current!.ExpectedOccurrences.Select(item => item.OccurrenceId).ToArray();

        // Act
        var blocked = period.RecordCloseValidation(period.Revision,
            new TypeIIPeriodCloseValidationContent(SourceReferences(), occurrences, occurrences, [], [], [GapId]),
            ValidatorId, "Close validator", Now.AddMonths(12));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(blocked).Code);
        Assert.Equal("active", period.Current.Status);
    }

    static ImmutableContentRef Synthetic() => new(Uuid.CreateVersion4(), new string('a', 64), 2, "application/json",
        "synthetic-content.v1");

    static OperationProvenance Provenance(TypeIIPeriodAggregate period, Uuid root) =>
        Provenance(period, root, root);

    static OperationProvenance Provenance(TypeIIPeriodAggregate period, Uuid logicalId, Uuid root) => new(logicalId, root,
        logicalId == root ? null : root, "Synthetic", ActorReference.ForMember(ProposerId, "Compliance lead"),
        period.Stream, "initial", null, new string('e', 64), Now, period.Stream, period.Stream);

    static TypeIIPeriodAggregate NewPeriod() => new(TenantId, ProgramId, PeriodId);
    static TypeIIPeriodAggregate StartedPeriod()
    {
        var period = NewPeriod();
        Assert.Null(period.Start(ApprovedPlan().Current!, Opening(), ProposerId, "Compliance lead", Now));
        return period;
    }

    static TypeIIPeriodAggregate ReadyToClosePeriod()
    {
        var period = StartedPeriod();
        var occurrences = period.Current!.ExpectedOccurrences.Select(item => item.OccurrenceId).ToArray();
        Assert.Null(period.RecordCloseValidation(period.Revision,
            new TypeIIPeriodCloseValidationContent(SourceReferences(), occurrences, occurrences,
                [], [], [GapId]), ValidatorId, "Close validator", Now.AddMonths(12)));
        Assert.Null(period.ApproveClose(period.Revision, [GapId], "Reviewed close",
            CEOId, "CEO", "Chief Executive", Now.AddMonths(12).AddHours(1)));
        return period;
    }

    static TypeIIPlanAggregate ProposedPlan()
    {
        var plan = new TypeIIPlanAggregate(TenantId, ProgramId, PlanId);
        Assert.Null(plan.Propose(PlanContent(), ProposerId, "Compliance lead", Now.AddDays(-3)));
        return plan;
    }

    static TypeIIPlanAggregate ApprovedPlan()
    {
        var plan = ProposedPlan();
        Assert.Null(plan.Review(plan.Revision, 1, "approve", "Reviewed controls and opening gap",
            ReviewerId, "Independent reviewer", Now.AddDays(-2)));
        Assert.Null(plan.Approve(plan.Revision, 1, "Approved period boundaries and owners",
            ApproverId, "CISO", "Executive approver", Now.AddDays(-1)));
        return plan;
    }

    static TypeIIPlanContent PlanContent() => new(ExaminationId, Uuid.CreateVersion4(),
        new string('a', 64), Uuid.CreateVersion4(), PeriodStart, PeriodEnd, "Audit Firm",
        ["security", "availability"], Uuid.CreateVersion4(), Uuid.CreateVersion4(),
        [Reference("policy"), Reference("risk"), Reference("vendor"),
            Reference("finding"), Reference("change")], [ReferenceControl()],
        [new("finding", GapId, Uuid.CreateVersion4(), 1,
            new string('b', 64), "carry")],
        [new(GapId, "Open control gap", "acknowledged", "Owner and due date confirmed")], []);

    static TypeIIControlPlanEntry ReferenceControl() => new(Uuid.CreateVersion4(),
        Uuid.CreateVersion4(), "Quarterly access review", ControlOwnerId, "quarterly", 4,
        PeriodStart, null, 5, ["Dated review record"]);

    static TypeIIPeriodOpeningContent Opening() => new(ExaminationId, Uuid.CreateVersion4(),
        new string('c', 64), SourceReferences(), [GapId]);

    static TypeIIPeriodCloseValidationContent TypeIIPeriodCloseValidationContent(
        IReadOnlyList<ExaminationContentReference> sourceReferences,
        IReadOnlyList<Uuid> expectedOccurrenceIds, IReadOnlyList<Uuid> actualOccurrenceIds,
        IReadOnlyList<Uuid> duplicateOccurrenceIds, IReadOnlyList<Uuid> missingWorkIds,
        IReadOnlyList<Uuid> unresolvedMaterialItemIds) => new(sourceReferences,
        expectedOccurrenceIds, actualOccurrenceIds, duplicateOccurrenceIds, missingWorkIds,
        unresolvedMaterialItemIds);

    static TypeIIPeriodCloseSnapshotContent Snapshot(
        IReadOnlyList<ExaminationContentReference> sourceReferences,
        IReadOnlyList<Uuid> expectedOccurrenceIds, Uuid? snapshotId = null) => new(
        snapshotId ?? Uuid.CreateVersion4(), sourceReferences, expectedOccurrenceIds, [GapId]);

    static IReadOnlyList<ExaminationContentReference> SourceReferences() =>
        [Reference("boundary"), Reference("inventory"), Reference("control"), Reference("policy"),
            Reference("provider"), Reference("finding")];

    static ExaminationContentReference Reference(string sourceType) => new(sourceType,
        Uuid.CreateVersion5(Uuid.Empty, "type-ii-period-source-record:" + sourceType),
        Uuid.CreateVersion5(Uuid.Empty, "type-ii-period-source-version:" + sourceType), 1,
        new string('d', 64));
}
