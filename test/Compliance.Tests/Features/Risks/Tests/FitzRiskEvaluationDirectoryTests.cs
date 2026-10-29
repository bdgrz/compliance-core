using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.Risks.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

public sealed class FitzRiskEvaluationDirectoryTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldProjectCurrentStateAndOrderedHistoryGivenEvaluationEvents()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var riskId = Uuid.CreateVersion4();
        await using var native = new RiskEvaluationSourceFixture();
        var directory = new FitzRiskEvaluationDirectory(native.Kv, native.Evaluations);
        var events = await EventsAsync(native, tenantId, programId, riskId);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            foreach (var domainEvent in events)
                await directory.ApplyAsync(domainEvent);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var current = await directory.GetAsync(tenantId, riskId);
        var history = await directory.ListHistoryAsync(tenantId, riskId, 2, null);
        var rest = await directory.ListHistoryAsync(tenantId, riskId, 2, history.NextCursor);
        var alien = await directory.GetAsync(Uuid.CreateVersion4(), riskId);

        // Assert
        Assert.NotNull(current);
        Assert.Equal(3, current.Revision);
        Assert.Equal("accept", current.Treatment!.Kind);
        Assert.Equal(2, current.Assessments.Count);
        Assert.Equal([1L, 2L], history.Items.Select(item => item.Revision));
        Assert.Equal("assessment_recorded", history.Items[0].Kind);
        Assert.Equal("treatment_chosen", history.Items[1].Kind);
        Assert.Equal("assessment_recorded", Assert.Single(rest.Items).Kind);
        Assert.Null(alien);
    }

    [Fact]
    public async Task ShouldRejectOutOfOrderRevisionGivenMissingPredecessor()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        await using var native = new RiskEvaluationSourceFixture();
        var directory = new FitzRiskEvaluationDirectory(native.Kv, native.Evaluations);
        var events = await EventsAsync(native, tenantId, Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            Identity(tenantId), ProjectionCheckpoint.Start));

        // Act
        var failure = await Record.ExceptionAsync(async () =>
            await directory.ApplyAsync(events[1]));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
    }

    static async Task<IReadOnlyList<DomainEvent>> EventsAsync(RiskEvaluationSourceFixture native, Uuid tenantId, Uuid programId, Uuid riskId)
    {
        var method = new RiskMethodRules(tenantId, programId);
        Assert.Null(method.Publish(programId, 0, RiskMethodTests.Scale(), RiskMethodTests.Scale(),
            12, ActorReference.ForMember(Uuid.CreateVersion4(), "Lead"), Now));
        var evaluation = new RiskEvaluationRules(tenantId, riskId);
        var assessor = Uuid.CreateVersion4();
        Assert.Null(evaluation.RecordAssessment(programId, 0, Uuid.CreateVersion4(),
            method.Current!, "inherent", 4, 4, "High", assessor, "Assessor", Now));
        Assert.Null(evaluation.ChooseTreatment(programId, 1, "accept", "Accept it", assessor,
            "Assessor", Now));
        Assert.Null(evaluation.RecordAssessment(programId, 2, Uuid.CreateVersion4(),
            method.Current!, "residual", 2, 2, "Low", assessor, "Assessor", Now));
        await native.SeedAsync(evaluation);
        return await native.FactsAsync(tenantId, riskId);
    }

    static CheckpointIdentity Identity(Uuid tenantId) => new("RiskEvaluationDirectoryV2",
        EventStreamPattern.ForPattern(tenantId.ToString(), "risk-evaluations"));
}
