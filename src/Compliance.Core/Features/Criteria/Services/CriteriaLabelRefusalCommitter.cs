using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Conditionally commits a claim refusal against a qualified exact source head.</summary>
public sealed class CriteriaLabelRefusalCommitter(IDomainEventReader events, IAggregateReader aggregates,
    IAggregateExecutor executor, ICriteriaFreshLabelClaimHead? head = null) : ICriteriaLabelRefusalCommitter
{
    /// <inheritdoc />
    public async ValueTask<Result> RefuseIfContestedAsync(string label, Uuid editionId, Uuid operationId,
        Uuid editionRequestEventId, ActorAttribution actor, IExecutionContext context, CancellationToken ct = default)
    {
        if (!EditionLabelClaim.TryNormalize(label, out var normalized) || label != normalized || editionId == Uuid.Empty ||
            operationId == Uuid.Empty || editionRequestEventId == Uuid.Empty || actor is null)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The exact label and edition intent are required."));
        if (head is null)
            return Pending();
        try
        {
            var edition = new CriteriaEdition(editionId);
            var request = await new CriteriaLabelSourceVerifier(events).FindAsync<DomainEvent>(edition.Stream,
                editionRequestEventId, ct).ConfigureAwait(false);
            if (request is null)
                return Pending();
            var matchesRequest = request.Metadata.AggregateId == edition.Id && (request switch
            {
                CriteriaEditionDraftRequested e => e.EditionId == editionId && e.OperationId == operationId && e.Label == label && e.Actor == actor,
                CriteriaInitialLabelRequested e => e.EditionId == editionId && e.OperationId == operationId && e.Label == label && e.Actor == actor,
                CriteriaEditionRenameRequested e => e.EditionId == editionId && e.OperationId == operationId && e.NewLabel == label && e.Actor == actor,
                _ => false,
            });
            if (!matchesRequest)
                return Result.Failure(new RequestError(RequestErrorKind.Validation, "The source does not match the exact edition intent."));
            var claim = new EditionLabelClaim(label, editionId, operationId);
            var snapshot = await ObserveAsync(claim, editionId, operationId, ct).ConfigureAwait(false);
            var hydrated = await aggregates.HydrateAsync(claim, ct).ConfigureAwait(false);
            if (hydrated.CommittedStreamPosition != snapshot.Next || hydrated.LastGeneration != snapshot.LastGeneration ||
                !SameReservation(hydrated.CurrentReservation, snapshot.Holder) ||
                !SameRefusal(hydrated.RefusalFor(editionId, operationId), snapshot.Refusal) ||
                !await head.IsCurrentAsync(claim.Stream, claim.Id, snapshot.Next, ct).ConfigureAwait(false))
                return Pending();

            if (snapshot.Refusal is { } prior &&
                (prior.EditionRequestEventId != editionRequestEventId || prior.RequestedBy != actor))
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The label operation is already bound to different refusal intent."));

            var generation = snapshot.Refusal?.RequestedGeneration ?? (snapshot.LastGeneration == long.MaxValue
                ? 0 : snapshot.LastGeneration + 1);
            if (generation <= 0)
                return Pending();
            var refusalId = Uuid.Empty;
            var result = await executor.ExecuteAsync(new EditionLabelClaim(label, editionId, operationId), owner =>
            {
                if (owner.CommittedStreamPosition != snapshot.Next)
                    return AggregateOutcome.Discard(Pending());
                var decision = owner.RefuseContested(editionId, operationId, generation, editionRequestEventId, actor);
                if (decision.IsSuccess)
                    refusalId = owner.RefusalFor(editionId, operationId)?.Metadata.EventId ?? Uuid.Empty;
                return AggregateOutcome.CommitOnSuccess(decision);
            }, context, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
                return result.Error!.Kind == RequestErrorKind.Conflict ? Pending() : result;
            if (refusalId == Uuid.Empty)
                return Pending();
            var verifier = new CriteriaLabelRefusalVerifier(events, new CriteriaLabelSourceVerifier(events));
            var evidence = await verifier.VerifyAsync(label, refusalId, ct).ConfigureAwait(false);
            if (evidence is null)
                return Pending();
            var refusal = evidence.Refusal;
            return refusal.EditionId == editionId && refusal.OperationId == operationId &&
                   refusal.EditionRequestEventId == editionRequestEventId && refusal.RequestedBy == actor &&
                   refusal.RequestedGeneration == generation
                ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "The committed claim refusal does not match the exact intent."));
        }
        catch (EventStreamConcurrencyException) { return Pending(); }
        catch (CriteriaSourceUnavailableException) { return Pending(); }
        catch (FitzException) { return Pending(); }
        catch (IOException) { return Pending(); }
        catch (TimeoutException) { return Pending(); }
    }

    async ValueTask<Snapshot> ObserveAsync(EditionLabelClaim claim, Uuid editionId, Uuid operationId, CancellationToken ct)
    {
        ulong expected = 0;
        long generation = 0;
        EditionLabelReserved? holder = null;
        EditionLabelReservationRefused? refusal = null;
        await foreach (var record in events.ReadAsync(claim.Stream, 0, ct).ConfigureAwait(false))
        {
            if (record.Stream != claim.Stream || record.ResourceOffset != expected ||
                record.Event.Metadata.AggregateId != claim.Id || record.Event.Metadata.IsAudit)
                throw new InvalidOperationException("The label source is not a contiguous exact claim history.");
            expected = checked(expected + 1);
            switch (record.Event)
            {
                case EditionLabelReserved reserved:
                    if (holder is not null || generation == long.MaxValue || reserved.Generation != generation + 1 || reserved.Label != claim.Label)
                        throw new InvalidOperationException("The label reservation history is inconsistent.");
                    holder = reserved;
                    generation = reserved.Generation;
                    break;
                case EditionLabelReleased released:
                    if (holder is null || released.Label != claim.Label || released.EditionId != holder.EditionId ||
                        released.OperationId != holder.OperationId || released.Generation != holder.Generation)
                        throw new InvalidOperationException("The label release does not match the live holder.");
                    holder = null;
                    break;
                case EditionLabelReservationRefused refused when refused.EditionId == editionId && refused.OperationId == operationId:
                    if (refusal is not null)
                        throw new InvalidOperationException("The selected request has multiple refusal decisions.");
                    refusal = refused;
                    break;
            }
        }
        return new Snapshot(expected, generation, holder, refusal);
    }

    static Result Pending() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The exact current label claim or durable refusal outcome is unavailable.", true));

    static bool SameReservation(EditionLabelReserved? first, EditionLabelReserved? second) =>
        first is null ? second is null : second is not null && first.Label == second.Label &&
        first.EditionId == second.EditionId && first.OperationId == second.OperationId && first.Generation == second.Generation &&
        first.EditionRequestEventId == second.EditionRequestEventId && first.RequestedBy == second.RequestedBy &&
        first.Metadata.EventId == second.Metadata.EventId && first.Metadata.AggregateVersion == second.Metadata.AggregateVersion;

    internal static bool SameRefusal(EditionLabelReservationRefused? first, EditionLabelReservationRefused? second) =>
        first is null ? second is null : second is not null && first.Label == second.Label &&
        first.EditionId == second.EditionId && first.OperationId == second.OperationId &&
        first.RequestedGeneration == second.RequestedGeneration && first.EditionRequestEventId == second.EditionRequestEventId &&
        first.RequestedBy == second.RequestedBy && first.HolderEditionId == second.HolderEditionId &&
        first.HolderOperationId == second.HolderOperationId && first.HolderGeneration == second.HolderGeneration &&
        first.HolderReservationEventId == second.HolderReservationEventId && first.Metadata.EventId == second.Metadata.EventId &&
        first.Metadata.AggregateVersion == second.Metadata.AggregateVersion;

    sealed record Snapshot(ulong Next, long LastGeneration, EditionLabelReserved? Holder, EditionLabelReservationRefused? Refusal);
}
