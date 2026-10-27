using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskAssessmentIdentityTests
{
    [Fact]
    public async Task ShouldReturnOriginalViewWithoutReselectingMethodGivenLaterAssessmentAndDurableReplay()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var risk = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var actor = RbacIds.Member(tenant, user);
        var now = DateTimeOffset.UtcNow;
        var methodOwner = new RiskMethodRules(tenant, program);
        Assert.Null(methodOwner.Publish(program, 0, RiskMethodTests.Scale(), RiskMethodTests.Scale(), 12,
            Bdgrz.Compliance.Features.AccessControl.Models.ActorReference.ForMember(actor, "Author"), now));
        var method = methodOwner.GetVersion(1)!;
        var request = new RecordRiskAssessment(Uuid.CreateVersion4(), tenant, program, risk, 0, method.Version,
            "inherent", 2, 2, "Original rationale");
        var evaluation = new RiskEvaluationRules(tenant, risk);
        Assert.Null(evaluation.RecordAssessment(program, 0, request.CommandId, method, request.Phase,
            request.Likelihood, request.Impact, request.Rationale, actor, "Original display", now));
        Assert.Null(evaluation.RecordAssessment(program, 1, Uuid.CreateVersion4(), method, "target", 1, 1,
            "Later assessment", actor, "Later display", now.AddMinutes(1)));
        await using var source = new RiskEvaluationSourceFixture();
        await source.SeedAsync(evaluation);
        var facts = await source.FactsAsync(tenant, risk);
        var replay = await source.Evaluations.ReadAsync(tenant, program, risk);

        // Act
        var result = await source.Operations().RecordAssessmentAsync(RiskEvaluationSourceFixture.Context(request, user),
            new RiskGovernanceReader(source.Reader, source.Events, source.Content), CancellationToken.None);

        // Assert
        var original = Assert.IsType<Result<RiskAssessmentView>>(result);
        Assert.True(original.IsSuccess);
        Assert.Equal("Original rationale", original.Value.Rationale);
        Assert.Equal("Original display", original.Value.Assessor.Display);
        Assert.Equal(now, original.Value.AssessedAt);
        Assert.Equal(2, replay.Revision);
        Assert.Equal(facts.Count, (await source.FactsAsync(tenant, risk)).Count);
    }
}
