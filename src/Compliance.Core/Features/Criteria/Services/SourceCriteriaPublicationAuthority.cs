using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Verifies one edition's irreversible publication against its exact source facts.</summary>
/// <remarks>
/// A missing source fact or missing ordered-position evidence is retryable, never proof that
/// a delayed fact cannot commit. This verifier reads the edition and the two named
/// participants only; it does not enumerate the edition's criteria.
/// </remarks>
public sealed class SourceCriteriaPublicationAuthority(
    IAggregateReader aggregates,
    ICriteriaExactSourceReader source,
    ICriteriaSourceCutCoverage? coverage = null, ICriteriaParticipantCausalOrder? causal = null) : ICriteriaPublicationAuthority
{
    /// <inheritdoc />
    public async ValueTask<Result<CriteriaPublicationProof>> ReadPublishedAsync(Uuid editionId,
        CancellationToken ct = default)
    {
        try
        {
            return await ReadCoreAsync(editionId, ct).ConfigureAwait(false);
        }
        catch (CriteriaSourceUnavailableException) { ct.ThrowIfCancellationRequested(); return Pending(); }
        catch (FitzException) { ct.ThrowIfCancellationRequested(); return Pending(); }
        catch (IOException) { ct.ThrowIfCancellationRequested(); return Pending(); }
        catch (TimeoutException) { ct.ThrowIfCancellationRequested(); return Pending(); }
    }

    async ValueTask<Result<CriteriaPublicationProof>> ReadCoreAsync(Uuid editionId, CancellationToken ct)
    {
        if (editionId == Uuid.Empty)
            return Result<CriteriaPublicationProof>.Failure(new RequestError(RequestErrorKind.Validation,
                "A criteria edition identity is required."));

        var edition = await aggregates.HydrateAsync(new CriteriaEdition(editionId), ct).ConfigureAwait(false);
        var published = edition.Publication;
        if (!edition.IsPublished || published is null)
            return Pending();

        if (edition.Framework != "tsc" || published.EditionId != editionId || published.DraftGeneration == Uuid.Empty ||
            published.PublicationAttemptId == Uuid.Empty || published.AttemptOrdinal <= 0 ||
            string.IsNullOrWhiteSpace(published.WitnessIdentifier) || published.WitnessIncarnationId == Uuid.Empty ||
            published.WitnessPinEventId == Uuid.Empty || published.LabelConfirmationEventId == Uuid.Empty ||
            published.LabelGeneration <= 0 || !EditionLabelClaim.TryNormalize(published.Label, out var label) ||
            !string.Equals(label, published.Label, StringComparison.Ordinal))
            throw new InvalidOperationException("The committed criteria publication has inconsistent identities.");

        try
        {
            var publication = await source.FindAsync(edition.Stream, edition.Id, published.Metadata.EventId, ct)
                .ConfigureAwait(false);
            if (publication is null || publication.Event is not CriteriaEditionPublished committed ||
                committed != published)
                return Pending();

            var witness = new CriteriaEntry(editionId, published.WitnessIdentifier);
            var pin = await source.FindAsync(witness.Stream, witness.Id, published.WitnessPinEventId, ct)
                .ConfigureAwait(false);
            if (pin?.Event is not CriterionWitnessPinned pinned)
                return Pending();
            if (pinned.EditionId != editionId || pinned.DraftGeneration != published.DraftGeneration ||
                pinned.Identifier != published.WitnessIdentifier ||
                pinned.PublicationAttemptId != published.PublicationAttemptId ||
                pinned.AttemptOrdinal != published.AttemptOrdinal ||
                pinned.IncarnationId != published.WitnessIncarnationId ||
                pinned.PublicationStartedEventId == Uuid.Empty)
                throw new InvalidOperationException("The publication witness does not match the edition attempt.");

            var started = await source.FindAsync(edition.Stream, edition.Id, pinned.PublicationStartedEventId, ct)
                .ConfigureAwait(false);
            if (started?.Event is not CriteriaPublicationStarted attempt)
                return Pending();
            if (attempt.EditionId != editionId || attempt.DraftGeneration != published.DraftGeneration ||
                attempt.PublicationAttemptId != published.PublicationAttemptId ||
                attempt.AttemptOrdinal != published.AttemptOrdinal ||
                attempt.WitnessIdentifier != published.WitnessIdentifier ||
                attempt.WitnessIncarnationId != published.WitnessIncarnationId ||
                attempt.Label != published.Label || attempt.LabelGeneration != published.LabelGeneration ||
                attempt.LabelConfirmationEventId != published.LabelConfirmationEventId ||
                attempt.Actor != published.PublishedBy || started.AreaOffset >= pin.AreaOffset ||
                pin.AreaOffset >= publication.AreaOffset)
                throw new InvalidOperationException("The publication attempt, witness and source order do not agree.");

            if (coverage is null || causal is null || !await new CriteriaPublicationWitnessVerifier(source, coverage, causal)
                    .VerifyAsync(editionId, published.WitnessIdentifier, published.DraftGeneration,
                        published.WitnessIncarnationId, pin.AreaOffset, null, ct).ConfigureAwait(false))
                return Pending();

            var labelOwner = new EditionLabelClaim(published.Label);
            var labelFact = await source.FindAsync(labelOwner.Stream, labelOwner.Id,
                published.LabelConfirmationEventId, ct).ConfigureAwait(false);
            if (labelFact?.Event is not EditionLabelConfirmed confirmation)
                return Pending();
            if (confirmation.EditionId != editionId || confirmation.Label != published.Label ||
                confirmation.Generation != published.LabelGeneration || confirmation.OperationId == Uuid.Empty ||
                confirmation.EditionAcceptedEventId == Uuid.Empty)
                throw new InvalidOperationException("The publication label confirmation does not match its edition.");

            var acceptedLabel = await source.FindAsync(edition.Stream, edition.Id, confirmation.EditionAcceptedEventId, ct)
                .ConfigureAwait(false);
            if (acceptedLabel is null)
                return Pending();
            var labelMatches = acceptedLabel.Event switch
            {
                CriteriaEditionDraftActivated activation => activation.EditionId == editionId && activation.Framework == edition.Framework &&
                    activation.OperationId == confirmation.OperationId && activation.Label == confirmation.Label &&
                    activation.LabelGeneration == confirmation.Generation,
                CriteriaEditionDraftRenamed rename => rename.EditionId == editionId &&
                    rename.OperationId == confirmation.OperationId && rename.NewLabel == confirmation.Label &&
                    rename.NewLabelGeneration == confirmation.Generation,
                _ => false,
            };
            if (!labelMatches || acceptedLabel.AreaOffset >= started.AreaOffset)
                throw new InvalidOperationException("The confirmed label is not bound to the prior edition decision.");

            var reservationId = acceptedLabel.Event switch
            {
                CriteriaEditionDraftActivated activation => activation.ReservationEventId,
                CriteriaEditionDraftRenamed rename => rename.ReservationEventId,
                _ => Uuid.Empty,
            };
            if (reservationId == Uuid.Empty)
                throw new InvalidOperationException("The edition label decision has no reservation evidence.");
            var reservationFact = await source.FindAsync(labelOwner.Stream, labelOwner.Id, reservationId, ct)
                .ConfigureAwait(false);
            if (reservationFact?.Event is not EditionLabelReserved reservation)
                return Pending();
            if (reservation.Label != confirmation.Label || reservation.EditionId != editionId ||
                reservation.OperationId != confirmation.OperationId ||
                reservation.Generation != confirmation.Generation || reservation.EditionRequestEventId == Uuid.Empty ||
                reservationFact.ResourceOffset >= labelFact.ResourceOffset)
                throw new InvalidOperationException("The edition label reservation does not match its confirmation.");

            var request = await source.FindAsync(edition.Stream, edition.Id, reservation.EditionRequestEventId, ct)
                .ConfigureAwait(false);
            if (request is null)
                return Pending();
            var requestMatches = (acceptedLabel.Event, request.Event) switch
            {
                (CriteriaEditionDraftActivated, CriteriaEditionDraftRequested draft) => draft.EditionId == editionId &&
                    draft.Framework == edition.Framework &&
                    draft.DraftGeneration == published.DraftGeneration && draft.OperationId == reservation.OperationId &&
                    draft.Label == reservation.Label && draft.Actor == reservation.RequestedBy,
                (CriteriaEditionDraftActivated, CriteriaInitialLabelRequested initial) => initial.EditionId == editionId &&
                    initial.OperationId == reservation.OperationId && initial.Label == reservation.Label &&
                    initial.Actor == reservation.RequestedBy,
                (CriteriaEditionDraftRenamed, CriteriaEditionRenameRequested rename) => rename.EditionId == editionId &&
                    rename.OperationId == reservation.OperationId && rename.NewLabel == reservation.Label &&
                    rename.Actor == reservation.RequestedBy,
                _ => false,
            };
            if (!requestMatches || request.AreaOffset >= acceptedLabel.AreaOffset)
                throw new InvalidOperationException("The label reservation does not name the edition's exact request.");

            if (!await causal.PrecedesAsync(request, reservationFact, ct).ConfigureAwait(false) ||
                !await causal.PrecedesAsync(reservationFact, acceptedLabel, ct).ConfigureAwait(false) ||
                !await causal.PrecedesAsync(acceptedLabel, labelFact, ct).ConfigureAwait(false) ||
                !await new CriteriaValueEditionParticipantVerifier(source, coverage, causal)
                    .VerifyConfirmationHistoryAsync(labelOwner, reservationFact, labelFact, ct).ConfigureAwait(false))
                return Pending();

            var confirmedByEdition = false;
            var participants = new CriteriaValueEditionParticipantVerifier(source, coverage, causal);
            var lifecycle = new CriteriaEntryEditionLifecycle(editionId, published.DraftGeneration);
            ulong next = 0;
            await foreach (var item in source.ReadAsync(edition.Stream, edition.Id, ct).ConfigureAwait(false))
            {
                if (item.Stream != edition.Stream || item.Event.Metadata.AggregateId != edition.Id ||
                    item.Event.Metadata.IsAudit || item.ResourceOffset != next++)
                    return Pending();
                if (item.AreaOffset >= started.AreaOffset)
                    continue;
                if (!lifecycle.Apply(item.Event) || !await participants.ObserveAsync(item, ct).ConfigureAwait(false))
                    return Pending();
                if (item.Event is CriteriaEditionDrafted drafted && drafted.EditionId == editionId && drafted.Framework == edition.Framework &&
                    drafted.OperationId == confirmation.OperationId && drafted.Label == confirmation.Label &&
                    drafted.LabelGeneration == confirmation.Generation &&
                    drafted.ConfirmationEventId == confirmation.Metadata.EventId &&
                    await causal.PrecedesAsync(labelFact, item, ct).ConfigureAwait(false))
                    confirmedByEdition = true;
                if (item.Event is CriteriaEditionRenameCompleted renamed && renamed.EditionId == editionId &&
                    renamed.OperationId == confirmation.OperationId && renamed.Label == confirmation.Label &&
                    renamed.LabelGeneration == confirmation.Generation &&
                    renamed.ConfirmationEventId == confirmation.Metadata.EventId &&
                    await causal.PrecedesAsync(labelFact, item, ct).ConfigureAwait(false))
                    confirmedByEdition = true;
            }
            if (!confirmedByEdition || !await coverage.CoversAsync(edition.Stream, next, started.AreaOffset, ct).ConfigureAwait(false))
                return Pending();

            return Result<CriteriaPublicationProof>.Success(new CriteriaPublicationProof(editionId,
                committed.Metadata.EventId, publication.ResourceOffset));
        }
        catch (CriteriaSourceUnavailableException)
        {
            ct.ThrowIfCancellationRequested();
            return Pending();
        }
    }

    static Result<CriteriaPublicationProof> Pending() => Result<CriteriaPublicationProof>.Failure(
        new RequestError(RequestErrorKind.Conflict,
            "The edition publication and its exact source evidence have not yet been established.", true));
}
