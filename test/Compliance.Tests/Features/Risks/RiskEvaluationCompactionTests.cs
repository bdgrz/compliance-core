using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using System.Reflection;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskEvaluationCompactionTests
{
    [Fact]
    public async Task ShouldKeepOwnerAuthorityBoundedGivenLongAssessmentHistory()
    {
        // Arrange
        await using var source = new RiskEvaluationSourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var risk = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var method = new RiskMethodRules(tenant, program);
        var now = source.Clock.Now;
        Assert.Null(method.Publish(program, 0, RiskMethodTests.Scale(), RiskMethodTests.Scale(), 12, ActorReference.ForMember(actor, "Lead"), now));
        var rules = new RiskEvaluationRules(tenant, risk);
        for (var index = 0; index < 100; index++)
            Assert.Null(rules.RecordAssessment(program, index, Uuid.CreateVersion4(), method.Current!, "inherent", 2, 2,
                new string('界', 4000), actor, "Assessor", now.AddMinutes(index)));
        await source.SeedAsync(rules);
        var evaluation = await source.Reader.HydrateAsync(new RiskEvaluation(tenant, risk));
        var facts = await source.FactsAsync(tenant, risk);

        // Act
        var collections = typeof(RiskEvaluation).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(field => field.GetValue(evaluation) is System.Collections.ICollection).ToArray();
        var maximum = facts.Max(value => JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), ComplianceCoreJsonContext.Default).Length);
        var read = await source.Evaluations.ReadAsync(tenant, program, risk);

        // Assert
        Assert.Empty(collections);
        Assert.True(maximum <= 8192, $"A native evaluation fact must remain compact; largest source was {maximum} bytes.");
        Assert.Equal(100, evaluation.Revision);
        Assert.Equal(100, read.ToView().Assessments.Count);
        Assert.All(read.ToView().Assessments, value => Assert.Equal(new string('界', 4000), value.Rationale));
    }
}
