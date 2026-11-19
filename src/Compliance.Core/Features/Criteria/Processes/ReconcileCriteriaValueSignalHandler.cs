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

/// <summary>Independently retries one exact proposal until its source-qualified terminal cut is known.</summary>
sealed class ReconcileCriteriaValueSignalHandler(IDomainEventReader events, IImmutableContentStore content, ICriteriaExactSourceReader source,
    IAggregateReader aggregates, IAggregateExecutor executor, ICriteriaSourceCutCoverage? coverage = null,
    ICriteriaPublicationAuthority? publications = null, ICriteriaFreshEntryHead? freshHead = null,
    ICriteriaParticipantCausalOrder? causalOrder = null)
    : IRequestHandler<ReconcileCriteriaValueSignal, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ReconcileCriteriaValueSignal> context,
        CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Failure(RequestErrorKind.Forbidden, "A trusted system worker is required.", false);
        if (coverage is null || publications is null || freshHead is null || causalOrder is null)
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
                physical.Event is not CriteriaEditionValueProposed proposal || proposal.Metadata.EventId != signal.Source.EventId ||
                proposal.Metadata.AggregateVersion != signal.Source.AggregateVersion || proposal.Metadata.IsAudit ||
                !CriteriaValueSignal.TryAddress(proposal, out var expected, out var ownerId) ||
                expected != physical.Stream || proposal.Metadata.AggregateId != ownerId ||
                !frozen.Span.SequenceEqual(CriteriaValueSignal.Payload(physical.Stream, proposal, physical.ResourceOffset)))
                return Failure(RequestErrorKind.Validation, "The frozen proposal signal differs from its exact source.", false);
            var record = await source.FindAsync(signal.Source.Stream, ownerId, signal.Source.EventId, ct).ConfigureAwait(false);
            if (record is null)
                return Pending("The ordered proposal has not been observed.");
            if (record.ResourceOffset != physical.ResourceOffset || record.Event != proposal)
                return Failure(RequestErrorKind.Validation, "The ordered proposal differs from its frozen source.", false);

            var intent = proposal.Intent;
            var address = new CriteriaEditionValue(intent.EditionId, intent.TargetKind, intent.Field, intent.Category,
                intent.Code, intent.OperationId);
            var verifier = new CriteriaEditionValueDecisionEvidenceVerifier(source, coverage, publications, freshHead, causalOrder);
            var history = await ReadTargetAsync(source, address, record, ct).ConfigureAwait(false);
            if (!history.FoundProposal)
                return Pending("The exact proposal is not visible in its owner.");
            if (history.Outcome is { } previous)
            {
                var verified = await verifier.VerifyAsync(record, previous, ct).ConfigureAwait(false);
                return verified is CriteriaEntryDecisionVerification.Verified or CriteriaEntryDecisionVerification.Abandoned
                    ? Performed() : Pending("The prior outcome source evidence is not yet qualified.");
            }
            var authority = new CriteriaEditionValueDecisionAuthority(source, coverage, publications, freshHead, causalOrder);
            var decision = await authority.DecideAsync(record, ct).ConfigureAwait(false);
            if (decision is null)
                return Pending("The source ordering or edition head is unresolved.");
            var owner = await aggregates.HydrateAsync(address, ct).ConfigureAwait(false);
            if (owner.CommittedStreamPosition != history.Next ||
                !await freshHead.IsCurrentAsync(address.Stream, address.Id, history.Next, ct).ConfigureAwait(false))
                return Pending("The target source head is not qualified.");
            var applied = await executor.ExecuteAsync(new CriteriaEditionValue(intent.EditionId, intent.TargetKind,
                intent.Field, intent.Category, intent.Code, intent.OperationId), target =>
            {
                if (target.CommittedStreamPosition != history.Next)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The value target changed before its decision.", true)));
                return AggregateOutcome.CommitOnSuccess(target.Decide(intent.OperationId, proposal.ExecutionGeneration,
                    proposal.Metadata.EventId, decision.Accepted, decision.Reason, decision.Evidence));
            }, context, ct).ConfigureAwait(false);
            if (!applied.IsSuccess)
                return Pending("The exact target outcome is unresolved.");
            var after = await ReadTargetAsync(source, address, record, ct).ConfigureAwait(false);
            if (after.Outcome is null)
                return Pending("The committed outcome is not yet observable.");
            var checkedOutcome = await verifier.VerifyAsync(record, after.Outcome, ct).ConfigureAwait(false);
            return checkedOutcome is CriteriaEntryDecisionVerification.Verified or CriteriaEntryDecisionVerification.Abandoned
                ? Performed() : Pending("The outcome source evidence is not yet qualified.");
        }
        catch (CriteriaSourceUnavailableException) { return Pending("Criteria source coverage is unresolved."); }
        catch (ImmutableContentUnavailableException) { return Pending("The signal intent is unavailable."); }
        catch (ImmutableContentIntegrityException) { return Failure(RequestErrorKind.Validation, "The signal intent failed integrity verification.", false); }
        catch (EventStreamConcurrencyException) { return Pending("The target changed before its decision."); }
        catch (FitzException) { return Pending("The source or append outcome is unresolved."); }
        catch (IOException) { return Pending("The source or append outcome is unresolved."); }
        catch (TimeoutException) { return Pending("The source or append outcome is unresolved."); }
    }

    static async ValueTask<TargetHistory> ReadTargetAsync(ICriteriaExactSourceReader source, CriteriaEditionValue owner,
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
                throw new InvalidOperationException("The value target source is not contiguous.");
            if (item.Event.Metadata.EventId == proposal.Event.Metadata.EventId)
            {
                if (item.ResourceOffset != proposal.ResourceOffset || item.AreaOffset != proposal.AreaOffset || item.Event != proposal.Event)
                    throw new InvalidOperationException("The proposal differs between exact source reads.");
                found = true;
            }
            var outcomeProposal = item.Event switch
            {
                CriteriaEditionValueAccepted accepted => accepted.ProposalEventId,
                CriteriaEditionValueRefused refused => refused.ProposalEventId,
                _ => Uuid.Empty,
            };
            if (outcomeProposal == proposal.Event.Metadata.EventId)
            {
                if (outcome is not null)
                    throw new InvalidOperationException("A proposal has multiple outcomes.");
                outcome = item;
            }
        }
        return new TargetHistory(next, found, outcome);
    }

    sealed record TargetHistory(ulong Next, bool FoundProposal, CriteriaOrderedSourceEvent? Outcome);
    static Result<ReceiverOutcome> Performed() =>
        Result<ReceiverOutcome>.Success(new ReceiverOutcome.Performed("criteria-value-signal"));

    static Result<ReceiverOutcome> Pending(string message) => Failure(RequestErrorKind.Conflict, message, true);
    static Result<ReceiverOutcome> Failure(RequestErrorKind kind, string message, bool transient) =>
        Result<ReceiverOutcome>.Failure(new RequestError(kind, message, transient));
}
