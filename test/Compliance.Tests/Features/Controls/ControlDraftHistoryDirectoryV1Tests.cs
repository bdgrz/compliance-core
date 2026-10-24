using System.Security.Claims;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class ControlDraftHistoryDirectoryV1Tests
{
    [Fact]
    public async Task ShouldPreserveImmutablePagedRevisionsGivenReplayedDraftEvents()
    {
        // Arrange
        await using var source = new ControlProjectionScenario();
        var directory = new FitzControlDraftHistoryDirectoryV1(source.Kv, source.Controls);
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = ControlDraft.IdFor(tenantId, programId, "AC-01");
        var now = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        var rules = Created(tenantId, programId, "AC-01", "Original", now);
        await source.PublishAsync(rules);
        Assert.Null(rules.Revise(programId, 1, Content("Second"), Uuid.CreateVersion4(), "Reviewer", now.AddMinutes(1)));
        await source.PublishAsync(rules);
        Assert.Null(rules.Revise(programId, 2, Content("Third"), Uuid.CreateVersion4(), "Reviewer", now.AddMinutes(2)));
        await source.PublishAsync(rules);
        await source.ProjectAsync(directory, directory.ApplyAsync, FitzControlDraftHistoryDirectoryV1.ProjectorName, tenantId);

        // Act
        var first = await directory.ListRevisionsAsync(tenantId, controlId, 2, null);
        var secondPage = await directory.ListRevisionsAsync(tenantId, controlId, 2, first.NextCursor);
        var foreign = await directory.ListRevisionsAsync(otherTenantId, controlId, 2, null);

        // Assert
        Assert.Equal([1L, 2L], first.Items.Select(item => item.Revision));
        Assert.Equal([3L], secondPage.Items.Select(item => item.Revision));
        Assert.Equal("Original", first.Items[0].Content.Title);
        Assert.Equal("Second", first.Items[1].Content.Title);
        Assert.Equal("Third", Assert.Single(secondPage.Items).Content.Title);
        Assert.Equal("AC-01", first.Items[1].Identifier);
        Assert.Equal("AC-01", Assert.Single(secondPage.Items).Identifier);
        Assert.Empty(foreign.Items);
        Assert.Null(await directory.GetRevisionAsync(otherTenantId, controlId, 1));
    }

    [Fact]
    public async Task ShouldRejectMalformedAndCrossControlCursorsGivenRevisionHistoryPage()
    {
        // Arrange
        await using var source = new ControlProjectionScenario();
        var directory = new FitzControlDraftHistoryDirectoryV1(source.Kv, source.Controls);
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = ControlDraft.IdFor(tenantId, programId, "AC-01");
        var otherControlId = ControlDraft.IdFor(tenantId, programId, "AC-02");
        var now = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        await source.PublishAsync(Created(tenantId, programId, "AC-01", "Target", now));
        var other = Created(tenantId, programId, "AC-02", "Other", now);
        await source.PublishAsync(other);
        Assert.Null(other.Revise(programId, 1, Content("Other revised"), Uuid.CreateVersion4(), "Reviewer",
            now.AddMinutes(1)));
        await source.PublishAsync(other);
        await source.ProjectAsync(directory, directory.ApplyAsync, FitzControlDraftHistoryDirectoryV1.ProjectorName, tenantId);
        var foreignCursor = (await directory.ListRevisionsAsync(tenantId, otherControlId, 1, null)).NextCursor;
        var handler = new ListControlDraftRevisionsHandler(directory,
            new ControlDraftHistoryReadConsistency(directory, source.Controls));

        // Act
        var crossDirectory = Assert.ThrowsAsync<KvDirectoryQueryException>(async () =>
            await directory.ListRevisionsAsync(tenantId, controlId, 1, foreignCursor));
        var malformedDirectory = Assert.ThrowsAsync<KvDirectoryQueryException>(async () =>
            await directory.ListRevisionsAsync(tenantId, controlId, 1, "not-a-cursor"));
        var crossControl = await handler.HandleAsync(Context(new ListControlDraftRevisions(tenantId,
            programId, controlId, Cursor: foreignCursor)), CancellationToken.None);
        var malformed = await handler.HandleAsync(Context(new ListControlDraftRevisions(tenantId,
            programId, controlId, Cursor: "not-a-cursor")), CancellationToken.None);

        // Assert
        Assert.NotNull(foreignCursor);
        await crossDirectory;
        await malformedDirectory;
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(crossControl.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(malformed.Error).Kind);
    }

    [Fact]
    public async Task ShouldRollBackAndReplayHistoryGivenOutOfOrderRevision()
    {
        // Arrange
        await using var source = new ControlProjectionScenario();
        var directory = new FitzControlDraftHistoryDirectoryV1(source.Kv, source.Controls);
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = ControlDraft.IdFor(tenantId, programId, "AC-01");
        var now = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        var rules = Created(tenantId, programId, "AC-01", "Original", now);
        var created = Assert.IsType<ControlDraftCreated>(Assert.Single(await source.PublishAsync(rules)));
        var malformed = new ControlProjectionFacts();
        var invalid = await malformed.FreezeAsync(new ControlDraftRevisedContent(tenantId, programId, controlId,
            3, Content("Skipped"), Uuid.CreateVersion4(), "Reviewer", now.AddMinutes(1)));
        var identity = Checkpoint(tenantId);

        // Act
        await using (var failed = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(created);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await directory.ApplyAsync(invalid));
        }
        Assert.Null(rules.Revise(programId, 1, Content("Second"), Uuid.CreateVersion4(), "Reviewer", now.AddMinutes(1)));
        var second = Assert.Single(await source.PublishAsync(rules));
        await source.ProjectAsync(directory, directory.ApplyAsync, FitzControlDraftHistoryDirectoryV1.ProjectorName,
            tenantId, [created, second]);

        // Assert
        var page = await directory.ListRevisionsAsync(tenantId, controlId, 10, null);
        Assert.Equal([1L, 2L], page.Items.Select(item => item.Revision));
        Assert.Equal(ProjectionCheckpoint.Start, await directory.LoadCheckpointAsync(tenantId));
    }

    static CheckpointIdentity Checkpoint(Uuid tenantId) => new(FitzControlDraftHistoryDirectoryV1.ProjectorName,
        EventStreamPattern.ForPattern(tenantId.ToString(), ControlDraft.Area));

    static ControlRules Created(Uuid tenantId, Uuid programId, string identifier, string title, DateTimeOffset changedAt)
    {
        var rules = new ControlRules(tenantId, ControlDraft.IdFor(tenantId, programId, identifier));
        Assert.True(rules.Create(programId, Uuid.CreateVersion4(), identifier, Content(title), Uuid.CreateVersion4(),
            "Author", changedAt).IsSuccess);
        return rules;
    }

    static ControlDraftContent Content(string title) => new(title, "Review access",
        "Management reviews access.", "The owner reviews access quarterly.", ["Review record"]);

    static RequestContext<TRequest> Context<TRequest>(TRequest request) where TRequest : IRequestBase =>
        new(request, new ClaimsPrincipal());
}
