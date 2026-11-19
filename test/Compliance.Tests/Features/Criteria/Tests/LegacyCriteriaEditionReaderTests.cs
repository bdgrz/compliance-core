using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Projections;
using Bdgrz.Compliance.Features.Criteria.Projectors;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class LegacyCriteriaEditionReaderTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid EditionId = Uuid.CreateVersion4();
    static readonly Uuid PublicationId = Uuid.CreateVersion4();
    static readonly DateTimeOffset PublishedAt = new(2026, 9, 24, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldReturnSourceMetadataAndDisclosuresGivenQualifiedSnapshot()
    {
        // Arrange
        var client = new InMemoryKvClient();
        await new SeedStore(client).SeedAsync(Edition(), [Disclosure("a"), Disclosure("b")]);
        var reader = new FitzRuntimeCriteriaCatalog(client);

        // Act
        var result = await reader.GetLegacyEditionAsync(TenantId, EditionId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("tsc", result.Value!.Framework);
        Assert.Equal("2017_tsc", result.Value.EditionLabel);
        Assert.Equal(PublishedAt, result.Value.PublishedAt);
        Assert.True(result.Value.IsComplete);
        Assert.Equal(new[] { "a", "b" }, result.Value.SupportGaps.Select(static gap => gap.Code));
    }

    [Fact]
    public async Task ShouldReturnLegacyEditionListGivenQualifiedBoundedDirectory()
    {
        // Arrange
        var client = new InMemoryKvClient();
        await new SeedStore(client).SeedAsync(Edition(), [Disclosure("a")]);
        var reader = new FitzRuntimeCriteriaCatalog(client);

        // Act
        var result = await reader.ListLegacyEditionsAsync(TenantId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var edition = Assert.Single(result.Value!, item => item.EditionId == EditionId);
        Assert.Equal(EditionId, edition.EditionId);
        Assert.Equal("a", Assert.Single(edition.SupportGaps).Code);
    }

    [Fact]
    public async Task ShouldReadLegacyEditionListAsOfCheckpointGivenNoExternalCoverage()
    {
        // Arrange
        var client = new InMemoryKvClient();
        await new SeedStore(client).SeedAsync(Edition(), [Disclosure("a")]);
        var reader = new FitzRuntimeCriteriaCatalog(client);

        // Act
        var result = await reader.ListLegacyEditionsAsync(TenantId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("a", Assert.Single(Assert.Single(result.Value!).SupportGaps).Code);
    }

    [Fact]
    public async Task ShouldRequirePagedDirectoryGivenLegacyResponseWouldBeTooLarge()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var editions = Enumerable.Range(0, FitzRuntimeCriteriaCatalog.LegacyEditionLimit + 1)
            .Select(index => Edition() with
            {
                EditionId = Uuid.CreateVersion4(),
                PublicationEventId = Uuid.CreateVersion4(),
                PublicationOrdinal = index + 1,
                Label = $"edition_{index:D2}",
            }).ToArray();
        await new SeedStore(client).SeedEditionsAsync(editions);
        var reader = new FitzRuntimeCriteriaCatalog(client);

        // Act
        var result = await reader.ListLegacyEditionsAsync(TenantId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error?.IsTransient);
        Assert.Contains("paged", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldReadLegacyEditionAsOfCheckpointGivenNoExternalCoverage()
    {
        // Arrange
        var client = new InMemoryKvClient();
        await new SeedStore(client).SeedAsync(Edition(), [Disclosure("a")]);
        var reader = new FitzRuntimeCriteriaCatalog(client);

        // Act
        var result = await reader.GetLegacyEditionAsync(TenantId, EditionId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("a", Assert.Single(result.Value!.SupportGaps).Code);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenMissingSourceMetadata()
    {
        // Arrange
        var client = new InMemoryKvClient();
        await new SeedStore(client).SeedAsync(Edition() with { ContentRights = null }, []);
        var reader = new FitzRuntimeCriteriaCatalog(client);

        // Act
        var result = await reader.GetLegacyEditionAsync(TenantId, EditionId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error?.IsTransient);
    }

    [Fact]
    public async Task ShouldKeepSeedPendingUntilCompletionFactIsProjected()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var seed = PlatformCriteriaCatalog.SeedEdition();
        var row = Edition() with
        {
            EditionId = seed.EditionId,
            SeedVersion = CriteriaSeedFingerprint.Version,
            SeedFingerprint = CriteriaSeedFingerprint.Compute(seed, PlatformCriteriaCatalog.EnumerateSeedEntries())
        };
        await new SeedStore(client).SeedAsync(row, []);
        var reader = new FitzRuntimeCriteriaCatalog(client);

        // Act
        var result = await reader.GetEditionAsync(seed.EditionId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value!.Coverage);
    }

    [Fact]
    public async Task ShouldReportSeedSnapshotCompleteGivenProjectedCompletionFact()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var seed = PlatformCriteriaCatalog.SeedEdition();
        var row = Edition() with
        {
            EditionId = seed.EditionId,
            SeedVersion = CriteriaSeedFingerprint.Version,
            SeedFingerprint = CriteriaSeedFingerprint.Compute(seed, PlatformCriteriaCatalog.EnumerateSeedEntries()),
            SeedImportCompletionEventId = Uuid.CreateVersion4()
        };
        await new SeedStore(client).SeedAsync(row, []);
        var reader = new FitzRuntimeCriteriaCatalog(client);

        // Act
        var result = await reader.GetEditionAsync(seed.EditionId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("complete", result.Value!.Coverage);
    }

    [Fact]
    public async Task ShouldReportPendingDirectoryGivenOnlyUnpublishedInitialSeed()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var seed = PlatformCriteriaCatalog.SeedEdition();
        var row = Edition() with
        {
            EditionId = seed.EditionId,
            Status = "pending",
            PublicationEventId = null,
            PublishedAt = null,
            SeedVersion = CriteriaSeedFingerprint.Version,
            SeedFingerprint = CriteriaSeedFingerprint.Compute(seed, PlatformCriteriaCatalog.EnumerateSeedEntries())
        };
        await new SeedStore(client).SeedAsync(row, []);
        var reader = new FitzRuntimeCriteriaCatalog(client);

        // Act
        var runtime = await reader.ListEditionsAsync(TenantId, 20, null);
        var directory = await reader.ListLegacyEditionsAsync(TenantId);
        var seedRead = await reader.GetEditionAsync(seed.EditionId);

        // Assert
        Assert.True(runtime.Error!.IsTransient);
        Assert.True(directory.Error!.IsTransient);
        Assert.True(seedRead.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldReadAuthoredEditionWhileSeedCompletionIsPending()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var seed = PlatformCriteriaCatalog.SeedEdition();
        var seedRow = Edition() with
        {
            EditionId = seed.EditionId,
            Label = seed.EditionLabel,
            PublicationEventId = Uuid.CreateVersion4(),
            SeedVersion = CriteriaSeedFingerprint.Version,
            SeedFingerprint = CriteriaSeedFingerprint.Compute(seed, PlatformCriteriaCatalog.EnumerateSeedEntries()),
            PublicationOrdinal = 1
        };
        var authored = Edition() with { PublicationOrdinal = 2 };
        await new SeedStore(client).SeedEditionsAsync([seedRow, authored]);
        var reader = new FitzRuntimeCriteriaCatalog(client);

        // Act
        var detail = await reader.GetEditionAsync(EditionId);
        var directory = await reader.ListLegacyEditionsAsync(TenantId);
        var runtime = await reader.ListEditionsAsync(TenantId, 20, null);
        var seedDetail = await reader.GetEditionAsync(seed.EditionId);

        // Assert
        Assert.True(detail.IsSuccess);
        Assert.Equal("complete", detail.Value!.Coverage);
        Assert.True(directory.Error!.IsTransient);
        Assert.True(runtime.IsSuccess);
        Assert.Equal("pending", runtime.Value!.Coverage);
        Assert.Contains(runtime.Value!.Items, item => item.EditionId == EditionId);
        Assert.Contains(runtime.Value.Items, item => item.EditionId == seed.EditionId && item.Coverage == "pending");
        Assert.Equal("pending", seedDetail.Value!.Coverage);
    }

    [Fact]
    public async Task ShouldMarkDirectoryPendingGivenAuthoredEditionBeforeSeedIsObserved()
    {
        // Arrange
        var client = new InMemoryKvClient();
        await new SeedStore(client).SeedEditionsAsync([Edition()], includeSeed: false);
        var reader = new FitzRuntimeCriteriaCatalog(client);

        // Act
        var directory = await reader.ListEditionsAsync(TenantId, 20, null);
        var legacy = await reader.ListLegacyEditionsAsync(TenantId);
        var authored = await reader.GetEditionAsync(EditionId);

        // Assert
        Assert.True(directory.IsSuccess);
        Assert.Equal("pending", directory.Value!.Coverage);
        Assert.Equal(EditionId, Assert.Single(directory.Value.Items).EditionId);
        Assert.True(legacy.Error!.IsTransient);
        Assert.True(authored.IsSuccess);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenUnknownEditionAndNoSourceProofOfAbsence()
    {
        // Arrange
        var reader = new FitzRuntimeCriteriaCatalog(new InMemoryKvClient());

        // Act
        var result = await reader.GetLegacyEditionAsync(TenantId, EditionId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error?.IsTransient);
    }

    [Fact]
    public async Task ShouldRequirePagedDisclosureApiGivenTooManyDisclosures()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var rows = Enumerable.Range(0, FitzRuntimeCriteriaCatalog.LegacyDisclosureLimit + 1)
            .Select(index => Disclosure(index.ToString("D3"))).ToArray();
        await new SeedStore(client).SeedAsync(Edition(), rows);
        var reader = new FitzRuntimeCriteriaCatalog(client);

        // Act
        var result = await reader.GetLegacyEditionAsync(TenantId, EditionId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error?.IsTransient);
        Assert.Contains("paged", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldRequirePagedDisclosureApiGivenOversizedDisclosureText()
    {
        // Arrange
        var client = new InMemoryKvClient();
        await new SeedStore(client).SeedAsync(Edition(), [Disclosure("a") with { Note = new string('x', 3000) }]);
        var reader = new FitzRuntimeCriteriaCatalog(client);

        // Act
        var result = await reader.GetLegacyEditionAsync(TenantId, EditionId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error?.IsTransient);
    }

    [Fact]
    public async Task ShouldHonorCancellationGivenCancelledRead()
    {
        // Arrange
        var reader = new FitzRuntimeCriteriaCatalog(new InMemoryKvClient());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act and assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await reader.GetLegacyEditionAsync(TenantId, EditionId, cancellation.Token));
    }

    static RuntimeCriteriaEditionIndexRow Edition() => new("live", EditionId, Uuid.CreateVersion4(),
        "published", "2017_tsc", PublicationId, PublishedAt, 0, Framework: "tsc", CoverageNote: "partial focus",
        SourceUrl: "https://example.test/source", ContentRights: "original summaries", NumberedCriteriaComplete: true,
        Revision: 7);

    static RuntimeCriteriaDisclosureIndexRow Disclosure(string code) =>
        new("live", EditionId, "security", code, "A source-recorded support gap.");

    sealed class SeedStore(IKvClient client) : FitzKvProjectionStore(client,
        "kv://bdgrz/runtime-criteria-catalog-v1/projection", RuntimeCriteriaCatalogProjector.WorkloadName)
    {
        public async Task SeedAsync(RuntimeCriteriaEditionIndexRow edition,
            IReadOnlyList<RuntimeCriteriaDisclosureIndexRow> disclosures)
        {
            var identity = new CheckpointIdentity(RuntimeCriteriaCatalogProjector.WorkloadName,
                EventStreamPattern.ForPattern("bdgrz"));
            await using var batch = await BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
            await RuntimeCriteriaProjectionSchema.Clock.InsertAsync(Transaction,
                new RuntimeCriteriaProjectionClock("live", 1, "epoch", "cursor", 1));
            if (edition.EditionId != PlatformCriteriaCatalog.SeedEdition().EditionId)
                await RuntimeCriteriaProjectionSchema.Editions.InsertAsync(Transaction, CompletedSeed());
            await RuntimeCriteriaProjectionSchema.Editions.InsertAsync(Transaction, edition);
            foreach (var disclosure in disclosures)
                await RuntimeCriteriaProjectionSchema.Disclosures.InsertAsync(Transaction, disclosure);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        public async Task SeedEditionsAsync(IReadOnlyList<RuntimeCriteriaEditionIndexRow> editions, bool includeSeed = true)
        {
            var identity = new CheckpointIdentity(RuntimeCriteriaCatalogProjector.WorkloadName,
                EventStreamPattern.ForPattern("bdgrz"));
            await using var batch = await BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
            await RuntimeCriteriaProjectionSchema.Clock.InsertAsync(Transaction,
                new RuntimeCriteriaProjectionClock("live", 1, "epoch", "cursor", 1));
            if (includeSeed && editions.All(edition => edition.EditionId != PlatformCriteriaCatalog.SeedEdition().EditionId))
                await RuntimeCriteriaProjectionSchema.Editions.InsertAsync(Transaction, CompletedSeed());
            foreach (var edition in editions)
                await RuntimeCriteriaProjectionSchema.Editions.InsertAsync(Transaction, edition);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        static RuntimeCriteriaEditionIndexRow CompletedSeed()
        {
            var seed = PlatformCriteriaCatalog.SeedEdition();
            return new RuntimeCriteriaEditionIndexRow("live", seed.EditionId, Uuid.CreateVersion4(), "published",
                seed.EditionLabel, Uuid.CreateVersion4(), seed.PublishedAt, 0, Framework: seed.Framework,
                CoverageNote: seed.CoverageNote, SourceUrl: seed.SourceUrl, ContentRights: seed.ContentRights,
                NumberedCriteriaComplete: seed.IsComplete, SeedVersion: CriteriaSeedFingerprint.Version,
                SeedFingerprint: CriteriaSeedFingerprint.Compute(seed, PlatformCriteriaCatalog.EnumerateSeedEntries()),
                Revision: 1, SeedImportCompletionEventId: Uuid.CreateVersion4());
        }
    }
}
