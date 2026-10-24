using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Processes;

public sealed class ProcessDriverTests
{
    [Fact]
    public async Task ShouldAcquireInCanonicalKeyOrderGivenReversedAuthoredManifest()
    {
        // Arrange
        await using var host = new ProcessTestHost();
        await host.StartAsync("\U0001F600-last", "b-second", "a-first", "～-third");

        // Act
        var record = await host.RunAsync();

        // Assert
        Assert.Equal(ProcessState.Completed, record.State);
        var acquires = host.Recorder.Calls.Where(c => c.Receiver == "AcquireX").Select(c => c.Target!).ToArray();
        Assert.Equal(["a-first", "b-second", "～-third", "\U0001F600-last"], acquires);
        Assert.Equal(["AcquireX", "AcquireX", "AcquireX", "AcquireX", "ApplyA", "ApplyB", "SettleX", "SettleX", "SettleX", "SettleX"],
            host.Recorder.Calls.Select(c => c.Receiver).ToArray());
    }

    [Fact]
    public async Task ShouldReplayIdenticalCommandIdGivenCrashAfterReceiverSuccessBeforeCas()
    {
        // Arrange
        var crash = new CrashOnceStore();
        await using var host = new ProcessTestHost(inner => crash.Wrap(inner));
        await host.StartAsync("t1");
        crash.ArmAfterReplaceCount(3); // lease, AcquireX advance and the next lease land; the advance after ApplyA crashes

        // Act
        var failure = await Record.ExceptionAsync(() => host.RunAsync());
        var record = await host.RunAsync();

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal(ProcessState.Completed, record.State);
        var repeated = host.Recorder.Calls.GroupBy(c => c.CommandId).Single(g => g.Count() == 2);
        Assert.Equal("ApplyA", repeated.First().Receiver);
        Assert.Equal(repeated.First(), repeated.Last());
    }

    [Fact]
    public async Task ShouldNotResumeGivenAnotherWorkerHoldsAnUnexpiredLease()
    {
        // Arrange
        await using var host = new ProcessTestHost();
        var started = await host.StartAsync("t1");
        await host.Store.ReplaceAsync(started with
        {
            Lease = new ProcessLease("other", host.Clock.GetUtcNow() + ProcessDriver.LeaseDuration),
        }, started.Version);

        // Act
        var held = await host.RunAsync("w1");
        host.Clock.Advance(ProcessDriver.LeaseDuration + TimeSpan.FromSeconds(1));
        var resumed = await host.RunAsync("w1");

        // Assert
        Assert.Empty(host.Recorder.Calls.Take(0));
        Assert.Equal(ProcessState.Pending, held.State);
        Assert.Equal(ProcessState.Completed, resumed.State);
    }

    [Fact]
    public async Task ShouldSetNextDueGivenTransientReceiverFailure()
    {
        // Arrange
        await using var host = new ProcessTestHost();
        await host.StartAsync("t1");
        var attempts = 0;
        host.Recorder.Script("ApplyA", _ => ++attempts == 1
            ? Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "target busy", true))
            : Result<ReceiverOutcome>.Success(new ReceiverOutcome.Applied(ProcessTestHost.Source)));

        // Act
        var waiting = await host.RunAsync();
        var early = await host.Store.ScanPendingAsync(ProcessTestHost.Process, host.Clock.GetUtcNow(), 10);
        host.Clock.Advance(ProcessDriver.RetryDelay);
        var due = await host.Store.ScanPendingAsync(ProcessTestHost.Process, host.Clock.GetUtcNow(), 10);
        var done = await host.RunAsync();

        // Assert
        Assert.Equal(ProcessState.Pending, waiting.State);
        Assert.Equal(1, waiting.Attempts);
        Assert.Null(waiting.Lease);
        Assert.Empty(early);
        Assert.Equal([ProcessTestHost.Key], due);
        Assert.Equal(ProcessState.Completed, done.State);
    }

    [Fact]
    public async Task ShouldQuarantineGivenConflictingReceiverIntent()
    {
        // Arrange
        await using var host = new ProcessTestHost();
        await host.StartAsync("t1");
        host.Recorder.Script("ApplyB", _ =>
            Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "different intent", false)));

        // Act
        var record = await host.RunAsync();
        var again = await host.RunAsync();

        // Assert
        Assert.Equal(ProcessState.Quarantined, record.State);
        Assert.Contains("different intent", record.Note, StringComparison.Ordinal);
        Assert.Equal(record, again);
        Assert.Equal(3, host.Recorder.Calls.Count);
        Assert.Empty(await host.Store.ScanPendingAsync(ProcessTestHost.Process, host.Clock.GetUtcNow().AddDays(1), 10));
    }

    [Fact]
    public async Task ShouldCompleteWithoutLaterStagesGivenProvenSupersession()
    {
        // Arrange
        await using var host = new ProcessTestHost();
        await host.StartAsync("t1");
        host.Recorder.Script("ApplyA", _ =>
            Result<ReceiverOutcome>.Success(new ReceiverOutcome.Superseded("later generation", null)));

        // Act
        var record = await host.RunAsync();

        // Assert
        Assert.Equal(ProcessState.Completed, record.State);
        Assert.Equal(["AcquireX", "ApplyA"], host.Recorder.Calls.Select(c => c.Receiver).ToArray());
    }

    [Fact]
    public async Task ShouldSettleRemainingTargetsGivenSupersededSettlement()
    {
        // Arrange
        await using var host = new ProcessTestHost();
        await host.StartAsync("t1", "t2");
        host.Recorder.Script("SettleX", command => Result<ReceiverOutcome>.Success(command.Target == "t1"
            ? new ReceiverOutcome.Superseded("already settled", ProcessTestHost.Source)
            : new ReceiverOutcome.Applied(ProcessTestHost.Source)));

        // Act
        var record = await host.RunAsync();

        // Assert
        Assert.Equal(ProcessState.Completed, record.State);
        Assert.Equal(["t1", "t2"], host.Recorder.Calls.Where(call => call.Receiver == "SettleX")
            .Select(call => call.Target!).ToArray());
    }

    [Fact]
    public async Task ShouldFinishCleanupGivenAbortReceiverAlreadySuperseded()
    {
        // Arrange
        await using var host = new ProcessTestHost();
        await host.StartAsync("t1", "t2");
        host.Recorder.Script("ApplyA", _ =>
            Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "wait", true)));
        await host.RunAsync();
        await using var scope = host.Provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ProcessDriver>().AbortAsync(ProcessTestHost.Key);
        host.Recorder.Script("RejectX", _ => Result<ReceiverOutcome>.Success(
            new ReceiverOutcome.Superseded("already fenced", ProcessTestHost.Source)));

        // Act
        var record = await host.RunAsync();

        // Assert
        Assert.Equal(ProcessState.Aborted, record.State);
        Assert.Equal(["RejectX", "SettleX", "SettleX"], host.Recorder.Calls.Skip(3)
            .Select(call => call.Receiver).ToArray());
    }

    [Fact]
    public async Task ShouldFenceThroughAbortReceiversBeforeSettlementGivenAbortRequest()
    {
        // Arrange
        await using var host = new ProcessTestHost();
        await host.StartAsync("t1", "t2");
        host.Recorder.Script("ApplyA", _ =>
            Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "wait", true)));
        await host.RunAsync();
        await using var scope = host.Provider.CreateAsyncScope();
        var driver = scope.ServiceProvider.GetRequiredService<ProcessDriver>();

        // Act
        var aborting = await driver.AbortAsync(ProcessTestHost.Key);
        var record = await host.RunAsync();

        // Assert
        Assert.Equal(ProcessState.Aborting, aborting.State);
        Assert.Equal(ProcessState.Aborted, record.State);
        var afterAbort = host.Recorder.Calls.Skip(3).Select(c => c.Receiver).ToArray();
        Assert.Equal(["RejectX", "SettleX", "SettleX"], afterAbort);
        Assert.DoesNotContain(host.Recorder.Calls.Skip(3), c => c.Receiver is "ApplyA" or "ApplyB");
    }

    [Fact]
    public async Task ShouldRecordProducedFactByReceiverGivenAppliedOutcome()
    {
        // Arrange
        await using var host = new ProcessTestHost();
        await host.StartAsync("t1");

        // Act
        var record = await host.RunAsync();
        var stored = await host.Store.ReadAsync(ProcessTestHost.Key);

        // Assert
        Assert.Equal(["AcquireX", "ApplyA", "ApplyB", "SettleX"], record.Facts.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(record.Facts, stored!.Facts);
    }

    [Fact]
    public async Task ShouldRetryLaterGivenReceiverSourceNotProvableYet()
    {
        // Arrange
        var unprovable = true;
        var definition = ProcessTestHost.Definition() with
        {
            CreateReceiver = (step, _, _) => unprovable && step.Receiver == "ApplyA"
                ? throw new Bdgrz.Compliance.Features.SourceFacts.SourceFactVerificationException("lagging")
                : ValueTask.FromResult<IRequest<ReceiverOutcome>?>(new ProbeReceive(step.CommandId, step.Receiver,
                    step.Target)),
        };
        await using var host = new ProcessTestHost(definition: definition);
        await host.StartAsync("t1");

        // Act
        var waiting = await host.RunAsync();
        unprovable = false;
        var resumed = await host.RunAsync();

        // Assert
        Assert.Equal(ProcessState.Pending, waiting.State);
        Assert.Contains("not provable yet", waiting.Note, StringComparison.Ordinal);
        Assert.Equal(1, waiting.Attempts);
        Assert.Equal(ProcessState.Completed, resumed.State);
    }

    /// <summary>Decorates the store so one ReplaceAsync, chosen by count, fails after the receiver already ran.</summary>
    sealed class CrashOnceStore
    {
        int _remaining = int.MaxValue;

        public void ArmAfterReplaceCount(int successfulReplacesBeforeCrash) => _remaining = successfulReplacesBeforeCrash;

        public IProcessRecordStore Wrap(IProcessRecordStore inner) => new Decorator(inner, this);

        sealed class Decorator(IProcessRecordStore inner, CrashOnceStore owner) : IProcessRecordStore
        {
            public Task<ProcessRecord?> ReadAsync(ProcessKey key, CancellationToken ct = default) => inner.ReadAsync(key, ct);

            public Task<ProcessRecord> CreateIfAbsentAsync(ProcessRecord record, CancellationToken ct = default) =>
                inner.CreateIfAbsentAsync(record, ct);

            public Task<ProcessRecord> ReplaceAsync(ProcessRecord record, long expectedVersion, CancellationToken ct = default)
            {
                if (owner._remaining-- == 0)
                    throw new InvalidOperationException("Worker crashed before the CAS.");
                return inner.ReplaceAsync(record, expectedVersion, ct);
            }

            public Task<IReadOnlyList<ProcessKey>> ScanPendingAsync(string processKind, DateTimeOffset dueBefore, int limit,
                CancellationToken ct = default) => inner.ScanPendingAsync(processKind, dueBefore, limit, ct);
        }
    }

    [Fact]
    public async Task ShouldAbortThroughFenceAndSettleEveryTargetGivenRejectedOutcome()
    {
        // Arrange
        await using var host = new ProcessTestHost();
        await host.StartAsync("t1", "t2");
        host.Recorder.Script("AcquireX", command => command.Target == "t2"
            ? Result<ReceiverOutcome>.Success(new ReceiverOutcome.Rejected("stale source", null))
            : Result<ReceiverOutcome>.Success(new ReceiverOutcome.Applied(new(
                new EventStreamAddress("tenant-a", "targets", "t1"), command.CommandId, 1))));

        // Act
        var record = await host.RunAsync();

        // Assert
        Assert.Equal(ProcessState.Aborted, record.State);
        Assert.Equal(["AcquireX", "AcquireX", "RejectX", "SettleX", "SettleX"],
            host.Recorder.Calls.Select(c => c.Receiver).ToArray());
        Assert.DoesNotContain(host.Recorder.Calls, c => c.Receiver is "ApplyA" or "ApplyB");
        Assert.Contains("stale source", record.Note ?? "stale source", StringComparison.Ordinal);
    }
}
