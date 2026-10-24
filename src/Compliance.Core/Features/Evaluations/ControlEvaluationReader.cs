using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

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
        return (owner, await ControlEvaluationFold.ReadAsync(owner.Facts, content, tenantId, ct)
            .ConfigureAwait(false));
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
    ///     <paramref name="through" /> is given, only facts committed at or before it are folded; the fence always
    ///     covers every fact of the program, so a later write changes it.
    /// </summary>
    internal async ValueTask<(IReadOnlyList<ControlEvaluationState> States, SourceFence Fence)> ReadStatesAsync(
        Uuid tenantId, Uuid programId, DateTimeOffset? through, CancellationToken ct = default)
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
            if (through is { } limit && record.Event.Metadata.OccurredOn > limit)
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
