using Bdgrz.Compliance.Features.Changes;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using SignificantChangeAggregate = Bdgrz.Compliance.Features.Changes.SignificantChange;

namespace Bdgrz.Compliance.Tests.Features.Changes;

public sealed class SignificantChangeTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ChangeId = Uuid.CreateVersion4();
    static readonly Uuid OwnerId = Uuid.CreateVersion4();
    static readonly Uuid RecorderId = Uuid.CreateVersion4();
    static readonly Uuid AssessorId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRecordManualChangeWithoutExternalProvenanceGivenAuthorizedOwner()
    {
        // Arrange
        var change = NewChange();

        // Act
        var failure = change.Record(Content(), RecorderId, "Compliance lead", Now);

        // Assert
        Assert.Null(failure);
        Assert.Equal("recorded", change.Current!.Status);
        Assert.Null(change.Current.Current!.Content.SourceSystem);
        Assert.Null(change.Current.Current.Content.SourceIdentifier);
        Assert.Equal("confidential", change.Current.Current.Content.HandlingClass);
        Assert.Equal(OwnerId, change.Current.Current.Content.OwnerMemberId);
    }

    [Fact]
    public void ShouldRequireRuleBasedSignificanceAndAffectedRecordsGivenChangeImpactAssessment()
    {
        // Arrange
        var change = RecordedChange();

        // Act
        var contradictory = change.Assess(change.Revision, Impact("not_significant",
            [AffectedRecord("control")], changesKeyControl: true), AssessorId,
            "Compliance lead", Now.AddHours(1));
        var missingAffectedRecord = change.Assess(change.Revision, Impact("significant",
            [], changesKeyControl: true), AssessorId, "Compliance lead", Now.AddHours(1));
        var assessed = change.Assess(change.Revision, Impact("significant",
            [AffectedRecord("control")], changesKeyControl: true, requiresSystemDescriptionReview: true),
            AssessorId, "Compliance lead", Now.AddHours(1));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(contradictory).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(missingAffectedRecord).Code);
        Assert.Null(assessed);
        Assert.Equal("significant", change.Current!.CurrentAssessment!.Significance);
        Assert.Equal("needs_review", change.Current.CurrentAssessment.SystemDescriptionStatus);
        Assert.False(change.Current.PeriodCloseEligible);
    }

    [Fact]
    public void ShouldRequireExactReviewWorkAndSystemDescriptionDispositionGivenSignificantChange()
    {
        // Arrange
        var change = RecordedChange();
        var workId = Uuid.CreateVersion4();
        Assert.Null(change.Assess(change.Revision, Impact("significant",
            [AffectedRecord("system_description_section")], changesBoundary: true, requiresSystemDescriptionReview: true,
            relatedWorkIds: [workId]), AssessorId, "Compliance lead", Now.AddHours(1)));

        // Act
        var missingWork = change.LinkReviewWork(change.Revision, [], ReviewerId,
            "Compliance lead", Now.AddHours(2));
        var linked = change.LinkReviewWork(change.Revision, [workId], ReviewerId,
            "Compliance lead", Now.AddHours(2));
        var selfDecision = change.RecordSystemDescriptionDecision(change.Revision, "no_change",
            Uuid.CreateVersion4(), "Change does not alter the approved description", AssessorId,
            "Compliance lead", Now.AddHours(3));
        var decisionId = Uuid.CreateVersion4();
        var decided = change.RecordSystemDescriptionDecision(change.Revision, "no_change",
            decisionId, "The approved description remains accurate", ReviewerId,
            "Compliance lead", Now.AddHours(3));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(missingWork).Code);
        Assert.Null(linked);
        Assert.Equal(CommandFailureCode.ActorProhibited, Assert.IsType<CommandFailure>(selfDecision).Code);
        Assert.Null(decided);
        Assert.Equal("no_change", change.Current!.CurrentSystemDescriptionDecision!.Decision);
        Assert.True(change.Current.PeriodCloseEligible);
    }

    [Fact]
    public void ShouldRetainExactRelatedExceptionReferencesGivenImpactAssessment()
    {
        // Arrange
        var change = RecordedChange();
        var exceptionId = Uuid.CreateVersion4();

        // Act
        var assessed = change.Assess(change.Revision, Impact("not_significant", [],
            relatedExceptionIds: [exceptionId]), AssessorId, "Compliance lead", Now.AddHours(1));

        // Assert
        Assert.Null(assessed);
        Assert.Equal([exceptionId], change.Current!.CurrentAssessment!.RelatedExceptionIds);
    }

    [Fact]
    public void ShouldReviseAndReplayChangeChronologyGivenLaterCorrection()
    {
        // Arrange
        var change = RecordedChange();
        Assert.Null(change.Assess(change.Revision, Impact("not_significant", [], []),
            AssessorId, "Compliance lead", Now.AddHours(1)));
        var revision = Content() with { Title = "Revised network migration" };
        Assert.Null(change.Revise(change.Revision, revision, "Corrected title", RecorderId,
            "Compliance lead", Now.AddHours(2)));
        var events = new AggregateScenario<SignificantChangeAggregate>(change).PendingEvents.ToArray();

        // Act
        var replay = new AggregateScenario<SignificantChangeAggregate>(NewChange()).Given(events).Aggregate;

        // Assert
        Assert.Equal("recorded", replay.Current!.Status);
        Assert.Equal(2, replay.Current.CurrentVersionNumber);
        Assert.Equal("Revised network migration", replay.Current.Current!.Content.Title);
        Assert.Single(replay.Current.Assessments);
        Assert.Equal(change.Revision, replay.Current.Revision);
    }

    static SignificantChangeAggregate NewChange() => new(TenantId, ProgramId, ChangeId);

    static SignificantChangeAggregate RecordedChange()
    {
        var change = NewChange();
        Assert.Null(change.Record(Content(), RecorderId, "Compliance lead", Now));
        return change;
    }

    static SignificantChangeContent Content() => new(null, null, null,
        "Network migration", "Move the in-scope payment service to a new subnet",
        new DateOnly(2026, 11, 1), null, OwnerId, [Uuid.CreateVersion4()], "high",
        "confidential", [Uuid.CreateVersion4()]);

    static SignificantChangeImpactContent Impact(string significance,
        IReadOnlyList<SignificantChangeAffectedRecordReference> affectedRecords,
        IReadOnlyList<Uuid> relatedWorkIds = default!,
        IReadOnlyList<Uuid> relatedExceptionIds = default!,
        bool changesBoundary = false, bool changesCommitment = false,
        bool changesKeyControl = false, bool changesInfrastructure = false,
        bool requiresSystemDescriptionReview = false) => new(significance,
        "Reviewed the change against scope and control operation",
        changesBoundary, changesCommitment, changesKeyControl, changesInfrastructure,
        ["scope", "control", "system_description"], affectedRecords,
        relatedWorkIds ?? [], [], relatedExceptionIds ?? [], [], requiresSystemDescriptionReview);

    static SignificantChangeAffectedRecordReference AffectedRecord(string recordType) => new(
        recordType, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1);
}
