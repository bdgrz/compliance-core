using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskAssessmentIdentityTests
{
    [Fact]
    public void ShouldReturnOriginalViewWithoutReselectingMethodGivenLaterAssessmentAndDurableReplay()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var risk = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var methodOwner = new RiskMethod(tenant, program);
        Assert.Null(methodOwner.Publish(program, 0, RiskMethodTests.Scale(), RiskMethodTests.Scale(), 12,
            Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(actor, "Author"), now));
        var method = methodOwner.GetVersion(1)!;
        var request = new RecordRiskAssessment(Uuid.CreateVersion4(), tenant, program, risk, 0, method.Version,
            "inherent", 2, 2, "Original rationale");
        var evaluation = new RiskEvaluation(tenant, risk);
        Assert.Null(evaluation.RecordAssessment(program, 0, request.CommandId, method, request.Phase,
            request.Likelihood, request.Impact, request.Rationale, actor, "Original display", now));
        Assert.Null(evaluation.RecordAssessment(program, 1, Uuid.CreateVersion4(), method, "target", 1, 1,
            "Later assessment", actor, "Later display", now.AddMinutes(1)));
        var facts = new AggregateScenario<RiskEvaluation>(evaluation).PendingEvents.ToArray();
        var replay = new AggregateScenario<RiskEvaluation>(new(tenant, risk)).Given(facts);

        // Act
        var result = replay.Aggregate.ReplayAssessment(request, actor);

        // Assert
        var original = Assert.IsType<Result<RiskAssessmentView>>(result);
        Assert.True(original.IsSuccess);
        Assert.Equal("Original rationale", original.Value.Rationale);
        Assert.Equal("Original display", original.Value.Assessor.Display);
        Assert.Equal(now, original.Value.AssessedAt);
        Assert.Equal(2, replay.Aggregate.Revision);
        Assert.Empty(replay.PendingEvents);
    }
}
