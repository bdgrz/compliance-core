using System.Collections;
using System.Reflection;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Events;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evaluations.Queries;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.Evaluations.Tests;

public sealed class ControlEvaluationPlanHandlerTests
{
    static readonly IReadOnlyList<EvaluationProcedureStep> Steps =
    [
        new("design", "inspection",
            [new EvaluationInspectedItem("record", "policy/access-review", "v3")],
            "Policy requires a monthly signed review."),
        new("implementation", "reperformance",
            [new EvaluationInspectedItem("artifact", "exports/q3.csv", "sha256:ab12")],
            "Every leaver was removed."),
    ];

    [Theory]
    [InlineData("control_version")]
    [InlineData("logical_id")]
    [InlineData("content_digest")]
    public async Task ShouldRejectContradictoryPlanBodyGivenNativeVersionSource(string fault)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.DefineEvaluationPlanAsync(0, "Original objective", Steps);
        var owner = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new ControlEvaluationPlan(fixture.TenantId, fixture.ProgramId, fixture.ControlId));
        var fact = Assert.IsType<ControlEvaluationPlanVersionDefined>(owner.Latest);
        var changed = fault switch
        {
            "control_version" => fact with { ControlVersionId = Uuid.CreateVersion4() },
            "logical_id" => fact with { PlanVersionId = Uuid.CreateVersion4() },
            "content_digest" => fact with { Content = fact.Content with { Sha256 = new string('f', 64) } },
            _ => fact,
        };

        // Act
        var error = await Record.ExceptionAsync(async () =>
            await ControlEvaluationPlanReader.ReadVersionAsync(fixture.Content, changed));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(error);
    }

    [Fact]
    public async Task ShouldKeepEvaluationPlanAuthorityBoundedGivenManyDefinitionsWithHistoricalReads()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        ControlEvaluationPlanVersionView? first = null;
        for (var version = 0; version < 32; version++)
        {
            var defined = await fixture.DefineEvaluationPlanAsync(version, $"Evaluation objective {version + 1}", Steps);
            first ??= defined;
        }

        // Act
        var owner = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new ControlEvaluationPlan(fixture.TenantId, fixture.ProgramId, fixture.ControlId));
        var historical = await fixture.AsAsync(fixture.OutsiderUserId, new GetControlEvaluationPlanVersion(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, first!.PlanVersionId));

        // Assert
        Assert.Equal(32, owner.Revision);
        Assert.Equal("Evaluation objective 1", historical.Objective);
        Assert.All(typeof(ControlEvaluationPlan).GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly), field =>
            Assert.False(field.GetValue(owner) is ICollection { Count: > 2 },
                $"The evaluation plan owner retains lifetime definitions in {field.Name}."));
    }

    [Fact]
    public async Task ShouldReplayOriginalPlanVersionGivenFreshTransportRequestAfterSuccessor()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var request = new DefineControlEvaluationPlan(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
            fixture.ControlVersionId, 0, "Original evaluation objective", Steps, false);
        var first = await fixture.AsAsync(fixture.LeadUserId, request);
        await fixture.DefineEvaluationPlanAsync(1, "Successor objective", Steps);

        // Act
        var replay = await fixture.AsAsync(fixture.LeadUserId, request);

        // Assert
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(first, ComplianceCoreJsonContext.Default.ControlEvaluationPlanVersionView),
            System.Text.Json.JsonSerializer.Serialize(replay, ComplianceCoreJsonContext.Default.ControlEvaluationPlanVersionView));
        Assert.Equal(1, replay.Version);
    }

    [Fact]
    public async Task ShouldReadStablePlanHistoryGivenManagerDefinesSuccessor()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var first = await fixture.DefineEvaluationPlanAsync(0,
            "Management reviews access.", Steps, testerIndependenceRequired: true);

        // Act
        var second = await fixture.DefineEvaluationPlanAsync(first.Version,
            "Management reviews privileged access monthly.", Steps);
        var read = await fixture.AsAsync(fixture.OutsiderUserId,
            new GetControlEvaluationPlanVersion(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, first.PlanVersionId));
        var history = await fixture.AsAsync(fixture.OutsiderUserId,
            new ListControlEvaluationPlanVersions(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId));

        // Assert
        Assert.Equal(1, first.Version);
        Assert.Equal(2, second.Version);
        Assert.Equal(fixture.ControlVersionId, first.ControlVersionId);
        Assert.Equal(first.Objective, read.Objective);
        Assert.True(read.TesterIndependenceRequired);
        Assert.Equal([1L, 2L], history.Items.Select(static item => item.Version));
        Assert.Equal(first.PlanVersionId, history.Items[0].PlanVersionId);
    }

    [Fact]
    public async Task ShouldRejectUnsupportedPageLimitGivenPlanHistoryList()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();

        // Act
        // Assert
        await fixture.Scenario(fixture.OutsiderUserId)
            .When(new ListControlEvaluationPlanVersions(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, 0))
            .ExpectFailure(RequestErrorKind.Validation);
        await fixture.Scenario(fixture.OutsiderUserId)
            .When(new ListControlEvaluationPlanVersions(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, 201))
            .ExpectFailure(RequestErrorKind.Validation);
    }

    [Fact]
    public async Task ShouldRequireProgramManagerGivenPlanDefinition()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();

        // Act
        // Assert
        await fixture.Scenario(fixture.OwnerUserId)
            .When(new DefineControlEvaluationPlan(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, fixture.ControlVersionId, 0, "Evaluate the control.",
                Steps, false))
            .ExpectFailure(RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldRequireApprovedControlVersionGivenPlanDefinition()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();

        // Act
        // Assert
        await fixture.Scenario(fixture.LeadUserId)
            .When(new DefineControlEvaluationPlan(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, Uuid.CreateVersion4(), 0, "Evaluate the control.", Steps, false))
            .ExpectFailure(RequestErrorKind.NotFound);
    }

    [Fact]
    public async Task ShouldFreezeExactPlanAndRejectOldVersionGivenPlanChangesAfterStart()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var first = await fixture.DefineEvaluationPlanAsync(0, "Evaluate access review.", Steps);

        // Act
        var started = await fixture.AsAsync(fixture.OwnerUserId, new StartControlEvaluation(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, first.PlanVersionId));
        var second = await fixture.DefineEvaluationPlanAsync(first.Version,
            "Evaluate privileged access review.", Steps);
        var read = await fixture.AsAsync(fixture.OutsiderUserId, new GetControlEvaluation(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, started.EvaluationId));

        // Assert
        Assert.Equal(first.PlanVersionId, read.PlanVersionId);
        Assert.Equal(first.Version, read.PlanVersion);
        Assert.Equal(first.Steps.Count, read.Steps.Count);
        for (var index = 0; index < first.Steps.Count; index++)
        {
            Assert.Equal(first.Steps[index].Assertion, read.Steps[index].Assertion);
            Assert.Equal(first.Steps[index].Method, read.Steps[index].Method);
            Assert.Equal(first.Steps[index].ExpectedCondition,
                read.Steps[index].ExpectedCondition);
            Assert.Equal(first.Steps[index].InspectedItems.Select(static item =>
                    (item.Kind, item.Reference, item.Version)),
                read.Steps[index].InspectedItems.Select(static item =>
                    (item.Kind, item.Reference, item.Version)));
        }
        Assert.Equal(2, second.Version);
        await fixture.Scenario(fixture.OwnerUserId)
            .When(new StartControlEvaluation(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, first.PlanVersionId))
            .ExpectFailure(RequestErrorKind.Conflict);
    }

    [Fact]
    public async Task ShouldRequireIndependentEvaluatorGivenPlanFlag()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.DefineEvaluationPlanAsync(0, "Independent test.", Steps,
            testerIndependenceRequired: true);

        // Act
        await fixture.Scenario(fixture.OwnerUserId)
            .When(new StartControlEvaluation(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, plan.PlanVersionId))
            .ExpectFailure(RequestErrorKind.Forbidden);
        var startedByManager = await fixture.AsAsync(fixture.LeadUserId,
            new StartControlEvaluation(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, plan.PlanVersionId));

        // Assert
        Assert.Equal(fixture.LeadMemberId, startedByManager.EvaluatorMemberId);
    }

    [Fact]
    public async Task ShouldNotDisclosePlanGivenDifferentTenantOrControl()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.GetOrDefineEvaluationPlanAsync();

        // Act
        // Assert
        await fixture.Scenario(fixture.OutsiderUserId)
            .When(new GetControlEvaluationPlanVersion(Uuid.CreateVersion4(), fixture.ProgramId,
                fixture.ControlId, plan.PlanVersionId))
            .ExpectFailure(RequestErrorKind.NotFound);
        await fixture.Scenario(fixture.OutsiderUserId)
            .When(new ListControlEvaluationPlanVersions(fixture.TenantId, fixture.ProgramId,
                Uuid.CreateVersion4()))
            .ExpectFailure(RequestErrorKind.NotFound);
    }
}
