using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Projections;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class CassieMethodologyTemplateDirectoryTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldReturnExactPublishedIdentityGivenNativeBatchCommitted()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieMethodologyTemplateSchema.Statements);
        await using var directory = new CassieMethodologyTemplateDirectory(database.Database);
        var fact = Publication(Uuid.CreateVersion4(), Now);
        await ProjectAsync(directory, fact);

        // Act
        var version = await directory.FindVersionAsync(VersionId(fact));
        var catalog = await directory.ListAsync(10, null);

        // Assert
        Assert.NotNull(version);
        Assert.Equal(fact.TemplateId, version.TemplateId);
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal(Now, version.PublishedAt);
        Assert.True(version.SeparateDraftLine);
        Assert.Equal(fact.TemplateId, Assert.Single(catalog.Items).TemplateId);
        Assert.Null(catalog.NextCursor);
    }

    [Fact]
    public async Task ShouldRollBackRowsWithCheckpointGivenBatchDisposedBeforeCommit()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieMethodologyTemplateSchema.Statements);
        await using var directory = new CassieMethodologyTemplateDirectory(database.Database);
        var fact = Publication(Uuid.CreateVersion4(), Now);
        var identity = Identity();

        // Act
        await using (await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
            await directory.ApplyAsync(fact);
        var version = await directory.FindVersionAsync(VersionId(fact));
        var catalog = await directory.ListAsync(10, null);
        var checkpoint = await directory.LoadCheckpointAsync();

        // Assert
        Assert.Null(version);
        Assert.Empty(catalog.Items);
        Assert.Equal(ProjectionCheckpoint.Start, checkpoint);
    }

    [Fact]
    public async Task ShouldKeepLiveRowsSeparateGivenCandidateGenerationProjected()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieMethodologyTemplateSchema.Statements);
        await using var directory = new CassieMethodologyTemplateDirectory(database.Database);
        var live = Publication(Uuid.CreateVersion4(), Now);
        var candidate = Publication(Uuid.CreateVersion4(), Now.AddTicks(1));
        await ProjectAsync(directory, live);
        await ProjectAsync(directory, candidate, "candidate");

        // Act
        var active = await directory.FindVersionAsync(VersionId(live));
        var isolated = await directory.FindVersionAsync(VersionId(candidate));
        var list = await directory.ListAsync(10, null);

        // Assert
        Assert.NotNull(active);
        Assert.Null(isolated);
        Assert.Equal(live.TemplateId, Assert.Single(list.Items).TemplateId);
    }

    [Fact]
    public async Task ShouldPageEveryIdentityExactlyOnceGivenTiedTimesAndAdjacentLargeTickValues()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieMethodologyTemplateSchema.Statements);
        await using var directory = new CassieMethodologyTemplateDirectory(database.Database);
        var facts = new[] { Publication(Uuid.CreateVersion4(), Now), Publication(Uuid.CreateVersion4(), Now),
            Publication(Uuid.CreateVersion4(), Now.AddTicks(1)) };
        foreach (var fact in facts)
            await ProjectAsync(directory, fact);
        var expected = facts.OrderBy(fact => fact.Metadata.OccurredOn).ThenBy(fact => fact.TemplateId.ToString(), StringComparer.Ordinal)
            .Select(fact => fact.TemplateId).ToArray();

        // Act
        var read = new List<Uuid>();
        string? cursor = null;
        for (var index = 0; index <= expected.Length; index++)
        {
            var page = await directory.ListAsync(1, cursor);
            read.AddRange(page.Items.Select(item => item.TemplateId));
            cursor = page.NextCursor;
            if (cursor is null)
                break;
        }

        // Assert
        Assert.Equal(expected, read);
        Assert.Equal(expected.Length, read.Distinct().Count());
        Assert.Null(cursor);
    }

    [Fact]
    public async Task ShouldKeepOriginalIdentityAndCheckpointGivenConflictingPublicationMetadata()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieMethodologyTemplateSchema.Statements);
        await using var directory = new CassieMethodologyTemplateDirectory(database.Database);
        var original = Publication(Uuid.CreateVersion4(), Now);
        await ProjectAsync(directory, original);
        var before = await directory.LoadCheckpointAsync();
        var changed = Publication(original.TemplateId, Now.AddTicks(1));

        // Act
        await using (await directory.BeginAsync(new ProjectionBatchContext(Identity(), before)))
        {
            await directory.ApplyAsync(original);
            await Assert.ThrowsAsync<InvalidOperationException>(() => directory.ApplyAsync(changed).AsTask());
        }
        var identity = await directory.FindVersionAsync(VersionId(original));
        var after = await directory.LoadCheckpointAsync();

        // Assert
        Assert.NotNull(identity);
        Assert.Equal(Now, identity.PublishedAt);
        Assert.Equal(before, after);
    }

    static CheckpointIdentity Identity(string? rebuild = null) => new(CassieMethodologyTemplateDirectory.ProjectorName,
        CassieMethodologyTemplateDirectory.Pattern, rebuild);
    static Uuid VersionId(MethodologyTemplateVersionPublishedV4 fact) => Uuid.CreateVersion5(fact.TemplateId, $"methodology-template-version:{fact.Version}");
    static MethodologyTemplateVersionPublishedV4 Publication(Uuid template, DateTimeOffset at)
    {
        var fact = new MethodologyTemplateVersionPublishedV4(template, 1, Uuid.CreateVersion4(), 1, Uuid.CreateVersion4(),
            new ImmutableContentRef(Uuid.CreateVersion4(), new string('a', 64), 2, "application/json", MethodologyTemplateDraft.EntriesSchema),
            [], [], Uuid.CreateVersion4());
        fact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), template, 3, at));
        return fact;
    }
    static async Task ProjectAsync(CassieMethodologyTemplateDirectory directory, MethodologyTemplateVersionPublishedV4 fact,
        string? rebuild = null)
    {
        var identity = Identity(rebuild);
        var checkpoint = await ((IProjectionStore)directory).LoadCheckpointAsync(identity);
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity, checkpoint));
        await directory.ApplyAsync(fact);
        await batch.CommitAsync(new ProjectionCheckpoint(new EventCursor(Uuid.CreateVersion4().ToString())));
    }
}
