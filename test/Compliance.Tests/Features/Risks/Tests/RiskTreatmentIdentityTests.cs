using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Risks.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

public sealed class RiskTreatmentIdentityTests
{
    [Fact]
    public async Task ShouldReplayWithoutRestoringOldTreatmentGivenLaterChoice()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var risk = Uuid.CreateVersion4();
        var command = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var actor = RbacIds.Member(tenant, user);
        var now = DateTimeOffset.UtcNow;
        var method = new RiskMethodRules(tenant, program);
        Assert.Null(method.Publish(program, 0, RiskMethodTests.Scale(), RiskMethodTests.Scale(), 12,
            ActorReference.ForMember(actor, "Author"), now));
        var owner = new RiskEvaluationRules(tenant, risk, command);
        Assert.Null(owner.RecordAssessment(program, 0, Uuid.CreateVersion4(), method.Current!, "inherent", 2, 2,
            "Initial assessment", actor, "Assessor", now));
        var request = new ChooseRiskTreatment(command, tenant, program, risk, 1, "accept", "Original choice");
        Assert.Null(owner.ChooseTreatment(request, actor, "Original display", now));
        Assert.Null(owner.ChooseTreatment(program, 2, "mitigate", "Later choice", actor, "Author", now));
        await using var source = new RiskEvaluationSourceFixture();
        await source.SeedAsync(owner);
        var facts = await source.FactsAsync(tenant, risk);
        var hydrated = await source.Evaluations.ReadAsync(tenant, program, risk);

        // Act
        var original = await source.Operations().ChooseTreatmentAsync(RiskEvaluationSourceFixture.Context(request, user), CancellationToken.None);
        var changed = await source.Operations().ChooseTreatmentAsync(RiskEvaluationSourceFixture.Context(request with { ExpectedRevision = 2 }, user), CancellationToken.None);

        // Assert
        Assert.True(original.IsSuccess, original.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal("mitigate", hydrated.Treatment!.Kind);
        Assert.Equal(3, hydrated.Revision);
        Assert.Equal(facts.Count, (await source.FactsAsync(tenant, risk)).Count);
    }

    [Fact]
    public async Task ShouldRejectAnotherCommandKindGivenAlreadyAcceptedEvaluationIdentity()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var risk = Uuid.CreateVersion4();
        var command = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var actor = RbacIds.Member(tenant, user);
        var now = DateTimeOffset.UtcNow;
        var method = new RiskMethodRules(tenant, program);
        Assert.Null(method.Publish(program, 0, RiskMethodTests.Scale(), RiskMethodTests.Scale(), 12,
            ActorReference.ForMember(actor, "Author"), now));
        var owner = new RiskEvaluationRules(tenant, risk, command);
        Assert.Null(owner.RecordAssessment(program, 0, command, method.Current!, "inherent", 2, 2,
            "Initial assessment", actor, "Assessor", now));

        await using var source = new RiskEvaluationSourceFixture();
        await source.SeedAsync(owner);

        // Act
        var result = await source.Operations().ChooseTreatmentAsync(RiskEvaluationSourceFixture.Context(new ChooseRiskTreatment(command, tenant, program, risk, 1,
            "accept", "Another command"), user), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(1, owner.Revision);
        Assert.Null(owner.Treatment);
    }
}
