using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Processes;

/// <summary>
/// Dispatches the receivers of a process record in stage order under a worker lease, advancing the record by CAS
/// after every receiver outcome. Transient failures set the next due time; conflicting intent or an invalid proof
/// quarantines; supersession completes; an abort fences through the abort receivers before settle/release stages.
/// Receiver command ids are deterministic from the source fact, receiver, target and episode, so a crash after a
/// receiver succeeded and before the CAS replays the identical command on resume.
/// </summary>
public sealed class ProcessDriver(IProcessRecordStore store, IRequestBus bus, ProcessCatalog catalog,
    TimeProvider clock)
{
    /// <summary>How long one worker holds a record before another may resume it.</summary>
    public static TimeSpan LeaseDuration { get; } = TimeSpan.FromSeconds(30);

    /// <summary>Delay before a transiently failed step is due again.</summary>
    public static TimeSpan RetryDelay { get; } = TimeSpan.FromSeconds(15);

    /// <summary>Resumes the record until it is terminal, waits on a transient failure, or loses its lease.</summary>
    public async Task<ProcessRecord> RunAsync(ProcessKey key, string worker, IExecutionContext parent,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(worker);
        ArgumentNullException.ThrowIfNull(parent);
        var definition = catalog.Get(key.ProcessKind);
        var record = await store.ReadAsync(key, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Process {key} does not exist.");
        while (!record.IsTerminal)
        {
            ct.ThrowIfCancellationRequested();
            var now = clock.GetUtcNow();
            if (record.Lease is { } lease && lease.Worker != worker && lease.Until > now)
                return record;
            record = await store.ReplaceAsync(record with { Lease = new ProcessLease(worker, now + LeaseDuration) },
                record.Version, ct).ConfigureAwait(false);
            var step = NextStep(definition, record);
            if (step is null)
            {
                record = await store.ReplaceAsync(record with
                {
                    State = record.State == ProcessState.Aborting ? ProcessState.Aborted : ProcessState.Completed,
                    Lease = null,
                }, record.Version, ct).ConfigureAwait(false);
                break;
            }

            var command = definition.CreateReceiver(step);
            if (command is null)
            {
                record = await Advance(record, step, definition, ct).ConfigureAwait(false);
                continue;
            }

            var result = await bus.SendAsync(command, parent, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                var error = result.Error!;
                record = await store.ReplaceAsync(record with
                {
                    Attempts = record.Attempts + 1,
                    Lease = null,
                    State = error.IsTransient ? record.State : ProcessState.Quarantined,
                    NextDueAt = error.IsTransient ? now + RetryDelay : record.NextDueAt,
                    Note = $"{step.Receiver}: {error.Kind} {error.Message}",
                }, record.Version, ct).ConfigureAwait(false);
                return record;
            }

            if (result.Value is Features.Processes.ReceiverOutcome.Superseded superseded)
            {
                record = await store.ReplaceAsync(record with
                {
                    State = ProcessState.Completed,
                    Lease = null,
                    Note = $"{step.Receiver} superseded: {superseded.Reason}",
                }, record.Version, ct).ConfigureAwait(false);
                break;
            }

            record = await Advance(record, step, definition, ct).ConfigureAwait(false);
        }

        return record;
    }

    /// <summary>Requests an abort: abort receivers fence first, then only settle/release stages run.</summary>
    public async Task<ProcessRecord> AbortAsync(ProcessKey key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        var record = await store.ReadAsync(key, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Process {key} does not exist.");
        if (record.IsTerminal || record.State == ProcessState.Aborting)
            return record;
        return await store.ReplaceAsync(record with
        {
            State = ProcessState.Aborting,
            NextDueAt = clock.GetUtcNow(),
            Note = "abort requested",
        }, record.Version, ct).ConfigureAwait(false);
    }

    /// <summary>The next (stage, receiver, target) to dispatch, or null when the record is finished.</summary>
    public static ProcessStep? NextStep(ProcessDefinition definition, ProcessRecord record)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(record);
        if (record.State == ProcessState.Aborting && record.AbortIndex < definition.Abort.Count)
            return Step(record, "abort", definition.Abort[record.AbortIndex], null);
        for (var stage = record.StageIndex; stage < definition.Stages.Count; stage++)
        {
            var current = definition.Stages[stage];
            if (record.State == ProcessState.Aborting && !current.RunsAfterAbort)
                continue;
            var receiver = stage == record.StageIndex ? record.ReceiverIndex : 0;
            if (receiver >= current.Receivers.Count)
                continue;
            if (!current.IsPerTarget || record.Targets.Count == 0)
                return Step(record, current.Stage, current.Receivers[receiver], null);
            var target = stage == record.StageIndex && receiver == record.ReceiverIndex ? record.TargetIndex : 0;
            return Step(record, current.Stage, current.Receivers[receiver], record.Targets[target]);
        }

        return null;
    }

    static ProcessStep Step(ProcessRecord record, string stage, string receiver, string? target) =>
        new(record, stage, receiver, target,
            EffectIdentity.CommandId(record.Source.EventId, $"{record.Key.ProcessKind}:{receiver}", target ?? "-",
                record.Episode));

    async Task<ProcessRecord> Advance(ProcessRecord record, ProcessStep step, ProcessDefinition definition,
        CancellationToken ct)
    {
        ProcessRecord next;
        if (step.Stage == "abort")
        {
            next = record with { AbortIndex = record.AbortIndex + 1 };
        }
        else
        {
            var stageIndex = definition.Stages.ToList().FindIndex(s => s.Stage == step.Stage);
            var stage = definition.Stages[stageIndex];
            var receiverIndex = stage.Receivers.ToList().IndexOf(step.Receiver);
            var targetIndex = step.Target is null ? record.Targets.Count : record.Targets.ToList().IndexOf(step.Target) + 1;
            next = stage.IsPerTarget && targetIndex < record.Targets.Count
                ? record with { StageIndex = stageIndex, ReceiverIndex = receiverIndex, TargetIndex = targetIndex }
                : receiverIndex + 1 < stage.Receivers.Count
                    ? record with { StageIndex = stageIndex, ReceiverIndex = receiverIndex + 1, TargetIndex = 0 }
                    : record with { StageIndex = stageIndex + 1, ReceiverIndex = 0, TargetIndex = 0 };
        }

        return await store.ReplaceAsync(next with { Attempts = 0, Note = null }, record.Version, ct).ConfigureAwait(false);
    }
}
