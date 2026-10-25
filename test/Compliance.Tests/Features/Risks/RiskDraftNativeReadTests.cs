using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskDraftNativeReadTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldClassifyImmutableRiskDraftFailureGivenCurrentAndRevisionRead(bool unavailable)
    {
        // Arrange
        await using var fixture = new RiskDraftSourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var id = RiskDraft.IdFor(tenant, program, "R-READ");
        await fixture.SeedProgramAsync(tenant, program, RbacIds.Member(tenant, user));
        var created = await new CreateRiskDraftHandler(fixture.Operations()).HandleAsync(RiskDraftSourceFixture.Context(
            new CreateRiskDraft(id, tenant, program, "R-READ", "Risk title", "Scenario", "Effect"), user), CancellationToken.None);
        Assert.True(created.IsSuccess, created.Error?.Message);
        var kv = new InMemoryKvClient();
        var projection = new FitzRiskDraftDirectory(kv, fixture.Content, fixture.Drafts);
        await using (var batch = await projection.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(FitzRiskDraftDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenant.ToString(), RiskDraft.Area)), ProjectionCheckpoint.Start)))
        {
            await projection.ApplyAsync(Assert.Single(await fixture.FactsAsync(tenant, id)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var failing = new FitzRiskDraftDirectory(kv, new FaultingContent(fixture.Content, unavailable), fixture.Drafts);
        var consistency = new RiskDraftReadConsistency(failing, fixture.Reader);

        // Act
        var current = await new GetRiskDraftHandler(consistency).HandleAsync(RiskDraftSourceFixture.Context(new GetRiskDraft(tenant, program, id), user), CancellationToken.None);
        var first = (IRiskDraftFact)Assert.Single(await fixture.FactsAsync(tenant, id));
        Assert.True((await new ReviseRiskDraftHandler(fixture.Operations()).HandleAsync(RiskDraftSourceFixture.Context(
            new ReviseRiskDraft(Uuid.CreateVersion4(), tenant, program, id, 1, "Current revision", "Current scenario", "Effect"), user), CancellationToken.None)).IsSuccess);
        await using (var batch = await projection.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(FitzRiskDraftDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenant.ToString(), RiskDraft.Area)), ProjectionCheckpoint.Start)))
        {
            await projection.ApplyAsync((await fixture.FactsAsync(tenant, id))[^1]);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var historicalFailure = new FitzRiskDraftDirectory(kv, new FaultingContent(fixture.Content, unavailable, first.Content), fixture.Drafts);
        var historicalConsistency = new RiskDraftReadConsistency(historicalFailure, fixture.Reader);
        var revision = await new GetRiskDraftRevisionHandler(historicalFailure, historicalConsistency).HandleAsync(RiskDraftSourceFixture.Context(new GetRiskDraftRevision(tenant, program, id, 1), user), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, current.Error?.Kind);
        Assert.Equal(unavailable, current.Error!.IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, revision.Error?.Kind);
        Assert.Equal(unavailable, revision.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldReplayOriginalBeforeCurrentNarrativeReadGivenAcceptedRevisionAndLaterState()
    {
        // Arrange
        await using var fixture = new RiskDraftSourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var id = RiskDraft.IdFor(tenant, program, "R-REPLAY");
        await fixture.SeedProgramAsync(tenant, program, RbacIds.Member(tenant, user));
        var create = new CreateRiskDraft(id, tenant, program, "R-REPLAY", "Original", "Scenario", "Effect");
        Assert.True((await new CreateRiskDraftHandler(fixture.Operations()).HandleAsync(RiskDraftSourceFixture.Context(create, user), CancellationToken.None)).IsSuccess);
        var request = new ReviseRiskDraft(Uuid.CreateVersion4(), tenant, program, id, 1, "Accepted revision", "Scenario", "Effect");
        var revision = new ReviseRiskDraftHandler(fixture.Operations());
        Assert.True((await revision.HandleAsync(RiskDraftSourceFixture.Context(request, user), CancellationToken.None)).IsSuccess);
        Assert.True((await revision.HandleAsync(RiskDraftSourceFixture.Context(request with
        {
            CommandId = Uuid.CreateVersion4(),
            ExpectedRevision = 2,
            Title = "Later state"
        }, user), CancellationToken.None)).IsSuccess);
        var failing = new ReviseRiskDraftHandler(fixture.Operations(content: new FaultingContent(fixture.Content, true)));

        // Act
        var replay = await failing.HandleAsync(RiskDraftSourceFixture.Context(request, user, "Updated profile"), CancellationToken.None);
        var newCommand = await failing.HandleAsync(RiskDraftSourceFixture.Context(request with { CommandId = Uuid.CreateVersion4(), ExpectedRevision = 3 }, user), CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, newCommand.Error?.Kind);
        Assert.True(newCommand.Error!.IsTransient);
        Assert.Equal(3, (await fixture.FactsAsync(tenant, id)).Count);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenRiskSourceAndProjectionAdvanceDuringListRead()
    {
        // Arrange
        await using var fixture = new RiskDraftSourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var id = RiskDraft.IdFor(tenant, program, "R-FENCE");
        await fixture.SeedProgramAsync(tenant, program, RbacIds.Member(tenant, user));
        Assert.True((await new CreateRiskDraftHandler(fixture.Operations()).HandleAsync(RiskDraftSourceFixture.Context(
            new CreateRiskDraft(id, tenant, program, "R-FENCE", "Initial", "Scenario", "Effect"), user), CancellationToken.None)).IsSuccess);
        var projection = new FitzRiskDraftDirectory(new InMemoryKvClient(), fixture.Content, fixture.Drafts);
        await ProjectAllAsync(fixture, projection, tenant);
        var changing = new ChangingList(projection, async () =>
        {
            Assert.True((await new ReviseRiskDraftHandler(fixture.Operations()).HandleAsync(RiskDraftSourceFixture.Context(
                new ReviseRiskDraft(Uuid.CreateVersion4(), tenant, program, id, 1, "Concurrent revision", "Scenario", "Effect"), user), CancellationToken.None)).IsSuccess);
            await ProjectAllAsync(fixture, projection, tenant, skip: 1);
        });
        var handler = new ListRiskDraftsHandler(changing, fixture.Reader, new RiskDraftListReadConsistency(changing, fixture.Events));

        // Act
        var result = await handler.HandleAsync(RiskDraftSourceFixture.Context(new ListRiskDrafts(tenant, program), user), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error!.IsTransient);
        Assert.Equal(2, (await fixture.Drafts.ReadAsync(tenant, program, id)).Owner.Revision);
    }

    static async Task ProjectAllAsync(RiskDraftSourceFixture fixture, FitzRiskDraftDirectory projection, Uuid tenant, int skip = 0)
    {
        var identity = new CheckpointIdentity(FitzRiskDraftDirectory.ProjectorName, EventStreamPattern.ForPattern(tenant.ToString(), RiskDraft.Area));
        var count = 0;
        var cursor = EventCursor.Start;
        var checkpoint = await projection.LoadCheckpointAsync(identity);
        await using var batch = await projection.BeginAsync(new ProjectionBatchContext(identity, checkpoint));
        await foreach (var record in fixture.Events.ReadAsync(identity.Pattern, EventCursor.Start, CancellationToken.None))
        {
            if (record.Event is IRiskDraftFact && count++ >= skip)
                await projection.ApplyAsync(record.Event);
            cursor = record.NextCursor;
        }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }
    sealed class ChangingList(IRiskDraftDirectoryReader inner, Func<Task> change) : IRiskDraftDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) => inner.LoadCheckpointAsync(tenantId, ct);
        public ValueTask<RiskDraftView?> GetAsync(Uuid tenantId, Uuid riskId, CancellationToken ct = default) => inner.GetAsync(tenantId, riskId, ct);
        public ValueTask<RiskDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid riskId, long revision, CancellationToken ct = default) => inner.GetRevisionAsync(tenantId, riskId, revision, ct);
        public async ValueTask<Page<RiskDraftView>> ListProgramAsync(Uuid tenantId, Uuid programId, int limit, string? cursor, CancellationToken ct = default)
        {
            var page = await inner.ListProgramAsync(tenantId, programId, limit, cursor, ct);
            await change();
            return page;
        }
    }

    sealed class FaultingContent(IImmutableContentStore inner, bool unavailable, ImmutableContentRef? selected = null) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            reference.Schema == RiskDraftFactContent.Schema && (selected is null || selected == reference) ? Task.FromException<ReadOnlyMemory<byte>>(unavailable
                ? new ImmutableContentUnavailableException("The exact risk draft revision is not visible yet.")
                : new ImmutableContentIntegrityException("The exact risk draft revision is corrupt.")) : inner.ReadVerifiedAsync(realm, reference, ct);
    }
}
