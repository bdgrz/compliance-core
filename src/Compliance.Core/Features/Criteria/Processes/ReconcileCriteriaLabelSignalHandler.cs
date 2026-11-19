using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Handlers;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Processes.Models;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Processes;

/// <summary>Re-reads source and dispatches one idempotent label participant operation.</summary>
sealed class ReconcileCriteriaLabelSignalHandler(IDomainEventReader events, IImmutableContentStore content,
    IRequestBus bus) : IRequestHandler<ReconcileCriteriaLabelSignal, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ReconcileCriteriaLabelSignal> context,
        CancellationToken ct)
    {
        try
        { return await ReconcileAsync(context, ct).ConfigureAwait(false); }
        catch (CriteriaSourceUnavailableException) { return Pending("The label source is unavailable."); }
        catch (ImmutableContentUnavailableException) { return Pending("The signal intent is unavailable."); }
        catch (ImmutableContentIntegrityException) { return Failure(RequestErrorKind.Validation, "The signal intent failed integrity verification.", false); }
        catch (EventStreamConcurrencyException) { return Pending("A label participant changed."); }
        catch (FitzException) { return Pending("The label source or append outcome is unresolved."); }
        catch (IOException) { return Pending("The label source or append outcome is unresolved."); }
        catch (TimeoutException) { return Pending("The label source or append outcome is unresolved."); }
    }

    async ValueTask<Result<ReceiverOutcome>> ReconcileAsync(IRequestContext<ReconcileCriteriaLabelSignal> context,
        CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Failure(RequestErrorKind.Forbidden, "A system worker is required.", false);
        var signal = context.Request;
        var bytes = await content.ReadVerifiedAsync(signal.Source.Stream.Realm, signal.Intent, ct).ConfigureAwait(false);
        await using var reader = events.ReadAsync(signal.Source.Stream, signal.Position, ct).GetAsyncEnumerator(ct);
        if (!await reader.MoveNextAsync().ConfigureAwait(false))
            return Pending("The source signal is not yet observable.");
        var record = reader.Current;
        var fact = record.Event;
        if (record.Stream != signal.Source.Stream || record.ResourceOffset != signal.Position ||
            fact.Metadata.EventId != signal.Source.EventId || fact.Metadata.AggregateVersion != signal.Source.AggregateVersion ||
            fact.Metadata.IsAudit || !CriteriaLabelSignal.TryAddress(fact, out var expected, out var aggregateId) ||
            expected != record.Stream || fact.Metadata.AggregateId != aggregateId ||
            !bytes.Span.SequenceEqual(CriteriaLabelSignal.Payload(record.Stream, fact, record.ResourceOffset)))
            return Failure(RequestErrorKind.Validation, "The frozen signal does not match its exact committed source.", false);

        var participant = fact as EditionLabelReserved;
        CriteriaLabelRefusalEvidence? claimRefusal = null;
        if (fact is EditionLabelReservationRefused refusal)
        {
            var verifier = new CriteriaLabelRefusalVerifier(events, new CriteriaLabelSourceVerifier(events));
            claimRefusal = await verifier.VerifyAsync(refusal.Label, refusal.Metadata.EventId, ct).ConfigureAwait(false);
            if (claimRefusal is null)
                return Pending("The exact claim refusal evidence is not yet observable.");
            if (!CriteriaLabelRefusalCommitter.SameRefusal(claimRefusal.Refusal, refusal))
                return Failure(RequestErrorKind.Validation, "The claim refusal source does not match the signal.", false);
        }
        if (fact is EditionLabelConfirmed confirmed)
        {
            var source = new CriteriaLabelSourceVerifier(events);
            var decision = await source.FindAsync<DomainEvent>(CriteriaEdition.AddressOf(confirmed.EditionId),
                confirmed.EditionAcceptedEventId, ct).ConfigureAwait(false);
            var reservationId = decision switch
            {
                CriteriaEditionDraftActivated e => e.ReservationEventId,
                CriteriaEditionDraftRenamed e => e.ReservationEventId,
                _ => Uuid.Empty,
            };
            if (reservationId == Uuid.Empty)
                return Pending("The confirmed edition decision is not yet observable.");
            participant = await source.FindAsync<EditionLabelReserved>(record.Stream, reservationId, ct).ConfigureAwait(false);
            if (participant is null)
                return Pending("The confirmed reservation is not yet observable.");
            if (participant.EditionId != confirmed.EditionId || participant.OperationId != confirmed.OperationId ||
                participant.Label != confirmed.Label || participant.Generation != confirmed.Generation)
                return Failure(RequestErrorKind.Validation, "The confirmation and reservation do not match.", false);
        }

        var requestIdentity = fact switch
        {
            CriteriaEditionDraftRequested e => (e.EditionId, e.OperationId),
            CriteriaInitialLabelRequested e => (e.EditionId, e.OperationId),
            CriteriaEditionRenameRequested e => (e.EditionId, e.OperationId),
            CriteriaInitialLabelRefused e => (e.EditionId, e.OperationId),
            CriteriaEditionRenameRefused e => (e.EditionId, e.OperationId),
            EditionLabelReservationRefused e => (e.EditionId, e.OperationId),
            _ when participant is not null => (participant.EditionId, participant.OperationId),
            _ => (Uuid.Empty, Uuid.Empty),
        };
        if (requestIdentity.Item1 != Uuid.Empty)
        {
            var refusalStatus = await EditionRefusalStatusAsync(requestIdentity.Item1, requestIdentity.Item2, ct).ConfigureAwait(false);
            if (refusalStatus is false)
                return Pending("The edition refusal needs an exact committed participant refusal proof.");
            if (refusalStatus is true)
            {
                if (participant is not null)
                    return Failure(RequestErrorKind.Validation, "One request cannot have both a reservation and refusal.", false);
                return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Performed("criteria-label-refusal"));
            }
        }

        var terminalCommand = participant is null ? null : await LateParticipantCommandAsync(participant, ct).ConfigureAwait(false);
        if (terminalCommand is null && claimRefusal is not null)
            terminalCommand = await LateRequestCommandAsync(claimRefusal.Request, ct).ConfigureAwait(false);
        if (terminalCommand is null && fact is CriteriaEditionDraftRequested or CriteriaInitialLabelRequested or CriteriaEditionRenameRequested)
            terminalCommand = await LateRequestCommandAsync(fact, ct).ConfigureAwait(false);
        IRequest? command = fact switch
        {
            CriteriaEditionDraftRequested e => new ReserveCriteriaEditionLabel(e.EditionId, e.OperationId, e.Label, signal.Position, e.Metadata.EventId),
            CriteriaInitialLabelRequested e => new ReserveCriteriaEditionLabel(e.EditionId, e.OperationId, e.Label, signal.Position, e.Metadata.EventId),
            CriteriaEditionRenameRequested e => new ReserveCriteriaEditionLabel(e.EditionId, e.OperationId, e.NewLabel, signal.Position, e.Metadata.EventId),
            CriteriaEditionDraftActivated e => new ConfirmCriteriaEditionLabel(e.EditionId, e.OperationId, e.Label, e.LabelGeneration, e.ReservationEventId, signal.Position, e.Metadata.EventId),
            CriteriaEditionDraftRenamed e => new ConfirmCriteriaEditionLabel(e.EditionId, e.OperationId, e.NewLabel, e.NewLabelGeneration, e.ReservationEventId, signal.Position, e.Metadata.EventId),
            EditionLabelReserved e => new ApplyCriteriaLabelReservation(e.EditionId, e.OperationId, e.Label, signal.Position, e.Metadata.EventId),
            EditionLabelConfirmed e => new ApplyCriteriaLabelConfirmation(e.EditionId, e.OperationId, e.Label, signal.Position, e.Metadata.EventId),
            EditionLabelReservationRefused e => new ApplyCriteriaEditionLabelRefusal(e.EditionId, e.OperationId, e.Label, signal.Position, e.Metadata.EventId),
            _ => null,
        };
        command = terminalCommand ?? command;
        if (command is null)
            command = await TerminalCommandAsync(fact, signal.Position, ct).ConfigureAwait(false);
        if (command is null)
            return Pending("The exact terminal participant evidence is not yet observable.");
        var outcome = await bus.SendAsync(command, context, ct).ConfigureAwait(false);
        var refusalDecision = false;
        if (!outcome.IsSuccess && outcome.Error!.Kind == RequestErrorKind.Conflict && command is ReserveCriteriaEditionLabel reserve)
        {
            // The reserve failure is not a decision. Only a qualified conditional claim append may refuse.
            refusalDecision = true;
            outcome = await bus.SendAsync(new DecideCriteriaEditionLabelRefusal(reserve.EditionId, reserve.OperationId,
                reserve.Label, reserve.EditionEventPosition, reserve.EditionEventId), context, ct).ConfigureAwait(false);
        }
        if (!outcome.IsSuccess)
        {
            var error = outcome.Error!;
            // Contention and delayed source are retried by this record rather than the shared reactor.
            return Failure(error.Kind, error.Message, error.IsTransient || !refusalDecision && error.Kind == RequestErrorKind.Conflict);
        }
        return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Performed("criteria-label-signal"));
    }

    async ValueTask<IRequest?> TerminalCommandAsync(DomainEvent terminal, ulong position, CancellationToken ct)
    {
        if (terminal is CriteriaEditionDraftAbandoned abandoned)
            return await ReleaseCommandAsync(abandoned.EditionId, abandoned.Label, abandoned.LabelGeneration,
                terminal, position, ct).ConfigureAwait(false);
        if (terminal is CriteriaEditionRenameCompleted completed)
        {
            var editionOwner = new CriteriaEdition(completed.EditionId);
            ulong expectedPosition = 0;
            await foreach (var item in events.ReadAsync(editionOwner.Stream, 0, ct).ConfigureAwait(false))
            {
                if (item.ResourceOffset >= position)
                    break;
                if (item.Stream != editionOwner.Stream || item.ResourceOffset != expectedPosition ||
                    item.Event.Metadata.AggregateId != editionOwner.Id || item.Event.Metadata.IsAudit)
                    throw new InvalidOperationException("The edition rename source is inconsistent.");
                expectedPosition = checked(expectedPosition + 1);
                if (item.Event is CriteriaEditionDraftRenamed rename && rename.EditionId == completed.EditionId &&
                    rename.OperationId == completed.OperationId && rename.NewLabel == completed.Label &&
                    rename.NewLabelGeneration == completed.LabelGeneration)
                    return await ReleaseCommandAsync(completed.EditionId, rename.OldLabel, rename.OldLabelGeneration,
                        terminal, position, ct).ConfigureAwait(false);
            }
            return null;
        }
        var (editionId, operationId) = terminal switch
        {
            CriteriaEditionDraftRequestCancelled e => (e.EditionId, e.TargetOperationId),
            CriteriaEditionRenameCancelled e => (e.EditionId, e.RenameOperationId),
            _ => (Uuid.Empty, Uuid.Empty),
        };
        if (editionId == Uuid.Empty || operationId == Uuid.Empty)
            return null;
        var edition = new CriteriaEdition(editionId);
        ulong expectedOffset = 0;
        await foreach (var item in events.ReadAsync(edition.Stream, 0, ct).ConfigureAwait(false))
        {
            if (item.Stream != edition.Stream || item.ResourceOffset != expectedOffset || item.Event.Metadata.AggregateId != edition.Id ||
                item.Event.Metadata.IsAudit)
                throw new InvalidOperationException("The edition source is not contiguous.");
            expectedOffset = checked(expectedOffset + 1);
            if (item.ResourceOffset >= position)
                break;
            var label = item.Event switch
            {
                CriteriaEditionDraftRequested e when e.OperationId == operationId => e.Label,
                CriteriaInitialLabelRequested e when e.OperationId == operationId => e.Label,
                CriteriaEditionRenameRequested e when e.OperationId == operationId => e.NewLabel,
                _ => null,
            };
            if (label is not null && CriteriaLabelReleaseRules.AuthorizeFence(editionId, operationId, label, item.Event, terminal) is not null)
                return new FenceCriteriaEditionLabelRequest(editionId, operationId, label, item.Event.Metadata.EventId,
                    position, terminal.Metadata.EventId);
        }
        return null;
    }

    async ValueTask<IRequest?> ReleaseCommandAsync(Uuid editionId, string label, long generation,
        DomainEvent terminal, ulong position, CancellationToken ct)
    {
        if (editionId == Uuid.Empty || generation <= 0 || !EditionLabelClaim.TryNormalize(label, out var normalized) || normalized != label)
            return null;
        var claim = new EditionLabelClaim(label);
        ulong expected = 0;
        await foreach (var item in events.ReadAsync(claim.Stream, 0, ct).ConfigureAwait(false))
        {
            if (item.Stream != claim.Stream || item.ResourceOffset != expected || item.Event.Metadata.AggregateId != claim.Id || item.Event.Metadata.IsAudit)
                throw new InvalidOperationException("The label source is not contiguous.");
            expected = checked(expected + 1);
            if (item.Event is EditionLabelReserved reservation && reservation.EditionId == editionId &&
                reservation.Label == label && reservation.Generation == generation)
                return new ReleaseCriteriaEditionLabel(editionId, reservation.OperationId, label, generation,
                    reservation.Metadata.EventId, position, terminal.Metadata.EventId);
        }
        return null;
    }

    async ValueTask<IRequest?> LateParticipantCommandAsync(EditionLabelReserved participant, CancellationToken ct)
    {
        var edition = new CriteriaEdition(participant.EditionId);
        ulong expected = 0;
        await foreach (var item in events.ReadAsync(edition.Stream, 0, ct).ConfigureAwait(false))
        {
            if (item.Stream != edition.Stream || item.ResourceOffset != expected || item.Event.Metadata.AggregateId != edition.Id || item.Event.Metadata.IsAudit)
                throw new InvalidOperationException("The edition source is not contiguous.");
            expected = checked(expected + 1);
            var couldAffect = item.Event switch
            {
                CriteriaEditionDraftRequestCancelled e => e.TargetOperationId == participant.OperationId,
                CriteriaEditionRenameCancelled e => e.RenameOperationId == participant.OperationId,
                CriteriaEditionDraftAbandoned e => e.Label == participant.Label && e.LabelGeneration == participant.Generation,
                CriteriaEditionRenameCompleted => true,
                _ => false,
            };
            if (!couldAffect)
                continue;
            var command = await TerminalCommandAsync(item.Event, item.ResourceOffset, ct).ConfigureAwait(false);
            if (command is FenceCriteriaEditionLabelRequest fence && fence.OperationId == participant.OperationId &&
                fence.EditionRequestEventId == participant.EditionRequestEventId && fence.Label == participant.Label)
                return fence;
            if (command is ReleaseCriteriaEditionLabel release && release.ClaimOperationId == participant.OperationId &&
                release.ReservationEventId == participant.Metadata.EventId && release.Generation == participant.Generation)
                return release;
        }
        // Absence is not a terminal conclusion; the target CAS or a later terminal signal resolves the race.
        return null;
    }

    async ValueTask<IRequest?> LateRequestCommandAsync(DomainEvent request, CancellationToken ct)
    {
        var (editionId, operationId, label) = request switch
        {
            CriteriaEditionDraftRequested e => (e.EditionId, e.OperationId, e.Label),
            CriteriaInitialLabelRequested e => (e.EditionId, e.OperationId, e.Label),
            CriteriaEditionRenameRequested e => (e.EditionId, e.OperationId, e.NewLabel),
            _ => (Uuid.Empty, Uuid.Empty, string.Empty),
        };
        var edition = new CriteriaEdition(editionId);
        ulong expected = 0;
        await foreach (var item in events.ReadAsync(edition.Stream, 0, ct).ConfigureAwait(false))
        {
            if (item.Stream != edition.Stream || item.ResourceOffset != expected || item.Event.Metadata.AggregateId != edition.Id || item.Event.Metadata.IsAudit)
                throw new InvalidOperationException("The edition source is not contiguous.");
            expected = checked(expected + 1);
            if (CriteriaLabelReleaseRules.AuthorizeFence(editionId, operationId, label, request, item.Event) is not "cancelled")
                continue;
            return new FenceCriteriaEditionLabelRequest(editionId, operationId, label, request.Metadata.EventId,
                item.ResourceOffset, item.Event.Metadata.EventId);
        }
        return null;
    }

    async ValueTask<bool?> EditionRefusalStatusAsync(Uuid editionId, Uuid operationId, CancellationToken ct)
    {
        var edition = new CriteriaEdition(editionId);
        string? label = null;
        var requestId = Uuid.Empty;
        ulong expected = 0;
        await foreach (var item in events.ReadAsync(edition.Stream, 0, ct).ConfigureAwait(false))
        {
            if (item.Stream != edition.Stream || item.ResourceOffset != expected ||
                item.Event.Metadata.AggregateId != edition.Id || item.Event.Metadata.IsAudit)
                throw new InvalidOperationException("The edition source is not contiguous.");
            expected = checked(expected + 1);
            switch (item.Event)
            {
                case CriteriaEditionDraftRequested draft when draft.OperationId == operationId:
                    label = draft.Label;
                    requestId = draft.Metadata.EventId;
                    break;
                case CriteriaInitialLabelRequested initial when initial.OperationId == operationId:
                    label = initial.Label;
                    requestId = initial.Metadata.EventId;
                    break;
                case CriteriaEditionRenameRequested rename when rename.OperationId == operationId:
                    label = rename.NewLabel;
                    requestId = rename.Metadata.EventId;
                    break;
            }
            var (isRefusal, refusalId, reason) = item.Event switch
            {
                CriteriaInitialLabelRefused initial when initial.OperationId == operationId => (true, initial.RefusalEventId, initial.Reason),
                CriteriaEditionRenameRefused rename when rename.OperationId == operationId => (true, rename.RefusalEventId, rename.Reason),
                _ => (false, Uuid.Empty, string.Empty),
            };
            if (!isRefusal)
                continue;
            if (refusalId == Uuid.Empty || label is null || requestId == Uuid.Empty ||
                reason != ApplyCriteriaEditionLabelRefusalHandler.Reason ||
                item.Event is CriteriaInitialLabelRefused initialRefusal && initialRefusal.Label != label)
                return false;
            var verifier = new CriteriaLabelRefusalVerifier(events, new CriteriaLabelSourceVerifier(events));
            var evidence = await verifier.VerifyAsync(label, refusalId, ct).ConfigureAwait(false);
            return evidence is not null && evidence.Refusal.EditionId == editionId && evidence.Refusal.OperationId == operationId &&
                evidence.Refusal.EditionRequestEventId == requestId && ((item.Event, evidence.Request) switch
                {
                    (CriteriaInitialLabelRefused, CriteriaEditionDraftRequested or CriteriaInitialLabelRequested) => true,
                    (CriteriaEditionRenameRefused, CriteriaEditionRenameRequested) => true,
                    _ => false,
                });
        }
        return null;
    }

    static Result<ReceiverOutcome> Pending(string message) => Failure(RequestErrorKind.Conflict, message, true);
    static Result<ReceiverOutcome> Failure(RequestErrorKind kind, string message, bool transient) =>
        Result<ReceiverOutcome>.Failure(new RequestError(kind, message, transient));
}
