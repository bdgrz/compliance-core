using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.MethodologyTemplates;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates;

public sealed class MethodologyTemplateReadHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldReturnOnlyPublishedIdentitiesGivenDraftAndPublishedTemplates()
    {
        // Arrange
        await using var fixture = new Fixture();
        var published = await fixture.PublishAsync("Published", Now);
        var draftId = Uuid.CreateVersion4();
        Assert.True((await fixture.Source.CreateAsync(new CreateMethodologyTemplate(draftId,
            Content("Private draft"), "Draft"), fixture.Author)).IsSuccess);
        await fixture.ProjectAsync();
        var draft = await fixture.Source.Views.ReadAsync(draftId);

        // Act
        var listed = await fixture.ListAsync();
        var read = await fixture.GetAsync(published.VersionId);
        var hidden = await fixture.GetAsync(Assert.Single(draft!.Versions).VersionId);

        // Assert
        Assert.True(listed.IsSuccess, listed.Error?.Message);
        var summary = Assert.Single(listed.Value!.Items);
        Assert.Equal(published.TemplateId, summary.MethodologyTemplateId);
        Assert.Equal("Published", summary.Name);
        Assert.Equal(fixture.Tenant, summary.TenantId);
        Assert.Equal(fixture.Program, summary.ProgramId);
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Equal(published.VersionId, read.Value!.VersionId);
        Assert.Equal("published", read.Value.Status);
        Assert.Equal(RequestErrorKind.NotFound, hidden.Error?.Kind);
    }

    [Fact]
    public async Task ShouldReadExactPublicationGivenUnpublishedSuccessorBodyUnavailable()
    {
        // Arrange
        await using var fixture = new Fixture();
        var published = await fixture.PublishAsync("Public name", Now);
        fixture.Source.Clock.Now = Now.AddMinutes(5);
        Assert.True((await fixture.Source.ReviseAsync(new ReviseMethodologyTemplateDraft(Uuid.CreateVersion4(),
            published.TemplateId, 3, Content("Secret new draft name"), "Private draft notes"), fixture.Author)).IsSuccess);
        await fixture.ProjectAsync();
        var owner = await fixture.Source.Reader.HydrateAsync(new MethodologyTemplate(published.TemplateId));
        var unavailable = new UnreadableContent(fixture.Source.Content, owner.CurrentContent!.ContentId, true);

        // Act
        var read = await fixture.GetAsync(published.VersionId, unavailable);
        var listed = await fixture.ListAsync(store: unavailable);

        // Assert
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.True(listed.IsSuccess, listed.Error?.Message);
        Assert.Equal("Public name", read.Value!.Content.Name);
        Assert.Equal("Public name", Assert.Single(listed.Value!.Items).Name);
        Assert.Equal(published.ApprovedAt, listed.Value.Items[0].UpdatedAt);
        Assert.Equal(0, unavailable.RejectedReads);
    }

    [Fact]
    public async Task ShouldPageStableFirstPublicationsGivenLaterNewVersionAndDifferentCallerScopes()
    {
        // Arrange
        await using var fixture = new Fixture();
        var first = await fixture.PublishAsync("First", Now);
        var second = await fixture.PublishAsync("Second", Now.AddDays(1));
        var third = await fixture.PublishAsync("Third", Now.AddDays(2));
        fixture.Source.Clock.Now = Now.AddDays(3);
        Assert.True((await fixture.Source.ReviseAsync(new ReviseMethodologyTemplateDraft(Uuid.CreateVersion4(),
            first.TemplateId, 3, Content("First successor"), "New version"), fixture.Author)).IsSuccess);
        Assert.True((await fixture.Source.ReviewAsync(new ReviewMethodologyTemplate(Uuid.CreateVersion4(),
            first.TemplateId, 2, "approved", "Reviewed"), fixture.Reviewer)).IsSuccess);
        Assert.True((await fixture.Source.PublishAsync(new ApproveMethodologyTemplateVersion(Uuid.CreateVersion4(),
            first.TemplateId, 2, "Approved"), fixture.Approver)).IsSuccess);
        await fixture.ProjectAsync();

        // Act
        var page1 = await fixture.ListAsync(2);
        var page2 = await fixture.ListAsync(2, page1.Value!.NextCursor);
        var wrongProgram = await fixture.ListAsync(2, page1.Value.NextCursor, program: Uuid.CreateVersion4());
        var wrongTenant = await fixture.ListAsync(2, page1.Value.NextCursor, tenant: Uuid.CreateVersion4());

        // Assert
        Assert.True(page1.IsSuccess, page1.Error?.Message);
        Assert.True(page2.IsSuccess, page2.Error?.Message);
        Assert.Equal(new[] { first.TemplateId, second.TemplateId }, page1.Value!.Items.Select(value => value.MethodologyTemplateId));
        Assert.Equal("First successor", page1.Value.Items[0].Name);
        Assert.Equal(third.TemplateId, Assert.Single(page2.Value!.Items).MethodologyTemplateId);
        Assert.Null(page2.Value.NextCursor);
        Assert.Equal(RequestErrorKind.Validation, wrongProgram.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, wrongTenant.Error?.Kind);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenPublicationDirectoryBehindNativeSource()
    {
        // Arrange
        await using var fixture = new Fixture();
        var published = await fixture.PublishAsync("Published", Now);

        // Act
        var read = await fixture.GetAsync(published.VersionId);
        var listed = await fixture.ListAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, read.Error?.Kind);
        Assert.True(read.Error!.IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, listed.Error?.Kind);
        Assert.True(listed.Error!.IsTransient);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldReturnNoPartialPublicationPageGivenSelectedContentUnavailableOrCorrupt(bool unavailable)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.PublishAsync("Readable first", Now);
        var second = await fixture.PublishAsync("Unreadable second", Now.AddDays(1));
        await fixture.ProjectAsync();
        var owner = await fixture.Source.Reader.HydrateAsync(new MethodologyTemplate(second.TemplateId));
        var store = new UnreadableContent(fixture.Source.Content, owner.CurrentVersionContent!.ContentId, unavailable);

        // Act
        var read = await fixture.GetAsync(second.VersionId, store);
        var listed = await fixture.ListAsync(store: store);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, read.Error?.Kind);
        Assert.Equal(unavailable, read.Error!.IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, listed.Error?.Kind);
        Assert.Equal(unavailable, listed.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldShowRetiredStatusAndRetainHistoricalVersionGivenNativeRetirement()
    {
        // Arrange
        await using var fixture = new Fixture();
        var published = await fixture.PublishAsync("Historical", Now);
        fixture.Source.Clock.Now = Now.AddDays(1);
        Assert.True((await new RetireMethodologyTemplateHandler(fixture.Source.Operations()).HandleAsync(
            MethodologyTemplateScenario.Context(new RetireMethodologyTemplate(Uuid.CreateVersion4(),
                published.TemplateId, 3, new DateOnly(2026, 10, 25), "Retire"), fixture.Approver), CancellationToken.None)).IsSuccess);
        await fixture.ProjectAsync();

        // Act
        var read = await fixture.GetAsync(published.VersionId);
        var listed = await fixture.ListAsync();

        // Assert
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Equal("published", read.Value!.Status);
        Assert.Equal("retired", Assert.Single(listed.Value!.Items).Status);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(201, null)]
    [InlineData(50, "invalid")]
    public async Task ShouldReturnValidationGivenInvalidPublicationPagination(int limit, string? cursor)
    {
        // Arrange
        await using var fixture = new Fixture();

        // Act
        var result = await fixture.ListAsync(limit, cursor);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectPageGivenNativeSourceChangesDuringDiscovery()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.PublishAsync("First", Now);
        await fixture.ProjectAsync();
        var directory = new HookedDirectory(fixture.Directory, async () =>
        {
            await fixture.PublishAsync("Arrived during read", Now.AddDays(1));
            await fixture.ProjectAsync();
        });
        var handler = new ListMethodologyTemplatesHandler(directory,
            new MethodologyTemplateDirectoryReadConsistency(directory, fixture.Source.Events), fixture.Source.Views);

        // Act
        var result = await handler.HandleAsync(MethodologyTemplateScenario.Context(new ListMethodologyTemplates(
            fixture.Tenant, fixture.Program), Uuid.CreateVersion4()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldRefuseChangedPublishedIdentityGivenProjectionReplay()
    {
        // Arrange
        await using var fixture = new Fixture();
        var version = await fixture.PublishAsync("First", Now);
        await fixture.ProjectAsync();
        var published = Assert.IsType<MethodologyTemplateVersionPublished>((await fixture.Source.FactsAsync(version.TemplateId))[^1]);
        var checkpoint = await fixture.Directory.LoadCheckpointAsync();
        // Act
        await using (var batch = await fixture.Directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity(FitzMethodologyTemplateDirectory.ProjectorName, FitzMethodologyTemplateDirectory.Pattern), checkpoint)))
        {
            await fixture.Directory.ApplyAsync(published);
            var changed = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Directory.ApplyAsync(
                published with { VersionNumber = 9 }).AsTask());
            Assert.Contains("cannot change", changed.Message, StringComparison.Ordinal);
            await batch.CommitAsync(checkpoint);
        }

        // Assert
        var identity = await fixture.Directory.FindVersionAsync(version.VersionId);
        Assert.Equal(1, identity!.VersionNumber);
    }

    static MethodologyTemplateContent Content(string name) => new(name, "Original methodology", "soc2",
        [new MethodologyTemplateRecordContent("access", "control", "Review access", "Review access quarterly", [])]);

    sealed class Fixture : IAsyncDisposable
    {
        public MethodologyTemplateScenario Source { get; } = new(Now);
        public FitzMethodologyTemplateDirectory Directory { get; } = new(new InMemoryKvClient());
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Author { get; } = Uuid.CreateVersion4();
        public Uuid Reviewer { get; } = Uuid.CreateVersion4();
        public Uuid Approver { get; } = Uuid.CreateVersion4();

        public async Task<MethodologyTemplateVersionView> PublishAsync(string name, DateTimeOffset at)
        {
            var id = Uuid.CreateVersion4();
            Source.Clock.Now = at;
            Assert.True((await Source.CreateAsync(new CreateMethodologyTemplate(id, Content(name), "Create"), Author)).IsSuccess);
            Source.Clock.Now = at.AddMinutes(1);
            Assert.True((await Source.ReviewAsync(new ReviewMethodologyTemplate(Uuid.CreateVersion4(), id,
                1, "approved", "Reviewed"), Reviewer)).IsSuccess);
            Source.Clock.Now = at.AddMinutes(2);
            Assert.True((await Source.PublishAsync(new ApproveMethodologyTemplateVersion(Uuid.CreateVersion4(), id,
                1, "Approved"), Approver)).IsSuccess);
            return (await Source.Views.VersionAsync(id, 1))!;
        }

        public Task<Result<MethodologyTemplateVersionView>> GetAsync(Uuid versionId, IImmutableContentStore? store = null) =>
            new GetMethodologyTemplateVersionHandler(Directory,
                new MethodologyTemplateDirectoryReadConsistency(Directory, Source.Events),
                new MethodologyTemplateReader(Source.Reader, Source.Events, store ?? Source.Content))
            .HandleAsync(MethodologyTemplateScenario.Context(new GetMethodologyTemplateVersion(Tenant, Program, versionId),
                Uuid.CreateVersion4()), CancellationToken.None).AsTask();

        public Task<Result<Page<MethodologyTemplateSummaryView>>> ListAsync(int? limit = null, string? cursor = null,
            Uuid? tenant = null, Uuid? program = null, IImmutableContentStore? store = null) =>
            new ListMethodologyTemplatesHandler(Directory,
                new MethodologyTemplateDirectoryReadConsistency(Directory, Source.Events),
                new MethodologyTemplateReader(Source.Reader, Source.Events, store ?? Source.Content))
            .HandleAsync(MethodologyTemplateScenario.Context(new ListMethodologyTemplates(tenant ?? Tenant,
                program ?? Program, limit, cursor), Uuid.CreateVersion4()), CancellationToken.None).AsTask();

        public async Task ProjectAsync()
        {
            var checkpoint = await Directory.LoadCheckpointAsync();
            await using var batch = await Directory.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(
                FitzMethodologyTemplateDirectory.ProjectorName, FitzMethodologyTemplateDirectory.Pattern), checkpoint));
            var cursor = checkpoint.Cursor;
            await foreach (var record in Source.Events.ReadAsync(FitzMethodologyTemplateDirectory.Pattern, cursor, CancellationToken.None))
            {
                if (record.Event is MethodologyTemplateVersionPublished published)
                    await Directory.ApplyAsync(published);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }
        public ValueTask DisposeAsync() => Source.DisposeAsync();
    }

    sealed class UnreadableContent(IImmutableContentStore inner, Uuid failedContent, bool unavailable) : IImmutableContentStore
    {
        public int RejectedReads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (reference.ContentId != failedContent)
                return inner.ReadVerifiedAsync(realm, reference, ct);
            RejectedReads++;
            throw unavailable ? new ImmutableContentUnavailableException("Temporarily unavailable selected publication")
                : new ImmutableContentIntegrityException("Selected publication digest differs");
        }
    }

    sealed class HookedDirectory(IMethodologyTemplateDirectoryReader inner, Func<Task> afterPage)
        : IMethodologyTemplateDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(CancellationToken ct = default) => inner.LoadCheckpointAsync(ct);
        public ValueTask<MethodologyTemplatePublishedIdentity?> FindVersionAsync(Uuid id, CancellationToken ct = default) =>
            inner.FindVersionAsync(id, ct);
        public async ValueTask<Page<MethodologyTemplateCatalogIdentity>> ListAsync(int limit, string? cursor,
            CancellationToken ct = default)
        {
            var result = await inner.ListAsync(limit, cursor, ct);
            await afterPage();
            return result;
        }
    }
}
