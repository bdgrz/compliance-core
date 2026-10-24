using System.Globalization;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     The acquire, apply, settle and rejection receivers of <c>TypeIIRangeProcess</c>. Each verifies the frozen
///     range intent against the authoritative period owner, resolves an exact replay from the target's accepted facts
///     and appends one fact. The calendar is the only owner that decides overlap; the period is the only owner that
///     decides whether an opening or amendment still applies. A transient wait is a transient conflict; a changed
///     intent or invalid proof is a permanent conflict; missing period state is never proof to release anything.
/// </summary>
sealed class TypeIIRangeReceivers(IAggregateReader reader, IAggregateExecutor executor, IImmutableContentStore content,
    IRequestBus bus, TimeProvider clock, TypeIIRangeVerifier verifier)
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> AcquireAsync(IExecutionContext context,
        AcquireTypeIIPeriodRange command, CancellationToken ct)
    {
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty ||
            command.Generation != TypeIIRangeEffects.Generation)
            return SourceUseReceiverSupport.Permanent("The acquisition names another generation or operation.");
        var change = await verifier.VerifyAsync(command.Target.Realm, command.Intent, command.OperationId,
            command.PeriodId, command.CommandId, ct).ConfigureAwait(false);
        if (change.Error is not null)
            return Result<ReceiverOutcome>.Failure(change.Error);
        var (tenant, period, accepted, intent) = (change.TenantId, change.Period!, change.Accepted!, change.Intent!);
        var calendarStream = new TypeIIObservationCalendar(tenant, intent.ProgramId).Stream;
        if (command.Target != calendarStream || command.NewRange != intent.NewRange ||
            command.OldRange != intent.OldRange ||
            intent.NewReservationKey != TypeIIRangeEffects.KeyOf(command.OperationId, period.Stream, calendarStream))
            return SourceUseReceiverSupport.Permanent("The acquisition differs from the frozen range intent.");
        if (command.Source.Stream != period.Stream || accepted.Metadata is not { } metadata ||
            metadata.EventId != command.Source.EventId || metadata.AggregateVersion != command.Source.AggregateVersion)
            return SourceUseReceiverSupport.Permanent("The source is not the accepted operation of this period.");
        if (period.IsFenced(command.OperationId))
            return SourceUseReceiverSupport.Superseded("The opening or amendment ended before its range was acquired.",
                Fact(period.Stream, period.TerminalFor(command.OperationId)!));

        var provenance = TypeIIRangeEffects.Child(command.CommandId, nameof(AcquireTypeIIPeriodRange),
            command.Source.EventId, intent, calendarStream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var priorOperation = intent.PriorGrant?.Key.RootOperationId;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var calendar = await reader.HydrateAsync(new TypeIIObservationCalendar(tenant, intent.ProgramId,
                    command.CommandId), ct).ConfigureAwait(false);
                var early = Evaluate(calendar, binding, intent);
                if (early is Decided decided)
                    return decided.Outcome;
                if (early is Conflicted blocked)
                    return await RejectConflictAsync(context, command, change, blocked.Reservation, calendar, ct)
                        .ConfigureAwait(false);

                var step = await executor.ExecuteAsync(new TypeIIObservationCalendar(tenant, intent.ProgramId,
                    command.CommandId), fresh =>
                    {
                        var current = Evaluate(fresh, binding, intent);
                        if (current is not null)
                            return AggregateOutcome.Discard(Result<Step>.Success(current));
                        var raised = fresh.Authorize(provenance, command.Intent, intent.PeriodId,
                            intent.OperationKind, intent.NewRange, priorOperation);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(new Decided(SourceUseReceiverSupport.Applied(
                                Fact(fresh.Stream, fresh.Acceptance.SelectedFact!)))))
                            : AggregateOutcome.Discard(Result<Step>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                if (step.Value is Conflicted conflicted)
                    return await RejectConflictAsync(context, command, change, conflicted.Reservation, calendar, ct)
                        .ConfigureAwait(false);
                return ((Decided)step.Value!).Outcome;
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The calendar changed concurrently; rehydrate and decide again against the new overlap set.
            }
        }
    }

    public async ValueTask<Result<ReceiverOutcome>> ApplyAsync(IExecutionContext context,
        ApplyTypeIIPeriodRange command, CancellationToken ct)
    {
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty ||
            command.Generation != TypeIIRangeEffects.Generation)
            return SourceUseReceiverSupport.Permanent("The apply names another generation or operation.");
        var change = await verifier.VerifyAsync(command.Target.Realm, command.Intent, command.OperationId,
            command.PeriodId, command.CommandId, ct).ConfigureAwait(false);
        if (change.Error is not null)
            return Result<ReceiverOutcome>.Failure(change.Error);
        var (tenant, period, intent) = (change.TenantId, change.Period!, change.Intent!);
        if (command.Target != period.Stream)
            return SourceUseReceiverSupport.Permanent("The apply names another period stream.");

        var operation = command.OperationId;
        var provenance = TypeIIRangeEffects.Child(command.CommandId, nameof(ApplyTypeIIPeriodRange),
            command.Source.EventId, intent, period.Stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        // The period-local fence wins over a delayed apply: a canceled or rejected operation never applies.
        if (DecidePeriod(period, binding, operation, intent) is { } early)
            return early;

        var calendar = await reader.HydrateAsync(new TypeIIObservationCalendar(tenant, intent.ProgramId), ct)
            .ConfigureAwait(false);
        if (command.Source.Stream != calendar.Stream)
            return SourceUseReceiverSupport.Permanent("The permit is not a fact of this program calendar.");
        if (calendar.FindByAuthorization(command.Source.EventId) is not { } reservation ||
            reservation.AuthorizedFact!.Metadata.AggregateVersion != command.Source.AggregateVersion)
            return SourceUseReceiverSupport.Transient("The calendar permit is not visible yet.");
        if (reservation.OperationId != operation || reservation.PeriodId != intent.PeriodId ||
            reservation.Range != intent.NewRange || reservation.Kind != intent.OperationKind ||
            reservation.Generation <= 0)
            return SourceUseReceiverSupport.Permanent("The permit differs from the frozen range intent.");
        if (!reservation.IsLive)
            return SourceUseReceiverSupport.Permanent("The calendar permit was released before it was applied.");
        var permit = new TypeIIPeriodCalendarAuthorization(operation, reservation.Generation);

        TypeIIPeriodOpeningChange? opening = null;
        TypeIIPeriodAmendmentChange? amendment = null;
        try
        {
            if (intent.OperationKind == TypeIIRangeOperationKind.Opening)
                opening = await content.ReadJsonAsync<TypeIIPeriodOpeningChange>(command.Target.Realm,
                    intent.OpeningOrAmendmentContent, ct).ConfigureAwait(false);
            else
                amendment = await content.ReadJsonAsync<TypeIIPeriodAmendmentChange>(command.Target.Realm,
                    intent.OpeningOrAmendmentContent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Permanent($"The frozen change cannot be verified: {exception.Message}");
        }

        var result = await TypeIIRangeContent.UnitResultAsync(content, command.Target.Realm, operation, ct)
            .ConfigureAwait(false);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new TypeIIPeriod(tenant, intent.ProgramId, intent.PeriodId,
                    command.CommandId), fresh =>
                    {
                        if (DecidePeriod(fresh, binding, operation, intent) is { } decided)
                            return AggregateOutcome.Discard(Result<Step>.Success(new Decided(decided)));
                        var raised = opening is not null
                            ? fresh.ApplyOpening(provenance, opening.State, permit, result)
                            : fresh.ApplyAmendment(provenance, amendment!, permit, result);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(new Decided(SourceUseReceiverSupport.Applied(
                                Fact(fresh.Stream, fresh.Acceptance.SelectedFact!)))))
                            : AggregateOutcome.Discard(Result<Step>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                return step.IsSuccess
                    ? ((Decided)step.Value!).Outcome
                    : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The period changed concurrently; rehydrate and decide again, including against a late fence.
            }
        }
    }

    public async ValueTask<Result<ReceiverOutcome>> SettleAsync(IExecutionContext context,
        SettleTypeIIPeriodRange command, CancellationToken ct)
    {
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty ||
            command.Generation != TypeIIRangeEffects.Generation)
            return SourceUseReceiverSupport.Permanent("The settlement names another generation or operation.");
        var change = await verifier.VerifyAsync(command.Target.Realm, command.Intent, command.OperationId,
            command.PeriodId, command.CommandId, ct).ConfigureAwait(false);
        if (change.Error is not null)
            return Result<ReceiverOutcome>.Failure(change.Error);
        var (tenant, period, intent) = (change.TenantId, change.Period!, change.Intent!);
        var calendarStream = new TypeIIObservationCalendar(tenant, intent.ProgramId).Stream;
        if (command.Target != calendarStream)
            return SourceUseReceiverSupport.Permanent("The settlement names another calendar stream.");
        // Missing or unknown period state is transient: it is never proof that anything can be released.
        if (command.Source.Stream != period.Stream || period.FactById(command.Source.EventId) is not { } fact)
            return SourceUseReceiverSupport.Transient("The period fact that proves the settlement is not visible yet.");
        if (fact.Metadata!.AggregateVersion != command.Source.AggregateVersion)
            return SourceUseReceiverSupport.Permanent("The settlement source differs from the period fact.");
        var plan = PlanOf(fact, command.OperationId, intent);
        if (plan is null)
            return SourceUseReceiverSupport.Permanent("The source is not a settlement proof of this operation.");

        var provenance = TypeIIRangeEffects.Child(command.CommandId, nameof(SettleTypeIIPeriodRange),
            command.Source.EventId, intent, calendarStream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var calendar = await reader.HydrateAsync(new TypeIIObservationCalendar(tenant, intent.ProgramId,
                    command.CommandId), ct).ConfigureAwait(false);
                if (DecideSettlement(calendar, binding, command.OperationId, plan) is Decided early)
                    return early.Outcome;

                var step = await executor.ExecuteAsync(new TypeIIObservationCalendar(tenant, intent.ProgramId,
                    command.CommandId), fresh =>
                    {
                        var current = DecideSettlement(fresh, binding, command.OperationId, plan);
                        if (current is Decided decided)
                            return AggregateOutcome.Discard(Result<Step>.Success(decided));
                        var action = (Settlement)current!;
                        var raised = fresh.Settle(provenance, command.Intent, intent.PeriodId, action.Kind,
                            command.OperationId, action.Released);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(new Decided(SourceUseReceiverSupport.Applied(
                                Fact(fresh.Stream, fresh.Acceptance.SelectedFact!)))))
                            : AggregateOutcome.Discard(Result<Step>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                return step.IsSuccess
                    ? ((Decided)step.Value!).Outcome
                    : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The calendar changed concurrently; rehydrate and decide again.
            }
        }
    }

    public ValueTask<Result<ReceiverOutcome>> RejectOpeningAsync(IExecutionContext context,
        RejectTypeIIPeriodOpening command, CancellationToken ct) => RejectAsync(context, command.CommandId,
        command.OperationId, command.Target, command.Generation, command.ExpectedRevision, command.FailureProof, true,
        ct);

    public ValueTask<Result<ReceiverOutcome>> RejectAmendmentAsync(IExecutionContext context,
        RejectTypeIIPeriodAmendment command, CancellationToken ct) => RejectAsync(context, command.CommandId,
        command.OperationId, command.Target, command.Generation, command.ExpectedRevision, command.FailureProof, false,
        ct);

    async ValueTask<Result<ReceiverOutcome>> RejectAsync(IExecutionContext context, Uuid commandId, Uuid operationId,
        EventStreamAddress target, long generation, long expectedRevision, ImmutableContentRef failureProof,
        bool opening, CancellationToken ct)
    {
        if (commandId == Uuid.Empty || operationId == Uuid.Empty || generation != TypeIIRangeEffects.Generation ||
            !Uuid.TryParse(target.Resource, CultureInfo.InvariantCulture, out var periodId))
            return SourceUseReceiverSupport.Permanent("The rejection names another generation, operation or period.");
        TypeIIRangeFailureProof proof;
        try
        {
            proof = await content.ReadJsonAsync<TypeIIRangeFailureProof>(target.Realm, failureProof, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Permanent($"The failure proof cannot be verified: {exception.Message}");
        }

        var change = await verifier.VerifyAsync(target.Realm, proof.Intent, operationId, periodId, commandId, ct)
            .ConfigureAwait(false);
        if (change.Error is not null)
            return Result<ReceiverOutcome>.Failure(change.Error);
        var (tenant, period, accepted, intent) = (change.TenantId, change.Period!, change.Accepted!, change.Intent!);
        if (target != period.Stream || opening != (intent.OperationKind == TypeIIRangeOperationKind.Opening) ||
            expectedRevision != change.AcceptedRevision)
            return SourceUseReceiverSupport.Permanent(
                "A rejection requires the exact pending operation of its period at its accepted revision.");

        // The authoritative calendar must hold another period's range that overlaps this one.
        var calendar = await reader.HydrateAsync(new TypeIIObservationCalendar(tenant, intent.ProgramId), ct)
            .ConfigureAwait(false);
        if (proof.Conflicting.Stream != calendar.Stream ||
            calendar.FindByAuthorization(proof.Conflicting.EventId) is not { } conflicting ||
            conflicting.AuthorizedFact!.Metadata.AggregateVersion != proof.Conflicting.AggregateVersion ||
            conflicting.PeriodId == intent.PeriodId || conflicting.Range is not { } range ||
            !range.Overlaps(intent.NewRange))
            return SourceUseReceiverSupport.Permanent("The calendar conflict is not proven by the failure proof.");

        var kind = opening ? nameof(RejectTypeIIPeriodOpening) : nameof(RejectTypeIIPeriodAmendment);
        var provenance = TypeIIRangeEffects.Child(commandId, kind, accepted.Metadata!.EventId, intent, period.Stream,
            clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new TypeIIPeriod(tenant, intent.ProgramId, intent.PeriodId,
                    commandId), pending =>
                    {
                        switch (pending.Acceptance.Decide(binding))
                        {
                            case NativeAcceptanceDecision.Replayed:
                                return AggregateOutcome.Discard(Result<Step>.Success(new Decided(
                                    SourceUseReceiverSupport.Replayed(Fact(pending.Stream,
                                        pending.Acceptance.SelectedFact!)))));
                            case NativeAcceptanceDecision.Conflict:
                                return AggregateOutcome.Discard(Result<Step>.Success(new Decided(
                                    Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError))));
                        }

                        if (pending.TerminalFor(operationId) is { } terminal)
                            return AggregateOutcome.Discard(Result<Step>.Success(new Decided(
                                SourceUseReceiverSupport.Superseded(pending.IsFenced(operationId)
                                    ? "The opening or amendment was already rejected or canceled."
                                    : "The opening or amendment already applied and recovers forward.",
                                Fact(pending.Stream, terminal)))));
                        var raised = opening
                            ? pending.RejectOpening(provenance, failureProof)
                            : pending.RejectAmendment(provenance, failureProof);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(new Decided(SourceUseReceiverSupport.Applied(
                                Fact(pending.Stream, pending.Acceptance.SelectedFact!)))))
                            : AggregateOutcome.Discard(Result<Step>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                return step.IsSuccess
                    ? ((Decided)step.Value!).Outcome
                    : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The period changed concurrently; rehydrate and decide again.
            }
        }
    }

    /// <summary>
    ///     The calendar refused the range: the period's opening or amendment is rejected first, which fences a delayed
    ///     apply, and the process ends. The pending reservation, if any, is released only after that fence.
    /// </summary>
    async ValueTask<Result<ReceiverOutcome>> RejectConflictAsync(IExecutionContext context,
        AcquireTypeIIPeriodRange command, TypeIIRangeChange change, TypeIIRangeReservation conflicting, TypeIIObservationCalendar calendar,
        CancellationToken ct)
    {
        var (period, accepted, intent) = (change.Period!, change.Accepted!, change.Intent!);
        var proof = await content.WriteJsonAsync(command.Target.Realm, Uuid.CreateVersion4(),
            TypeIIRangeFailureProof.Schema, new TypeIIRangeFailureProof(command.Intent,
                Fact(calendar.Stream, conflicting.AuthorizedFact!)), ct).ConfigureAwait(false);
        var opening = intent.OperationKind == TypeIIRangeOperationKind.Opening;
        IRequest<ReceiverOutcome> rejection = opening
            ? new RejectTypeIIPeriodOpening(TypeIIRangeEffects.ReceiverCommandId(accepted.Metadata!.EventId,
                nameof(RejectTypeIIPeriodOpening), intent.Header.Episode), command.OperationId, period.Stream,
                TypeIIRangeEffects.Generation, change.AcceptedRevision, proof)
            : new RejectTypeIIPeriodAmendment(TypeIIRangeEffects.ReceiverCommandId(accepted.Metadata!.EventId,
                nameof(RejectTypeIIPeriodAmendment), intent.Header.Episode), command.OperationId, period.Stream,
                TypeIIRangeEffects.Generation, change.AcceptedRevision, proof);
        var rejected = await bus.SendAsync(rejection, context, ct).ConfigureAwait(false);
        if (!rejected.IsSuccess)
            return rejected;
        var evidence = rejected.Value switch
        {
            ReceiverOutcome.Applied applied => applied.Fact,
            ReceiverOutcome.Replayed replayed => replayed.Fact,
            ReceiverOutcome.Superseded superseded => superseded.Evidence,
            _ => null,
        };
        return SourceUseReceiverSupport.Superseded(
            "The calendar proved the range conflicts with another period; the operation was rejected.", evidence);
    }

    /// <summary>The acquisition decision: an exact replay, a fence, a refused prior range or an overlap.</summary>
    static Step? Evaluate(TypeIIObservationCalendar calendar, AcceptedOperationBinding binding,
        TypeIIRangeIntent intent)
    {
        switch (calendar.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return new Decided(SourceUseReceiverSupport.Replayed(Fact(calendar.Stream,
                    calendar.Acceptance.SelectedFact!)));
            case NativeAcceptanceDecision.Conflict:
                return new Decided(Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError));
        }

        if (calendar.Find(intent.Header.RootOperationId) is { } existing)
            return new Decided(existing.State == TypeIIRangeReservationState.Fenced
                ? SourceUseReceiverSupport.Superseded(
                    "The range was settled before it was acquired; the operation is fenced.",
                    Fact(calendar.Stream, existing.SettledFact!))
                : existing.PeriodId == intent.PeriodId && existing.Range == intent.NewRange &&
                  existing.Kind == intent.OperationKind
                    ? SourceUseReceiverSupport.Replayed(Fact(calendar.Stream, existing.AuthorizedFact!))
                    : SourceUseReceiverSupport.Permanent("The operation holds a different range on the calendar."));
        if (intent.PriorGrant is { } grant)
        {
            if (calendar.Find(grant.Key.RootOperationId) is not { } prior)
                return new Decided(SourceUseReceiverSupport.Transient("The prior calendar range is not visible yet."));
            if (!prior.IsLive || prior.PeriodId != intent.PeriodId || prior.Range != intent.OldRange ||
                prior.Generation != (long)grant.ReservationGeneration)
                return new Decided(SourceUseReceiverSupport.Permanent(
                    "The held old range differs from the frozen amendment intent."));
        }

        return calendar.FindOverlap(intent.PeriodId, intent.NewRange) is { } conflicting
            ? new Conflicted(conflicting)
            : null;
    }

    /// <summary>The apply decision of the period: an exact replay, a fence, or an operation that already applied.</summary>
    static Result<ReceiverOutcome>? DecidePeriod(TypeIIPeriod period, AcceptedOperationBinding binding, Uuid operation,
        TypeIIRangeIntent intent)
    {
        switch (period.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(Fact(period.Stream, period.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }

        if (period.IsFenced(operation))
            return SourceUseReceiverSupport.Superseded(
                "The opening or amendment was rejected or canceled; the delayed apply does nothing.",
                Fact(period.Stream, period.TerminalFor(operation)!));
        return period.IsApplied(operation)
            ? SourceUseReceiverSupport.Replayed(Fact(period.Stream, period.TerminalFor(operation)!))
            : intent.PeriodId == period.Id ? null
            : SourceUseReceiverSupport.Permanent("The apply names another period.");
    }

    /// <summary>What the calendar does for a settlement proof: replay, supersede, wait, refuse, or settle.</summary>
    static Step DecideSettlement(TypeIIObservationCalendar calendar, AcceptedOperationBinding binding,
        Uuid operation, SettlementPlan plan)
    {
        switch (calendar.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return new Decided(SourceUseReceiverSupport.Replayed(Fact(calendar.Stream,
                    calendar.Acceptance.SelectedFact!)));
            case NativeAcceptanceDecision.Conflict:
                return new Decided(Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError));
        }

        var target = calendar.Find(operation);
        if (target is null)
            return plan.Source == SettlementSource.Ended
                ? new Settlement(TypeIIRangeSettlementKind.Fenced, null)
                : new Decided(SourceUseReceiverSupport.Transient("The calendar reservation is not visible yet."));
        if (target.State == TypeIIRangeReservationState.Fenced)
            return new Decided(SourceUseReceiverSupport.Replayed(Fact(calendar.Stream, target.SettledFact!)));
        if (plan.Generation is { } generation && target.Generation != generation)
            return new Decided(SourceUseReceiverSupport.Permanent(
                "The proof names another calendar generation than the reservation it settles."));
        if (target.State == TypeIIRangeReservationState.Released)
            return new Decided(SourceUseReceiverSupport.Superseded(
                "The reservation was already released; nothing newer is touched.",
                Fact(calendar.Stream, target.SettledFact!)));
        switch (plan.Source)
        {
            case SettlementSource.Applied:
                if (target.State == TypeIIRangeReservationState.Active)
                    return new Decided(SourceUseReceiverSupport.Superseded("The reservation is already active.",
                        target.SettledFact is null ? null : Fact(calendar.Stream, target.SettledFact)));
                var replaced = plan.Replaces is { } prior && calendar.Find(prior) is { IsLive: true } held &&
                               held.PeriodId == target.PeriodId
                    ? prior
                    : (Uuid?)null;
                return new Settlement(TypeIIRangeSettlementKind.Activated, replaced);
            case SettlementSource.Close:
                return new Settlement(TypeIIRangeSettlementKind.Released, null);
            default:
                return target.State == TypeIIRangeReservationState.Active
                    ? new Decided(SourceUseReceiverSupport.Superseded(
                        "The operation already applied and recovers forward; only a pending range is released.",
                        Fact(calendar.Stream, target.SettledFact!)))
                    : new Settlement(TypeIIRangeSettlementKind.Released, null);
        }
    }

    static SettlementPlan? PlanOf(DomainEvent fact, Uuid operation, TypeIIRangeIntent intent) => fact switch
    {
        ObservationPeriodStarted started when started.CalendarAuthorizationId == operation &&
            intent.OperationKind == TypeIIRangeOperationKind.Opening =>
            new SettlementPlan(SettlementSource.Applied, started.CalendarGeneration, null),
        ObservationPeriodAmended amended when amended.CalendarAuthorizationId == operation &&
            intent.PriorGrant is not null && intent.OperationKind == TypeIIRangeOperationKind.Amendment =>
            new SettlementPlan(SettlementSource.Applied, amended.CalendarGeneration,
                intent.PriorGrant.Key.RootOperationId),
        ObservationPeriodCloseFrozen frozen when frozen.CalendarAuthorizationId == operation =>
            new SettlementPlan(SettlementSource.Close, frozen.CalendarGeneration, null),
        TypeIIPeriodOpeningRejected rejected when rejected.Operation.RootOperationId == operation &&
            intent.OperationKind == TypeIIRangeOperationKind.Opening =>
            new SettlementPlan(SettlementSource.Ended, null, null),
        TypeIIPeriodAmendmentCanceled canceled when canceled.Operation.RootOperationId == operation &&
            intent.OperationKind == TypeIIRangeOperationKind.Amendment =>
            new SettlementPlan(SettlementSource.Ended, null, null),
        _ => null,
    };

    static NativeFactReference Fact(EventStreamAddress stream, DomainEvent fact) => NativeFactReference.Of(stream, fact);

    enum SettlementSource
    {
        Applied,
        Close,
        Ended,
    }

    sealed record SettlementPlan(SettlementSource Source, long? Generation, Uuid? Replaces);

    abstract record Step;

    sealed record Decided(Result<ReceiverOutcome> Outcome) : Step;

    sealed record Conflicted(TypeIIRangeReservation Reservation) : Step;

    sealed record Settlement(TypeIIRangeSettlementKind Kind, Uuid? Released) : Step;
}
