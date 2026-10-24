using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskTreatmentIdentityTests
{
    [Fact]
    public void ShouldReplayWithoutRestoringOldTreatmentGivenLaterChoice()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var risk = Uuid.CreateVersion4();
        var command = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var method = new RiskMethod(tenant, program);
        Assert.Null(method.Publish(program, 0, RiskMethodTests.Scale(), RiskMethodTests.Scale(), 12,
            ActorReference.ForMember(actor, "Author"), now));
        var owner = new RiskEvaluation(tenant, risk, command);
        Assert.Null(owner.RecordAssessment(program, 0, Uuid.CreateVersion4(), method.Current!, "inherent", 2, 2,
            "Initial assessment", actor, "Assessor", now));
        var request = new ChooseRiskTreatment(command, tenant, program, risk, 1, "accept", "Original choice");
        Assert.Null(owner.ChooseTreatment(request, actor, "Original display", now));
        Assert.Null(owner.ChooseTreatment(program, 2, "mitigate", "Later choice", actor, "Author", now));
        var facts = new AggregateScenario<RiskEvaluation>(owner).PendingEvents.ToArray();
        var hydrated = new AggregateScenario<RiskEvaluation>(new(tenant, risk, command)).Given(facts);

        // Act
        var original = hydrated.Aggregate.ChooseTreatment(request, actor, "Updated display", now.AddDays(1));
        var changed = hydrated.Aggregate.ChooseTreatment(request with { ExpectedRevision = 2 }, actor, "Author", now);

        // Assert
        Assert.Null(original);
        Assert.Equal(CommandFailureCode.StateConflict, changed?.Code);
        Assert.Equal("mitigate", hydrated.Aggregate.Treatment!.Kind);
        Assert.Equal(3, hydrated.Aggregate.Revision);
        Assert.Empty(hydrated.PendingEvents);
    }

    [Fact]
    public void ShouldRejectAnotherCommandKindGivenAlreadyAcceptedEvaluationIdentity()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var risk = Uuid.CreateVersion4();
        var command = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var method = new RiskMethod(tenant, program);
        Assert.Null(method.Publish(program, 0, RiskMethodTests.Scale(), RiskMethodTests.Scale(), 12,
            ActorReference.ForMember(actor, "Author"), now));
        var owner = new RiskEvaluation(tenant, risk, command);
        Assert.Null(owner.RecordAssessment(program, 0, command, method.Current!, "inherent", 2, 2,
            "Initial assessment", actor, "Assessor", now));

        // Act
        var result = owner.ChooseTreatment(new ChooseRiskTreatment(command, tenant, program, risk, 1,
            "accept", "Another command"), actor, "Author", now);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, result?.Code);
        Assert.Equal(1, owner.Revision);
        Assert.Null(owner.Treatment);
    }
}
