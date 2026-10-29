using System.Reflection;
using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

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
