using Cntryl.Portia;
using Cntryl.Portia.Testing;
using AuditOutcomeAggregate = Bdgrz.Compliance.Features.Examinations.Aggregates.AuditOutcome;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class AuditOutcomeLifecycleTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid OutcomeId = Uuid.CreateVersion4();
    static readonly Uuid ExaminationId = Uuid.CreateVersion4();
    static readonly Uuid RecorderId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 17, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldAppendNoFactGivenInvalidProvenanceOrArtifactBinding()
    {
        // Arrange
        var good = Content();
        var invalid = new[]
        {
            good with { ScopeKind = "type_iii" },
            good with { PackageVersion = 0 },
            good with { SourceSnapshotSha256 = "not-a-hash" },
            good with { PackageManifestId = Uuid.Empty },
            good with { OutcomeCode = "has space" },
            good with { OutcomeSummary = "   " },
            good with { ReportArtifactSha256 = null },
            good with { ReportArtifactId = null },
            good with { ReportArtifactId = Uuid.Empty },
        };

        // Act
        var outcomes = new AuditOutcomeAggregate[invalid.Length];
        var failures = new CommandFailure?[invalid.Length];
        for (var index = 0; index < invalid.Length; index++)
        {
            outcomes[index] = new AuditOutcomeAggregate(TenantId, ProgramId, OutcomeId);
            failures[index] = outcomes[index].Record(invalid[index], RecorderId, "Audit manager", Now);
        }

        // Assert
        for (var index = 0; index < invalid.Length; index++)
        {
            Assert.Equal(CommandFailureCode.InvalidContent,
                Assert.IsType<CommandFailure>(failures[index]).Code);
            Assert.Empty(new AggregateScenario<AuditOutcomeAggregate>(outcomes[index]).PendingEvents);
            Assert.Null(outcomes[index].Current);
        }
    }

    [Fact]
    public void ShouldAppendNoFactGivenMissingRecorderOrEmptyProgramScope()
    {
        // Arrange
        var noRecorder = new AuditOutcomeAggregate(TenantId, ProgramId, OutcomeId);
        var noProgram = new AuditOutcomeAggregate(TenantId, Uuid.Empty, OutcomeId);
        var blankDisplay = new AuditOutcomeAggregate(TenantId, ProgramId, OutcomeId);

        // Act
        var recorderFailure = noRecorder.Record(Content(), Uuid.Empty, "Audit manager", Now);
        var programFailure = noProgram.Record(Content(), RecorderId, "Audit manager", Now);
        var displayFailure = blankDisplay.Record(Content(), RecorderId, " ", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(recorderFailure).Code);
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(programFailure).Code);
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(displayFailure).Code);
        Assert.Empty(new AggregateScenario<AuditOutcomeAggregate>(noRecorder).PendingEvents);
        Assert.Empty(new AggregateScenario<AuditOutcomeAggregate>(noProgram).PendingEvents);
        Assert.Empty(new AggregateScenario<AuditOutcomeAggregate>(blankDisplay).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseRetryWithoutAppendingGivenDifferentRecorder()
    {
        // Arrange
        var outcome = new AuditOutcomeAggregate(TenantId, ProgramId, OutcomeId);
        var content = Content();
        Assert.Null(outcome.Record(content, RecorderId, "Audit manager", Now));

        // Act
        var failure = outcome.Record(content, Uuid.CreateVersion4(), "Other manager",
            Now.AddMinutes(1));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(failure).Code);
        Assert.Single(new AggregateScenario<AuditOutcomeAggregate>(outcome).PendingEvents);
        Assert.Equal(RecorderId, outcome.Current!.RecordedByMemberId);
    }

    [Fact]
    public void ShouldTreatNormalizedEquivalentRetryAsNoOpGivenHashCaseAndPaddingDifferences()
    {
        // Arrange
        var outcome = new AuditOutcomeAggregate(TenantId, ProgramId, OutcomeId);
        var content = Content();
        Assert.Null(outcome.Record(content, RecorderId, "Audit manager", Now));
        var variant = content with
        {
            SourceSnapshotSha256 = content.SourceSnapshotSha256.ToUpperInvariant(),
            OutcomeCode = " QUALIFIED ",
            OutcomeSummary = " " + content.OutcomeSummary + " ",
        };

        // Act
        var retried = outcome.Record(variant, RecorderId, "Audit manager", Now.AddMinutes(2));

        // Assert
        Assert.Null(retried);
        Assert.Single(new AggregateScenario<AuditOutcomeAggregate>(outcome).PendingEvents);
        Assert.Equal("qualified", outcome.Current!.Content.OutcomeCode);
    }

    [Fact]
    public void ShouldKeepImmutableOutcomeGivenRetryAndChangedIntentAfterReplay()
    {
        // Arrange
        var source = new AuditOutcomeAggregate(TenantId, ProgramId, OutcomeId);
        var content = Content();
        Assert.Null(source.Record(content, RecorderId, "Audit manager", Now));
        var events = new AggregateScenario<AuditOutcomeAggregate>(source).PendingEvents.ToArray();
        var replay = new AggregateScenario<AuditOutcomeAggregate>(
            new AuditOutcomeAggregate(TenantId, ProgramId, OutcomeId)).Given(events).Aggregate;
        var digest = replay.Current!.ContentSha256;

        // Act
        var exact = replay.Record(content, RecorderId, "Audit manager", Now.AddHours(1));
        var changedPackage = replay.Record(content with { PackageVersion = 2 }, RecorderId,
            "Audit manager", Now.AddHours(1));

        // Assert
        Assert.Null(exact);
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(changedPackage).Code);
        Assert.Equal(1, replay.Revision);
        Assert.Equal(digest, replay.Current!.ContentSha256);
        Assert.Equal(Now, replay.Current.RecordedAt);
    }

    static AuditOutcomeContent Content() => new(ExaminationId, "type_i",
        Uuid.CreateVersion4(), new string('a', 64), Uuid.CreateVersion4(), 1,
        Uuid.CreateVersion4(), new string('b', 64), "auditor", "qualified",
        "Auditor-supplied opinion summary", "report-ref-2026-09", Uuid.CreateVersion4(),
        new string('c', 64), new DateOnly(2026, 9, 30));
}
