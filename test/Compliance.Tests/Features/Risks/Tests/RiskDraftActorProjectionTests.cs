using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Risks.History;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.Risks.Rules;
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
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

public sealed class RiskDraftActorProjectionTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly Uuid RiskId = RiskDraft.IdFor(TenantId, ProgramId, "R-01");
    static readonly ActorReference Process =
        ActorReference.ForSystemProcess("reactor:risk-import", "risk-import");

    [Fact]
    public async Task ShouldProjectMemberActorGivenLegacyCreatedEventInDraftDirectory()
    {
        // Arrange
        await using var native = new RiskDraftSourceFixture();
        var directory = new FitzRiskDraftDirectory(new InMemoryKvClient(), native.Content, native.Drafts);
        await ProjectAsync(directory, directory.ApplyAsync, FitzRiskDraftDirectory.ProjectorName, await NativeCreatedAsync(native, LegacyCreated()));

        // Act
        var view = await directory.GetAsync(TenantId, RiskId);

        // Assert
        Assert.Equal(ActorReference.ForMember(MemberId, "Legacy display"), view?.LastChangedBy);
    }

    [Fact]
    public async Task ShouldProjectSnapshottedActorGivenSnapshottedCreatedEventInDraftDirectory()
    {
        // Arrange
        await using var native = new RiskDraftSourceFixture();
        var directory = new FitzRiskDraftDirectory(new InMemoryKvClient(), native.Content, native.Drafts);
        await ProjectAsync(directory, directory.ApplyAsync, FitzRiskDraftDirectory.ProjectorName,
            await NativeCreatedAsync(native, LegacyCreated() with { StoredActor = Process }));

        // Act
        var view = await directory.GetAsync(TenantId, RiskId);

        // Assert
        Assert.Equal(Process, view?.LastChangedBy);
    }

    [Fact]
    public async Task ShouldProjectMemberActorGivenLegacyCreatedEventInHistoryDirectory()
    {
        // Arrange
        await using var native = new RiskDraftSourceFixture();
        var directory = new FitzRiskDraftHistoryDirectoryV1(new InMemoryKvClient(), native.Content, native.Drafts);
        await ProjectAsync(directory, directory.ApplyAsync, FitzRiskDraftHistoryDirectoryV1.ProjectorName, await NativeCreatedAsync(native, LegacyCreated()));

        // Act
        var view = await directory.GetRevisionAsync(TenantId, RiskId, 1);

        // Assert
        Assert.Equal(ActorReference.ForMember(MemberId, "Legacy display"), view?.Actor);
    }

    [Fact]
    public async Task ShouldProjectSnapshottedActorGivenSnapshottedCreatedEventInHistoryDirectory()
    {
        // Arrange
        await using var native = new RiskDraftSourceFixture();
        var directory = new FitzRiskDraftHistoryDirectoryV1(new InMemoryKvClient(), native.Content, native.Drafts);
        await ProjectAsync(directory, directory.ApplyAsync, FitzRiskDraftHistoryDirectoryV1.ProjectorName,
            await NativeCreatedAsync(native, LegacyCreated() with { StoredActor = Process }));

        // Act
        var view = await directory.GetRevisionAsync(TenantId, RiskId, 1);

        // Assert
        Assert.Equal(Process, view?.Actor);
    }

    static async Task<DomainEvent> NativeCreatedAsync(RiskDraftSourceFixture native, RiskDraftCreatedContent created)
    {
        var rules = new RiskDraftRules(created.TenantId, created.RiskId);
        rules.Apply(created);
        await RiskDraftSeeds.WriteAsync(native.Writer, native.Content, rules);
        return Assert.Single(await native.FactsAsync(created.TenantId, created.RiskId));
    }

    static RiskDraftCreatedContent LegacyCreated() => Assert.IsType<RiskDraftCreatedContent>(
        JsonSerializer.Deserialize($$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","risk_id":"{{RiskId}}","create_request_id":"{{Uuid.CreateVersion4()}}","identifier":"R-01","content":{"title":"Provider outage","scenario":"Provider unavailable","potential_effect":"Requests cannot be processed","source_note":"Management note"},"actor_member_id":"{{MemberId}}","actor_display":"Legacy display","changed_at":"2026-09-23T12:00:00+00:00"}
            """, ComplianceCoreJsonContext.Default.RiskDraftCreatedContent));

    static async Task ProjectAsync(IProjectionStore store,
        Func<DomainEvent, CancellationToken, ValueTask> apply, string name,
        DomainEvent domainEvent)
    {
        await using var batch = await store.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity(name, EventStreamPattern.ForPattern(TenantId.ToString(),
                "risks")), ProjectionCheckpoint.Start));
        await apply(domainEvent, CancellationToken.None);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
