using Bdgrz.Compliance.Features.Proofs.Models;
using System.Text.Json;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Tests.Testing;
using Microsoft.Extensions.DependencyInjection;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Readiness.Services;
namespace Bdgrz.Compliance.Tests.Features.Readiness.Tests;

public sealed class ReadinessAssessmentContractTests
{
    static readonly DateTimeOffset AsOf = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldSerializeAssessmentScopeSnapshotUsingSnakeCaseGivenDetailResponse()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var assessmentId = Uuid.CreateVersion4();
        var systemInstanceId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Avery Reviewer");
        var decision = ScopeDecision(tenantId, programId, systemInstanceId, actor);
        var view = new ReadinessAssessmentView(tenantId, programId, assessmentId, 1,
            ReadinessRules.Version, AsOf, null, "fingerprint", [], [], [], 0, 0, actor,
            AsOf, null)
        {
            AccessReviewScopes =
            [
                new ReadinessAccessReviewScopeView(systemInstanceId, "included", true, decision),
            ],
        };

        // Act
        var json = JsonSerializer.Serialize(view,
            ComplianceCoreJsonContext.Default.ReadinessAssessmentView);
        using var document = JsonDocument.Parse(json);

        // Assert
        var row = Assert.Single(document.RootElement.GetProperty("access_review_scopes")
            .EnumerateArray());
        Assert.Equal(systemInstanceId.ToString(),
            row.GetProperty("system_instance_id").GetString());
        Assert.Equal("included", row.GetProperty("status").GetString());
        Assert.True(row.GetProperty("review_overdue").GetBoolean());
        var serializedDecision = row.GetProperty("effective_decision");
        Assert.Equal(decision.DecisionId.ToString(),
            serializedDecision.GetProperty("decision_id").GetString());
        Assert.Equal(actor.Display, serializedDecision.GetProperty("approved_by")
            .GetProperty("display").GetString());
        Assert.Equal(decision.SeparationOfDutiesWaiverId.ToString(),
            serializedDecision.GetProperty("separation_of_duties_waiver_id").GetString());
    }

    [Fact]
    public async Task ShouldRetainCompleteScopeManifestGivenNativeFactRoundTripAndReplay()
    {
        // Arrange
        await using var provider = ReadinessTestServices.Build();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var assessmentId = Uuid.CreateVersion4();
        var runnerId = Uuid.CreateVersion4();
        var snapshot = new ReadinessAccessReviewScopeView(Uuid.CreateVersion4(), "excluded", false, null);
        var evaluation = new ReadinessEvaluation([], [], [], new string('a', 64))
        {
            AccessReviewScopes = [snapshot],
        };
        await ReadinessSeed.AdoptAsync(reader, executor, content, tenantId, programId, assessmentId, evaluation,
            AsOf, runnerId, AsOf);
        var owner = await reader.HydrateAsync(new ReadinessAssessment(tenantId, programId, assessmentId));
        var recorded = Assert.IsType<ReadinessAssessmentRecorded>(owner.Recorded);

        // Act
        var json = JsonSerializer.Serialize(recorded, ComplianceCoreJsonContext.Default.ReadinessAssessmentRecorded);
        var restored = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.ReadinessAssessmentRecorded)!;
        var replayed = new ReadinessAssessment(tenantId, programId, assessmentId);
        new AggregateScenario<ReadinessAssessment>(replayed).Given(DomainEventSeed.Attach(restored,
            ReadinessAssessment.IdentityOf(tenantId, programId, assessmentId), 1));
        var scopes = await content.ReadJsonAsync<IReadOnlyList<ReadinessAccessReviewScopeView>>(tenantId.ToString(),
            restored.AccessReviewScopesManifest);

        // Assert
        Assert.True(replayed.IsRecorded);
        Assert.Equal(recorded.Operation, replayed.Recorded!.Operation);
        Assert.Equal(recorded.Intent, restored.Intent);
        Assert.Equal(recorded.InputFingerprint, restored.InputFingerprint);
        Assert.Equal(snapshot, Assert.Single(scopes));
        Assert.Equal(owner.Stream, restored.Operation.Owner);
    }

    [Fact]
    public void ShouldDeriveStableIdentityGivenTenantProgramAndAssessment()
    {
        // Arrange
        var (tenantId, programId, assessmentId) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        var same = ReadinessAssessment.IdentityOf(tenantId, programId, assessmentId);
        var again = new ReadinessAssessment(tenantId, programId, assessmentId).Id;
        var otherAssessment = ReadinessAssessment.IdentityOf(tenantId, programId, Uuid.CreateVersion4());
        var otherProgram = ReadinessAssessment.IdentityOf(tenantId, Uuid.CreateVersion4(), assessmentId);

        // Assert
        Assert.Equal(same, again);
        Assert.NotEqual(same, otherAssessment);
        Assert.NotEqual(same, otherProgram);
    }

    [Fact]
    public void ShouldAppendOneRevisionOneFactGivenFirstComputation()
    {
        // Arrange
        var (tenantId, programId, assessmentId) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var assessment = new ReadinessAssessment(tenantId, programId, assessmentId);
        var scenario = new AggregateScenario<ReadinessAssessment>(assessment);
        var operation = Operation(assessment, Uuid.CreateVersion4());

        // Act
        var result = Record(assessment, operation);

        // Assert
        Assert.True(result.IsSuccess);
        var recorded = Assert.IsType<ReadinessAssessmentRecorded>(Assert.Single(scenario.PendingEvents));
        Assert.Equal((tenantId, programId, assessmentId, 1L), (recorded.TenantId, recorded.ProgramId,
            recorded.AssessmentId, recorded.Revision));
        Assert.True(assessment.IsRecorded);
        Assert.Equal(1, assessment.Revision);
    }

    [Fact]
    public void ShouldRefuseSecondComputationGivenAssessmentAlreadyRecorded()
    {
        // Arrange
        var assessment = new ReadinessAssessment(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        _ = Record(assessment, Operation(assessment, Uuid.CreateVersion4()));
        var scenario = new AggregateScenario<ReadinessAssessment>(assessment);
        var pending = scenario.PendingEvents.Count;

        // Act
        var prepared = assessment.PrepareRecord();
        var second = Record(assessment, Operation(assessment, Uuid.CreateVersion4()));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, prepared.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, second.Error?.Kind);
        Assert.Equal(pending, scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldAppendNothingGivenOperationOwnedByAnotherAssessment()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var assessment = new ReadinessAssessment(tenantId, programId, Uuid.CreateVersion4());
        var other = new ReadinessAssessment(tenantId, programId, Uuid.CreateVersion4());
        var scenario = new AggregateScenario<ReadinessAssessment>(assessment);

        // Act
        var result = Record(assessment, Operation(other, Uuid.CreateVersion4()));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.False(assessment.IsRecorded);
    }

    [Fact]
    public void ShouldRestoreRecordedAssessmentAndAcceptExactRetryGivenValidHistoryReplay()
    {
        // Arrange
        var (tenantId, programId, assessmentId) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var logicalId = Uuid.CreateVersion4();
        var source = new ReadinessAssessment(tenantId, programId, assessmentId);
        var operation = Operation(source, logicalId);
        _ = Record(source, operation);
        var history = NativeFacts.Raised(source);
        var replayed = new ReadinessAssessment(tenantId, programId, assessmentId, logicalId);
        new AggregateScenario<ReadinessAssessment>(replayed).Given(history);

        // Act
        var exact = replayed.Acceptance.Decide(AcceptedOperationBinding.Of(operation));
        var changed = replayed.Acceptance.Decide(AcceptedOperationBinding.Of(
            operation with { AuthoredIntentSha256 = new string('c', 64) }));
        var again = Record(replayed, Operation(replayed, Uuid.CreateVersion4()));

        // Assert
        Assert.True(replayed.IsRecorded);
        Assert.Equal(1, replayed.Revision);
        Assert.Equal(NativeAcceptanceDecision.Replayed, exact);
        Assert.Equal(NativeAcceptanceDecision.Conflict, changed);
        Assert.Equal(RequestErrorKind.Conflict, again.Error?.Kind);
    }

    static OperationProvenance Operation(ReadinessAssessment owner, Uuid logicalId) => ReadinessContent.Provenance(
        logicalId, "ComputeReadinessAssessment", owner.Stream, ActorReference.ForSystemProcess("system", "system"),
        null, new string('a', 64), AsOf);

    static ImmutableContentRef Content(string schema) => new(Uuid.CreateVersion4(), new string('b', 64), 2,
        "application/json", schema);

    static Result Record(ReadinessAssessment assessment, OperationProvenance operation) => assessment.Record(
        operation, ReadinessRules.Version, AsOf, null, new string('a', 64), Content("inputs"), Content("findings"),
        Content("gaps"), Content("scopes"), Uuid.CreateVersion4(),
        ActorReference.ForMember(Uuid.CreateVersion4(), "Runner"), AsOf, Content("intent"));

    static AccessReviewScopeDecisionView ScopeDecision(Uuid tenantId, Uuid applicationId,
        Uuid systemInstanceId, ActorReference actor) => new(tenantId, applicationId,
        systemInstanceId, 2, Uuid.CreateVersion4(), 3, "included", "Quarterly review",
        AsOf.AddDays(-30), AsOf, actor, AsOf, Uuid.CreateVersion4());
}
