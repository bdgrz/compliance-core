using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>Applies exact committed primary-seat participant facts with Portia OCC.</summary>
/// <remarks>
/// The caller must durably retain the initially selected target physical position and aggregate
/// revision for retries of an attempt. A conflict or unknown result remains pending; this service
/// never silently changes those positions. Only a trusted system process may invoke these methods.
/// This service is not registered and does not migrate or switch the legacy operator authority.
/// </remarks>
public sealed class PlatformPrimaryParticipant(IDomainEventReader events, IAggregateReader aggregates,
    IAggregateExecutor executor)
{
    /// <summary>Applies one exact seat reservation at the original target position.</summary>
    public async ValueTask<Result> ApplyReservationAsync(Uuid sourceEventId, ulong sourcePosition,
        ulong targetPosition, ulong targetRevision, IExecutionContext context, CancellationToken ct = default)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Forbidden();
        try
        {
            var reservation = await ReadAsync<PrimaryPlatformOperatorSeatReserved>(PrimaryPlatformOperators.Address,
                PrimaryPlatformOperators.OwnerId, sourcePosition, sourceEventId, ct).ConfigureAwait(false);
            if (reservation is null)
                return Pending();
            var seat = await aggregates.HydrateAsync(new PrimaryPlatformOperators(reservation.OperationId), ct).ConfigureAwait(false);
            if (seat.SelectedCommand is not PrimaryPlatformOperatorSeatReserved selected || selected.Metadata.EventId != sourceEventId || selected != reservation)
                return Pending();
            var target = new PlatformOperator(reservation.UserId, reservation.OperationId);
            var observed = await aggregates.HydrateAsync(target, ct).ConfigureAwait(false);
            if (observed.SelectedOutcome is not null)
                return await VerifyReservationOutcomeAsync(reservation, targetPosition, targetRevision, ct).ConfigureAwait(false);
            if (seat.SelectedOutcome is not null || observed.CommittedStreamPosition < targetPosition)
                return Pending();
            if (observed.CommittedStreamPosition > targetPosition)
            {
                // A real committed record at the old slot proves that the original mark cannot
                // subsequently win there. Fence its generation at the newly observed target head.
                if (!await PositionOccupiedAsync(target.Stream, target.Id, targetPosition, ct).ConfigureAwait(false))
                    return Pending();
                var fencePosition = observed.CommittedStreamPosition;
                var fenceRevision = observed.Version;
                var fenced = await executor.ExecuteAsync(new PlatformOperator(reservation.UserId, reservation.OperationId), owner =>
                {
                    if (owner.CommittedStreamPosition != fencePosition || owner.Version != fenceRevision)
                        return AggregateOutcome.Discard(Pending());
                    return AggregateOutcome.CommitOnSuccess(owner.FencePrimaryMark(reservation.OperationId, fenceRevision,
                        reservation.GrantEpisodeId, reservation.LineageId, reservation.Generation, sourceEventId));
                }, context, ct).ConfigureAwait(false);
                return fenced.IsSuccess ? await VerifyReservationOutcomeAsync(reservation, targetPosition, targetRevision, ct).ConfigureAwait(false) : Pending();
            }
            if (observed.Version != targetRevision)
                return Pending();
            var result = await executor.ExecuteAsync(new PlatformOperator(reservation.UserId, reservation.OperationId), owner =>
            {
                if (owner.CommittedStreamPosition != targetPosition || owner.Version != targetRevision)
                    return AggregateOutcome.Discard(Pending());
                var applied = reservation.Bootstrap && !reservation.InitialExistingGrant
                    ? owner.AcceptBootstrap(reservation.OperationId, targetRevision, reservation.GrantEpisodeId,
                        reservation.LineageId, reservation.Generation, sourceEventId, reservation.Actor)
                    : owner.AcceptPrimaryMark(reservation.OperationId, targetRevision, reservation.GrantEpisodeId,
                        reservation.LineageId, reservation.Generation, sourceEventId);
                return AggregateOutcome.CommitOnSuccess(applied.IsSuccess ? applied : owner.FencePrimaryMark(
                    reservation.OperationId, targetRevision, reservation.GrantEpisodeId, reservation.LineageId,
                    reservation.Generation, sourceEventId));
            }, context, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
                return Pending();
            return await VerifyReservationOutcomeAsync(reservation, targetPosition, targetRevision, ct).ConfigureAwait(false);
        }
        catch (EventStreamConcurrencyException) { return Pending(); }
        catch (FitzException) { return Pending(); }
        catch (IOException) { return Pending(); }
        catch (TimeoutException) { return Pending(); }
    }

    /// <summary>Applies one exact committed release at the original target position.</summary>
    public async ValueTask<Result> ApplyReleaseAsync(Uuid sourceEventId, ulong sourcePosition,
        ulong targetPosition, ulong targetRevision, IExecutionContext context, CancellationToken ct = default)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Forbidden();
        try
        {
            var release = await ReadAsync<PrimaryPlatformOperatorSeatReleased>(PrimaryPlatformOperators.Address,
                PrimaryPlatformOperators.OwnerId, sourcePosition, sourceEventId, ct).ConfigureAwait(false);
            if (release is null)
                return Pending();
            var seat = await aggregates.HydrateAsync(new PrimaryPlatformOperators(release.OperationId), ct).ConfigureAwait(false);
            if (seat.SelectedCommand is not PrimaryPlatformOperatorSeatReleased selected || selected.Metadata.EventId != sourceEventId || selected != release)
                return Pending();
            var observed = await aggregates.HydrateAsync(new PlatformOperator(release.UserId, release.OperationId), ct).ConfigureAwait(false);
            if (observed.SelectedOutcome is not null)
                return await VerifyUnmarkAsync(release, targetPosition, targetRevision, ct).ConfigureAwait(false);
            if (seat.SelectedOutcome is not null || observed.CommittedStreamPosition < targetPosition)
                return Pending();
            var writePosition = targetPosition;
            var writeRevision = targetRevision;
            if (observed.CommittedStreamPosition > targetPosition)
            {
                if (!await PositionOccupiedAsync(PlatformOperator.AddressOf(release.UserId), PlatformOperator.IdentityOf(release.UserId),
                        targetPosition, ct).ConfigureAwait(false))
                    return Pending();
                writePosition = observed.CommittedStreamPosition;
                writeRevision = observed.Version;
            }
            else if (observed.Version != targetRevision)
                return Pending();
            var result = await executor.ExecuteAsync(new PlatformOperator(release.UserId, release.OperationId), owner =>
            {
                if (owner.CommittedStreamPosition != writePosition || owner.Version != writeRevision)
                    return AggregateOutcome.Discard(Pending());
                return AggregateOutcome.CommitOnSuccess(owner.RemovePrimaryMark(release.OperationId, writeRevision,
                    release.GrantEpisodeId, release.LineageId, release.Generation, release.ReservationEventId, sourceEventId));
            }, context, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
                return Pending();
            return await VerifyUnmarkAsync(release, targetPosition, targetRevision, ct).ConfigureAwait(false);
        }
        catch (EventStreamConcurrencyException) { return Pending(); }
        catch (FitzException) { return Pending(); }
        catch (IOException) { return Pending(); }
        catch (TimeoutException) { return Pending(); }
    }

    /// <summary>Records a re-read target acceptance, fence or unmark against its exact seat fact.</summary>
    public async ValueTask<Result> RecordOutcomeAsync(Uuid userId, ulong targetPosition, Uuid targetEventId,
        ulong seatSourcePosition, Uuid seatSourceEventId, IExecutionContext context, CancellationToken ct = default)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Forbidden();
        if (userId == Uuid.Empty || targetEventId == Uuid.Empty || seatSourceEventId == Uuid.Empty)
            return Pending();
        try
        {
            var fact = await ReadAtAsync(PlatformOperator.AddressOf(userId), PlatformOperator.IdentityOf(userId), targetPosition, ct)
                .ConfigureAwait(false);
            if (fact is null || fact.Metadata.EventId != targetEventId)
                return Pending();
            var (operation, sourceId) = fact switch
            {
                PlatformOperatorPrimaryMarkAccepted x when x.UserId == userId => (x.OperationId, x.ReservationEventId),
                PlatformOperatorBootstrapGrantAndMarkAccepted x when x.UserId == userId => (x.OperationId, x.ReservationEventId),
                PlatformOperatorPrimaryMarkFenced x when x.UserId == userId => (x.OperationId, x.ReservationEventId),
                PlatformOperatorPrimaryUnmarked x when x.UserId == userId => (x.OperationId, x.ReleaseEventId),
                _ => (Uuid.Empty, Uuid.Empty),
            };
            if (operation == Uuid.Empty || sourceId != seatSourceEventId)
                return Pending();
            var source = await ReadAtAsync(PrimaryPlatformOperators.Address, PrimaryPlatformOperators.OwnerId, seatSourcePosition, ct)
                .ConfigureAwait(false);
            if (source is null || source.Metadata.EventId != sourceId)
                return Pending();
            var matches = (source, fact) switch
            {
                (PrimaryPlatformOperatorSeatReserved s, PlatformOperatorPrimaryMarkAccepted x) => (!s.Bootstrap || s.InitialExistingGrant) &&
                    Match(s, x.UserId, x.OperationId, x.GrantEpisodeId, x.LineageId, x.Generation),
                (PrimaryPlatformOperatorSeatReserved s, PlatformOperatorBootstrapGrantAndMarkAccepted x) => s.Bootstrap && !s.InitialExistingGrant &&
                    s.Actor == x.Actor && Match(s, x.UserId, x.OperationId, x.GrantEpisodeId, x.LineageId, x.Generation),
                (PrimaryPlatformOperatorSeatReserved s, PlatformOperatorPrimaryMarkFenced x) =>
                    Match(s, x.UserId, x.OperationId, x.GrantEpisodeId, x.LineageId, x.Generation),
                (PrimaryPlatformOperatorSeatReleased s, PlatformOperatorPrimaryUnmarked x) => s.UserId == x.UserId &&
                    s.OperationId == x.OperationId && s.GrantEpisodeId == x.GrantEpisodeId && s.LineageId == x.LineageId &&
                    s.Generation == x.Generation && s.ReservationEventId == x.ReservationEventId,
                _ => false,
            };
            if (!matches)
                return Pending();
            var target = await aggregates.HydrateAsync(new PlatformOperator(userId, operation), ct).ConfigureAwait(false);
            if (target.SelectedOutcome?.Metadata.EventId != targetEventId)
                return Pending();
            if (source is PrimaryPlatformOperatorSeatReserved reservation && fact is not PlatformOperatorPrimaryMarkFenced &&
                (!target.IsActive || !target.IsPrimary || target.GrantEpisodeId != reservation.GrantEpisodeId ||
                 target.PrimaryLineageId != reservation.LineageId || target.PrimaryGeneration != reservation.Generation ||
                 target.PrimaryReservationEventId != seatSourceEventId))
                return Pending();
            var observed = await aggregates.HydrateAsync(new PrimaryPlatformOperators(operation), ct).ConfigureAwait(false);
            if (observed.SelectedCommand?.Metadata.EventId != seatSourceEventId)
                return Pending();
            var position = observed.CommittedStreamPosition;
            return await executor.ExecuteAsync(new PrimaryPlatformOperators(operation), owner =>
            {
                if (owner.CommittedStreamPosition != position || owner.SelectedCommand?.Metadata.EventId != seatSourceEventId)
                    return AggregateOutcome.Discard(Pending());
                var result = fact is PlatformOperatorPrimaryUnmarked unmarked
                    ? owner.RecordTargetUnmark(unmarked) : owner.RecordTargetOutcome(fact);
                return AggregateOutcome.CommitOnSuccess(result);
            }, context, ct).ConfigureAwait(false);
        }
        catch (EventStreamConcurrencyException) { return Pending(); }
        catch (FitzException) { return Pending(); }
        catch (IOException) { return Pending(); }
        catch (TimeoutException) { return Pending(); }
    }

    static bool Match(PrimaryPlatformOperatorSeatReserved source, Uuid userId, Uuid operationId,
        Uuid grantId, Uuid lineageId, ulong generation) => source.UserId == userId && source.OperationId == operationId &&
        source.GrantEpisodeId == grantId && source.LineageId == lineageId && source.Generation == generation;

    async ValueTask<Result> VerifyReservationOutcomeAsync(PrimaryPlatformOperatorSeatReserved source,
        ulong position, ulong revision, CancellationToken ct)
    {
        var outcome = await FindTargetOutcomeAsync(source.UserId, source.OperationId, position, ct).ConfigureAwait(false);
        if (outcome is null)
            return Pending();
        var (actualPosition, fact) = outcome.Value;
        return fact switch
        {
            PlatformOperatorPrimaryMarkAccepted x when actualPosition == position && (!source.Bootstrap || source.InitialExistingGrant) && x.UserId == source.UserId &&
                x.OperationId == source.OperationId && x.ExpectedRevision == revision && x.GrantEpisodeId == source.GrantEpisodeId &&
                x.LineageId == source.LineageId && x.Generation == source.Generation && x.ReservationEventId == source.Metadata.EventId => Result.Success,
            PlatformOperatorBootstrapGrantAndMarkAccepted x when actualPosition == position && source.Bootstrap && !source.InitialExistingGrant && x.UserId == source.UserId &&
                x.OperationId == source.OperationId && x.ExpectedRevision == revision && x.GrantEpisodeId == source.GrantEpisodeId &&
                x.LineageId == source.LineageId && x.Generation == source.Generation && x.ReservationEventId == source.Metadata.EventId &&
                x.Actor == source.Actor => Result.Success,
            PlatformOperatorPrimaryMarkFenced x when x.UserId == source.UserId && x.OperationId == source.OperationId &&
                x.GrantEpisodeId == source.GrantEpisodeId && x.LineageId == source.LineageId && x.Generation == source.Generation &&
                x.ReservationEventId == source.Metadata.EventId => Result.Success,
            _ => Pending(),
        };
    }

    async ValueTask<Result> VerifyUnmarkAsync(PrimaryPlatformOperatorSeatReleased source, ulong position, ulong revision,
        CancellationToken ct)
    {
        var outcome = await FindTargetOutcomeAsync(source.UserId, source.OperationId, position, ct).ConfigureAwait(false);
        if (outcome is null)
            return Pending();
        var (actualPosition, fact) = outcome.Value;
        return fact is PlatformOperatorPrimaryUnmarked x && x.UserId == source.UserId && x.OperationId == source.OperationId &&
            (actualPosition != position || x.ExpectedRevision == revision) && x.GrantEpisodeId == source.GrantEpisodeId && x.LineageId == source.LineageId &&
            x.Generation == source.Generation && x.ReservationEventId == source.ReservationEventId &&
            x.ReleaseEventId == source.Metadata.EventId ? Result.Success : Pending();
    }

    /// <summary>Finds the selected operation's actual committed target fact at or after its original slot.</summary>
    public async ValueTask<(ulong Position, DomainEvent Fact)?> FindTargetOutcomeAsync(Uuid userId, Uuid operationId,
        ulong originalPosition, CancellationToken ct = default)
    {
        var owner = await aggregates.HydrateAsync(new PlatformOperator(userId, operationId), ct).ConfigureAwait(false);
        var selected = owner.SelectedOutcome;
        if (selected is null || selected.Metadata.EventId == Uuid.Empty)
            return null;
        var next = originalPosition;
        await foreach (var record in events.ReadAsync(owner.Stream, originalPosition, ct).ConfigureAwait(false))
        {
            if (record.Stream != owner.Stream || record.ResourceOffset != next++ || record.Event.Metadata.AggregateId != owner.Id)
                return null;
            if (record.Event.Metadata.EventId == selected.Metadata.EventId)
                return record.Event == selected && !record.Event.Metadata.IsAudit ? (record.ResourceOffset, record.Event) : null;
        }
        return null;
    }

    async ValueTask<T?> ReadAsync<T>(EventStreamAddress stream, Uuid ownerId, ulong position, Uuid eventId,
        CancellationToken ct) where T : DomainEvent
    {
        var fact = await ReadAtAsync(stream, ownerId, position, ct).ConfigureAwait(false);
        return fact is T typed && fact.Metadata.EventId == eventId && eventId != Uuid.Empty ? typed : null;
    }

    async ValueTask<DomainEvent?> ReadAtAsync(EventStreamAddress stream, Uuid ownerId, ulong position, CancellationToken ct)
    {
        await using var reader = events.ReadAsync(stream, position, ct).GetAsyncEnumerator(ct);
        if (!await reader.MoveNextAsync().ConfigureAwait(false))
            return null;
        var record = reader.Current;
        return record.Stream == stream && record.ResourceOffset == position && !record.Event.Metadata.IsAudit &&
            record.Event.Metadata.AggregateId == ownerId && record.Event.Metadata.EventId != Uuid.Empty ? record.Event : null;
    }

    async ValueTask<bool> PositionOccupiedAsync(EventStreamAddress stream, Uuid ownerId, ulong position, CancellationToken ct)
    {
        await using var reader = events.ReadAsync(stream, position, ct).GetAsyncEnumerator(ct);
        if (!await reader.MoveNextAsync().ConfigureAwait(false))
            return false;
        var record = reader.Current;
        return record.Stream == stream && record.ResourceOffset == position && record.Event.Metadata.AggregateId == ownerId &&
            record.Event.Metadata.EventId != Uuid.Empty;
    }

    static Result Pending() => Result.Failure(new RequestError(RequestErrorKind.Conflict, "The exact primary participant outcome is pending.", true));
    static Result Forbidden() => Result.Failure(new RequestError(RequestErrorKind.Forbidden, "A system participant is required."));
}
