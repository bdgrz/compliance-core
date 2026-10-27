using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Applications.Tests;

public sealed class FitzApplicationImportDirectoryTests
{
    [Fact]
    public async Task ShouldPageAllRowsWithoutTenantDisclosureGivenTwoHundredRowBatch()
    {
        // Arrange
        var directory = new FitzApplicationImportDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var batchId = Uuid.CreateVersion4();
        var identity = Checkpoint(tenantId);
        await using (var projection = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await Stage(directory, tenantId, batchId, 200);
            await projection.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var first = await directory.ListRowsAsync(tenantId, batchId, 137, null);
        var second = await directory.ListRowsAsync(tenantId, batchId, 137, first.NextCursor);
        var alienBatch = await directory.GetAsync(otherTenantId, batchId);
        var alienRows = await directory.ListRowsAsync(otherTenantId, batchId, 200, null);

        // Assert
        Assert.Equal(137, first.Items.Count);
        Assert.Equal(63, second.Items.Count);
        Assert.Equal(Enumerable.Range(1, 200), first.Items.Concat(second.Items)
            .Select(row => row.RowNumber));
        Assert.Null(alienBatch);
        Assert.Empty(alienRows.Items);
        Assert.Equal(200, (await directory.GetAsync(tenantId, batchId))?.RowCount);
    }

    [Fact]
    public async Task ShouldLeaveNoBatchOrRowsGivenAbortedProjectionBatch()
    {
        // Arrange
        var directory = new FitzApplicationImportDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var batchId = Uuid.CreateVersion4();

        // Act
        await using (var projection = await directory.BeginAsync(
                         new ProjectionBatchContext(Checkpoint(tenantId), ProjectionCheckpoint.Start)))
        {
            await Stage(directory, tenantId, batchId, 3);
        }
        var missing = await directory.GetAsync(tenantId, batchId);
        var missingRows = await directory.ListRowsAsync(tenantId, batchId, 200, null);
        await using (var retry = await directory.BeginAsync(
                         new ProjectionBatchContext(Checkpoint(tenantId), ProjectionCheckpoint.Start)))
        {
            await Stage(directory, tenantId, batchId, 3);
            await retry.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        Assert.Null(missing);
        Assert.Empty(missingRows.Items);
        Assert.Equal(3, (await directory.GetAsync(tenantId, batchId))?.RowCount);
        Assert.Equal(3, (await directory.ListRowsAsync(tenantId, batchId, 200, null)).Items.Count);
    }

    static CheckpointIdentity Checkpoint(Uuid tenantId) => new(
        "ApplicationImportDirectoryV1", EventStreamPattern.ForPattern(tenantId.ToString()));

    static ValueTask Stage(FitzApplicationImportDirectory directory, Uuid tenantId, Uuid batchId, int rowCount)
    {
        var rows = Enumerable.Range(1, rowCount)
            .Select(number => new ApplicationImportStagedRow(Uuid.CreateVersion4(), number,
                $"record-{number}", $"Application {number}", "Purpose", null, []))
            .ToArray();
        var stream = new EventStreamAddress(tenantId.ToString(), ApplicationImportBatch.Area, batchId.ToString());
        var content = new ImmutableContentRef(Uuid.CreateVersion4(), new string('0', 64), 2, "application/json",
            ApplicationImportRowsManifest.Schema);
        var staged = new ApplicationImportStaged(tenantId, batchId, Uuid.CreateVersion4(), "source", "primary",
            "partial", new string('A', 64), content, Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow,
            new OperationProvenance(Uuid.CreateVersion4(), Uuid.CreateVersion4(), null, "StageApplicationImport",
                ActorReference.ForSystemProcess("test", "test"), stream, "initial", null, new string('0', 64),
                DateTimeOffset.UtcNow, stream, stream), content, content, rowCount, 0);
        return directory.ApplyStagedAsync(staged, rows);
    }
}
