using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Conditionally records an exact witness refusal only against a qualified target head.</summary>
public sealed class CriteriaWitnessRefusalCommitter(IDomainEventReader events, IAggregateReader aggregates,
    IAggregateExecutor executor, ICriteriaFreshEntryHead? fresh = null)
{
    /// <summary>Returns success only when the matching refusal fact is confirmed in its target source.</summary>
    public async ValueTask<Result> RefuseAsync(CriteriaPublicationStarted started, IExecutionContext context, CancellationToken ct)
    {
        if (fresh is null)
            return Pending();
        try
        {
            var edition = new CriteriaEdition(started.EditionId);
            var source = new CriteriaLabelSourceVerifier(events);
            var original = await source.FindAsync<CriteriaPublicationStarted>(edition.Stream, started.Metadata.EventId, ct).ConfigureAwait(false);
            if (original is null)
                return Pending();
            if (original.Metadata.AggregateId != edition.Id || original != started)
                return Result.Failure(new RequestError(RequestErrorKind.Validation, "The source publication start differs."));
            var owner = new CriteriaEntry(started.EditionId, started.WitnessIdentifier,
                selectedPublicationAttemptId: started.PublicationAttemptId);
            ulong next = 0;
            var activeAcceptance = Uuid.Empty;
            var selectedRefusal = Uuid.Empty;
            await foreach (var record in events.ReadAsync(owner.Stream, 0, ct).ConfigureAwait(false))
            {
                if (record.Stream != owner.Stream || record.ResourceOffset != next || record.Event.Metadata.AggregateId != owner.Id || record.Event.Metadata.IsAudit)
                    return Pending();
                next = checked(next + 1);
                if (record.Event is CriterionChanged changed)
                    activeAcceptance = changed.Metadata.EventId;
                if (record.Event is CriterionWitnessRefused refusal && refusal.PublicationAttemptId == started.PublicationAttemptId)
                    selectedRefusal = refusal.Metadata.EventId;
            }
            var hydrated = await aggregates.HydrateAsync(owner, ct).ConfigureAwait(false);
            if (hydrated.CommittedStreamPosition != next ||
                (hydrated.SelectedWitnessRefusal?.Metadata.EventId ?? Uuid.Empty) != selectedRefusal ||
                (hydrated.CurrentAcceptedEventId != Uuid.Empty && hydrated.CurrentAcceptedEventId != activeAcceptance) ||
                !await fresh.IsCurrentAsync(owner.Stream, owner.Id, next, ct).ConfigureAwait(false))
                return Pending();
            var decision = Uuid.Empty;
            var result = await executor.ExecuteAsync(new CriteriaEntry(started.EditionId, started.WitnessIdentifier,
                selectedPublicationAttemptId: started.PublicationAttemptId), target =>
            {
                if (target.CommittedStreamPosition != next)
                    return AggregateOutcome.Discard(Pending());
                var outcome = target.RefuseWitness(started);
                if (outcome.IsSuccess)
                    decision = target.SelectedWitnessRefusal?.Metadata.EventId ?? Uuid.Empty;
                return AggregateOutcome.CommitOnSuccess(outcome);
            }, context, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
                return result.Error!.Kind == RequestErrorKind.Conflict ? Pending() : result;
            if (decision == Uuid.Empty)
                return Pending();
            var fact = await source.FindAsync<CriterionWitnessRefused>(owner.Stream, decision, ct).ConfigureAwait(false);
            if (fact is null)
                return Pending();
            return fact.Metadata.AggregateId == owner.Id && fact.EditionId == started.EditionId &&
                fact.DraftGeneration == started.DraftGeneration && fact.Identifier == started.WitnessIdentifier &&
                fact.PublicationAttemptId == started.PublicationAttemptId && fact.AttemptOrdinal == started.AttemptOrdinal &&
                fact.IncarnationId == started.WitnessIncarnationId && fact.PublicationStartedEventId == started.Metadata.EventId
                ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Validation, "The witness refusal does not match its start."));
        }
        catch (EventStreamConcurrencyException) { return Pending(); }
        catch (FitzException) { return Pending(); }
        catch (IOException) { return Pending(); }
        catch (TimeoutException) { return Pending(); }
    }

    static Result Pending() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The exact witness target and durable refusal outcome are not qualified.", true));
}
