using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class ExaminationEngagementTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid EngagementId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly Uuid BoundaryId = Uuid.CreateVersion4();
    static readonly Uuid BoundaryVersionId = Uuid.CreateVersion4();
    static readonly Uuid BaselineId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 17, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRejectIncompleteEngagementGivenCreate()
    {
        // Arrange
        var engagement = new ExaminationEngagementRules(TenantId, ProgramId, EngagementId);
        var content = ValidContent() with { AuditFirm = " " };

        // Act
        var failure = engagement.Create(content, MemberId, "Lead", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(failure).Code);
        Assert.Empty(engagement.PendingChanges);
        Assert.Equal(0, engagement.Revision);
    }

    [Fact]
    public void ShouldKeepEngagementIdentityAndRejectStaleRevisionGivenRevision()
    {
        // Arrange
        var engagement = new ExaminationEngagementRules(TenantId, ProgramId, EngagementId);
        Assert.Null(engagement.Create(ValidContent(), MemberId, "Lead", Now));

        // Act
        var stale = engagement.Revise(0, ValidContent() with { AuditFirm = "Other Firm" },
            MemberId, "Lead", Now.AddDays(1));
        var changed = engagement.Revise(1, ValidContent() with { AuditFirm = "  Second Firm  " },
            MemberId, "Lead", Now.AddDays(2));

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict,
            Assert.IsType<CommandFailure>(stale).Code);
        Assert.Null(changed);
        Assert.Equal("Second Firm", engagement.Current!.AuditFirm);
        Assert.Equal(2, engagement.Revision);
        Assert.Collection(engagement.PendingChanges,
            created => Assert.Equal(ValidContent().AuditFirm,
                Assert.IsType<ExaminationEngagementRuleOpened>(created).Engagement.AuditFirm),
            revised => Assert.Equal("Second Firm",
                Assert.IsType<ExaminationEngagementRuleRevised>(revised).Engagement.AuditFirm));
    }

    [Fact]
    public void ShouldBlockBaselineWithUnresolvedSourcesGivenFreeze()
    {
        // Arrange
        var engagement = NewEngagement();
        var preview = TypeIBaselinePreview.Create(TenantId, ProgramId, EngagementId, [],
            ["The boundary has an unresolved system."]);

        // Act
        var failure = engagement.FreezeBaseline(1, BaselineId, preview, "Ready to freeze",
            MemberId, "Lead", Now.AddDays(1));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(failure).Code);
        Assert.Null(engagement.Baseline);
        Assert.Single(engagement.PendingChanges);
    }

    [Fact]
    public void ShouldRequireApprovedBoundaryReferenceGivenFreeze()
    {
        // Arrange
        var engagement = NewEngagement();
        var source = new TypeIBaselineSourceReference("control", Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 1, new string('a', 64));
        var preview = TypeIBaselinePreview.Create(TenantId, ProgramId, EngagementId, [source]);

        // Act
        var failure = engagement.FreezeBaseline(1, BaselineId, preview, "Scope confirmed",
            MemberId, "Lead", Now.AddDays(1));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(failure).Code);
        Assert.Null(engagement.Baseline);
        Assert.Single(engagement.PendingChanges);
    }

    [Fact]
    public void ShouldBindApprovedBoundaryReferenceToEngagementGivenFreeze()
    {
        // Arrange
        var engagement = NewEngagement();
        var source = new TypeIBaselineSourceReference("approved_boundary", Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 1, new string('a', 64));
        var preview = TypeIBaselinePreview.Create(TenantId, ProgramId, EngagementId, [source]);

        // Act
        var failure = engagement.FreezeBaseline(1, BaselineId, preview, "Scope confirmed",
            MemberId, "Lead", Now.AddDays(1));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(failure).Code);
        Assert.Null(engagement.Baseline);
        Assert.Single(engagement.PendingChanges);
    }

    [Fact]
    public void ShouldRequireEverySourceFamilyToBeCoveredGivenFreeze()
    {
        // Arrange
        var engagement = NewEngagement();
        var completePreview = ValidPreview();
        var incompletePreview = completePreview with
        {
            SourceCoverage = completePreview.SourceCoverage
                .Where(static coverage => coverage.Family == "approved_boundary").ToArray(),
        };

        // Act
        var failure = engagement.FreezeBaseline(1, BaselineId, incompletePreview,
            "Scope confirmed", MemberId, "Lead", Now.AddDays(1));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(failure).Code);
        Assert.Null(engagement.Baseline);
        Assert.Single(engagement.PendingChanges);
    }

    [Fact]
    public void ShouldRejectFrozenEventWithoutExactBoundaryGivenReplay()
    {
        // Arrange
        var openedAggregate = NewEngagement();
        var opened = Assert.IsType<ExaminationEngagementRuleOpened>(
            Assert.Single(openedAggregate.PendingChanges));
        var preview = TypeIBaselinePreview.Create(TenantId, ProgramId, EngagementId,
            [new TypeIBaselineSourceReference("control", Uuid.CreateVersion4(),
                Uuid.CreateVersion4(), 1, new string('a', 64))]);
        var frozenAt = Now.AddDays(1);
        var frozenEngagement = openedAggregate.Current! with
        {
            Status = "baseline_frozen",
            Revision = openedAggregate.Revision + 1,
            UpdatedAt = frozenAt,
        };
        var baseline = new TypeIBaselineView(TenantId, ProgramId, EngagementId, BaselineId,
            "frozen", 1, preview.Sources, [], preview.CanonicalManifest,
            preview.ContentSha256, frozenAt, MemberId, "Lead", "Scope confirmed", null,
            null, [], [], 0);
        var frozen = new ExaminationEngagementRuleFrozen(TenantId, frozenEngagement, baseline, frozenAt);

        // Act
        var replay = () =>
        {
            var rules = new ExaminationEngagementRules(TenantId, ProgramId, EngagementId);
            rules.Replay(opened);
            rules.Replay(frozen);
        };

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    [Fact]
    public void ShouldFreezeExactBaselineAndPreserveItGivenLaterSourceChange()
    {
        // Arrange
        var engagement = NewEngagement();
        var original = ValidPreview();

        // Act
        var failure = engagement.FreezeBaseline(1, BaselineId, original, "Scope confirmed",
            MemberId, "Lead", Now.AddDays(1));
        var afterFreeze = engagement.Baseline;
        var revise = engagement.Revise(2, ValidContent() with { BoundaryId = Uuid.CreateVersion4() },
            MemberId, "Lead", Now.AddDays(2));

        // Assert
        Assert.Null(failure);
        Assert.Equal(original.ContentSha256, afterFreeze!.ContentSha256);
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(revise).Code);
        Assert.Same(afterFreeze, engagement.Baseline);
        Assert.Equal("frozen", engagement.Baseline!.Status);
        Assert.Equal(2, engagement.Revision);
    }

    [Fact]
    public void ShouldRequireReasonAndAffectedRecordsGivenBaselineAmendment()
    {
        // Arrange
        var engagement = NewEngagement();
        Assert.Null(engagement.FreezeBaseline(1, BaselineId, ValidPreview(), "Confirmed",
            MemberId, "Lead", Now.AddDays(1)));
        var current = engagement.Baseline!;
        var amended = ValidPreview(Uuid.CreateVersion4(), current.Sources[1].RecordId);

        // Act
        var missingReason = engagement.AmendBaseline(2, Uuid.CreateVersion4(), amended, [], [],
            " ", MemberId, "Approver", Now.AddDays(2));
        var valid = engagement.AmendBaseline(2, Uuid.CreateVersion4(), amended,
            [amended.Sources[1].RecordId], [], "Corrected scope source", MemberId,
            "Approver", Now.AddDays(2));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(missingReason).Code);
        Assert.Null(valid);
        Assert.Equal(2, engagement.Baseline!.Revision);
        Assert.Equal(BaselineId, engagement.Baseline.AmendsBaselineId);
        Assert.Equal("Corrected scope source", engagement.Baseline.AmendmentReason);
        Assert.Single(engagement.Baseline.ChangedRecordIds);
    }

    [Fact]
    public void ShouldRejectChangedReplayOfExistingBaselineIdGivenFreezeRetry()
    {
        // Arrange
        var engagement = NewEngagement();
        var original = ValidPreview();
        Assert.Null(engagement.FreezeBaseline(1, BaselineId, original, "Confirmed",
            MemberId, "Lead", Now.AddDays(1)));

        // Act
        var exactRetry = engagement.FreezeBaseline(2, BaselineId, original, "Confirmed",
            MemberId, "Lead", Now.AddDays(1));
        var changedRetry = engagement.FreezeBaseline(2, BaselineId,
            ValidPreview(Uuid.CreateVersion4()), "Confirmed", MemberId, "Lead", Now.AddDays(1));

        // Assert
        Assert.Null(exactRetry);
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(changedRetry).Code);
        Assert.Equal(2, engagement.PendingChanges.Count);
    }

    static ExaminationEngagementRules NewEngagement()
    {
        var engagement = new ExaminationEngagementRules(TenantId, ProgramId, EngagementId);
        Assert.Null(engagement.Create(ValidContent(), MemberId, "Lead", Now));
        return engagement;
    }

    static ExaminationEngagementContent ValidContent() => new("type_i", "Audit Firm",
        new DateOnly(2026, 9, 30), null, ["security"], BoundaryId, [MemberId]);

    static TypeIBaselinePreview ValidPreview(Uuid? versionId = null, Uuid? recordId = null)
    {
        var boundary = new TypeIBaselineSourceReference("approved_boundary", BoundaryId,
            BoundaryVersionId, 3, new string('a', 64));
        var control = new TypeIBaselineSourceReference("control",
            recordId ?? Uuid.CreateVersion4(), versionId ?? Uuid.CreateVersion4(), 3,
            new string('b', 64));
        return TypeIBaselinePreviewTestFactory.Create(TenantId, ProgramId, EngagementId,
            [boundary, control]);
    }
}
