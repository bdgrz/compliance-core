using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Processes;

/// <summary>Independently reconciles one proposal without blocking a shared reactor checkpoint.</summary>
sealed class ReconcileCriteriaEntrySignalHandler(IDomainEventReader events, IImmutableContentStore content,
    ICriteriaExactSourceReader source, IAggregateReader aggregates, IAggregateExecutor executor,
    ICriteriaSourceCutCoverage? coverage = null, ICriteriaPublicationAuthority? publications = null,
    ICriteriaFreshEntryHead? freshHead = null, ICriteriaParticipantCausalOrder? causal = null) : IRequestHandler<ReconcileCriteriaEntrySignal, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ReconcileCriteriaEntrySignal> context,
        CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Failure(RequestErrorKind.Forbidden, "A trusted system worker is required.", false);
        if (coverage is null || publications is null)
            return Pending("Qualified criteria source authority is unavailable.");
        try
        {
            var signal = context.Request;
            var frozen = await content.ReadVerifiedAsync(signal.Source.Stream.Realm, signal.Intent, ct).ConfigureAwait(false);
            await using var reader = events.ReadAsync(signal.Source.Stream, signal.Position, ct).GetAsyncEnumerator(ct);
            if (!await reader.MoveNextAsync().ConfigureAwait(false))
                return Pending("The exact proposal has not been observed.");
            var physical = reader.Current;
            if (physical.Stream != signal.Source.Stream || physical.ResourceOffset != signal.Position ||
                physical.Event.Metadata.EventId != signal.Source.EventId ||
                physical.Event.Metadata.AggregateVersion != signal.Source.AggregateVersion || physical.Event.Metadata.IsAudit ||
                !CriteriaEntrySignal.TryAddress(physical.Event, out var expected, out var ownerId) ||
                expected != physical.Stream || physical.Event.Metadata.AggregateId != ownerId ||
                !frozen.Span.SequenceEqual(CriteriaEntrySignal.Payload(physical.Stream, physical.Event, physical.ResourceOffset)))
                return Failure(RequestErrorKind.Validation, "The frozen proposal signal differs from its exact source.", false);
            var proposal = await source.FindAsync(physical.Stream, ownerId, signal.Source.EventId, ct).ConfigureAwait(false);
            if (proposal is null)
                return Pending("The ordered proposal has not been observed.");
            if (proposal.ResourceOffset != physical.ResourceOffset || proposal.Event != physical.Event)
                return Failure(RequestErrorKind.Validation, "The ordered proposal differs from its frozen source.", false);

            var intent = CriteriaEntrySignal.Intent(proposal.Event)!;
            var target = new CriteriaEntry(intent.EditionId, intent.Identifier, intent.OperationId);
            var verification = new CriteriaEntryDecisionEvidenceVerifier(source, coverage, publications, causal);
            var history = await ReadTargetAsync(source, target, proposal, ct).ConfigureAwait(false);
            if (!history.FoundProposal)
                return Pending("The exact proposal is not visible in its owner.");
            if (history.Outcome is { } previous)
            {
                var verified = await verification.VerifyAsync(proposal, previous, ct).ConfigureAwait(false);
                return verified is CriteriaEntryDecisionVerification.Verified or CriteriaEntryDecisionVerification.Abandoned
                    ? Performed() : Pending("The prior outcome's source evidence is not yet qualified.");
            }
            if (freshHead is null)
                return Pending("The target head is unavailable for a new decision.");
            var decision = await new CriteriaEntryDecisionAuthority(source, coverage, publications, freshHead, causal)
                .DecideAsync(proposal, ct).ConfigureAwait(false);
            if (decision is null)
                return Pending("The entry's claim, parent or edition source is unresolved.");

            var owner = await aggregates.HydrateAsync(target, ct).ConfigureAwait(false);
            if (owner.CommittedStreamPosition != history.Next ||
                !await freshHead.IsCurrentAsync(target.Stream, target.Id, history.Next, ct).ConfigureAwait(false))
                return Pending("The target source head is not qualified.");

            var generation = proposal.Event switch
            {
                CriterionChangeProposed human => human.ExecutionGeneration,
                CriterionSeedProposed seed => seed.ExecutionGeneration,
                _ => 0L,
            };
            var applied = await executor.ExecuteAsync(new CriteriaEntry(intent.EditionId, intent.Identifier,
                intent.OperationId), current =>
            {
                if (current.CommittedStreamPosition != history.Next)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The entry target changed before its decision.", true)));
                return AggregateOutcome.CommitOnSuccess(current.Decide(intent.OperationId, generation,
                    proposal.Event.Metadata.EventId, decision.Accepted, decision.Reason, decision.Evidence));
            }, context, ct).ConfigureAwait(false);
            if (!applied.IsSuccess)
                return Pending("The target outcome is unresolved.");

            // A successful append response is not an independently replayable
            // proof. Re-read and verify the exact resulting event before completion.
            var after = await ReadTargetAsync(source, target, proposal, ct).ConfigureAwait(false);
            if (after.Outcome is null)
                return Pending("The committed outcome is not yet observable.");
            var checkedOutcome = await verification.VerifyAsync(proposal, after.Outcome, ct).ConfigureAwait(false);
            return checkedOutcome is CriteriaEntryDecisionVerification.Verified or CriteriaEntryDecisionVerification.Abandoned
                ? Performed() : Pending("The outcome's source evidence is not yet qualified.");
        }
        catch (CriteriaSourceUnavailableException) { return Pending("Criteria source coverage is unresolved."); }
        catch (ImmutableContentUnavailableException) { return Pending("The signal intent is unavailable."); }
        catch (ImmutableContentIntegrityException) { return Failure(RequestErrorKind.Validation, "The signal intent failed integrity verification.", false); }
        catch (EventStreamConcurrencyException) { return Pending("The target changed before its decision."); }
        catch (FitzException) { return Pending("The source or append outcome is unresolved."); }
        catch (IOException) { return Pending("The source or append outcome is unresolved."); }
        catch (TimeoutException) { return Pending("The source or append outcome is unresolved."); }
    }

    static async ValueTask<TargetHistory> ReadTargetAsync(ICriteriaExactSourceReader source, CriteriaEntry owner,
        CriteriaOrderedSourceEvent proposal, CancellationToken ct)
    {
        ulong next = 0;
        var found = false;
        CriteriaOrderedSourceEvent? outcome = null;
        await foreach (var item in source.ReadAsync(owner.Stream, owner.Id, ct).ConfigureAwait(false))
        {
            if (item.Stream != owner.Stream || item.ResourceOffset != next++ || item.Event.Metadata.AggregateId != owner.Id ||
                item.Event.Metadata.IsAudit || item.Event.Metadata.EventId == Uuid.Empty ||
                item.Event.Metadata.AggregateVersion != checked(item.ResourceOffset + 1))
                throw new InvalidOperationException("The entry target source is not contiguous.");
            if (item.Event.Metadata.EventId == proposal.Event.Metadata.EventId)
            {
                if (item.ResourceOffset != proposal.ResourceOffset || item.AreaOffset != proposal.AreaOffset || item.Event != proposal.Event)
                    throw new InvalidOperationException("The proposal differs between exact source reads.");
                found = true;
            }
            var outcomeProposal = item.Event switch
            {
                CriterionChanged accepted => accepted.ProposalEventId,
                CriterionChangeRefused refused => refused.ProposalEventId,
                _ => Uuid.Empty,
            };
            if (outcomeProposal == proposal.Event.Metadata.EventId)
            {
                if (outcome is not null)
                    throw new InvalidOperationException("A proposal has multiple terminal outcomes.");
                outcome = item;
            }
        }
        return new TargetHistory(next, found, outcome);
    }

    sealed record TargetHistory(ulong Next, bool FoundProposal, CriteriaOrderedSourceEvent? Outcome);
    static Result<ReceiverOutcome> Performed() =>
        Result<ReceiverOutcome>.Success(new ReceiverOutcome.Performed("criteria-entry-signal"));
    static Result<ReceiverOutcome> Pending(string message) => Failure(RequestErrorKind.Conflict, message, true);
    static Result<ReceiverOutcome> Failure(RequestErrorKind kind, string message, bool transient) =>
        Result<ReceiverOutcome>.Failure(new RequestError(kind, message, transient));
}
