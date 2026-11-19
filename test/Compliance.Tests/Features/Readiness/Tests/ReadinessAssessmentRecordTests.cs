using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Readiness.Services;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Readiness.Tests;

public sealed class ReadinessAssessmentRecordTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Program = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Readiness contributor");

    [Fact]
    public void ShouldUseTenantAndAssessmentIdentityGivenDifferentPrograms()
    {
        // Arrange
        var assessment = Uuid.CreateVersion4();
        var first = new ReadinessAssessmentRecord(Tenant, assessment);
        var second = new ReadinessAssessmentRecord(Tenant, assessment);
        var otherTenant = new ReadinessAssessmentRecord(Uuid.CreateVersion4(), assessment);

        // Act
        var command = new RecordReadinessAssessment(assessment, Tenant, Program, Now);
        var result = first.Record(command, "readiness-rules/13", Now, Content(), Actor, Now);
        var wrongProgram = first.Record(command with { ProgramId = Uuid.CreateVersion4() }, "readiness-rules/13", Now, Content(), Actor, Now);

        // Assert
        Assert.Equal(first.Stream, second.Stream);
        Assert.NotEqual(first.Stream, otherTenant.Stream);
        Assert.True(result.IsSuccess);
        Assert.False(wrongProgram.IsSuccess);
        Assert.Single(new AggregateScenario<ReadinessAssessmentRecord>(first).PendingEvents);
    }

    [Fact]
    public void ShouldKeepOriginalResultGivenExactRetryWithNewCalculation()
    {
        // Arrange
        var command = new RecordReadinessAssessment(Uuid.CreateVersion4(), Tenant, Program, null);
        var owner = new ReadinessAssessmentRecord(Tenant, command.AssessmentId);
        var original = Content();
        Assert.True(owner.Record(command, "readiness-rules/13", Now, original, Actor, Now).IsSuccess);

        // Act
        var result = owner.Record(command, "readiness-rules/14", Now.AddDays(1), Content(), Actor, Now.AddDays(1));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(original, owner.Recorded!.ResultContent);
        Assert.Equal(Now, owner.Recorded.AsOf);
        Assert.Single(new AggregateScenario<ReadinessAssessmentRecord>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldUseStableSameDayIdentityGivenScheduledProgramRun()
    {
        // Arrange
        var day = new DateOnly(2026, 11, 4);

        // Act
        var first = DailyReadinessAssessment.IdFor(Tenant, Program, day);
        var retry = DailyReadinessAssessment.IdFor(Tenant, Program, day);
        var next = DailyReadinessAssessment.IdFor(Tenant, Program, day.AddDays(1));
        var otherProgram = DailyReadinessAssessment.IdFor(Tenant, Uuid.CreateVersion4(), day);

        // Assert
        Assert.Equal(first, retry);
        Assert.NotEqual(first, next);
        Assert.NotEqual(first, otherProgram);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenScheduledIdentityDoesNotMatchProgramDay()
    {
        // Arrange
        var command = new RecordReadinessAssessment(Uuid.CreateVersion4(), Tenant, Program, Now,
            new DateOnly(2026, 11, 4));
        var owner = new ReadinessAssessmentRecord(Tenant, command.AssessmentId);

        // Act
        var result = owner.Record(command, "readiness-rules/13", Now, Content(),
            ActorReference.ForSystemProcess(DailyReadinessAssessment.ProcessId, "Daily readiness"), Now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(new AggregateScenario<ReadinessAssessmentRecord>(owner).PendingEvents);
    }

    static ImmutableContentRef Content() => new(Uuid.CreateVersion4(), new string('a', 64), 100,
        "application/json", "readiness-assessment-result.v2");
}
