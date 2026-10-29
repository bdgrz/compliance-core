using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Commitments.History;
using Bdgrz.Compliance.Features.Commitments.Readers;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
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
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Commitments.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Commands;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Commitments.Tests;

public sealed class CommitmentDraftActorProjectionTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly Uuid DraftId = CommitmentDraft.IdFor(TenantId, ProgramId, "service_commitment", "SC-01");
    static readonly ActorReference Process =
        ActorReference.ForSystemProcess("reactor:commitment-import", "commitment-import");

    [Fact]
    public async Task ShouldProjectMemberActorGivenLegacyCreatedEventInDraftDirectory()
    {
        // Arrange
        await using var native = new CommitmentProjectionScenario();
        var directory = new FitzCommitmentDraftDirectory(native.Kv, native.Commitments);
        await ProjectAsync(native, directory, directory.ApplyAsync, FitzCommitmentDraftDirectory.ProjectorName, LegacyCreated());

        // Act
        var view = await directory.GetAsync(TenantId, DraftId);

        // Assert
        Assert.Equal(ActorReference.ForMember(MemberId, "Legacy display"), view?.LastChangedBy);
    }

    [Fact]
    public async Task ShouldProjectSnapshottedActorGivenSnapshottedCreatedEventInDraftDirectory()
    {
        // Arrange
        await using var native = new CommitmentProjectionScenario();
        var directory = new FitzCommitmentDraftDirectory(native.Kv, native.Commitments);
        await ProjectAsync(native, directory, directory.ApplyAsync, FitzCommitmentDraftDirectory.ProjectorName,
            LegacyCreated() with { StoredActor = Process });

        // Act
        var view = await directory.GetAsync(TenantId, DraftId);

        // Assert
        Assert.Equal(Process, view?.LastChangedBy);
    }

    [Fact]
    public async Task ShouldProjectMemberActorGivenLegacyCreatedEventInHistoryDirectory()
    {
        // Arrange
        await using var native = new CommitmentProjectionScenario();
        var directory = new FitzCommitmentDraftHistoryDirectoryV1(native.Kv, native.Commitments);
        await ProjectAsync(native, directory, directory.ApplyAsync, FitzCommitmentDraftHistoryDirectoryV1.ProjectorName, LegacyCreated());

        // Act
        var view = await directory.GetRevisionAsync(TenantId, DraftId, 1);

        // Assert
        Assert.Equal(ActorReference.ForMember(MemberId, "Legacy display"), view?.Actor);
    }

    [Fact]
    public async Task ShouldProjectSnapshottedActorGivenSnapshottedCreatedEventInHistoryDirectory()
    {
        // Arrange
        await using var native = new CommitmentProjectionScenario();
        var directory = new FitzCommitmentDraftHistoryDirectoryV1(native.Kv, native.Commitments);
        await ProjectAsync(native, directory, directory.ApplyAsync, FitzCommitmentDraftHistoryDirectoryV1.ProjectorName,
            LegacyCreated() with { StoredActor = Process });

        // Act
        var view = await directory.GetRevisionAsync(TenantId, DraftId, 1);

        // Assert
        Assert.Equal(Process, view?.Actor);
    }

    static CommitmentDraftCreatedContent LegacyCreated() => Assert.IsType<CommitmentDraftCreatedContent>(
        JsonSerializer.Deserialize($$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","draft_id":"{{DraftId}}","create_request_id":"{{Uuid.CreateVersion4()}}","service_id":"{{Uuid.CreateVersion4()}}","kind":"service_commitment","identifier":"SC-01","statement":"First statement","context":"Draft context","source_reference":"Source A","actor_member_id":"{{MemberId}}","actor_display":"Legacy display","changed_at":"2026-09-23T12:00:00+00:00"}
            """, ComplianceCoreJsonContext.Default.CommitmentDraftCreatedContent));

    static async Task ProjectAsync(CommitmentProjectionScenario native, IProjectionStore store,
        Func<DomainEvent, CancellationToken, ValueTask> apply, string name, CommitmentDraftCreatedContent change)
    {
        var rules = new CommitmentRules(TenantId, DraftId);
        rules.Apply(change);
        var fact = await native.PublishExplicitAsync(rules, change, nameof(CreateCommitmentDraft), DraftId,
            change.ActorMemberId, change.ActorDisplay, change.ChangedAt);
        await native.ProjectAsync(store, apply, name, TenantId, [fact]);
    }
}
