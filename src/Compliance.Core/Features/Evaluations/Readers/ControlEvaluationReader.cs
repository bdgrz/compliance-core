using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.History;
using Bdgrz.Compliance.Features.Evaluations.Projections;
using Bdgrz.Compliance.Features.Evaluations.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Events;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Evaluations.Readers;

/// <summary>
///     Reads evaluations from their own owner streams. A program's evaluations are discovered from the committed
///     facts of the tenant's evaluation area, never from a projection, and retest relationships are composed from the
///     retests' own start facts, each verified against the exact accepting review of its predecessor. Every step,
///     submission and review is verified immutable content.
/// </summary>
public sealed class ControlEvaluationReader(IAggregateReader reader, IDomainEventReader events,
    IImmutableContentStore content)
{
    /// <summary>Opens one evaluation stream and folds it, optionally selecting a logical operation.</summary>
    internal async ValueTask<(ControlEvaluation Owner, ControlEvaluationState? State)> ReadAsync(Uuid tenantId,
        Uuid evaluationId, Uuid selectedLogicalId = default, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new ControlEvaluation(tenantId, evaluationId, selectedLogicalId), ct)
            .ConfigureAwait(false);
        return (owner, await ReadStateAsync(owner, ct).ConfigureAwait(false));
    }

    internal async ValueTask<ControlEvaluationState?> ReadStateAsync(ControlEvaluation owner, CancellationToken ct)
    {
        var facts = await ControlEvaluationHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
        return await ControlEvaluationFold.ReadAsync(facts, content, owner.TenantId, ct).ConfigureAwait(false);
    }

    /// <summary>The evaluation of a program and control with its retests composed, or null when there is none.</summary>
    public async ValueTask<ControlEvaluationView?> ReadViewAsync(Uuid tenantId, Uuid programId, Uuid controlId,
        Uuid evaluationId, CancellationToken ct = default)
    {
        var (owner, state) = await ReadAsync(tenantId, evaluationId, default, ct).ConfigureAwait(false);
        if (state is null || !owner.Belongs(programId, controlId))
            return null;
        var retests = new List<ControlEvaluationState>();
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                           ControlEvaluation.Area), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit || record.Event is not ControlEvaluationStarted started ||
                started.RetestOfEvaluationId != evaluationId || started.ProgramId != programId)
                continue;
            var (_, retest) = await ReadAsync(tenantId, started.EvaluationId, default, ct).ConfigureAwait(false);
            retests.Add(Verified(retest!, state));
        }

        return state.ToView(retests);
    }

    /// <summary>Reads every evaluation of the program with the fence of the facts it was folded from.</summary>
    public async ValueTask<ControlEvaluationSnapshot> ReadProgramAsync(Uuid tenantId, Uuid programId,
        CancellationToken ct = default)
    {
        var (sequence, fence) = await ReadStatesAsync(tenantId, programId, null, ct).ConfigureAwait(false);
        var states = sequence.ToDictionary(static state => state.EvaluationId);
        var retests = new Dictionary<Uuid, List<ControlEvaluationState>>();
        foreach (var state in sequence.Where(static state => state.RetestOfEvaluationId is not null))
        {
            var predecessor = states.GetValueOrDefault(state.RetestOfEvaluationId!.Value) ??
                              throw new InvalidOperationException("A retest names an evaluation that does not exist.");
            if (!retests.TryGetValue(predecessor.EvaluationId, out var list))
                retests[predecessor.EvaluationId] = list = [];
            list.Add(Verified(state, predecessor));
        }

        return new ControlEvaluationSnapshot([.. sequence.Select(state =>
            state.ToView(retests.GetValueOrDefault(state.EvaluationId) ?? []))], fence);
    }

    /// <summary>
    ///     The folded state of every evaluation of the program in the order of their first facts. When
    ///     <paramref name="receiptCut" /> is given, only its authenticated broker-receipt prefix is folded.
    ///     Without that explicit close cut, <paramref name="through" /> preserves the Type I occurrence-date selection.
    ///     The fence always covers every fact of the program, so a later write changes it.
    /// </summary>
    internal ValueTask<(IReadOnlyList<ControlEvaluationState> States, SourceFence Fence)> ReadStatesAsync(
        Uuid tenantId, Uuid programId, DateTimeOffset? through, CancellationToken ct = default) =>
        ReadStatesAsync(tenantId, programId, through, null, ct);

    internal async ValueTask<(IReadOnlyList<ControlEvaluationState> States, SourceFence Fence)> ReadStatesAsync(
        Uuid tenantId, Uuid programId, DateTimeOffset? through, RecordedSourceReceiptCut? receiptCut, CancellationToken ct = default)
    {
        var order = new List<string>();
        var streams = new Dictionary<string, List<DomainEvent>>(StringComparer.Ordinal);
        var facts = 0L;
        Uuid? last = null;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                           ControlEvaluation.Area), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            var key = record.Stream.Resource;
            if (!streams.TryGetValue(key, out var stream))
            {
                stream = [];
                streams[key] = stream;
                order.Add(key);
            }

            facts++;
            last = record.Event.Metadata.EventId;
            if (receiptCut is not null)
            {
                if (receiptCut.TenantId != tenantId || receiptCut.RecordedThrough != through)
                    throw new ImmutableContentIntegrityException("The evaluation receipt selection has another exact scope or cutoff.");
                if (!await receiptCut.IncludesAsync(record, ct).ConfigureAwait(false))
                    continue;
            }
            else if (through is { } limit && record.Event.Metadata.OccurredOn > limit)
                continue;
            stream.Add(record.Event);
        }

        var sequence = new List<ControlEvaluationState>();
        foreach (var key in order)
        {
            var state = await ControlEvaluationFold.ReadAsync(streams[key], content, tenantId, ct)
                .ConfigureAwait(false);
            if (state is not null && state.ProgramId == programId)
                sequence.Add(state);
        }

        return (sequence, new SourceFence(facts, last));
    }

    /// <summary>A control's evaluations, newest first.</summary>
    public async ValueTask<IReadOnlyList<ControlEvaluationView>> ReadControlAsync(Uuid tenantId, Uuid programId,
        Uuid controlId, CancellationToken ct = default) =>
        (await ReadProgramAsync(tenantId, programId, ct).ConfigureAwait(false)).ReadControl(controlId);

    /// <summary>
    ///     A retest's lineage must name the exact accepting review of its predecessor: the event id and aggregate
    ///     version recorded when the retest started. Anything else fails closed.
    /// </summary>
    static ControlEvaluationState Verified(ControlEvaluationState retest, ControlEvaluationState predecessor)
    {
        if (retest.RetestPredecessor is not { } proof || predecessor.AcceptingFact is not { } accepting ||
            proof.Stream.Resource != predecessor.EvaluationId.ToString() || proof.EventId != accepting.EventId ||
            proof.AggregateVersion != accepting.AggregateVersion || retest.ControlId != predecessor.ControlId)
            throw new InvalidOperationException(
                "A retest does not name the exact accepting review of its predecessor evaluation.");
        return retest;
    }
}
