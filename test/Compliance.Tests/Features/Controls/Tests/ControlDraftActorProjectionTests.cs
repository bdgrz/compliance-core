using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Controls.History;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
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
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Commands;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.Controls.Tests;

public sealed class ControlDraftActorProjectionTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly Uuid ControlId = ControlDraft.IdFor(TenantId, ProgramId, "AC-01");
    static readonly ActorReference Process = ActorReference.ForSystemProcess("reactor:control-import", "control-import");

    [Fact]
    public async Task ShouldProjectMemberActorGivenLegacyCreatedEventInDraftDirectory()
    {
        // Arrange
        await using var source = new ControlProjectionScenario();
        var directory = new FitzControlDraftDirectoryV2(source.Kv, source.Controls);
        await PublishCreatedAsync(source);
        await source.ProjectAsync(directory, directory.ApplyAsync, FitzControlDraftDirectoryV2.ProjectorName, TenantId);

        // Act
        var view = await directory.GetAsync(TenantId, ControlId);

        // Assert
        Assert.Equal(ActorReference.ForMember(MemberId, "Legacy display"), view?.LastChangedBy);
    }

    [Fact]
    public async Task ShouldProjectSnapshottedActorGivenSnapshottedCreatedEventInDraftDirectory()
    {
        // Arrange
        await using var source = new ControlProjectionScenario();
        var directory = new FitzControlDraftDirectoryV2(source.Kv, source.Controls);
        await PublishCreatedAsync(source, Process);
        await source.ProjectAsync(directory, directory.ApplyAsync, FitzControlDraftDirectoryV2.ProjectorName, TenantId);

        // Act
        var view = await directory.GetAsync(TenantId, ControlId);

        // Assert
        Assert.Equal(Process, view?.LastChangedBy);
    }

    [Fact]
    public async Task ShouldProjectMemberActorGivenLegacyCreatedEventInHistoryDirectory()
    {
        // Arrange
        await using var source = new ControlProjectionScenario();
        var directory = new FitzControlDraftHistoryDirectoryV1(source.Kv, source.Controls);
        await PublishCreatedAsync(source);
        await source.ProjectAsync(directory, directory.ApplyAsync, FitzControlDraftHistoryDirectoryV1.ProjectorName, TenantId);

        // Act
        var view = await directory.GetRevisionAsync(TenantId, ControlId, 1);

        // Assert
        Assert.Equal(ActorReference.ForMember(MemberId, "Legacy display"), view?.Actor);
    }

    [Fact]
    public async Task ShouldProjectSnapshottedActorGivenSnapshottedCreatedEventInHistoryDirectory()
    {
        // Arrange
        await using var source = new ControlProjectionScenario();
        var directory = new FitzControlDraftHistoryDirectoryV1(source.Kv, source.Controls);
        await PublishCreatedAsync(source, Process);
        await source.ProjectAsync(directory, directory.ApplyAsync, FitzControlDraftHistoryDirectoryV1.ProjectorName, TenantId);

        // Act
        var view = await directory.GetRevisionAsync(TenantId, ControlId, 1);

        // Assert
        Assert.Equal(Process, view?.Actor);
    }

    static async Task PublishCreatedAsync(ControlProjectionScenario source, ActorReference? actor = null)
    {
        var created = LegacyCreated();
        if (actor is not null)
            created = created with { StoredActor = actor };
        var rules = new ControlRules(TenantId, ControlId);
        rules.Apply(created);
        await source.PublishExplicitAsync(rules, created, nameof(CreateControlDraft), ControlId, MemberId, created.ChangedAt);
    }

    static ControlDraftCreatedContent LegacyCreated() => Assert.IsType<ControlDraftCreatedContent>(
        JsonSerializer.Deserialize($$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","control_id":"{{ControlId}}","create_request_id":"{{Uuid.CreateVersion4()}}","identifier":"AC-01","content":{"title":"Access review","objective":"Review access","description":"Management reviews access","implementation_narrative":"The owner reviews access quarterly.","expected_evidence_descriptions":["Dated review record"]},"actor_member_id":"{{MemberId}}","actor_display":"Legacy display","changed_at":"2026-09-23T12:00:00+00:00"}
            """, ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(ControlDraftCreatedContent))!));
}
