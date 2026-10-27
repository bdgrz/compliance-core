using Bdgrz.Compliance.Features.Boundaries.Aggregates;
using Bdgrz.Compliance.Features.Boundaries.Commands;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Boundaries;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationBoundaryReferenceDirectoryTests
{
    [Fact]
    public async Task ShouldPreserveApprovedHistoryGivenBoundaryReferenceTransitions()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        await using var native = new BoundaryProjectionScenario();
        var firstVersion = Uuid.CreateVersion5(boundaryId, "draft-1");
        var successorVersion = native.NewSuccessorVersion();
        var applicationEntry = Entry("application", applicationId);
        var instanceEntry = Entry("system_instance", instanceId);
        var now = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        var directory = new FitzApplicationBoundaryReferenceDirectory(native.Kv, native.Content);
        var identity = Identity(tenantId);
        var rules = new BoundaryRules(tenantId, boundaryId);
        var author = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        Assert.True(rules.Create(programId, firstVersion, Content(applicationEntry, instanceEntry), author, "Author", now).IsSuccess);
        await PublishProjectAsync(native, rules, directory, identity);
        Assert.Single((await directory.ListAsync(tenantId, "application", applicationId, 20, null)).Items);
        Assert.Single((await directory.ListAsync(tenantId, "system_instance", instanceId, 20, null)).Items);

        // Act
        Assert.Null(rules.Revise(firstVersion, 1, Content(instanceEntry), author, "Author", now.AddMinutes(1)));
        await PublishProjectAsync(native, rules, directory, identity);
        var removedDraft = await directory.ListAsync(tenantId, "application", applicationId, 20, null);
        Approve(rules, firstVersion, 2, reviewer, new DateOnly(2026, 9, 20), now.AddMinutes(2));
        await PublishProjectAsync(native, rules, directory, identity);
        Assert.Null(rules.ProposeSuccessor(firstVersion, successorVersion, Content(applicationEntry), author, "Author", now.AddMinutes(3),
            commandId: native.SuccessorCommand(successorVersion)));
        await PublishProjectAsync(native, rules, directory, identity);
        var proposed = await directory.ListAsync(tenantId, "application", applicationId, 20, null);
        Approve(rules, successorVersion, 1, reviewer, new DateOnly(2026, 9, 21), now.AddMinutes(4));
        await PublishProjectAsync(native, rules, directory, identity);
        var current = await directory.ListAsync(tenantId, "application", applicationId, 20, null);
        var historical = await directory.ListAsync(tenantId, "system_instance", instanceId, 20, null);
        var otherTenant = await directory.ListAsync(otherTenantId, "application", applicationId, 20, null);

        // Assert
        Assert.Empty(removedDraft.Items);
        Assert.Equal("draft", Assert.Single(proposed.Items).Status);
        Assert.Equal("approved", Assert.Single(current.Items).Status);
        Assert.Equal(successorVersion, current.Items[0].VersionId);
        Assert.Equal("historical", Assert.Single(historical.Items).Status);
        Assert.Equal(firstVersion, historical.Items[0].VersionId);
        Assert.Equal(new DateOnly(2026, 9, 20), historical.Items[0].EffectiveFrom);
        Assert.Empty(otherTenant.Items);
    }

    [Fact]
    public async Task ShouldRollBackReferenceRowsGivenFailedProjectionBatch()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        await using var native = new BoundaryProjectionScenario();
        var versionId = Uuid.CreateVersion5(boundaryId, "draft-1");
        var author = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var rules = new BoundaryRules(tenantId, boundaryId);
        Assert.True(rules.Create(Uuid.CreateVersion4(), versionId, Content(Entry("application", applicationId)), author, "Author", now).IsSuccess);
        var created = Assert.Single(await native.PublishAsync(rules));
        Assert.Null(rules.Revise(versionId, 1, Content(), author, "Author", now));
        var valid = Assert.IsType<BoundaryDraftRevisedContent>(Assert.Single(rules.PendingChanges));
        var malformed = valid with { DraftVersionId = Uuid.CreateVersion4() };
        var owner = await native.Reader.HydrateAsync(new SystemBoundary(tenantId, boundaryId));
        var operation = OwnerProvenance.For(owner, Uuid.CreateVersion4(), nameof(ReviseBoundaryDraft),
            ActorReference.ForMember(author, "Author"), boundaryId.ToString(), 1, AuthoredIntentDigest.Compute(malformed), now);
        var invalid = await BoundarySeeds.FreezeAsync(native.Content, rules, [malformed], operation, owner.NativeSequence + 1);
        var directory = new FitzApplicationBoundaryReferenceDirectory(native.Kv, native.Content);
        var identity = Identity(tenantId);

        // Act
        await using (var failed = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(created);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(invalid));
        }
        var afterFailure = await directory.ListAsync(tenantId, "application",
            applicationId, 20, null);
        await ApplyAsync(directory, identity, created);
        var afterRetry = await directory.ListAsync(tenantId, "application",
            applicationId, 20, null);

        // Assert
        Assert.Empty(afterFailure.Items);
        Assert.Single(afterRetry.Items);
        Assert.Equal(ProjectionCheckpoint.Start,
            await directory.LoadCheckpointAsync(tenantId));
    }

    [Fact]
    public async Task ShouldRemoveDraftReferenceGivenSuccessorDiscard()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        await using var native = new BoundaryProjectionScenario();
        var firstVersion = Uuid.CreateVersion5(boundaryId, "draft-1");
        var successorVersion = native.NewSuccessorVersion();
        var directory = new FitzApplicationBoundaryReferenceDirectory(native.Kv, native.Content);
        var identity = Identity(tenantId);
        var now = DateTimeOffset.UtcNow;
        var author = Uuid.CreateVersion4();
        var rules = new BoundaryRules(tenantId, boundaryId);
        Assert.True(rules.Create(programId, firstVersion, Content(Entry("application", applicationId)), author, "Author", now).IsSuccess);
        Approve(rules, firstVersion, 1, Uuid.CreateVersion4(), new DateOnly(2026, 9, 20), now);
        Assert.Null(rules.ProposeSuccessor(firstVersion, successorVersion, Content(Entry("application", applicationId)), author,
            "Author", now, commandId: native.SuccessorCommand(successorVersion)));
        await PublishProjectAsync(native, rules, directory, identity);

        // Act
        Assert.Null(rules.DiscardDraft(successorVersion, 1, "Discarded", author, "Author", now));
        await PublishProjectAsync(native, rules, directory, identity);
        var result = await directory.ListAsync(tenantId, "application", applicationId,
            20, null);

        // Assert
        Assert.Equal("approved", Assert.Single(result.Items).Status);
        Assert.Equal(firstVersion, result.Items[0].VersionId);
    }

    [Fact]
    public async Task ShouldReportLagThenAllowEmptyReadGivenSourceCheckpointCatchup()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        await using var native = new BoundaryProjectionScenario();
        var directory = new FitzApplicationBoundaryReferenceDirectory(native.Kv, native.Content);
        var events = new InMemoryEventStore();
        var consistency = new ApplicationBoundaryReferenceReadConsistency(directory, events);
        var stream = new EventStreamAddress(tenantId.ToString(), "boundaries", boundaryId.ToString());
        var rules = new BoundaryRules(tenantId, boundaryId);
        Assert.True(rules.Create(Uuid.CreateVersion4(), Uuid.CreateVersion5(boundaryId, "draft-1"), Content(),
            Uuid.CreateVersion4(), "Author", DateTimeOffset.UtcNow).IsSuccess);
        var created = Assert.Single(await native.PublishAsync(rules));

        // Act
        var noSource = await consistency.EnsureCaughtUpAsync(tenantId, CancellationToken.None);
        await events.AppendAsync(stream, 0, [created]);
        var lagged = await consistency.EnsureCaughtUpAsync(tenantId, CancellationToken.None);
        await using var source = events.ReadAsync(
            EventStreamPattern.ForPattern(tenantId.ToString(), "boundaries"),
            EventCursor.Start, CancellationToken.None).GetAsyncEnumerator();
        Assert.True(await source.MoveNextAsync());
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(created);
            await batch.CommitAsync(new ProjectionCheckpoint(source.Current.NextCursor));
        }
        var caughtUp = await consistency.EnsureCaughtUpAsync(tenantId,
            CancellationToken.None);

        // Assert
        Assert.True(noSource.IsSuccess);
        Assert.False(lagged.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, lagged.Error.Kind);
        Assert.True(lagged.Error.IsTransient);
        Assert.True(caughtUp.IsSuccess);
        Assert.Empty((await directory.ListAsync(tenantId, "application",
            Uuid.CreateVersion4(), 20, null)).Items);
    }

    [Fact]
    public async Task ShouldIndexInventoryReferencesGivenComponentAndInformationEntries()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var componentId = Uuid.CreateVersion4();
        var assetId = Uuid.CreateVersion4();
        await using var native = new BoundaryProjectionScenario();
        var directory = new FitzApplicationBoundaryReferenceDirectory(native.Kv, native.Content);
        var boundaryId = Uuid.CreateVersion4();
        var rules = new BoundaryRules(tenantId, boundaryId);

        // Act
        Assert.True(rules.Create(Uuid.CreateVersion4(), Uuid.CreateVersion5(boundaryId, "draft-1"),
            Content(Entry("component", componentId), Entry("information", assetId), Entry("process", Uuid.CreateVersion4())),
            Uuid.CreateVersion4(), "Author", DateTimeOffset.UtcNow).IsSuccess);
        await PublishProjectAsync(native, rules, directory, Identity(tenantId));
        var components = await directory.ListAsync(tenantId, "component", componentId, 20, null);
        var assets = await directory.ListAsync(tenantId, "information", assetId, 20, null);

        // Assert
        Assert.Equal("draft", Assert.Single(components.Items).Status);
        Assert.Equal(assetId, Assert.Single(assets.Items).GovernedRecordId);
    }

    static BoundaryScopeEntry Entry(string type, Uuid recordId) =>
        new(Uuid.CreateVersion4(), "inclusion", type, "Governed subject", recordId,
            "owner", "In scope", false);

    static BoundaryContent Content(params BoundaryScopeEntry[] entries) =>
        new("Scoped system", "readiness", ["security"], entries);

    static CheckpointIdentity Identity(Uuid tenantId) =>
        new("ApplicationBoundaryReferencesV3",
            EventStreamPattern.ForPattern(tenantId.ToString(), "boundaries"));

    static void Approve(BoundaryRules rules, Uuid version, long revision, Uuid reviewer, DateOnly effective, DateTimeOffset now)
    {
        var review = Uuid.CreateVersion4();
        Assert.Null(rules.Review(version, revision, review, "accept", "Reviewed", reviewer, "Reviewer", now));
        Assert.Null(rules.Approve(version, revision, Uuid.CreateVersion4(), review, effective, "Approved", "digest", reviewer, "Reviewer", now));
    }

    static async Task PublishProjectAsync(BoundaryProjectionScenario native, BoundaryRules rules,
        FitzApplicationBoundaryReferenceDirectory directory, CheckpointIdentity identity)
    {
        foreach (var fact in await native.PublishAsync(rules))
            await ApplyAsync(directory, identity, fact);
    }

    static async Task ApplyAsync(FitzApplicationBoundaryReferenceDirectory directory,
        CheckpointIdentity identity, DomainEvent domainEvent)
    {
        await using var batch = await directory.BeginAsync(
            new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
