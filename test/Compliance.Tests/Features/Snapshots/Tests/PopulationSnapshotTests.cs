using Bdgrz.Compliance.Features.Snapshots.Services;
using System.Globalization;
using System.Text.Json;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Snapshots.Aggregates;
using Bdgrz.Compliance.Features.Snapshots.Events;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Snapshots.Tests;

public sealed class PopulationSnapshotTests
{
    const string Kind = "access_population";
    static readonly Uuid TenantId = Uuid.Parse("3c0d5f0e-5b6f-4f0b-9a52-6d3a5a2d1f01", CultureInfo.InvariantCulture);
    static readonly Uuid SnapshotId = Uuid.Parse("9d1c7a52-0c1e-4c63-8f6a-0a6f2f5b7e02", CultureInfo.InvariantCulture);
    static readonly Uuid OtherSnapshotId = Uuid.Parse("a7c4e1b9-2d83-4e0a-9b11-3f5d8c6e4a03", CultureInfo.InvariantCulture);
    static readonly Uuid AuthorId = Uuid.Parse("5e8f2b34-71a6-4d9c-b0e3-8c4a1d7f6b04", CultureInfo.InvariantCulture);
    static readonly DateTimeOffset FrozenAt = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(AuthorId, "Author");
    static readonly ActorReference Other = ActorReference.ForMember(Uuid.CreateVersion4(), "Other");

    [Fact]
    public void ShouldFreezeInlineGivenValidPopulation()
    {
        // Arrange
        var rows = Rows(3);
        var digest = PopulationContentIdentity.Compute(Kind, rows).Value;
        var scenario = New();

        // Act
        var result = scenario.Aggregate.Freeze(SnapshotId, null, Kind, rows, digest.Sha256, null, Author, FrozenAt);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(new SnapshotRegistration(SnapshotId, digest.Sha256), result.Value);
        var frozen = Assert.IsType<PopulationSnapshotFrozen>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(TenantId, frozen.TenantId);
        Assert.Equal(SnapshotId, frozen.SnapshotId);
        Assert.Equal(SnapshotId, frozen.RootSnapshotId);
        Assert.Null(frozen.AmendsSnapshotId);
        Assert.Equal(3, frozen.RowCount);
        Assert.Equal(3, frozen.Rows.Count);
        Assert.Equal(0, frozen.StoredChunkCount);
        Assert.Null(frozen.StorageManifestSha256);
        Assert.True(scenario.Aggregate.IsFrozen);
        Assert.True(scenario.Aggregate.HasIntactIdentity);
        Assert.True(scenario.Aggregate.HasIntactContent);
    }

    [Fact]
    public void ShouldFreezeAmendmentGivenLineageToOriginalRoot()
    {
        // Arrange
        var rows = Rows(2);
        var digest = PopulationContentIdentity.Compute(Kind, rows).Value;
        var scenario = New(OtherSnapshotId);

        // Act
        var result = scenario.Aggregate.Freeze(SnapshotId, SnapshotId, Kind, rows, digest.Sha256,
            "  Metadata corrected  ", Author, FrozenAt);

        // Assert
        Assert.True(result.IsSuccess);
        var frozen = Assert.IsType<PopulationSnapshotFrozen>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(OtherSnapshotId, frozen.SnapshotId);
        Assert.Equal(SnapshotId, frozen.RootSnapshotId);
        Assert.Equal(SnapshotId, frozen.AmendsSnapshotId);
        Assert.Equal("Metadata corrected", frozen.AmendmentReason);
    }

    [Fact]
    public void ShouldRejectFreezeWithoutFactGivenContentHashMismatch()
    {
        // Arrange
        var scenario = New();

        // Act
        var result = scenario.Aggregate.Freeze(SnapshotId, null, Kind, Rows(2), new string('0', 64), null,
            Author, FrozenAt);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.False(scenario.Aggregate.IsFrozen);
    }

    [Fact]
    public void ShouldRejectFreezeWithoutFactGivenInvalidKind()
    {
        // Arrange
        var scenario = New();

        // Act
        var result = scenario.Aggregate.Freeze(SnapshotId, null, "Not Snake", Rows(1), new string('0', 64), null,
            Author, FrozenAt);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectFreezeWithoutFactGivenRowsBeyondInlineBound()
    {
        // Arrange
        var rows = Rows(PopulationSnapshot.MaximumInlineRows + 1);
        var digest = PopulationContentIdentity.Compute(Kind, rows).Value;
        var scenario = New();

        // Act
        var result = scenario.Aggregate.Freeze(SnapshotId, null, Kind, rows, digest.Sha256, null, Author, FrozenAt);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectFreezeWithoutFactGivenInlineRowsExceedingOneStorageChunk()
    {
        // Arrange
        var rows = new PopulationRow[PopulationSnapshot.MaximumInlineRows];
        for (var index = 0; index < rows.Length; index++)
            rows[index] = Row($"account/{index.ToString("D6", CultureInfo.InvariantCulture)}",
                new string('x', 200));
        var digest = PopulationContentIdentity.Compute(Kind, rows).Value;
        var scenario = New();

        // Act
        var result = scenario.Aggregate.Freeze(SnapshotId, null, Kind, rows, digest.Sha256, null, Author, FrozenAt);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectFreezeWithoutFactGivenOriginalSnapshotRootedElsewhere()
    {
        // Arrange
        var rows = Rows(1);
        var digest = PopulationContentIdentity.Compute(Kind, rows).Value;
        var scenario = New();

        // Act
        var result = scenario.Aggregate.Freeze(OtherSnapshotId, null, Kind, rows, digest.Sha256, null, Author,
            FrozenAt);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectFreezeWithoutFactGivenAmendmentWithoutReason()
    {
        // Arrange
        var rows = Rows(1);
        var digest = PopulationContentIdentity.Compute(Kind, rows).Value;
        var scenario = New(OtherSnapshotId);

        // Act
        var result = scenario.Aggregate.Freeze(SnapshotId, SnapshotId, Kind, rows, digest.Sha256, null, Author,
            FrozenAt);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectFreezeWithoutFactGivenAmendmentOfItself()
    {
        // Arrange
        var rows = Rows(1);
        var digest = PopulationContentIdentity.Compute(Kind, rows).Value;
        var scenario = New();

        // Act
        var result = scenario.Aggregate.Freeze(OtherSnapshotId, SnapshotId, Kind, rows, digest.Sha256, "Fix",
            Author, FrozenAt);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldReturnOriginalRegistrationWithoutFactGivenExactInlineRetry()
    {
        // Arrange
        var rows = Rows(3);
        var digest = PopulationContentIdentity.Compute(Kind, rows).Value;
        var scenario = Frozen(rows, digest.Sha256);

        // Act
        var result = scenario.Aggregate.Freeze(SnapshotId, null, Kind, rows, digest.Sha256, null, Author,
            FrozenAt.AddHours(1));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(new SnapshotRegistration(SnapshotId, digest.Sha256), result.Value);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectRetryWithoutFactGivenDifferentActor()
    {
        // Arrange
        var rows = Rows(3);
        var digest = PopulationContentIdentity.Compute(Kind, rows).Value;
        var scenario = Frozen(rows, digest.Sha256);

        // Act
        var result = scenario.Aggregate.Freeze(SnapshotId, null, Kind, rows, digest.Sha256, null, Other, FrozenAt);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectRetryWithoutFactGivenChangedContent()
    {
        // Arrange
        var rows = Rows(3);
        var changed = Rows(4);
        var changedDigest = PopulationContentIdentity.Compute(Kind, changed).Value;
        var scenario = Frozen(rows, PopulationContentIdentity.Compute(Kind, rows).Value.Sha256);

        // Act
        var result = scenario.Aggregate.Freeze(SnapshotId, null, Kind, changed, changedDigest.Sha256, null, Author,
            FrozenAt);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.Equal(3, scenario.Aggregate.RowCount);
    }

    [Fact]
    public void ShouldRejectRetryWithoutFactGivenChangedLineage()
    {
        // Arrange
        var rows = Rows(3);
        var digest = PopulationContentIdentity.Compute(Kind, rows).Value;
        var scenario = Frozen(rows, digest.Sha256);

        // Act
        var result = scenario.Aggregate.Freeze(OtherSnapshotId, OtherSnapshotId, Kind, rows, digest.Sha256,
            "Reframed", Author, FrozenAt);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldFreezeManifestGivenOrderedStorageChunks()
    {
        // Arrange
        var rows = Rows(5_000);
        var digest = PopulationContentIdentity.Compute(Kind, rows).Value;
        var chunks = PopulationSnapshotStorage.Plan(Kind, rows).Value;
        var scenario = New();

        // Act
        var result = scenario.Aggregate.FreezeChunked(SnapshotId, null, Kind, digest, chunks, null, Author,
            FrozenAt);

        // Assert
        Assert.True(result.IsSuccess, result.IsSuccess ? null : result.Error.Message);
        var frozen = Assert.IsType<PopulationSnapshotFrozen>(Assert.Single(scenario.PendingEvents));
        Assert.Empty(frozen.Rows);
        Assert.Equal(5_000, frozen.RowCount);
        Assert.Equal(chunks.Count, frozen.StoredChunkCount);
        Assert.Equal(PopulationSnapshotStorage.ManifestSha256(Kind, 5_000, chunks), frozen.StorageManifestSha256);
        Assert.False(scenario.Aggregate.HasIntactContent);
    }

    [Fact]
    public void ShouldReturnOriginalRegistrationWithoutFactGivenExactChunkedRetry()
    {
        // Arrange
        var rows = Rows(5_000);
        var digest = PopulationContentIdentity.Compute(Kind, rows).Value;
        var chunks = PopulationSnapshotStorage.Plan(Kind, rows).Value;
        var scenario = New();
        _ = scenario.Aggregate.FreezeChunked(SnapshotId, null, Kind, digest, chunks, null, Author, FrozenAt);
        var replayed = new AggregateScenario<PopulationSnapshot>(new PopulationSnapshot(TenantId, SnapshotId))
            .Given(NativeFacts.Raised(scenario.Aggregate));

        // Act
        var result = replayed.Aggregate.FreezeChunked(SnapshotId, null, Kind, digest, chunks, null, Author,
            FrozenAt.AddHours(1));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(digest.Sha256, result.Value.ContentSha256);
        Assert.Empty(replayed.PendingEvents);
    }

    [Fact]
    public void ShouldRejectChunkedFreezeWithoutFactGivenChunkedSnapshotAlreadyFrozenWithDifferentManifest()
    {
        // Arrange
        var rows = Rows(5_000);
        var digest = PopulationContentIdentity.Compute(Kind, rows).Value;
        var chunks = PopulationSnapshotStorage.Plan(Kind, rows).Value;
        var scenario = New();
        _ = scenario.Aggregate.FreezeChunked(SnapshotId, null, Kind, digest, chunks, null, Author, FrozenAt);
        var replayed = new AggregateScenario<PopulationSnapshot>(new PopulationSnapshot(TenantId, SnapshotId))
            .Given(NativeFacts.Raised(scenario.Aggregate));
        var altered = chunks.Select(chunk => chunk with { Sha256 = new string('1', 64) }).ToArray();

        // Act
        var result = replayed.Aggregate.FreezeChunked(SnapshotId, null, Kind, digest, altered, null, Author,
            FrozenAt);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(replayed.PendingEvents);
    }

    [Fact]
    public void ShouldRejectInlineRetryWithoutFactGivenSnapshotFrozenAsChunkedManifest()
    {
        // Arrange
        var rows = Rows(5_000);
        var digest = PopulationContentIdentity.Compute(Kind, rows).Value;
        var chunks = PopulationSnapshotStorage.Plan(Kind, rows).Value;
        var scenario = New();
        _ = scenario.Aggregate.FreezeChunked(SnapshotId, null, Kind, digest, chunks, null, Author, FrozenAt);
        var replayed = new AggregateScenario<PopulationSnapshot>(new PopulationSnapshot(TenantId, SnapshotId))
            .Given(NativeFacts.Raised(scenario.Aggregate));
        var small = Rows(2);

        // Act
        var result = replayed.Aggregate.Freeze(SnapshotId, null, Kind, small,
            PopulationContentIdentity.Compute(Kind, small).Value.Sha256, null, Author, FrozenAt);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(replayed.PendingEvents);
    }

    [Fact]
    public void ShouldRejectChunkedFreezeWithoutFactGivenUnorderedChunks()
    {
        // Arrange
        var rows = Rows(5_000);
        var digest = PopulationContentIdentity.Compute(Kind, rows).Value;
        var chunks = PopulationSnapshotStorage.Plan(Kind, rows).Value.Reverse().ToArray();
        var scenario = New();

        // Act
        var result = scenario.Aggregate.FreezeChunked(SnapshotId, null, Kind, digest, chunks, null, Author,
            FrozenAt);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectChunkedFreezeWithoutFactGivenSingleChunk()
    {
        // Arrange
        var rows = Rows(3);
        var digest = PopulationContentIdentity.Compute(Kind, rows).Value;
        var chunks = PopulationSnapshotStorage.Plan(Kind, rows).Value;
        var scenario = New();

        // Act
        var result = scenario.Aggregate.FreezeChunked(SnapshotId, null, Kind, digest, chunks, null, Author,
            FrozenAt);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectChunkedFreezeWithoutFactGivenMalformedChunkDigest()
    {
        // Arrange
        var rows = Rows(5_000);
        var digest = PopulationContentIdentity.Compute(Kind, rows).Value;
        var chunks = PopulationSnapshotStorage.Plan(Kind, rows).Value.ToArray();
        chunks[1] = chunks[1] with { Sha256 = "not-hex" };
        var scenario = New();

        // Act
        var result = scenario.Aggregate.FreezeChunked(SnapshotId, null, Kind, digest, chunks, null, Author,
            FrozenAt);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectChunkedFreezeWithoutFactGivenNonContiguousChunkStart()
    {
        // Arrange
        var rows = Rows(5_000);
        var digest = PopulationContentIdentity.Compute(Kind, rows).Value;
        var chunks = PopulationSnapshotStorage.Plan(Kind, rows).Value.ToArray();
        chunks[1] = chunks[1] with { Start = chunks[1].Start + 1 };
        var scenario = New();

        // Act
        var result = scenario.Aggregate.FreezeChunked(SnapshotId, null, Kind, digest, chunks, null, Author,
            FrozenAt);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectChunkedFreezeWithoutFactGivenChunkOverByteBudget()
    {
        // Arrange
        var rows = Rows(5_000);
        var digest = PopulationContentIdentity.Compute(Kind, rows).Value;
        var chunks = PopulationSnapshotStorage.Plan(Kind, rows).Value.ToArray();
        chunks[0] = chunks[0] with { EncodedBytes = PopulationSnapshotStorage.ChunkByteBudget + 1 };
        var scenario = New();

        // Act
        var result = scenario.Aggregate.FreezeChunked(SnapshotId, null, Kind, digest, chunks, null, Author,
            FrozenAt);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectChunkedFreezeWithoutFactGivenInvalidKind()
    {
        // Arrange
        var rows = Rows(5_000);
        var digest = PopulationContentIdentity.Compute(Kind, rows).Value;
        var chunks = PopulationSnapshotStorage.Plan(Kind, rows).Value;
        var scenario = New();

        // Act
        var result = scenario.Aggregate.FreezeChunked(SnapshotId, null, "Bad Kind", digest, chunks, null, Author,
            FrozenAt);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectChunkedFreezeWithoutFactGivenInconsistentChunkCount()
    {
        // Arrange
        var rows = Rows(5_000);
        var digest = PopulationContentIdentity.Compute(Kind, rows).Value with { ChunkCount = 1 };
        var chunks = PopulationSnapshotStorage.Plan(Kind, rows).Value;
        var scenario = New();

        // Act
        var result = scenario.Aggregate.FreezeChunked(SnapshotId, null, Kind, digest, chunks, null, Author,
            FrozenAt);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    static AggregateScenario<PopulationSnapshot> New(Uuid? snapshotId = null) =>
        new(new PopulationSnapshot(TenantId, snapshotId ?? SnapshotId));

    static AggregateScenario<PopulationSnapshot> Frozen(PopulationRow[] rows, string sha256)
    {
        var owner = new PopulationSnapshot(TenantId, SnapshotId);
        _ = owner.Freeze(SnapshotId, null, Kind, rows, sha256, null, Author, FrozenAt);
        return new AggregateScenario<PopulationSnapshot>(new PopulationSnapshot(TenantId, SnapshotId))
            .Given(NativeFacts.Raised(owner));
    }

    static PopulationRow[] Rows(int count)
    {
        var rows = new PopulationRow[count];
        for (var index = 0; index < count; index++)
            rows[index] = Row($"account/{index.ToString("D6", CultureInfo.InvariantCulture)}",
                $"user-{index}@example.com");
        return rows;
    }

    static PopulationRow Row(string key, string email)
    {
        using var document = JsonDocument.Parse($$"""{"email":"{{email}}","status":"active"}""");
        return new PopulationRow(key, document.RootElement.Clone());
    }
}
