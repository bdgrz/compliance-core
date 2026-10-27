using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
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
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
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
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Events;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Responsibilities.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.Controls.Tests;

public sealed class FitzControlDraftDirectoryV2Tests
{
    [Fact]
    public async Task ShouldUseIndependentV2CheckpointGivenCurrentControlDraftProjection()
    {
        // Arrange
        await using var source = new ControlProjectionScenario();
        var directory = new FitzControlDraftDirectoryV2(source.Kv, source.Controls);
        var tenantId = Uuid.CreateVersion4();
        var checkpoint = new ProjectionCheckpoint(new EventCursor("control-draft-v3"));
        await source.PublishAsync(Created(tenantId, Uuid.CreateVersion4(), "AC-V2",
            new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero)));

        // Act
        await source.ProjectAsync(directory, directory.ApplyAsync, FitzControlDraftDirectoryV2.ProjectorName,
            tenantId, checkpoint: checkpoint);

        // Assert
        Assert.Equal(checkpoint, await directory.LoadCheckpointAsync(tenantId));
        Assert.Equal(ProjectionCheckpoint.Start, await new LegacyCheckpointRepository(source.Kv).LoadCheckpointAsync(new CheckpointIdentity(
            "ControlDraftDirectoryV2", EventStreamPattern.ForPattern(tenantId.ToString(), ControlDraft.Area))));
    }

    [Fact]
    public async Task ShouldPageProgramDraftsWithoutDisclosingOtherProgramsOrTenantsGivenSharedDirectory()
    {
        // Arrange
        await using var source = new ControlProjectionScenario();
        var directory = new FitzControlDraftDirectoryV2(source.Kv, source.Controls);
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var otherProgramId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        var expected = Enumerable.Range(1, 201).Select(index => $"AC-{index:D3}").ToArray();
        var otherControlId = ControlDraft.IdFor(tenantId, otherProgramId, "OTHER");
        foreach (var identifier in expected)
            await source.PublishAsync(Created(tenantId, programId, identifier, now));
        await source.PublishAsync(Created(tenantId, otherProgramId, "OTHER", now));
        await source.ProjectAsync(directory, directory.ApplyAsync, FitzControlDraftDirectoryV2.ProjectorName, tenantId);

        // Act
        var first = await directory.ListProgramAsync(tenantId, programId, 200, null);
        var second = await directory.ListProgramAsync(tenantId, programId, 200, first.NextCursor);
        var otherProgram = await directory.ListProgramAsync(tenantId, otherProgramId, 200, null);
        var alienProgram = await directory.ListProgramAsync(otherTenantId, programId, 200, null);

        // Assert
        Assert.Equal(200, first.Items.Count);
        Assert.Single(second.Items);
        Assert.Equal(expected, first.Items.Concat(second.Items).Select(item => item.Identifier));
        Assert.Equal("OTHER", Assert.Single(otherProgram.Items).Identifier);
        Assert.Empty(alienProgram.Items);
        Assert.Null(await directory.GetAsync(otherTenantId, otherControlId));
        Assert.Null(await directory.GetRevisionAsync(otherTenantId, otherControlId, 1));
    }

    [Fact]
    public async Task ShouldPreserveRevisionHistoryAndAttributionGivenCommittedProjection()
    {
        // Arrange
        await using var source = new ControlProjectionScenario();
        var directory = new FitzControlDraftDirectoryV2(source.Kv, source.Controls);
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        var rules = Created(tenantId, programId, "AC-01", now);
        var created = Assert.IsType<ControlDraftCreated>(Assert.Single(await source.PublishAsync(rules)));
        var nextActorId = Uuid.CreateVersion4();
        Assert.Null(rules.Revise(programId, 1, Content() with { Title = "Revised" }, nextActorId,
            "Reviewer", now.AddMinutes(1)));
        await source.PublishAsync(rules);
        await source.ProjectAsync(directory, directory.ApplyAsync, FitzControlDraftDirectoryV2.ProjectorName, tenantId);

        // Act
        var current = await directory.GetAsync(tenantId, created.ControlId);
        var first = await directory.GetRevisionAsync(tenantId, created.ControlId, 1);
        var second = await directory.GetRevisionAsync(tenantId, created.ControlId, 2);

        // Assert
        Assert.Equal(2, current?.Revision);
        Assert.Equal("draft", current?.Status);
        Assert.Equal("unresolved", current?.OwnerResolution);
        Assert.Equal("unresolved", current?.ApplicabilityResolution);
        Assert.Equal("Revised", current?.Content.Title);
        Assert.Equal(nextActorId, current?.LastChangedByMemberId);
        Assert.Equal(created.ActorMemberId, first?.ChangedByMemberId);
        Assert.Equal("Access review", first?.Content.Title);
        Assert.Equal(nextActorId, second?.ChangedByMemberId);
        Assert.Equal("Revised", second?.Content.Title);
    }

    [Fact]
    public async Task ShouldRollBackDraftAndCheckpointGivenInvalidProjectionRevision()
    {
        // Arrange
        await using var source = new ControlProjectionScenario();
        var directory = new FitzControlDraftDirectoryV2(source.Kv, source.Controls);
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var identity = Checkpoint(tenantId);
        var now = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        var rules = Created(tenantId, programId, "AC-01", now);
        var created = Assert.IsType<ControlDraftCreated>(Assert.Single(await source.PublishAsync(rules)));
        var malformed = new ControlProjectionFacts();
        var invalid = await malformed.FreezeAsync(new ControlDraftRevisedContent(tenantId, programId,
            created.ControlId, 3, Content(), created.ActorMemberId, "Author", now.AddMinutes(1)));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(created);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await directory.ApplyAsync(invalid));
        }
        var missing = await directory.GetAsync(tenantId, created.ControlId);
        var missingRevision = await directory.GetRevisionAsync(tenantId, created.ControlId, 1);
        var checkpoint = await directory.LoadCheckpointAsync(identity);
        await source.ProjectAsync(directory, directory.ApplyAsync, FitzControlDraftDirectoryV2.ProjectorName,
            tenantId, [created]);

        // Assert
        Assert.Null(missing);
        Assert.Null(missingRevision);
        Assert.Equal(ProjectionCheckpoint.Start, checkpoint);
        Assert.Equal(1, (await directory.GetAsync(tenantId, created.ControlId))?.Revision);
    }

    [Fact]
    public async Task ShouldRemoveCurrentDraftGivenDiscardOfUnpublishedControl()
    {
        // Arrange
        await using var source = new ControlProjectionScenario();
        var directory = new FitzControlDraftDirectoryV2(source.Kv, source.Controls);
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
        var rules = Created(tenantId, programId, "AC-06", now);
        await source.PublishAsync(rules);
        Assert.Null(rules.Discard(programId, 1, "Not needed", Uuid.CreateVersion4(), "Author", now.AddMinutes(1)));
        await source.PublishAsync(rules);

        // Act
        await source.ProjectAsync(directory, directory.ApplyAsync, FitzControlDraftDirectoryV2.ProjectorName, tenantId);

        // Assert
        Assert.Null(await directory.GetAsync(tenantId, rules.Id));
        Assert.Empty((await directory.ListProgramAsync(tenantId, programId, 20, null)).Items);
    }

    [Fact]
    public async Task ShouldExposeSuccessorVersionIdGivenProjectedSuccessorProposal()
    {
        // Arrange
        await using var source = new ControlProjectionScenario();
        var directory = new FitzControlDraftDirectoryV2(source.Kv, source.Controls);
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
        var rules = Created(tenantId, programId, "AC-07", now);
        var owner = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var approver = Uuid.CreateVersion4();
        var review = Uuid.CreateVersion4();
        Assert.Null(rules.AssignResponsibility(new ResponsibilityScope("control", rules.Id, rules.DraftVersionId, 1),
            Uuid.CreateVersion4(), owner, ResponsibilityType.ControlOwner, Uuid.CreateVersion4(), "Manager", now,
            now.AddMinutes(-1), null, []));
        Assert.Null(rules.Review(programId, 1, review, "accept", "Reviewed", reviewer, "Reviewer", now.AddMinutes(1)));
        Assert.Null(rules.Approve(programId, 1, Uuid.CreateVersion4(), review, DateOnly.FromDateTime(now.UtcDateTime),
            "Approved", new HashSet<Uuid> { owner }, approver, "Approver", now.AddMinutes(2)));
        await source.PublishAsync(rules);
        var successorId = ControlVersionIds.Sequence(rules.Id, 2);
        Assert.Null(rules.ProposeSuccessor(programId, rules.ApprovedVersion!.VersionId,
            Content() with { Title = "Successor" }, Uuid.CreateVersion4(), "Author", now.AddDays(1)));
        await source.PublishAsync(rules);

        // Act
        await source.ProjectAsync(directory, directory.ApplyAsync, FitzControlDraftDirectoryV2.ProjectorName, tenantId);

        // Assert
        var draft = Assert.IsType<ControlDraftView>(await directory.GetAsync(tenantId, rules.Id));
        Assert.Equal(successorId, draft.DraftVersionId);
        Assert.Equal(2, draft.Revision);
        Assert.Equal("Successor", draft.Content.Title);
    }

    static CheckpointIdentity Checkpoint(Uuid tenantId) => new(FitzControlDraftDirectoryV2.ProjectorName,
        EventStreamPattern.ForPattern(tenantId.ToString(), ControlDraft.Area));

    static ControlRules Created(Uuid tenantId, Uuid programId, string identifier, DateTimeOffset now)
    {
        var rules = new ControlRules(tenantId, ControlDraft.IdFor(tenantId, programId, identifier));
        Assert.True(rules.Create(programId, Uuid.CreateVersion4(), identifier, Content(), Uuid.CreateVersion4(),
            "Author", now).IsSuccess);
        return rules;
    }

    sealed class LegacyCheckpointRepository(Cntryl.Fitz.IKvClient client)
        : FitzKvProjectionStore(client, "kv://bdgrz/control-draft-directory-v2/projection", "ControlDraftDirectoryV2");

    static ControlDraftContent Content() => new("Access review", "Review access",
        "Management reviews access", "The owner reviews the access list quarterly.", ["Dated review record"]);
}
