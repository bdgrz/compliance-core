using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Features.NativeOperations;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Processes;

public sealed class ProcessRootReactionTests
{
    static readonly EventStreamAddress Stream = ProcessTestHost.Source.Stream;
    static readonly DateTimeOffset At = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldCreateRecordAndDriveItGivenCommittedRootFact()
    {
        // Arrange
        await using var host = new ProcessTestHost();
        await using var scope = host.Provider.CreateAsyncScope();
        var reaction = scope.ServiceProvider.GetRequiredService<ProcessRootReaction>();

        // Act
        var record = await reaction.StartAsync(ProcessTestHost.Process, Context(Admitted()), ProcessTestHost.Intent,
            ["t2", "t1"], "w1");

        // Assert
        Assert.Equal(ProcessState.Completed, record.State);
        Assert.Equal(ProcessTestHost.Root, record.Key.RootOperationId);
        Assert.Equal(ProcessTestHost.SourceEvent, record.Source.EventId);
        Assert.Equal(["t1", "t2"], record.Targets);
        Assert.Equal(record with { Lease = null }, (await host.Store.ReadAsync(ProcessTestHost.Key))! with { Lease = null });
    }

    [Fact]
    public async Task ShouldReturnExistingRecordWithoutRedispatchGivenReplayedRootFact()
    {
        // Arrange
        await using var host = new ProcessTestHost();
        await using var scope = host.Provider.CreateAsyncScope();
        var reaction = scope.ServiceProvider.GetRequiredService<ProcessRootReaction>();
        await reaction.StartAsync(ProcessTestHost.Process, Context(Admitted()), ProcessTestHost.Intent, ["t1"], "w1");
        var calls = host.Recorder.Calls.Count;

        // Act
        var replayed = await reaction.StartAsync(ProcessTestHost.Process, Context(Admitted()), ProcessTestHost.Intent,
            ["t1"], "w2");

        // Assert
        Assert.Equal(ProcessState.Completed, replayed.State);
        Assert.Equal(calls, host.Recorder.Calls.Count);
    }

    [Fact]
    public async Task ShouldKeepSeparateProcessesGivenSameLogicalIdOnDifferentNativeOwners()
    {
        // Arrange
        await using var host = new ProcessTestHost();
        await using var scope = host.Provider.CreateAsyncScope();
        var reaction = scope.ServiceProvider.GetRequiredService<ProcessRootReaction>();
        var first = await reaction.StartAsync(ProcessTestHost.Process, Context(Admitted()), ProcessTestHost.Intent,
            ["t1"], "w1");
        var calls = host.Recorder.Calls.Count;
        var other = new EventStreamAddress(Stream.Realm, Stream.Area, "p2");

        // Act
        var second = await reaction.StartAsync(ProcessTestHost.Process,
            Context(Admitted(other, Uuid.CreateVersion4()), other), ProcessTestHost.Intent, ["t2"], "w2");

        // Assert
        Assert.Equal(first.Key.RootOperationId, second.Key.RootOperationId);
        Assert.NotEqual(first.Key, second.Key);
        Assert.Equal(other, second.Source.Stream);
        Assert.Equal(calls * 2, host.Recorder.Calls.Count);
    }

    [Fact]
    public async Task ShouldFailNonTransientlyGivenSourceThatIsNotANativeFact()
    {
        // Arrange
        await using var host = new ProcessTestHost();
        await using var scope = host.Provider.CreateAsyncScope();
        var reaction = scope.ServiceProvider.GetRequiredService<ProcessRootReaction>();
        var edited = new ProbeEdited(Uuid.CreateVersion4(), "p");
        edited.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, At));

        // Act
        var failure = await Record.ExceptionAsync(() => reaction.StartAsync(ProcessTestHost.Process, Context(edited),
            ProcessTestHost.Intent, ["t1"], "w1"));

        // Assert
        var reactionFailure = Assert.IsType<ReactionCommandFailedException>(failure);
        Assert.False(reactionFailure.Error.IsTransient);
        Assert.Null(await host.Store.ReadAsync(ProcessTestHost.Key));
    }

    [Fact]
    public async Task ShouldFailTransientlyWithoutCheckpointGivenRecordStoreFailure()
    {
        // Arrange
        await using var host = new ProcessTestHost(_ => new FailingStore());
        await using var scope = host.Provider.CreateAsyncScope();
        var reaction = scope.ServiceProvider.GetRequiredService<ProcessRootReaction>();

        // Act
        var failure = await Record.ExceptionAsync(() => reaction.StartAsync(ProcessTestHost.Process, Context(Admitted()),
            ProcessTestHost.Intent, ["t1"], "w1"));

        // Assert
        var reactionFailure = Assert.IsType<ReactionCommandFailedException>(failure);
        Assert.True(reactionFailure.Error.IsTransient);
        Assert.Empty(host.Recorder.Calls);
    }

    static ProbeAdmitted Admitted(EventStreamAddress? source = null, Uuid? eventId = null)
    {
        var owner = source ?? Stream;
        var provenance = new OperationProvenance(ProcessTestHost.Root, ProcessTestHost.Root, null, "Admit",
            new ActorReference("member", "m1", "Member One"), owner, "initial", 0, new string('d', 64), At, owner, owner);
        var fact = new ProbeAdmitted(Uuid.CreateVersion4(), "p", provenance, ProcessTestHost.Intent);
        fact.AttachMetadata(new DomainEventMetadata(eventId ?? ProcessTestHost.SourceEvent, Uuid.CreateVersion4(), 1, At));
        return fact;
    }

    static FakeReactorContext Context(DomainEvent source, EventStreamAddress? stream = null) => new(new DomainEventRecord(stream ?? Stream, source, 0,
        new EventCursor("1")));

    sealed class FakeReactorContext(DomainEventRecord source) : IReactorContext
    {
        public DomainEventRecord Source => source;

        public ClaimsPrincipal Actor => RequestActor.System;

        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();

        public Uuid CorrelationId { get; } = Uuid.CreateVersion4();

        public Uuid CauseId => source.Event.Metadata.EventId;

        public DateTimeOffset StartedAt => At;
    }

    sealed class FailingStore : IProcessRecordStore
    {
        public Task<ProcessRecord?> ReadAsync(ProcessKey key, CancellationToken ct = default) =>
            Task.FromResult<ProcessRecord?>(null);

        public Task<ProcessRecord> CreateIfAbsentAsync(ProcessRecord record, CancellationToken ct = default) =>
            throw new TimeoutException("KV unavailable.");

        public Task<ProcessRecord> ReplaceAsync(ProcessRecord record, long expectedVersion, CancellationToken ct = default) =>
            throw new TimeoutException("KV unavailable.");

        public Task<IReadOnlyList<ProcessKey>> ScanPendingAsync(string processKind, DateTimeOffset dueBefore, int limit,
            CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ProcessKey>>([]);
    }
}
