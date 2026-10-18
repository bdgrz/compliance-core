using System.Text.Json;
using System.Text.Json.Nodes;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Readiness;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Readiness;

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
    public void ShouldReplayHistoricAssessmentWithoutAccessReviewScopesGivenV1EventPayload()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var assessmentId = Uuid.CreateVersion4();
        var runnerId = Uuid.CreateVersion4();
        var recorded = new ReadinessAssessmentRecorded(tenantId, programId, 1, assessmentId,
            ReadinessRules.Version, AsOf, null, "fingerprint", [], [], [], runnerId,
            ActorReference.ForMember(runnerId, "Avery Runner"), AsOf)
        {
            AccessReviewScopes =
            [
                new ReadinessAccessReviewScopeView(Uuid.CreateVersion4(), "excluded", false,
                    null),
            ],
        };
        var oldPayload = JsonSerializer.SerializeToNode(recorded,
            ComplianceCoreJsonContext.Default.ReadinessAssessmentRecorded)!.AsObject();
        oldPayload.Remove("access_review_scopes");

        // Act
        var replayedEvent = oldPayload.Deserialize(
            ComplianceCoreJsonContext.Default.ReadinessAssessmentRecorded)!;
        var ledger = new ReadinessLedger(tenantId, programId);
        new AggregateScenario<ReadinessLedger>(ledger).Given(
            DomainEventSeed.Attach(replayedEvent, programId, 1));
        var assessment = ledger.Read(assessmentId);

        // Assert
        Assert.NotNull(assessment);
        Assert.Empty(assessment.AccessReviewScopes);
    }

    static AccessReviewScopeDecisionView ScopeDecision(Uuid tenantId, Uuid applicationId,
        Uuid systemInstanceId, ActorReference actor) => new(tenantId, applicationId,
        systemInstanceId, 2, Uuid.CreateVersion4(), 3, "included", "Quarterly review",
        AsOf.AddDays(-30), AsOf, actor, AsOf, Uuid.CreateVersion4());
}
