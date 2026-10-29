using System.Text.Json;
using System.Text.Json.Nodes;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
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
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Boundaries.Tests;

public sealed class BoundaryActorSnapshotTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid BoundaryId = Uuid.CreateVersion4();
    static readonly Uuid DecisionId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();

    [Fact]
    public void ShouldRoundTripReviewActorAndReadLegacyEventGivenMissingSnapshot()
    {
        // Arrange
        var legacyEventJson = $$"""
            {"tenant_id":"{{TenantId}}","boundary_id":"{{BoundaryId}}","draft_version_id":"{{Uuid.CreateVersion4()}}","revision":1,"decision_id":"{{DecisionId}}","outcome":"accept","actor_member_id":"{{MemberId}}","actor_display":"Reviewer at decision time","rationale":"Reviewed","decided_at":"2026-09-27T12:00:00+00:00"}
            """;
        var legacyEvent = Assert.IsType<BoundaryReviewedContent>(JsonSerializer.Deserialize(
            legacyEventJson, ComplianceCoreJsonContext.Default.BoundaryReviewedContent));
        var capturedActor = ActorReference.ForMember(MemberId, "Reviewer at decision time");
        var currentEvent = legacyEvent with { StoredActor = capturedActor };

        // Act
        var eventJson = JsonNode.Parse(JsonSerializer.Serialize(currentEvent,
            ComplianceCoreJsonContext.Default.BoundaryReviewedContent))!;

        // Assert
        Assert.Null(legacyEvent.StoredActor);
        Assert.Equal(capturedActor, legacyEvent.Actor);
        Assert.Equal("member", (string?)eventJson["actor"]?["kind"]);
        Assert.Equal(MemberId.ToString(), (string?)eventJson["actor"]?["id"]);
        Assert.Equal("Reviewer at decision time", (string?)eventJson["actor"]?["display"]);
    }

    [Fact]
    public void ShouldRoundTripDiscardActorAndReadLegacyEventGivenMissingSnapshot()
    {
        // Arrange
        var legacyEventJson = $$"""
            {"tenant_id":"{{TenantId}}","boundary_id":"{{BoundaryId}}","draft_version_id":"{{Uuid.CreateVersion4()}}","revision":1,"actor_member_id":"{{MemberId}}","actor_display":"Discarder at discard time","rationale":"Withdrawn","discarded_at":"2026-09-27T12:00:00+00:00"}
            """;
        var legacyEvent = Assert.IsType<BoundaryDraftDiscardedContent>(JsonSerializer.Deserialize(
            legacyEventJson, ComplianceCoreJsonContext.Default.BoundaryDraftDiscardedContent));
        var capturedActor = ActorReference.ForMember(MemberId, "Discarder at discard time");
        var currentEvent = legacyEvent with { StoredActor = capturedActor };

        // Act
        var eventJson = JsonNode.Parse(JsonSerializer.Serialize(currentEvent,
            ComplianceCoreJsonContext.Default.BoundaryDraftDiscardedContent))!;

        // Assert
        Assert.Null(legacyEvent.StoredActor);
        Assert.Equal(capturedActor, legacyEvent.Actor);
        Assert.Equal("member", (string?)eventJson["actor"]?["kind"]);
        Assert.Equal(MemberId.ToString(), (string?)eventJson["actor"]?["id"]);
        Assert.Equal("Discarder at discard time", (string?)eventJson["actor"]?["display"]);
    }

    [Fact]
    public void ShouldSerializeDecisionActorAndReadLegacyRowsGivenMissingSnapshot()
    {
        // Arrange
        var legacyDecisionJson = $$"""
            {"tenant_id":"{{TenantId}}","boundary_id":"{{BoundaryId}}","decision_id":"{{DecisionId}}","version_id":"{{Uuid.CreateVersion4()}}","revision":1,"outcome":"accept","actor_member_id":"{{MemberId}}","actor_display":"Reviewer at decision time","rationale":"Reviewed","decided_at":"2026-09-27T12:00:00+00:00","supersedes_decision_id":null,"relies_on_decision_id":null,"impact_digest":null}
            """;
        var legacyDecision = Assert.IsType<BoundaryDecisionView>(JsonSerializer.Deserialize(
            legacyDecisionJson,
            ComplianceCoreJsonContext.Default.BoundaryDecisionView));
        var currentDecision = legacyDecision with
        {
            Actor = ActorReference.ForMember(MemberId, "Reviewer at decision time"),
            SeparationOfDutiesWaiverId = Uuid.CreateVersion4(),
        };

        // Act
        var legacyJson = JsonNode.Parse(JsonSerializer.Serialize(legacyDecision,
            ComplianceCoreJsonContext.Default.BoundaryDecisionView))!;
        var currentJson = JsonNode.Parse(JsonSerializer.Serialize(currentDecision,
            ComplianceCoreJsonContext.Default.BoundaryDecisionView))!;

        // Assert
        AssertActor(legacyJson["actor"]);
        AssertActor(currentJson["actor"]);
        Assert.False((bool)legacyJson["separation_of_duties_waived"]!);
        Assert.True((bool)currentJson["separation_of_duties_waived"]!);
    }

    static void AssertActor(JsonNode? actor)
    {
        Assert.Equal("member", (string?)actor?["kind"]);
        Assert.Equal(MemberId.ToString(), (string?)actor?["id"]);
        Assert.Equal("Reviewer at decision time", (string?)actor?["display"]);
    }
}
