using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Publishes only an exact live witness and confirmed label at qualified source heads.</summary>
sealed class CompleteCriteriaPublicationHandler(IDomainEventReader events, IAggregateReader aggregates,
    IAggregateExecutor executor, ICriteriaExactSourceReader source, ICriteriaFreshEntryHead? entryHead = null,
    ICriteriaFreshLabelClaimHead? labelHead = null, ICriteriaFreshEditionHead? editionHead = null,
    ICriteriaSourceCutCoverage? coverage = null, ICriteriaParticipantCausalOrder? causal = null)
    : IRequestHandler<CompleteCriteriaPublication>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<CompleteCriteriaPublication> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Failure(RequestErrorKind.Forbidden, "A system reaction is required.", false);
        if (request.EditionId == Uuid.Empty || request.AttemptId == Uuid.Empty || request.SourceEventId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(request.Identifier))
            return Failure(RequestErrorKind.Validation, "The exact witness pin is required.", false);
        try
        {
            var witness = new CriteriaEntry(request.EditionId, request.Identifier, selectedPublicationAttemptId: request.AttemptId);
            var pinRecord = await source.FindAsync(witness.Stream, witness.Id, request.SourceEventId, ct).ConfigureAwait(false);
            if (pinRecord?.Event is not CriterionWitnessPinned pin)
                return Pending();
            if (pinRecord.ResourceOffset != request.SourcePosition || pin.EditionId != request.EditionId || pin.Identifier != request.Identifier ||
                pin.PublicationAttemptId != request.AttemptId || pin.PublicationStartedEventId == Uuid.Empty || pin.IncarnationId == Uuid.Empty)
                return Failure(RequestErrorKind.Validation, "The source pin names another witness attempt.", false);
            var edition = new CriteriaEdition(request.EditionId, request.AttemptId);
            var startedRecord = await source.FindAsync(edition.Stream, edition.Id, pin.PublicationStartedEventId, ct).ConfigureAwait(false);
            if (startedRecord?.Event is not CriteriaPublicationStarted started)
                return Pending();
            if (started.EditionId != pin.EditionId || started.DraftGeneration != pin.DraftGeneration ||
                started.PublicationAttemptId != pin.PublicationAttemptId || started.AttemptOrdinal != pin.AttemptOrdinal ||
                started.WitnessIdentifier != pin.Identifier || started.WitnessIncarnationId != pin.IncarnationId ||
                startedRecord.AreaOffset >= pinRecord.AreaOffset)
                return Failure(RequestErrorKind.Validation, "The witness pin does not match its committed start and order.", false);
            // A source-verified terminal publication remains accepted on retry even if
            // later participant reads are unavailable or their heads have advanced.
            var replay = await aggregates.HydrateAsync(new CriteriaEdition(request.EditionId, request.AttemptId), ct).ConfigureAwait(false);
            if (replay.Publication is { } prior)
            {
                if (prior.PublicationAttemptId != request.AttemptId || prior.WitnessPinEventId != pin.Metadata.EventId)
                    return Failure(RequestErrorKind.Conflict, "The edition was published under another attempt.", false);
                var proof = await new SourceCriteriaPublicationAuthority(aggregates, source, coverage, causal).ReadPublishedAsync(request.EditionId, ct)
                    .ConfigureAwait(false);
                return proof.IsSuccess && proof.Value!.EventId == prior.Metadata.EventId ? Result.Success : Pending();
            }
            if (entryHead is null || labelHead is null || editionHead is null || coverage is null)
                return Pending();
            if (!await new CriteriaPublicationWitnessVerifier(source, coverage, causal).VerifyAsync(request.EditionId,
                    request.Identifier, pin.DraftGeneration, pin.IncarnationId, pinRecord.AreaOffset, null, ct)
                    .ConfigureAwait(false))
                return Pending();
            var label = await new CriteriaPublicationLabelVerifier(source, coverage, causal).VerifyAsync(started, startedRecord.AreaOffset, ct).ConfigureAwait(false);
            if (label is null)
                return Pending();
            var witnessSource = await CriteriaPublicationSourceHead.ReadAsync(events, witness, pin.Metadata.EventId, ct).ConfigureAwait(false);
            var loadedWitness = await aggregates.HydrateAsync(witness, ct).ConfigureAwait(false);
            if (!witnessSource.Found || loadedWitness.CommittedStreamPosition != witnessSource.Next ||
                loadedWitness.ActiveWitnessPin?.Metadata.EventId != pin.Metadata.EventId ||
                loadedWitness.SelectedWitnessFence is not null || loadedWitness.SelectedWitnessRefusal is not null ||
                !await entryHead.IsCurrentAsync(witness.Stream, witness.Id, witnessSource.Next, ct).ConfigureAwait(false))
                return Pending();
            var claim = new EditionLabelClaim(started.Label, started.EditionId, label.OperationId);
            var claimSource = await CriteriaPublicationSourceHead.ReadAsync(events, claim, label.Metadata.EventId, ct).ConfigureAwait(false);
            var loadedClaim = await aggregates.HydrateAsync(claim, ct).ConfigureAwait(false);
            if (!claimSource.Found || loadedClaim.CommittedStreamPosition != claimSource.Next ||
                !loadedClaim.IsHeldBy(started.EditionId, label.OperationId, started.LabelGeneration) ||
                loadedClaim.CurrentConfirmation?.Metadata.EventId != label.Metadata.EventId ||
                !await labelHead.IsCurrentAsync(claim.Stream, claim.Id, claimSource.Next, ct).ConfigureAwait(false))
                return Pending();
            var editionSource = await CriteriaPublicationSourceHead.ReadAsync(events, edition, started.Metadata.EventId, ct).ConfigureAwait(false);
            var loadedEdition = await aggregates.HydrateAsync(edition, ct).ConfigureAwait(false);
            if (!editionSource.Found || loadedEdition.CommittedStreamPosition != editionSource.Next ||
                loadedEdition.PendingPublication?.Metadata.EventId != started.Metadata.EventId || loadedEdition.Status != "publishing" ||
                loadedEdition.Framework != "tsc" ||
                !await editionHead.IsCurrentAsync(edition.Stream, edition.Id, editionSource.Next, ct).ConfigureAwait(false))
                return Pending();

            var publicationId = Uuid.Empty;
            var result = await executor.ExecuteAsync(new CriteriaEdition(request.EditionId, request.AttemptId), owner =>
            {
                if (owner.CommittedStreamPosition != editionSource.Next || owner.PendingPublication?.Metadata.EventId != started.Metadata.EventId)
                    return AggregateOutcome.Discard(Pending());
                var decision = owner.Publish(request.AttemptId, pin.Metadata.EventId);
                if (decision.IsSuccess)
                    publicationId = owner.Publication?.Metadata.EventId ?? Uuid.Empty;
                return AggregateOutcome.CommitOnSuccess(decision);
            }, context, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
                return result.Error!.Kind == RequestErrorKind.Conflict ? Pending() : result;
            if (publicationId == Uuid.Empty)
                return Pending();
            var published = await source.FindAsync(edition.Stream, edition.Id, publicationId, ct).ConfigureAwait(false);
            if (published?.Event is not CriteriaEditionPublished fact)
                return Pending();
            if (fact.EditionId != started.EditionId || fact.DraftGeneration != started.DraftGeneration ||
                fact.PublicationAttemptId != started.PublicationAttemptId || fact.AttemptOrdinal != started.AttemptOrdinal ||
                fact.WitnessIdentifier != started.WitnessIdentifier || fact.WitnessIncarnationId != started.WitnessIncarnationId ||
                fact.WitnessPinEventId != pin.Metadata.EventId || fact.Label != started.Label ||
                fact.LabelGeneration != started.LabelGeneration || fact.LabelConfirmationEventId != started.LabelConfirmationEventId ||
                fact.PublishedBy != started.Actor || pinRecord.AreaOffset >= published.AreaOffset)
                return Failure(RequestErrorKind.Validation, "The committed publication does not match its exact participants.", false);
            return Result.Success;
        }
        catch (CriteriaSourceUnavailableException) { return Pending(); }
        catch (EventStreamConcurrencyException) { return Pending(); }
        catch (FitzException) { return Pending(); }
        catch (IOException) { return Pending(); }
        catch (TimeoutException) { return Pending(); }
    }

    static Result Pending() => Failure(RequestErrorKind.Conflict, "Publication source evidence or a qualified participant head is unavailable.", true);
    static Result Failure(RequestErrorKind kind, string message, bool transient) => Result.Failure(new RequestError(kind, message, transient));
}
