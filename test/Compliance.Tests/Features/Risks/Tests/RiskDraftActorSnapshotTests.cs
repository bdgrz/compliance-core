using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using System.Text.Json.Nodes;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Risks.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

public sealed class RiskDraftActorSnapshotTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly DateTimeOffset At = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
    static readonly string ContentJson = """{"title":"Provider outage","scenario":"Provider unavailable","potential_effect":"Requests cannot be processed","source_note":"Management note"}""";

    [Fact]
    public async Task ShouldSnapshotEachActorDisplayGivenDisplayChangesBetweenEvents()
    {
        // Arrange
        var riskId = RiskDraft.IdFor(TenantId, ProgramId, "R-01");
        await using var native = new RiskDraftSourceFixture();
        var risk = new RiskDraftRules(TenantId, riskId);
        Assert.True(risk.Create(ProgramId, Uuid.CreateVersion4(), "R-01", Content(),
            MemberId, "First display", At).IsSuccess);
        Assert.Null(risk.Revise(ProgramId, 1, Content() with { Scenario = "Extended outage" },
            MemberId, "Second display", At.AddMinutes(1)));

        // Act
        await RiskDraftSeeds.WriteAsync(native.Writer, native.Content, risk);
        var events = await native.FactsAsync(TenantId, riskId);

        // Assert
        Assert.Equal(ActorReference.ForMember(MemberId, "First display"),
            Assert.IsType<RiskDraftCreated>(events[0]).StoredActor);
        Assert.Equal(ActorReference.ForMember(MemberId, "Second display"),
            Assert.IsType<RiskDraftRevised>(events[1]).StoredActor);
    }

    [Fact]
    public async Task ShouldReplayLegacyEventsWithMemberActorGivenEventsWithoutActor()
    {
        // Arrange
        var riskId = RiskDraft.IdFor(TenantId, ProgramId, "R-01");
        var created = Deserialize<RiskDraftCreatedContent>($$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","risk_id":"{{riskId}}","create_request_id":"{{Uuid.CreateVersion4()}}","identifier":"R-01","content":{{ContentJson}},"actor_member_id":"{{MemberId}}","actor_display":"First display","changed_at":"2026-09-23T12:00:00+00:00"}
            """);
        var revised = Deserialize<RiskDraftRevisedContent>($$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","risk_id":"{{riskId}}","revision":2,"content":{{ContentJson}},"actor_member_id":"{{MemberId}}","actor_display":"Second display","changed_at":"2026-09-23T12:01:00+00:00"}
            """);

        // Act
        await using var native = new RiskDraftSourceFixture();
        var rules = new RiskDraftRules(TenantId, riskId);
        rules.Apply(created);
        rules.Apply(revised);
        await RiskDraftSeeds.WriteAsync(native.Writer, native.Content, rules);
        var replayed = await native.Reader.HydrateAsync(new RiskDraft(TenantId, riskId));

        // Assert
        Assert.True(replayed.IsCreated);
        Assert.Equal(2, replayed.Revision);
        Assert.Null(created.StoredActor);
        Assert.Null(revised.StoredActor);
        Assert.Equal(ActorReference.ForMember(MemberId, "First display"), created.Actor);
        Assert.Equal(ActorReference.ForMember(MemberId, "Second display"), revised.Actor);
    }

    [Fact]
    public void ShouldSerializeMemberLastChangedByGivenPrePrDraftViewJson()
    {
        // Arrange
        var json = $$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","risk_id":"{{Uuid.CreateVersion4()}}","identifier":"R-01","revision":2,"status":"draft","owner_resolution":"unresolved","content":{{ContentJson}},"last_changed_by_member_id":"{{MemberId}}","last_changed_by_display":"Second display","last_changed_at":"2026-09-23T12:01:00+00:00"}
            """;

        // Act
        var output = JsonNode.Parse(JsonSerializer.Serialize(Deserialize<RiskDraftView>(json),
            ComplianceCoreJsonContext.Default.RiskDraftView))!;

        // Assert
        Assert.Equal("member", (string?)output["last_changed_by"]?["kind"]);
        Assert.Equal(MemberId.ToString(), (string?)output["last_changed_by"]?["id"]);
        Assert.Equal("Second display", (string?)output["last_changed_by"]?["display"]);
    }

    [Fact]
    public void ShouldSerializeMemberActorGivenPrePrRevisionViewJson()
    {
        // Arrange
        var json = $$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","risk_id":"{{Uuid.CreateVersion4()}}","identifier":"R-01","revision":1,"content":{{ContentJson}},"changed_by_member_id":"{{MemberId}}","changed_by_display":"First display","changed_at":"2026-09-23T12:00:00+00:00"}
            """;

        // Act
        var output = JsonNode.Parse(JsonSerializer.Serialize(
            Deserialize<RiskDraftRevisionView>(json),
            ComplianceCoreJsonContext.Default.RiskDraftRevisionView))!;

        // Assert
        Assert.Equal("member", (string?)output["actor"]?["kind"]);
        Assert.Equal(MemberId.ToString(), (string?)output["actor"]?["id"]);
        Assert.Equal("First display", (string?)output["actor"]?["display"]);
    }

    static T Deserialize<T>(string json) where T : class =>
        Assert.IsType<T>(JsonSerializer.Deserialize(json,
            ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))!));

    static RiskDraftContent Content() => new("Provider outage", "Provider unavailable",
        "Requests cannot be processed", "Management note");
}
