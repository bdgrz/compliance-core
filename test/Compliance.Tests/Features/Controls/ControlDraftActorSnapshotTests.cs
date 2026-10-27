using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Commands;
using Bdgrz.Compliance.Features.Controls.Events;
using Bdgrz.Compliance.Features.Controls.Models;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class ControlDraftActorSnapshotTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly DateTimeOffset At = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
    static readonly string ContentJson = """{"title":"Access review","objective":"Review access","description":"Management reviews access","implementation_narrative":"The owner reviews access quarterly.","expected_evidence_descriptions":["Dated review record"]}""";

    [Fact]
    public async Task ShouldSnapshotEachActorDisplayGivenDisplayChangesBetweenEvents()
    {
        // Arrange
        await using var source = new ControlProjectionScenario();
        var controlId = ControlDraft.IdFor(TenantId, ProgramId, "AC-01");
        var rules = new ControlRules(TenantId, controlId);
        Assert.True(rules.Create(ProgramId, Uuid.CreateVersion4(), "AC-01", Content(), MemberId,
            "First display", At).IsSuccess);
        var created = Assert.IsType<ControlDraftCreated>(Assert.Single(await source.PublishAsync(rules)));
        Assert.Null(rules.Revise(ProgramId, 1, Content() with { Title = "Updated review" }, MemberId,
            "Second display", At.AddMinutes(1)));
        var revised = Assert.IsType<ControlDraftRevised>(Assert.Single(await source.PublishAsync(rules)));

        // Act
        Assert.Null(rules.Discard(ProgramId, 2, "Withdraw draft", MemberId, "Third display", At.AddMinutes(2)));
        var discarded = Assert.IsType<ControlDraftDiscarded>(Assert.Single(await source.PublishAsync(rules)));

        // Assert
        Assert.Equal(ActorReference.ForMember(MemberId, "First display"), created.StoredActor);
        Assert.Equal(ActorReference.ForMember(MemberId, "Second display"), revised.StoredActor);
        Assert.Equal(TenantId, discarded.TenantId);
        Assert.Equal(ProgramId, discarded.ProgramId);
        Assert.Equal(controlId, discarded.ControlId);
        Assert.Equal(2, discarded.Revision);
        Assert.Equal(MemberId, discarded.ActorMemberId);
        Assert.Equal("Third display", discarded.ActorDisplay);
        Assert.Equal("Withdraw draft", discarded.Rationale);
        Assert.Equal(At.AddMinutes(2), discarded.DiscardedAt);
        Assert.Equal(ActorReference.ForMember(MemberId, "Third display"), discarded.StoredActor);
        Assert.Equal(nameof(DiscardControlDraft), discarded.Operation.CommandKind);
        var retained = await source.Reader.HydrateAsync(new ControlDraft(TenantId, controlId));
        Assert.Equal(rules.SourcePhysicalPosition, retained.CommittedStreamPosition);
        Assert.False(retained.IsVisible);
    }

    [Fact]
    public async Task ShouldReplayLegacyEventsWithMemberActorGivenEventsWithoutActor()
    {
        // Arrange
        await using var source = new ControlProjectionScenario();
        var controlId = ControlDraft.IdFor(TenantId, ProgramId, "AC-01");
        var created = Deserialize<ControlDraftCreatedContent>($$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","control_id":"{{controlId}}","create_request_id":"{{Uuid.CreateVersion4()}}","identifier":"AC-01","content":{{ContentJson}},"actor_member_id":"{{MemberId}}","actor_display":"First display","changed_at":"2026-09-23T12:00:00+00:00"}
            """);
        var revised = Deserialize<ControlDraftRevisedContent>($$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","control_id":"{{controlId}}","revision":2,"content":{{ContentJson}},"actor_member_id":"{{MemberId}}","actor_display":"Second display","changed_at":"2026-09-23T12:01:00+00:00"}
            """);
        var discarded = Deserialize<ControlDraftDiscardedContent>($$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","control_id":"{{controlId}}","revision":2,"actor_member_id":"{{MemberId}}","actor_display":"Third display","rationale":"Withdraw draft","discarded_at":"2026-09-23T12:02:00+00:00"}
            """);
        var rules = new ControlRules(TenantId, controlId);
        rules.Apply(created);
        var nativeCreated = Assert.IsType<ControlDraftCreated>(await source.PublishExplicitAsync(rules, created,
            nameof(CreateControlDraft), controlId, MemberId, At));
        rules.Apply(revised);
        var nativeRevised = Assert.IsType<ControlDraftRevised>(await source.PublishExplicitAsync(rules, revised,
            nameof(ReviseControlDraft), Uuid.CreateVersion4(), MemberId, At.AddMinutes(1)));
        rules.Apply(discarded);
        var nativeDiscarded = Assert.IsType<ControlDraftDiscarded>(await source.PublishExplicitAsync(rules, discarded,
            nameof(DiscardControlDraft), Uuid.CreateVersion4(), MemberId, At.AddMinutes(2)));

        // Act
        var replayed = await source.Reader.HydrateAsync(new ControlDraft(TenantId, controlId));

        // Assert
        Assert.Equal(2, replayed.Revision);
        Assert.False(replayed.IsVisible);
        Assert.Null(created.StoredActor);
        Assert.Null(revised.StoredActor);
        Assert.Null(discarded.StoredActor);
        Assert.Null(nativeCreated.StoredActor);
        Assert.Null(nativeRevised.StoredActor);
        Assert.Null(nativeDiscarded.StoredActor);
        Assert.Equal(ActorReference.ForMember(MemberId, "First display"), created.Actor);
        Assert.Equal(ActorReference.ForMember(MemberId, "Second display"), revised.Actor);
        Assert.Equal(ActorReference.ForMember(MemberId, "Third display"), discarded.Actor);
        Assert.Equal(created.Actor, nativeCreated.Actor);
        Assert.Equal(revised.Actor, nativeRevised.Actor);
        Assert.Equal(discarded.Actor, nativeDiscarded.Actor);
    }

    [Fact]
    public void ShouldSerializeMemberLastChangedByGivenPrePrDraftViewJson()
    {
        // Arrange
        var controlId = Uuid.CreateVersion4();
        var json = $$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","control_id":"{{controlId}}","identifier":"AC-01","revision":2,"status":"draft","owner_resolution":"unresolved","applicability_resolution":"unresolved","content":{{ContentJson}},"last_changed_by_member_id":"{{MemberId}}","last_changed_by_display":"Second display","last_changed_at":"2026-09-23T12:01:00+00:00"}
            """;

        // Act
        var output = JsonNode.Parse(JsonSerializer.Serialize(
            Deserialize<ControlDraftView>(json),
            ComplianceCoreJsonContext.Default.ControlDraftView))!;

        // Assert
        Assert.Equal("member", (string?)output["last_changed_by"]?["kind"]);
        Assert.Equal(MemberId.ToString(), (string?)output["last_changed_by"]?["id"]);
        Assert.Equal("Second display", (string?)output["last_changed_by"]?["display"]);
    }

    [Fact]
    public void ShouldSerializeMemberActorGivenPrePrRevisionViewJson()
    {
        // Arrange
        var controlId = Uuid.CreateVersion4();
        var json = $$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","control_id":"{{controlId}}","identifier":"AC-01","revision":1,"content":{{ContentJson}},"changed_by_member_id":"{{MemberId}}","changed_by_display":"First display","changed_at":"2026-09-23T12:00:00+00:00"}
            """;

        // Act
        var output = JsonNode.Parse(JsonSerializer.Serialize(
            Deserialize<ControlDraftRevisionView>(json),
            ComplianceCoreJsonContext.Default.ControlDraftRevisionView))!;

        // Assert
        Assert.Equal("member", (string?)output["actor"]?["kind"]);
        Assert.Equal(MemberId.ToString(), (string?)output["actor"]?["id"]);
        Assert.Equal("First display", (string?)output["actor"]?["display"]);
    }

    static T Deserialize<T>(string json) where T : class =>
        Assert.IsType<T>(JsonSerializer.Deserialize(json,
            ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))!));

    static ControlDraftContent Content() => new("Access review", "Review access",
        "Management reviews access", "The owner reviews access quarterly.",
        ["Dated review record"]);
}
