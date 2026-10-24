using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Processes;

public sealed class FitzKvProcessRecordStoreTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    static ProcessRecord Fresh() => ProcessRecord.Start(ProcessTestHost.Key, ProcessTestHost.Source, "initial",
        ProcessTestHost.Intent, ["t1"], Now);

    [Fact]
    public async Task ShouldRoundTripGivenProcessRecordExceedsSingleFrame()
    {
        // Arrange
        var (store, _) = Create();
        var targets = Enumerable.Range(0, 2000).Select(index => $"target-{index:D4}-{new string('x', 64)}").ToArray();
        var fresh = ProcessRecord.Start(ProcessTestHost.Key, ProcessTestHost.Source, "initial",
            ProcessTestHost.Intent, targets, Now);

        // Act
        var stored = await store.CreateIfAbsentAsync(fresh);
        var read = await store.ReadAsync(fresh.Key);

        // Assert
        Assert.Equal(stored, read);
        Assert.Equal(2000, read!.Targets.Count);
    }

    [Fact]
    public async Task ShouldFailClosedGivenCorruptedProcessPayloadChunk()
    {
        // Arrange
        var (store, kv) = Create();
        var stored = await store.CreateIfAbsentAsync(Fresh());
        var route = FitzKvProcessRecordStore.Route(stored.Key);
        var chunk = kv.Snapshot(route).First(pair => System.Text.Encoding.UTF8.GetString(pair.Key.Span)
            .Contains("/chunk/", StringComparison.Ordinal));
        var bytes = chunk.Value.ToArray();
        bytes[0] ^= 0xFF;
        kv.Seed(route, chunk.Key, bytes);

        // Act
        var failure = await Record.ExceptionAsync(() => store.ReadAsync(stored.Key));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public async Task ShouldReturnExistingRecordGivenSecondCreate()
    {
        // Arrange
        var (store, _) = Create();
        var first = await store.CreateIfAbsentAsync(Fresh());
        var advanced = await store.ReplaceAsync(first with { StageIndex = 1 }, first.Version);

        // Act
        var second = await store.CreateIfAbsentAsync(Fresh());

        // Assert
        Assert.Equal(advanced, second);
        Assert.Equal(2, second.Version);
    }

    [Fact]
    public async Task ShouldRejectReplaceGivenStaleVersion()
    {
        // Arrange
        var (store, _) = Create();
        var first = await store.CreateIfAbsentAsync(Fresh());
        await store.ReplaceAsync(first with { StageIndex = 1 }, first.Version);

        // Act
        var failure = await Record.ExceptionAsync(() => store.ReplaceAsync(first with { StageIndex = 2 }, first.Version));

        // Assert
        Assert.IsType<ProcessConcurrencyException>(failure);
        Assert.Equal(1, (await store.ReadAsync(ProcessTestHost.Key))!.StageIndex);
    }

    [Fact]
    public async Task ShouldIndexByDueTimeAndDropTerminalRecordsGivenScan()
    {
        // Arrange
        var (store, _) = Create();
        var record = await store.CreateIfAbsentAsync(Fresh());
        var later = await store.CreateIfAbsentAsync(Fresh() with
        {
            Key = ProcessTestHost.Key with { RootOperationId = Uuid.CreateVersion4() },
            NextDueAt = Now.AddMinutes(10),
        });

        // Act
        var dueNow = await store.ScanPendingAsync(ProcessTestHost.Process, Now, 10);
        var dueLater = await store.ScanPendingAsync(ProcessTestHost.Process, Now.AddMinutes(10), 10);
        await store.ReplaceAsync(record with { State = ProcessState.Completed }, record.Version);
        var afterCompletion = await store.ScanPendingAsync(ProcessTestHost.Process, Now.AddMinutes(10), 10);

        // Assert
        Assert.Equal([ProcessTestHost.Key], dueNow);
        Assert.Equal(2, dueLater.Count);
        Assert.Equal([later.Key], afterCompletion);
    }

    [Fact]
    public async Task ShouldReachOtherShardGivenEarlierShardRemainsDue()
    {
        // Arrange
        var (store, _) = Create();
        var first = await store.CreateIfAbsentAsync(Fresh());
        var second = await store.CreateIfAbsentAsync(Fresh() with
        {
            Key = ProcessTestHost.Key with
            {
                RootOperationId = Uuid.Parse("01000000-0000-4000-8000-0000000000d1",
                    System.Globalization.CultureInfo.InvariantCulture),
            },
        });

        // Act
        var firstScan = await store.ScanPendingAsync(ProcessTestHost.Process, Now, 1);
        var nextScan = await store.ScanPendingAsync(ProcessTestHost.Process, Now, 1);

        // Assert
        Assert.Equal([first.Key], firstScan);
        Assert.Equal([second.Key], nextScan);
    }

    [Fact]
    public async Task ShouldRetryFromBeginGivenIsolationConflict()
    {
        // Arrange
        var (store, kv) = Create();
        kv.FailNext(KvTestOperation.Begin, nameof(FitzErrorCodes.KvIsolationConflict), "lost lock", null);

        // Act
        var record = await store.CreateIfAbsentAsync(Fresh());

        // Assert
        Assert.Equal(1, record.Version);
        Assert.Equal(record, await store.ReadAsync(ProcessTestHost.Key));
    }

    [Fact]
    public async Task ShouldReturnNullGivenUnknownKey()
    {
        // Arrange
        var (store, _) = Create();

        // Act
        var record = await store.ReadAsync(ProcessTestHost.Key);

        // Assert
        Assert.Null(record);
    }

    static (FitzKvProcessRecordStore Store, InMemoryKvClient Kv) Create()
    {
        var kv = new InMemoryKvClient();
        return (new FitzKvProcessRecordStore(new PersistenceKvClient(kv)), kv);
    }
}
