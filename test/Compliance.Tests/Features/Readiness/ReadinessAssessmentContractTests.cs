using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Readiness.Models;
using System.Text.Json;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Tests.Testing;
using Microsoft.Extensions.DependencyInjection;
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

    static AccessReviewScopeDecisionView ScopeDecision(Uuid tenantId, Uuid applicationId,
        Uuid systemInstanceId, ActorReference actor) => new(tenantId, applicationId,
        systemInstanceId, 2, Uuid.CreateVersion4(), 3, "included", "Quarterly review",
        AsOf.AddDays(-30), AsOf, actor, AsOf, Uuid.CreateVersion4());
}
