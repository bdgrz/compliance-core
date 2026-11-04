using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Proofs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Programs.Tests;

public sealed class ProgramNamedLifecycleTests
{
    static readonly Uuid Actor = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRetainExactEditionGivenCreation()
    {
        // Arrange
        var program = NewProgram();
        var edition = Uuid.CreateVersion4();

        // Act
        var error = program.CreateWithEdition("SOC 2", Plan(), edition, Actor, "Lead", Now);

        // Assert
        Assert.Null(error);
        Assert.True(program.IsCreated);
        Assert.Equal(edition, program.CriteriaEditionId);
        Assert.Equal("readiness", program.Stage);
        Assert.Equal(nameof(CreateProgramV3), Assert.Single(program.PendingChanges).Kind);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenCreationHasNoEdition()
    {
        // Arrange
        var program = NewProgram();

        // Act
        var error = program.CreateWithEdition("SOC 2", Plan(), Uuid.Empty, Actor, "Lead", Now);

        // Assert
        Assert.NotNull(error);
        Assert.False(program.IsCreated);
        Assert.Empty(program.PendingChanges);
    }

    [Fact]
    public void ShouldEmitOnlyNamedActualChangeGivenRenameAndTargetRevision()
    {
        // Arrange
        var program = Created();
        program.ClearPendingChanges();

        // Act
        Assert.Null(program.Rename(1, " SOC 2 renamed ", Actor, "Lead", Now));
        Assert.Null(program.Rename(2, "SOC 2 renamed", Actor, "Lead", Now));
        var targets = Plan() with { TargetTypeIAsOfDate = new DateOnly(2027, 4, 1) };
        Assert.Null(program.ReviseTargets(2, targets, Actor, "Lead", Now));
        Assert.Null(program.ReviseTargets(3, targets, Actor, "Lead", Now));

        // Assert
        Assert.Collection(program.PendingChanges,
            change => Assert.Equal(nameof(RenameProgram), change.Kind),
            change => Assert.Equal(nameof(ReviseProgramTargets), change.Kind));
        Assert.Equal("SOC 2 renamed", program.CurrentContent!.Name);
        Assert.Equal(targets, program.CurrentContent.Plan);
        Assert.Equal(3, program.Revision);
    }

    [Theory]
    [InlineData("readiness")]
    [InlineData("type_ii")]
    [InlineData("unknown")]
    public void ShouldRefuseWithoutFactGivenStageIsNotNext(string stage)
    {
        // Arrange
        var program = Created();
        program.ClearPendingChanges();

        // Act
        var error = program.ChangeStage(1, stage, Support(program, "readiness"), "Decision", Actor, "Lead", Now);

        // Assert
        Assert.NotNull(error);
        Assert.Equal("readiness", program.Stage);
        Assert.Equal(1, program.Revision);
        Assert.Empty(program.PendingChanges);
    }

    [Fact]
    public void ShouldRecordAttributableNextStagesGivenExactSupportingReferences()
    {
        // Arrange
        var program = Created();
        program.ClearPendingChanges();
        var readiness = Support(program, "readiness");
        var examination = Support(program, "examination");

        // Act
        Assert.Null(program.ChangeStage(1, "type_i", readiness, "Ready to examine", Actor, "Lead", Now));
        Assert.Null(program.ChangeStage(2, "type_ii", examination, "Type I report issued", Actor, "Lead", Now.AddMinutes(1)));

        // Assert
        Assert.Equal("type_ii", program.Stage);
        Assert.Equal(3, program.Revision);
        Assert.Collection(program.PendingChanges,
            first => Assert.Equal((nameof(ChangeProgramStage), "type_i", readiness, "Ready to examine"),
                (first.Kind, first.Stage, first.StageSupport, first.Rationale)),
            second => Assert.Equal((nameof(ChangeProgramStage), "type_ii", examination, "Type I report issued"),
                (second.Kind, second.Stage, second.StageSupport, second.Rationale)));
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenSupportingReferenceIsMissingOrForeign()
    {
        // Arrange
        var program = Created();
        program.ClearPendingChanges();
        var foreign = Support(program, "readiness") with { ProgramId = Uuid.CreateVersion4() };

        // Act
        var missing = program.ChangeStage(1, "type_i", null!, "Decision", Actor, "Lead", Now);
        var otherProgram = program.ChangeStage(1, "type_i", foreign, "Decision", Actor, "Lead", Now);
        var noReason = program.ChangeStage(1, "type_i", Support(program, "readiness"), " ", Actor, "Lead", Now);

        // Assert
        Assert.NotNull(missing);
        Assert.NotNull(otherProgram);
        Assert.NotNull(noReason);
        Assert.Empty(program.PendingChanges);
        Assert.Equal("readiness", program.Stage);
    }

    static ProgramRules NewProgram() => new(Uuid.CreateVersion4(), Uuid.CreateVersion4());
    static ProgramRules Created()
    {
        var program = NewProgram();
        Assert.Null(program.CreateWithEdition("SOC 2", Plan(), Uuid.CreateVersion4(), Actor, "Lead", Now));
        return program;
    }
    static ProgramPlan Plan() => new(null, null, null, null, null, null);
    static ProgramStageSupport Support(ProgramRules program, string kind) => new(kind, program.Id,
        new SourceFactLocator(new EventStreamAddress(program.TenantId.ToString(), kind, Uuid.CreateVersion4().ToString()),
            Uuid.CreateVersion4(), 0, 0, 0, 1, new string('a', 64)),
        new ImmutableContentRef(Uuid.CreateVersion4(), new string('b', 64), 1, "application/json", "program-stage-support.v1"));
}
