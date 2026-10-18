using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using ProgramRolloverAggregate = Bdgrz.Compliance.Features.Programs.ProgramRollover;

namespace Bdgrz.Compliance.Tests.Features.Programs;

public sealed class ProgramRolloverTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid RolloverId = Uuid.CreateVersion4();
    static readonly Uuid SourcePeriodId = Uuid.CreateVersion4();
    static readonly Uuid ExaminationId = Uuid.CreateVersion4();
    static readonly Uuid CEOId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    static readonly Uuid SourceEngagementId = Uuid.CreateVersion4();
    static readonly Uuid OutcomeId = Uuid.CreateVersion4();
    static readonly string OutcomeHash = Hash('e');
    static readonly TypeIIPeriodCloseSnapshotView CloseSnapshot = new(
        Uuid.CreateVersion4(), 1, [], [], [], Hash('a'), null, null, [], [], CEOId, "ceo", "CEO", Now);
    static readonly ProgramRolloverContent RolloverContent = BuildContent();

    [Fact]
    public void ShouldRequireBoundedProposalRationaleGivenExactCompletedSources()
    {
        // Arrange
        var rollover = NewRollover();
        var content = Content() with { ProposalRationale = "  " };

        // Act
        var failure = rollover.Propose(content, SourcePeriod(), Outcome(), CEOId,
            "Compliance lead", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(0, rollover.Revision);
    }

    [Fact]
    public void ShouldRetrySameProposalWithoutChangingFirstTimestampGivenLaterCommandTime()
    {
        // Arrange
        var rollover = NewRollover();
        var content = Content();
        Assert.Null(rollover.Propose(content, SourcePeriod(), Outcome(), CEOId,
            "Compliance lead", Now));

        // Act
        var retry = rollover.Propose(content, SourcePeriod(), Outcome(), CEOId,
            "Compliance lead", Now.AddHours(1));

        // Assert
        Assert.Null(retry);
        Assert.Equal(1, rollover.Revision);
        Assert.Equal(Now, rollover.Current!.CurrentVersion.ProposedAt);
    }

    [Fact]
    public void ShouldRequireExactCompletedTypeIISourceAndPreserveCarryForwardAndCommitmentProvenanceGivenProposal()
    {
        // Arrange
        var rollover = NewRollover();
        var content = Content();

        // Act
        var proposed = rollover.Propose(content, SourcePeriod(), Outcome(), CEOId,
            "Compliance lead", Now);

        // Assert
        Assert.Null(proposed);
        Assert.Equal("proposed", rollover.Current!.Status);
        Assert.Equal(SourceCloseSnapshot().CloseSnapshotId, rollover.Current.Content.SourceCloseSnapshotId);
        Assert.Equal("carried", rollover.Current.Content.Records.Single().Disposition);
        Assert.Equal(CEOId, rollover.Current.Content.Commitments.Single().OwnerMemberId);
        Assert.Equal(SourceEngagementId, rollover.Current.Content.Commitments.Single().SourceEngagementId);
        Assert.Contains("readiness", rollover.Current.Content.ComparisonMeasures);
    }

    [Fact]
    public void ShouldRejectHistoricalOccurrencesAndDuplicateRecordSelectionsGivenProposal()
    {
        // Arrange
        var rollover = NewRollover();
        var content = Content();
        var historicalOccurrence = content with
        {
            Records = [Record("control_occurrence", "carried")],
        };
        var duplicatedSource = content with
        {
            Records = [content.Records[0], content.Records[0] with { Disposition = "changed" }],
        };

        // Act
        var occurrenceFailure = rollover.Propose(historicalOccurrence, SourcePeriod(), Outcome(),
            CEOId, "Compliance lead", Now);
        var duplicateFailure = rollover.Propose(duplicatedSource, SourcePeriod(), Outcome(),
            CEOId, "Compliance lead", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(occurrenceFailure).Code);
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(duplicateFailure).Code);
        Assert.Equal(0, rollover.Revision);
    }

    [Fact]
    public void ShouldKeepCarriedChangedRetiredAndUnresolvedRecordsDistinctGivenApprovedScope()
    {
        // Arrange
        var rollover = NewRollover();
        var content = Content() with
        {
            Records = [
                Record("control", "carried"),
                Record("policy", "changed"),
                Record("risk", "retired"),
                Record("vendor", "unresolved"),
            ],
        };

        // Act
        var proposed = rollover.Propose(content, SourcePeriod(), Outcome(), CEOId,
            "Compliance lead", Now);

        // Assert
        Assert.Null(proposed);
        Assert.Collection(rollover.Current!.Content.Records,
            item => Assert.Equal("carried", item.Disposition),
            item => Assert.Equal("changed", item.Disposition),
            item => Assert.Equal("retired", item.Disposition),
            item => Assert.Equal("unresolved", item.Disposition));
    }

    [Fact]
    public void ShouldCreateOneSuccessorPeriodOnlyAfterIndependentApprovalGivenRollover()
    {
        // Arrange
        var rollover = NewRollover();
        Assert.Null(rollover.Propose(Content(), SourcePeriod(), Outcome(), CEOId,
            "Compliance lead", Now));
        var selfApproval = rollover.Approve(rollover.Revision, "approved",
            "Reviewed carry-forward proposal.", CEOId, "compliance_lead", "Compliance lead",
            Now.AddHours(1));
        var approved = rollover.Approve(rollover.Revision, "approved",
            "Reviewed carry-forward proposal.", ReviewerId, "compliance_lead", "Reviewer",
            Now.AddHours(1));

        // Act
        var created = rollover.RecordNextPeriodCreated(rollover.Revision,
            Content().NextPeriodId, Content().NextTypeIIPlanId, Now.AddHours(2));
        var retry = rollover.RecordNextPeriodCreated(rollover.Revision,
            Content().NextPeriodId, Content().NextTypeIIPlanId, Now.AddHours(2));

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited,
            Assert.IsType<CommandFailure>(selfApproval).Code);
        Assert.Null(approved);
        Assert.Null(created);
        Assert.Null(retry);
        Assert.Equal(Content().NextPeriodId, rollover.Current!.CreatedPeriodId);
        Assert.Equal("period_created", rollover.Current.Status);
        Assert.Equal(3, rollover.Revision);
    }

    [Fact]
    public void ShouldReplaySameApprovalIntentWithoutChangingFirstTimestampGivenApprovedRollover()
    {
        // Arrange
        var rollover = NewRollover();
        Assert.Null(rollover.Propose(Content(), SourcePeriod(), Outcome(), CEOId,
            "Compliance lead", Now));
        Assert.Null(rollover.Approve(rollover.Revision, "approved", "Reviewed the exact plan.",
            ReviewerId, "compliance_lead", "Reviewer", Now.AddHours(1)));

        // Act
        var retry = rollover.Approve(rollover.Revision, "approved", "Reviewed the exact plan.",
            ReviewerId, "compliance_lead", "Reviewer", Now.AddHours(4));
        var conflictingRetry = rollover.Approve(rollover.Revision, "approved",
            "Changed the rationale.", ReviewerId, "compliance_lead", "Reviewer", Now.AddHours(5));

        // Assert
        Assert.Null(retry);
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(conflictingRetry).Code);
        Assert.Equal(2, rollover.Revision);
        var approval = Assert.Single(rollover.Current!.Approvals);
        Assert.Equal(Now.AddHours(1), approval.ApprovedAt);
        Assert.Equal("Reviewed the exact plan.", approval.Rationale);
    }

    [Fact]
    public void ShouldReplayApprovedRolloverWithoutChangingCompletedSourceGivenSuccessorCreation()
    {
        // Arrange
        var rollover = NewRollover();
        Assert.Null(rollover.Propose(Content(), SourcePeriod(), Outcome(), CEOId,
            "Compliance lead", Now));
        Assert.Null(rollover.Approve(rollover.Revision, "approved", "Ready for next period.",
            ReviewerId, "compliance_lead", "Reviewer", Now.AddHours(1)));
        Assert.Null(rollover.RecordNextPeriodCreated(rollover.Revision,
            Content().NextPeriodId, Content().NextTypeIIPlanId, Now.AddHours(2)));
        var events = new AggregateScenario<ProgramRolloverAggregate>(rollover).PendingEvents.ToArray();

        // Act
        var replay = new AggregateScenario<ProgramRolloverAggregate>(NewRollover()).Given(events).Aggregate;

        // Assert
        Assert.Equal("period_created", replay.Current!.Status);
        Assert.Equal(Content().NextPeriodId, replay.Current.CreatedPeriodId);
        Assert.Equal(Content().SourceCloseSnapshotSha256, replay.Current.Content.SourceCloseSnapshotSha256);
        Assert.Equal(Content().Records, replay.Current.Content.Records);
        Assert.Equal(SourcePeriod().CloseSnapshots.Single().ContentSha256,
            replay.Current.Content.SourceCloseSnapshotSha256);
    }

    static ProgramRolloverAggregate NewRollover() => new(TenantId, ProgramId, RolloverId);
    static ProgramRolloverContent Content() => RolloverContent;

    static ProgramRolloverContent BuildContent() => new(SourcePeriodId, ExaminationId,
        CloseSnapshot.CloseSnapshotId, CloseSnapshot.ContentSha256,
        OutcomeId, OutcomeHash, new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 31),
        Uuid.CreateVersion5(RolloverId, "next-period"), Uuid.CreateVersion5(RolloverId, "next-plan"),
        [Record("control", "carried")],
        [new ProgramRolloverCommitmentContent(Uuid.CreateVersion4(),
            "Finish access recertification.", CEOId, new DateOnly(2027, 2, 15),
            SourceEngagementId, Uuid.CreateVersion4(), 2, Hash('d'))],
        ["readiness", "open_findings", "overdue_work"])
    {
        ProposalRationale = "Carry forward reviewed open obligations into the next Type II period.",
    };

    static ProgramRolloverRecordContent Record(string type, string disposition) => new(type,
        Uuid.CreateVersion4(), 2, Hash('c'), disposition,
        disposition is "carried" or "changed" ? Uuid.CreateVersion4() : null,
        "Reviewed against current approved record.");

    static TypeIIPeriodView SourcePeriod() => new(TenantId, ProgramId, SourcePeriodId,
        "closed", 4, ExaminationId, Uuid.CreateVersion4(), 2, Hash('b'),
        new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Uuid.CreateVersion4(), Hash('a'),
        [], [], [], [], null, [], [SourceCloseSnapshot()], CEOId, "CEO", Now);

    static TypeIIPeriodCloseSnapshotView SourceCloseSnapshot() => CloseSnapshot;

    static AuditOutcomeView Outcome() => new(TenantId, ProgramId, OutcomeId,
        new AuditOutcomeContent(ExaminationId, "type_ii", CloseSnapshot.CloseSnapshotId,
            CloseSnapshot.ContentSha256, Uuid.CreateVersion4(), 1, Uuid.CreateVersion4(),
            Hash('f'), "auditor", "unmodified", "No material modifications were reported.",
            "external-report-42", null, null, new DateOnly(2026, 10, 1)), "recorded", 1,
        CEOId, "Compliance lead", Now, Now, OutcomeHash);

    static string Hash(char value) => new(value, 64);
}
