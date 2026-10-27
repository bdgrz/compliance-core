using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Processes;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Reactors;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Processes.Processes;

/// <summary>
/// Dispatches the receivers of a process record in stage order under a worker lease, advancing the record by CAS
/// after every receiver outcome. Transient failures set the next due time; conflicting intent or an invalid proof
/// quarantines; supersession completes; an abort fences through the abort receivers before settle/release stages.
/// Receiver command ids are deterministic from the source fact, receiver, target and episode, so a crash after a
/// receiver succeeded and before the CAS replays the identical command on resume.
/// </summary>
public sealed class ProcessDriver(IProcessRecordStore store, IRequestBus bus, ProcessCatalog catalog,
    TimeProvider clock, IServiceProvider services)
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

            IRequest<Features.Processes.Models.ReceiverOutcome>? command;
            try
            {
                command = await definition.CreateReceiver(step, services, ct).ConfigureAwait(false);
            }
            catch (SourceFactVerificationException exception)
            {
                // The facts a receiver is built from are not readable or not provable yet; broker reads can lag.
                return await store.ReplaceAsync(record with
                {
                    Attempts = record.Attempts + 1,
                    Lease = null,
                    NextDueAt = now + RetryDelay,
                    Note = $"{step.Receiver}: source not provable yet: {exception.Message}",
                }, record.Version, ct).ConfigureAwait(false);
            }
            catch (ImmutableContentIntegrityException exception)
            {
                var unavailable = exception is ImmutableContentUnavailableException;
                return await store.ReplaceAsync(record with
                {
                    Attempts = record.Attempts + 1,
                    Lease = null,
                    State = unavailable ? record.State : ProcessState.Quarantined,
                    NextDueAt = unavailable ? now + RetryDelay : record.NextDueAt,
                    Note = $"{step.Receiver}: content cannot be verified: {exception.Message}",
                }, record.Version, ct).ConfigureAwait(false);
            }

            if (command is null)
            {
                record = await Advance(record, step, definition, null, ct).ConfigureAwait(false);
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

            // An already fenced or settled receiver completes only its cleanup step; every remaining cleanup
            // target still needs its exact fence/settlement before the process can become terminal.
            if (result.Value is Features.Processes.Models.ReceiverOutcome.Superseded superseded &&
                record.State != ProcessState.Aborting && step.Stage is not ("settle" or "release"))
            {
                record = await store.ReplaceAsync(record with
                {
                    State = ProcessState.Completed,
                    Lease = null,
                    Note = $"{step.Receiver} superseded: {superseded.Reason}",
                }, record.Version, ct).ConfigureAwait(false);
                break;
            }

            if (result.Value is Features.Processes.Models.ReceiverOutcome.Rejected rejected)
            {
                // The operation can never proceed: fence through the abort receivers, then settle what was held.
                record = await store.ReplaceAsync(record with
                {
                    State = record.State == ProcessState.Aborting ? ProcessState.Quarantined : ProcessState.Aborting,
                    AbortIndex = record.State == ProcessState.Aborting ? record.AbortIndex : 0,
                    Attempts = 0,
                    Lease = null,
                    NextDueAt = now,
                    Note = $"{step.Receiver} rejected: {rejected.Reason}",
                }, record.Version, ct).ConfigureAwait(false);
                if (record.State == ProcessState.Quarantined)
                    break;
                continue;
            }

            record = await Advance(record, step, definition, result.Value switch
            {
                Features.Processes.Models.ReceiverOutcome.Applied applied => applied.Fact,
                Features.Processes.Models.ReceiverOutcome.Replayed replayed => replayed.Fact,
                _ => null,
            }, ct).ConfigureAwait(false);
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
        NativeFactReference? produced, CancellationToken ct)
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
            next = NextCursor(record, stage, stageIndex, receiverIndex, step.Target);
        }

        if (produced is not null)
            next = next with
            {
                Facts = new Dictionary<string, NativeFactReference>(next.Facts, StringComparer.Ordinal)
                {
                    [ProcessRecord.FactKey(step.Receiver, step.Target)] = produced,
                },
            };
        return await store.ReplaceAsync(next with { Attempts = 0, Note = null }, record.Version, ct).ConfigureAwait(false);
    }

    /// <summary>Moves the cursor past the step just completed, receiver by receiver or target by target.</summary>
    static ProcessRecord NextCursor(ProcessRecord record, ProcessStageDefinition stage, int stageIndex,
        int receiverIndex, string? target)
    {
        var targetIndex = target is null ? record.Targets.Count : record.Targets.ToList().IndexOf(target) + 1;
        if (stage.IsInterleaved && target is not null)
        {
            // Targets are the outer loop: every receiver sees a target before the next target is offered.
            if (receiverIndex + 1 < stage.Receivers.Count)
                return record with { StageIndex = stageIndex, ReceiverIndex = receiverIndex + 1, TargetIndex = targetIndex - 1 };
            return targetIndex < record.Targets.Count
                ? record with { StageIndex = stageIndex, ReceiverIndex = 0, TargetIndex = targetIndex }
                : record with { StageIndex = stageIndex + 1, ReceiverIndex = 0, TargetIndex = 0 };
        }

        return stage.IsPerTarget && targetIndex < record.Targets.Count
            ? record with { StageIndex = stageIndex, ReceiverIndex = receiverIndex, TargetIndex = targetIndex }
            : receiverIndex + 1 < stage.Receivers.Count
                ? record with { StageIndex = stageIndex, ReceiverIndex = receiverIndex + 1, TargetIndex = 0 }
                : record with { StageIndex = stageIndex + 1, ReceiverIndex = 0, TargetIndex = 0 };
    }
}
