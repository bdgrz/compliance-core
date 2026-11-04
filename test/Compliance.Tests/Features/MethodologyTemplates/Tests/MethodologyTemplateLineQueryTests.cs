using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Queries;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class MethodologyTemplateLineQueryTests
{
    [Fact]
    public async Task ShouldReturnActualLineHeaderGivenOperatorLineQuery()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();

        // Act
        var result = await new GetMethodologyTemplateLineHandler(fixture.Views).HandleAsync(
            MethodologyTemplateScenario.Context(new GetMethodologyTemplateLine(fixture.Template), fixture.Operator), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Firm methodology", result.Value.Name);
        Assert.Equal("idle", result.Value.Status);
        Assert.Equal(1, result.Value.PublishedVersion);
        Assert.Null(result.Value.OpenDraftId);
    }

    [Fact]
    public async Task ShouldRefuseStaleDirectoryGivenNewPublicationNotProjected()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();

        // Act
        var get = await fixture.GetAsync();
        var list = await fixture.ListAsync();

        // Assert
        Assert.False(get.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, get.Error.Kind);
        Assert.True(get.Error.IsTransient);
        Assert.False(list.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, list.Error.Kind);
    }

    [Fact]
    public async Task ShouldListAndReadActualGlobalPublicationGivenDirectoryCaughtUp()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await fixture.ProjectAsync();

        // Act
        var get = await fixture.GetAsync();
        var anotherTenant = await fixture.GetAsync(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var list = await fixture.ListAsync();
        var identity = await fixture.Directory.FindVersionAsync(fixture.VersionId);

        // Assert
        Assert.True(get.IsSuccess);
        Assert.True(anotherTenant.IsSuccess);
        Assert.Equal(get.Value.PublicationSource, anotherTenant.Value.PublicationSource);
        Assert.Equal("Access review", Assert.Single(get.Value.Entries).Title);
        Assert.Equal(fixture.Template, get.Value.TemplateId);
        Assert.True(list.IsSuccess);
        Assert.Equal("Firm methodology", Assert.Single(list.Value.Items).Name);
        Assert.NotNull(identity);
        Assert.True(identity.SeparateDraftLine);
    }

    [Fact]
    public async Task ShouldKeepDtoFamiliesSeparateGivenLegacyAndSeparateDraftPublications()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var legacy = Uuid.CreateVersion4();
        var content = new MethodologyTemplateContent("Legacy methodology", "Legacy original", "soc2",
            [new("legacy-review", "control", "Review legacy access", "Review quarterly.", [])]);
        Assert.True((await fixture.Source.CreateAsync(new CreateMethodologyTemplate(legacy, content, "Create"), fixture.Operator)).IsSuccess);
        var reviewer = Uuid.CreateVersion4();
        var approver = Uuid.CreateVersion4();
        Assert.True((await fixture.Source.ReviewAsync(new ReviewMethodologyTemplate(Uuid.CreateVersion4(), legacy, 1, "approved", "Reviewed"), reviewer)).IsSuccess);
        Assert.True((await fixture.Source.PublishAsync(new ApproveMethodologyTemplateVersion(Uuid.CreateVersion4(), legacy, 1, "Approved"), approver)).IsSuccess);
        var legacyVersion = (await fixture.Source.Views.VersionAsync(legacy, 1))!;
        await fixture.ProjectAsync();

        // Act
        var v2List = await fixture.ListAsync();
        var legacyList = await new ListMethodologyTemplatesHandler(fixture.Directory, fixture.Consistency, fixture.Source.Views)
            .HandleAsync(MethodologyTemplateScenario.Context(new ListMethodologyTemplates(fixture.Tenant, fixture.Program), fixture.Operator), CancellationToken.None);
        var wrongNewDto = await new GetMethodologyTemplateVersionV2Handler(fixture.Directory, fixture.Consistency, fixture.Views)
            .HandleAsync(MethodologyTemplateScenario.Context(new GetMethodologyTemplateVersionV2(fixture.Tenant, fixture.Program,
                legacyVersion.VersionId), fixture.Operator), CancellationToken.None);
        var wrongOldDto = await new GetMethodologyTemplateVersionHandler(fixture.Directory, fixture.Consistency, fixture.Source.Views)
            .HandleAsync(MethodologyTemplateScenario.Context(new GetMethodologyTemplateVersion(fixture.Tenant, fixture.Program,
                fixture.VersionId), fixture.Operator), CancellationToken.None);

        // Assert
        Assert.True(v2List.IsSuccess);
        Assert.Equal("Firm methodology", Assert.Single(v2List.Value.Items).Name);
        Assert.True(legacyList.IsSuccess);
        Assert.Equal("Legacy methodology", Assert.Single(legacyList.Value.Items).Name);
        Assert.False(wrongNewDto.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, wrongNewDto.Error.Kind);
        Assert.False(wrongOldDto.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, wrongOldDto.Error.Kind);
    }

    sealed class Fixture : IAsyncDisposable
    {
        public MethodologyTemplateScenario Source { get; } = new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));
        public FitzMethodologyTemplateDirectory Directory { get; } = new(new InMemoryKvClient());
        public Uuid Template { get; } = Uuid.CreateVersion4();
        public Uuid Operator { get; } = Uuid.CreateVersion4();
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid VersionId => Uuid.CreateVersion5(Template, "methodology-template-version:1");
        public MethodologyTemplateLineReader Views => new(new EventStoreSnapshotReader(Source.Events), Source.Content);
        public MethodologyTemplateDirectoryReadConsistency Consistency => new(Directory, Source.Events);
        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            var owner = new MethodologyTemplate(fixture.Template);
            Assert.True(owner.CreateLine("Firm methodology", "Original firm entries", fixture.Operator).IsSuccess);
            IReadOnlyList<MethodologyTemplateRecordContent> empty = [];
            var opening = await fixture.Source.Content.WriteJsonAsync(MethodologyTemplate.Realm, Uuid.CreateVersion4(),
                MethodologyTemplateDraft.EntriesSchema, empty, CancellationToken.None);
            var draft = Uuid.CreateVersion4();
            Assert.True(owner.StartDraft(draft, 0, empty, opening, fixture.Operator).IsSuccess);
            IReadOnlyList<MethodologyTemplateRecordContent> entries = [new("access-review", "control", "Access review", "Review quarterly.", [])];
            var content = await fixture.Source.Content.WriteJsonAsync(MethodologyTemplate.Realm, Uuid.CreateVersion4(),
                MethodologyTemplateDraft.EntriesSchema, entries, CancellationToken.None);
            Assert.True(owner.PublishDraft(draft, Uuid.CreateVersion4(), 1, 0, entries, content, ["access-review"], fixture.Operator).IsSuccess);
            await fixture.Source.Writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
            return fixture;
        }
        public Task<Result<MethodologyTemplatePublishedVersionView>> GetAsync(Uuid? tenant = null, Uuid? program = null) =>
            new GetMethodologyTemplateVersionV2Handler(Directory, Consistency, Views).HandleAsync(MethodologyTemplateScenario.Context(
                new GetMethodologyTemplateVersionV2(tenant ?? Tenant, program ?? Program, VersionId), Operator), CancellationToken.None).AsTask();
        public Task<Result<Page<MethodologyTemplateSummaryView>>> ListAsync() =>
            new ListMethodologyTemplatesV2Handler(Directory, Consistency, Views).HandleAsync(MethodologyTemplateScenario.Context(
                new ListMethodologyTemplatesV2(Tenant, Program), Operator), CancellationToken.None).AsTask();
        public async Task ProjectAsync()
        {
            var checkpoint = await Directory.LoadCheckpointAsync();
            await using var batch = await Directory.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(
                FitzMethodologyTemplateDirectory.ProjectorName, FitzMethodologyTemplateDirectory.Pattern), checkpoint));
            var cursor = checkpoint.Cursor;
            await foreach (var record in Source.Events.ReadAsync(FitzMethodologyTemplateDirectory.Pattern, cursor, CancellationToken.None))
            {
                if (record.Event is MethodologyTemplateVersionPublishedV4 current)
                    await Directory.ApplyAsync(current);
                else if (record.Event is MethodologyTemplateVersionPublished legacy)
                    await Directory.ApplyAsync(legacy);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }
        public ValueTask DisposeAsync() => Source.DisposeAsync();
    }
}
