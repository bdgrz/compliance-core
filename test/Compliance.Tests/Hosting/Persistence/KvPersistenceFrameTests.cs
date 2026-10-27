using Bdgrz.Compliance.Hosting.Persistence;
using System.Text;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Hosting.Persistence;

public sealed class KvPersistenceFrameTests
{
    const string Route = "kv://bdgrz/records/évidence";

    [Theory]
    [InlineData("get")]
    [InlineData("delete")]
    [InlineData("delete_range")]
    [InlineData("scan")]
    public async Task ShouldRejectBeforeForwardingGivenOversizedKeyOperation(string operation)
    {
        // Arrange
        var inner = new InMemoryKvClient();
        await using var transaction = await Guard(inner).BeginAsync(Route);
        var recorded = inner.Operations.Count;
        var key = new byte[70_000];

        // Act
        var failure = await Record.ExceptionAsync(async () =>
        {
            switch (operation)
            {
                case "get":
                    _ = await transaction.GetAsync(key);
                    break;
                case "delete":
                    await transaction.DeleteAsync(key);
                    break;
                case "delete_range":
                    await transaction.DeleteRangeAsync(key, key);
                    break;
                default:
                    _ = await transaction.ScanAsync(new KvScanQuery(StartKey: key, EndKey: key, Limit: 1));
                    break;
            }
        });

        // Assert
        Assert.IsType<KvFrameSizeException>(failure);
        Assert.Equal(recorded, inner.Operations.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectWriteBeforeMutationGivenValueExceedsCompleteOperationLimit(bool insert)
    {
        // Arrange
        var inner = new InMemoryKvClient();
        var client = Guard(inner);
        await using var transaction = await client.BeginAsync(Route);
        var key = Encoding.UTF8.GetBytes("clé");
        var value = new byte[ushort.MaxValue - Encoding.UTF8.GetByteCount(Route) - key.Length - 20 + 1];

        // Act
        var failure = await Record.ExceptionAsync(() => insert
            ? transaction.InsertAsync(key, value) : transaction.PutAsync(key, value));

        // Assert
        Assert.IsType<KvFrameSizeException>(failure);
        Assert.DoesNotContain(inner.Operations, operation => operation.Operation is KvTestOperation.Put or KvTestOperation.Insert);
        Assert.Empty(inner.Snapshot(Route));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldCommitExactBoundaryValueGivenUnicodeRouteAndKey(bool insert)
    {
        // Arrange
        var inner = new InMemoryKvClient();
        var client = Guard(inner);
        var key = Encoding.UTF8.GetBytes("clé");
        var value = new byte[ushort.MaxValue - Encoding.UTF8.GetByteCount(Route) - key.Length - 20];

        // Act
        await using (var transaction = await client.BeginAsync(Route))
        {
            Assert.Equal(Route, transaction.Route);
            if (insert)
                await transaction.InsertAsync(key, value);
            else
                await transaction.PutAsync(key, value);
            await transaction.CommitAsync(KvDurability.Sync);
        }
        await using var read = await client.BeginAsync(Route, KvMode.ReadOnly);
        var result = await read.GetAsync(key);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(value, result.Value!.Value.ToArray());
        var operation = PinnedFitzFrameTests.OperationPayload(false, Encoding.UTF8.GetBytes(Route), key,
            result.Value.Value.ToArray());
        Assert.Equal(ushort.MaxValue + 14, PinnedFitzFrameTests.Encode(insert ? "KvInsert" : "KvPut", operation).Length);
    }

    [Fact]
    public async Task ShouldHonorCancellationBeforeWriteGivenCanceledOperation()
    {
        // Arrange
        var inner = new InMemoryKvClient();
        var client = Guard(inner);
        await using var transaction = await client.BeginAsync(Route);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            transaction.PutAsync("key"u8.ToArray(), new byte[70_000], canceled.Token));

        // Assert
        Assert.Empty(inner.Snapshot(Route));
    }

    static PersistenceKvClient Guard(IKvClient inner) => new(inner);
}
