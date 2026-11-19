using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Projections;
using Bdgrz.Compliance.Features.Risks.Services;

namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

public sealed class RiskReassessmentCadenceTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldKeepAnnualDeadlineGivenNewInherentAssessmentAfterOldResidual()
    {
        // Arrange
        var residualAt = Now.AddYears(-1);
        var view = View([Assessment("residual", residualAt), Assessment("inherent", Now.AddDays(-1))]);

        // Act
        var actual = RiskEvaluationStatus.AsOf(view, Now);

        // Assert
        Assert.Equal(residualAt.AddYears(1), actual.ReassessmentDueAt);
        Assert.Equal("reassessment_due", actual.Status);
    }

    [Fact]
    public void ShouldRequireResidualGivenOnlyInherentAssessment()
    {
        // Arrange
        var view = View([Assessment("inherent", Now)]);

        // Act
        var actual = RiskEvaluationStatus.AsOf(view, Now);

        // Assert
        Assert.Equal("reassessment_due", actual.Status);
        Assert.Null(actual.ReassessmentDueAt);
    }

    [Theory]
    [InlineData("inherent", 1, true)]
    [InlineData("residual", -1, true)]
    [InlineData("residual", 0, true)]
    [InlineData("residual", 1, false)]
    public void ShouldKeepRequirementUntilLaterResidualGivenAssessmentPhaseAndTime(string phase, int minutes, bool remainsOpen)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var risk = Uuid.CreateVersion4();
        var trigger = new RiskReassessmentTriggerView(Uuid.CreateVersion4(), risk, "boundary_changed", "boundary:1", Now);
        var snapshot = new RiskGovernanceSnapshot(tenant, program, new Dictionary<Uuid, RiskGovernanceRecord>
        {
            [risk] = new(1, null, [], [trigger], [], new Dictionary<(Uuid, Uuid), Uuid>()),
        });

        // Act
        var open = snapshot.OpenTriggers(risk, [Assessment(phase, Now.AddMinutes(minutes))]);

        // Assert
        Assert.Equal(remainsOpen ? 1 : 0, open.Count);
    }

    static RiskAssessmentView Assessment(string phase, DateTimeOffset at) => new(Uuid.CreateVersion4(), phase,
        Uuid.CreateVersion4(), 1, 2, 3, 6, "Assessed", ActorReference.ForMember(Uuid.CreateVersion4(), "Assessor"), at);

    static RiskEvaluationView View(IReadOnlyList<RiskAssessmentView> assessments) => new(Uuid.CreateVersion4(),
        Uuid.CreateVersion4(), Uuid.CreateVersion4(), assessments.Count, "assessed", assessments,
        new RiskTreatmentView("transfer", "Insured", ActorReference.ForMember(Uuid.CreateVersion4(), "Lead"), Now),
        [], null, null, null);
}
