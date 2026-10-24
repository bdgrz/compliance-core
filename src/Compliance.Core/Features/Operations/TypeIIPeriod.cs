using Bdgrz.Compliance.Hosting.Persistence;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
/// Owns one Type II observation period from its approved-plan opening through close and any
/// attributable close amendments. The approved plan and shared control/evidence records remain
/// separate streams and are bound here by identity and digest.
/// </summary>
public sealed class TypeIIPeriod : Aggregate
{
    /// <summary>Stream area of every Type II period (the realm is the tenant).</summary>
    public const string Area = "type-ii-periods";

    const int MaximumSources = 1000;
    const int MaximumOccurrences = 10000;
    const int MaximumAmendments = 64;
    const int MaximumEventBytes = DomainEventPayload.MaximumBytes;
    const int MaximumTextLength = 2000;
    static readonly HashSet<string> ExecutiveRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "ceo", "ciso", "chief executive", "chief executive officer",
        "chief information security officer"
    };

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Dictionary<Uuid, DomainEvent> _accepted = [];
    readonly Dictionary<Uuid, DomainEvent> _terminal = [];
    readonly List<DomainEvent> _facts = [];
    Uuid? _pending;
    TypeIIPeriodCalendarAuthorization? _live;
    TypeIIPeriodView? _current;
    readonly List<DueControlOccurrencesGenerated> _generations = [];

    public long Revision => _current?.Revision ?? 0;
    public TypeIIPeriodView? Current => _current;

    /// <summary>Every occurrence generation of the period in order; each selects a complete immutable manifest.</summary>
    public IReadOnlyList<DueControlOccurrencesGenerated> OccurrenceGenerations => _generations;

    /// <summary>Opens one period, selecting the logical operation to resolve.</summary>
    public TypeIIPeriod(Uuid tenantId, Uuid programId, Uuid periodId, Uuid selectedLogicalId = default)
        : base(periodId, new EventStreamAddress(tenantId.ToString(), Area, periodId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<TypeIIPeriodOpeningAccepted>(accepted =>
            Accept(accepted, accepted.Owner, accepted.Revision, accepted.Generation, accepted.Operation,
                accepted.FirstResponse, true));
        On<TypeIIPeriodAmendmentAccepted>(accepted =>
            Accept(accepted, accepted.Owner, accepted.Revision, accepted.Generation, accepted.Operation,
                accepted.FirstResponse, false));
        On<TypeIIPeriodOpeningRejected>(rejected => Fence(rejected, rejected.Owner, rejected.Operation,
            rejected.Intent, true));
        On<TypeIIPeriodAmendmentCanceled>(canceled => Fence(canceled, canceled.Owner, canceled.Operation,
            canceled.Intent, false));
        On<ObservationPeriodStarted>(Apply);
        On<ObservationPeriodAmended>(Apply);
        On<DueControlOccurrencesGenerated>(Apply);
        On<ObservationPeriodCloseProposed>(Apply);
        On<TypeIIPeriodCloseApproved>(Apply);
        On<ObservationPeriodCloseFrozen>(Apply);
        On<TypeIIPeriodCloseAmended>(Apply);
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The root operation of the accepted opening or amendment that is not applied or ended yet.</summary>
    public Uuid? PendingOperationId => _pending;

    /// <summary>The accepted opening or amendment that is neither applied, rejected nor canceled yet.</summary>
    public DomainEvent? Pending => _pending is { } id ? _accepted[id] : null;

    /// <summary>The exact live calendar reservation of the period: the latest applied opening or amendment.</summary>
    public TypeIIPeriodCalendarAuthorization? CalendarAuthorization => _live;

    /// <summary>The accepted opening or amendment of a root operation, once recorded.</summary>
    public DomainEvent? AcceptedFor(Uuid operationId) => _accepted.GetValueOrDefault(operationId);

    /// <summary>The applied, rejected or canceled fact that ended a root operation, once recorded.</summary>
    public DomainEvent? TerminalFor(Uuid operationId) => _terminal.GetValueOrDefault(operationId);

    /// <summary>True when the opening or amendment was rejected or canceled, so a delayed apply must do nothing.</summary>
    public bool IsFenced(Uuid operationId) =>
        TerminalFor(operationId) is TypeIIPeriodOpeningRejected or TypeIIPeriodAmendmentCanceled;

    /// <summary>True when the opening or amendment was applied to the period.</summary>
    public bool IsApplied(Uuid operationId) =>
        TerminalFor(operationId) is ObservationPeriodStarted or ObservationPeriodAmended;

    /// <summary>A committed range fact of the period (applied, close-frozen, rejected or canceled) by its event id.</summary>
    public DomainEvent? FactById(Uuid eventId) =>
        _facts.FirstOrDefault(fact => fact.Metadata?.EventId == eventId);

    /// <summary>The frozen intent of an accepted opening or amendment fact.</summary>
    public static ImmutableContentRef? IntentOf(DomainEvent? accepted) => accepted switch
    {
        TypeIIPeriodOpeningAccepted opening => opening.Intent,
        TypeIIPeriodAmendmentAccepted amendment => amendment.Intent,
        _ => null,
    };

    /// <summary>
    ///     Validates an opening against an approved plan and returns the complete initial state that is frozen into the
    ///     persisted intent. Nothing is raised.
    /// </summary>
    public CommandFailure? PrepareOpening(TypeIIPlanView approvedPlan, TypeIIPeriodOpeningContent opening,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset startedAt, out TypeIIPeriodView? initial)
    {
        initial = null;
        if (_current is not null || _accepted.Count > 0)
            return CommandFailure.StateConflict("The Type II period already exists with a different opening.");
        if (!ValidIdentity() || !ValidActor(actorMemberId, actorDisplay, startedAt))
            return Invalid("A Type II period requires tenant, program, period, and attributed actor identities.");
        if (!ApprovedPlan(approvedPlan) || approvedPlan.TenantId != _tenantId ||
            approvedPlan.ProgramId != _programId || NormalizeOpening(opening) is not { } normalizedOpening ||
            approvedPlan.Current!.Content.ExaminationId != normalizedOpening.ExaminationId)
            return CommandFailure.StateConflict("Opening requires a valid, approved Type II plan for the same examination.");
        var planContent = approvedPlan.Current.Content;
        if (!planContent.AcknowledgedGaps.Select(item => item.GapId)
                .OrderBy(IdKey, StringComparer.Ordinal)
                .SequenceEqual(normalizedOpening.AcknowledgedOpeningGapIds.OrderBy(IdKey, StringComparer.Ordinal)))
            return CommandFailure.StateConflict("Every approved-plan opening gap must be acknowledged exactly once.");
        if (BuildExpectedOccurrences(Id, planContent, planContent.PeriodStart, planContent.PeriodEnd)
            is not { } occurrences)
            return CommandFailure.InvalidContent("The approved control cadence does not fit the proposed observation period.");

        var state = new TypeIIPeriodView(_tenantId, _programId, Id, "active", 1,
            normalizedOpening.ExaminationId, approvedPlan.TypeIIPlanId,
            approvedPlan.CurrentVersionNumber, approvedPlan.Current.ContentSha256,
            planContent.PeriodStart, planContent.PeriodEnd, normalizedOpening.OpeningSnapshotId,
            normalizedOpening.OpeningSnapshotSha256, normalizedOpening.SourceReferences,
            normalizedOpening.AcknowledgedOpeningGapIds, occurrences, [], null, [], [],
            actorMemberId, actorDisplay.Trim(), startedAt);
        if (ValidateState(state) is { } invalid)
            return invalid;
        if (!ValidOpeningState(state))
            return Invalid("The Type II period transition is invalid.");
        initial = Freeze(state);
        return null;
    }

    /// <summary>Checks that the period can still accept its opening; the period opens at most once.</summary>
    public Result PrepareOpeningAcceptance() => _current is null && _accepted.Count == 0
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict,
            "The Type II period already exists with a different opening."));

    /// <summary>Accepts the complete opening intent as the root native fact of the period; no calendar is touched.</summary>
    public Result AcceptOpening(OperationProvenance operation, ImmutableContentRef intent,
        ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.Owner != Stream || operation.LogicalId != operation.RootOperationId ||
            operation.RootOperationId != Id)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A period opening is the logical operation of its own period."));
        var prepared = PrepareOpeningAcceptance();
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new TypeIIPeriodOpeningAccepted(Stream, Revision, TypeIIRangeEffects.Generation, intent, null,
            operation, firstResponse, null));
        return Result.Success;
    }

    /// <summary>
    ///     Validates an amendment against the current period and returns the state it would produce. The expected
    ///     revision is the period's logical revision; only one opening or amendment is pending at a time.
    /// </summary>
    public CommandFailure? PrepareAmendment(long expectedRevision, DateOnly periodStart, DateOnly periodEnd,
        string reason, Uuid actorMemberId, string actorDisplay, DateTimeOffset amendedAt, out TypeIIPeriodView? next)
    {
        next = null;
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The Type II period was not found.");
        if (_pending is not null)
            return CommandFailure.StateConflict(
                "Another opening or amendment is still being applied; retry after it ends.");
        if (expectedRevision != Revision)
            return StaleRevision();
        return AmendedState(current, periodStart, periodEnd, reason, actorMemberId, actorDisplay, amendedAt,
            out next);
    }

    /// <summary>Accepts the complete amendment intent, holding both ranges, as the root native fact of the amendment.</summary>
    public Result AcceptAmendment(long expectedRevision, OperationProvenance operation, ImmutableContentRef intent,
        ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.Owner != Stream || operation.LogicalId != operation.RootOperationId)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A period amendment is the logical operation of its own command."));
        if (_current is null)
            return Result.Failure(CommandFailure.MissingRecord("The Type II period was not found.").ToRequestError());
        if (_pending is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Another opening or amendment is still being applied; retry after it ends.", true));
        if (expectedRevision != Revision)
            return Result.Failure(StaleRevision().ToRequestError());
        RaiseEvent(new TypeIIPeriodAmendmentAccepted(Stream, Revision, TypeIIRangeEffects.Generation, intent, null,
            operation, firstResponse, null));
        return Result.Success;
    }

    /// <summary>Checks that a pending, not yet applied amendment can be canceled at the expected revision.</summary>
    public Result PrepareCancel(Uuid amendmentId, long expectedRevision)
    {
        if (_pending != amendmentId || _accepted[amendmentId] is not TypeIIPeriodAmendmentAccepted)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only a pending amendment can be canceled; an applied amendment is irreversible."));
        return expectedRevision == Revision
            ? Result.Success
            : Result.Failure(StaleRevision().ToRequestError());
    }

    /// <summary>
    ///     Cancels the pending amendment. This fact is the local receiver fence: a delayed apply sees it and does
    ///     nothing, and the pending calendar reservation is released only after it.
    /// </summary>
    public Result CancelAmendment(Uuid amendmentId, OperationProvenance operation, ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.Owner != Stream || operation.RootOperationId != amendmentId)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A cancellation names the amendment it ends."));
        if (_pending != amendmentId || _accepted[amendmentId] is not TypeIIPeriodAmendmentAccepted accepted)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only a pending amendment can be canceled; an applied amendment is irreversible."));
        RaiseEvent(new TypeIIPeriodAmendmentCanceled(Stream, Revision, TypeIIRangeEffects.Generation,
            accepted.Intent, null, operation, firstResponse, null));
        return Result.Success;
    }

    /// <summary>Rejects the pending opening that the calendar proved conflicts; an applied opening is never rejected.</summary>
    public Result RejectOpening(OperationProvenance operation, ImmutableContentRef failureProof)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(failureProof);
        if (_pending != operation.RootOperationId || operation.Owner != Stream ||
            _accepted[operation.RootOperationId] is not TypeIIPeriodOpeningAccepted accepted)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only the exact pending opening can be rejected."));
        RaiseEvent(new TypeIIPeriodOpeningRejected(Stream, Revision, TypeIIRangeEffects.Generation, accepted.Intent,
            failureProof, operation, null, null));
        return Result.Success;
    }

    /// <summary>Rejects the pending amendment that the calendar proved conflicts; the applied range is preserved.</summary>
    public Result RejectAmendment(OperationProvenance operation, ImmutableContentRef failureProof)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(failureProof);
        if (_pending != operation.RootOperationId || operation.Owner != Stream ||
            _accepted[operation.RootOperationId] is not TypeIIPeriodAmendmentAccepted accepted)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only the exact pending amendment can be rejected."));
        RaiseEvent(new TypeIIPeriodAmendmentCanceled(Stream, Revision, TypeIIRangeEffects.Generation,
            accepted.Intent, failureProof, operation, null, null));
        return Result.Success;
    }

    /// <summary>
    ///     Applies the frozen opening: the period starts with the exact initial state persisted at admission and the
    ///     calendar permit that holds its range.
    /// </summary>
    public Result ApplyOpening(OperationProvenance operation, TypeIIPeriodView state,
        TypeIIPeriodCalendarAuthorization permit, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(permit);
        ArgumentNullException.ThrowIfNull(operationResult);
        if (_pending != operation.RootOperationId || operation.Owner != Stream || _current is not null ||
            _accepted[operation.RootOperationId] is not TypeIIPeriodOpeningAccepted accepted)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only the exact pending opening can be applied."));
        if (ValidateState(state) is { } invalid)
            return Result.Failure(invalid.ToRequestError());
        var started = new ObservationPeriodStarted(_tenantId, Id, state.Revision, state.ActorMemberId,
            Freeze(state), state.UpdatedAt, accepted.Intent, operation, permit.AuthorizationId, permit.Generation,
            null, operationResult);
        if (DomainEventPayload.Size(started) > MaximumEventBytes)
            return Result.Failure(Invalid("The complete Type II period state exceeds the bounded event payload.")
                .ToRequestError());
        RaiseEvent(started);
        return Result.Success;
    }

    /// <summary>
    ///     Applies the frozen amendment to the current period with the calendar permit that holds the new range. The
    ///     state is derived from the period as it is now; the expected revision was proven at admission.
    /// </summary>
    public Result ApplyAmendment(OperationProvenance operation, TypeIIPeriodAmendmentChange change,
        TypeIIPeriodCalendarAuthorization permit, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(permit);
        ArgumentNullException.ThrowIfNull(operationResult);
        if (_pending != operation.RootOperationId || operation.Owner != Stream || _current is not { } current ||
            _accepted[operation.RootOperationId] is not TypeIIPeriodAmendmentAccepted accepted)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only the exact pending amendment can be applied."));
        var at = change.AmendedAt < current.UpdatedAt ? current.UpdatedAt : change.AmendedAt;
        var failure = AmendedState(current, change.PeriodStart, change.PeriodEnd, change.Reason,
            change.ActorMemberId, change.ActorDisplay, at, out var state);
        if (failure is not null)
            return Result.Failure(failure.ToRequestError());
        var amended = new ObservationPeriodAmended(_tenantId, Id, state!.Revision, change.ActorMemberId,
            Freeze(state), at, accepted.Intent, operation, permit.AuthorizationId, permit.Generation, null,
            operationResult);
        if (DomainEventPayload.Size(amended) > MaximumEventBytes)
            return Result.Failure(Invalid("The complete Type II period state exceeds the bounded event payload.")
                .ToRequestError());
        RaiseEvent(amended);
        return Result.Success;
    }

    CommandFailure? AmendedState(TypeIIPeriodView current, DateOnly periodStart, DateOnly periodEnd, string reason,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset amendedAt, out TypeIIPeriodView? next)
    {
        next = null;
        reason = Clean(reason);
        if (current.Status != "active" || current.CloseValidation is not null)
            return CommandFailure.StateConflict("Only an active period without a close validation can be amended.");
        if (!ValidActor(actorMemberId, actorDisplay, amendedAt) ||
            amendedAt < current.UpdatedAt ||
            !Bounded(reason, 1, MaximumTextLength) || current.Amendments.Count >= MaximumAmendments ||
            periodEnd < periodStart || periodStart == current.PeriodStart && periodEnd == current.PeriodEnd)
            return Invalid("A period amendment requires valid dates, attributed approval, and a bounded reason.");
        if (current.ExpectedOccurrences.Any(item => item.DueOn is { } due &&
                (due < periodStart || due > periodEnd)))
            return CommandFailure.StateConflict("A period amendment cannot exclude an approved expected occurrence.");
        if (_generations.Any(generation => generation.ThroughDate < periodStart ||
                generation.ThroughDate > periodEnd))
            return CommandFailure.StateConflict(
                "A period amendment cannot move its boundaries outside a recorded generation batch.");

        var amendment = new TypeIIPeriodAmendmentView(current.PeriodStart, current.PeriodEnd,
            periodStart, periodEnd, reason, actorMemberId, actorDisplay.Trim(), amendedAt);
        var state = current with
        {
            Revision = current.Revision + 1,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Amendments = Append(current.Amendments, amendment),
            ActorMemberId = actorMemberId,
            ActorDisplay = actorDisplay.Trim(),
            UpdatedAt = amendedAt,
        };
        if (ValidateState(state) is { } invalid)
            return invalid;
        if (!ValidTransition(current, state, actorMemberId, "period_amendment"))
            return Invalid("The Type II period transition is invalid.");
        next = state;
        return null;
    }

    /// <summary>The logical operation identity of one generation key of this period.</summary>
    public static Uuid GenerationLogicalId(Uuid periodId, string generationKey) =>
        Uuid.CreateVersion5(periodId, "due-occurrences:" + Clean(generationKey));

    /// <summary>Checks the generation guards without raising anything.</summary>
    public CommandFailure? PrepareGenerateDueOccurrences(string generationKey, DateOnly throughDate,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset generatedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The Type II observation period was not found.");
        generationKey = Clean(generationKey);
        if (_generations.Any(generation => generation.GenerationKey == generationKey))
            return CommandFailure.StateConflict("A generation key cannot be reused for another date or actor.");
        if (current.Status != "active")
            return CommandFailure.StateConflict("Occurrences can only be generated for an active period.");
        if (!Token(generationKey, 200) || throughDate < current.PeriodStart || throughDate > current.PeriodEnd ||
            !ValidActor(actorMemberId, actorDisplay, generatedAt) || generatedAt < current.UpdatedAt)
            return Invalid("Occurrence generation requires a bounded key, an in-period date, and an attributed actor.");
        return null;
    }

    /// <summary>
    ///     The recurring expected occurrences due on or before the date that no earlier generation selected, in
    ///     canonical order. <paramref name="alreadySelected" /> holds the expected occurrence ids of every earlier
    ///     manifest.
    /// </summary>
    public IReadOnlyList<TypeIIPeriodOccurrenceView> DueExpectedOccurrences(DateOnly throughDate,
        IReadOnlySet<Uuid> alreadySelected)
    {
        ArgumentNullException.ThrowIfNull(alreadySelected);
        return _current is not { } current
            ? []
            : current.ExpectedOccurrences
                .Where(item => item.DueOn is { } due && due <= throughDate && !alreadySelected.Contains(item.OccurrenceId))
                .OrderBy(static item => IdKey(item.OccurrenceId), StringComparer.Ordinal)
                .ToArray();
    }

    /// <summary>
    ///     Selects the due recurring occurrences of a generation key as one native operation of this period. The
    ///     complete selection is immutable content, never a count cap; selecting is not materializing.
    /// </summary>
    public Result GenerateDueOccurrences(OperationProvenance operation, string generationKey, DateOnly throughDate,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset generatedAt, ImmutableContentRef occurrenceIds,
        ImmutableContentRef creationIntents, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        generationKey = Clean(generationKey);
        if (operation.Owner != Stream || operation.LogicalId != GenerationLogicalId(Id, generationKey))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A generation is the logical operation of its own period and key."));
        if (PrepareGenerateDueOccurrences(generationKey, throughDate, actorMemberId, actorDisplay, generatedAt) is
            { } failure)
            return Result.Failure(failure.ToRequestError());
        RaiseEvent(new DueControlOccurrencesGenerated(_tenantId, Id, Revision + 1, generationKey, throughDate,
            occurrenceIds, actorMemberId, actorDisplay.Trim(), generatedAt, operation, creationIntents, firstResponse,
            operationResult));
        return Result.Success;
    }

    public CommandFailure? RecordCloseValidation(long expectedRevision,
        TypeIIPeriodCloseValidationContent content, Uuid validatorMemberId,
        string validatorDisplay, DateTimeOffset validatedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The Type II period was not found.");
        if (SameValidation(current.CloseValidation, content, validatorMemberId, validatorDisplay, validatedAt))
            return null;
        if (expectedRevision != Revision)
            return StaleRevision();
        if (_pending is not null)
            return CommandFailure.StateConflict(
                "An opening or amendment is still being applied; validate the close after it ends.");
        if (current.Status is not ("active" or "close_blocked" or "close_ready"))
            return CommandFailure.StateConflict("Close validation is only allowed before executive approval.");
        if (!ValidActor(validatorMemberId, validatorDisplay, validatedAt) || validatedAt < current.UpdatedAt ||
            NormalizeValidation(content, current.ExpectedOccurrences) is not { } normalized)
            return Invalid("Close validation must reconcile this period's exact expected population and source versions.");
        var ready = normalized.ExpectedOccurrenceIds.SequenceEqual(normalized.ActualOccurrenceIds) &&
                    normalized.DuplicateOccurrenceIds.Count == 0 && normalized.MissingWorkIds.Count == 0;
        var validation = new TypeIIPeriodCloseValidationView(ready ? "ready" : "blocked",
            normalized.SourceReferences, normalized.ExpectedOccurrenceIds,
            normalized.ActualOccurrenceIds, normalized.DuplicateOccurrenceIds,
            normalized.MissingWorkIds, normalized.UnresolvedMaterialItemIds,
            validatorMemberId, validatorDisplay.Trim(), validatedAt);
        var state = current with
        {
            Status = ready ? "close_ready" : "close_blocked",
            Revision = Revision + 1,
            CloseValidation = validation,
            CloseApprovals = [],
            ActorMemberId = validatorMemberId,
            ActorDisplay = validatorDisplay.Trim(),
            UpdatedAt = validatedAt,
        };
        return Emit(state, validatorMemberId, validatedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new ObservationPeriodCloseProposed(tenant, subject, revision, actor, snapshot, at),
            "validation");
    }

    public CommandFailure? ApproveClose(long expectedRevision,
        IReadOnlyList<Uuid> acknowledgedMaterialItemIds, string rationale,
        Uuid approverMemberId, string approverRole, string approverDisplay,
        DateTimeOffset approvedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The Type II period was not found.");
        rationale = Clean(rationale);
        approverRole = Clean(approverRole);
        var acknowledged = NormalizeIds(acknowledgedMaterialItemIds);
        if (current.CloseApprovals.Count > 0 && current.CloseApprovals[^1] is { } prior && acknowledged is not null &&
            prior.AcknowledgedMaterialItemIds.SequenceEqual(acknowledged) && prior.Rationale == rationale &&
            prior.ApprovedByMemberId == approverMemberId && prior.ApproverRole == approverRole &&
            prior.ApproverDisplay == Clean(approverDisplay))
            return null;
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.CloseValidation is not { } validation)
            return CommandFailure.StateConflict("A current close validation is required before approval.");
        if (validation.Status != "ready" || current.Status != "close_ready")
            return CommandFailure.StateConflict("The close cannot be approved until every expected occurrence reconciles.");
        if (!ValidActor(approverMemberId, approverDisplay, approvedAt) || approvedAt < validation.ValidatedAt ||
            !ExecutiveRoles.Contains(approverRole) || !Bounded(rationale, 1, MaximumTextLength) || acknowledged is null)
            return Invalid("Close approval requires an attributed CEO/CISO and a bounded rationale.");
        if (!validation.UnresolvedMaterialItemIds.SequenceEqual(acknowledged))
            return CommandFailure.InvalidContent("Close approval must acknowledge every unresolved material item exactly once.");
        if (approverMemberId == validation.ValidatorMemberId)
            return CommandFailure.ActorProhibited("The close validator cannot approve the same period close.");

        var approval = new TypeIIPeriodCloseApprovalView(acknowledged, rationale,
            approverMemberId, approverRole, approverDisplay.Trim(), approvedAt, Revision);
        var state = current with
        {
            Status = "close_approved",
            Revision = Revision + 1,
            CloseApprovals = Append(current.CloseApprovals, approval),
            ActorMemberId = approverMemberId,
            ActorDisplay = approverDisplay.Trim(),
            UpdatedAt = approvedAt,
        };
        return Emit(state, approverMemberId, approvedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new TypeIIPeriodCloseApproved(tenant, subject, revision, actor, snapshot, at),
            "approval");
    }

    public CommandFailure? FreezeClose(long expectedRevision,
        TypeIIPeriodCloseSnapshotContent content, Uuid actorMemberId,
        string actorRole, DateTimeOffset frozenAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The Type II period was not found.");
        actorRole = Clean(actorRole);
        if (content is not null && current.CloseSnapshots.FirstOrDefault(item =>
                item.CloseSnapshotId == content.CloseSnapshotId) is { } prior)
            return prior.OriginalExpectedRevision == expectedRevision &&
                   SameSnapshot(prior, content, actorMemberId, actorRole) ? null :
                CommandFailure.StateConflict("A close snapshot identity cannot be reused with different content.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status != "close_approved" || current.CloseValidation is not { } validation ||
            current.CloseApprovals.Count == 0 || current.CloseApprovals[^1] is not { } approval)
            return CommandFailure.StateConflict("An approved, reconciled period close is required before freezing.");
        if (!ValidActor(actorMemberId, actorRole, frozenAt) || frozenAt < approval.ApprovedAt ||
            !ExecutiveRoles.Contains(actorRole))
            return Invalid("Freezing a Type II close requires an attributed CEO/CISO.");
        if (actorMemberId != approval.ApprovedByMemberId)
            return CommandFailure.ActorProhibited("Only the approving executive can freeze this exact close decision.");
        if (NormalizeSnapshot(content) is not { } normalized ||
            !SameReferences(normalized.SourceReferences, validation.SourceReferences) ||
            !normalized.ExpectedOccurrenceIds.SequenceEqual(current.ExpectedOccurrences.Select(item => item.OccurrenceId)
                .OrderBy(IdKey, StringComparer.Ordinal)) ||
            !normalized.AcknowledgedMaterialItemIds.SequenceEqual(approval.AcknowledgedMaterialItemIds))
            return CommandFailure.InvalidContent("The close snapshot must preserve the exact validated sources, occurrences, and acknowledgements.");
        if (current.CloseSnapshots.Any(item => item.CloseSnapshotId == normalized.CloseSnapshotId))
            return CommandFailure.StateConflict("A close snapshot requires a new immutable identity.");

        var snapshot = NewSnapshot(normalized, 1, actorMemberId, actorRole, actorRole,
            frozenAt, null, null, [], []) with
        { OriginalExpectedRevision = expectedRevision };
        var state = current with
        {
            Status = "closed",
            Revision = Revision + 1,
            CloseSnapshots = Append(current.CloseSnapshots, snapshot),
            ActorMemberId = actorMemberId,
            ActorDisplay = actorRole,
            UpdatedAt = frozenAt,
        };
        if (_pending is not null)
            return CommandFailure.StateConflict(
                "An opening or amendment is still being applied; the close can freeze after it ends.");
        if (_live is not { } live)
            return CommandFailure.StateConflict("The period holds no live calendar range to release at close.");
        return Emit(state, actorMemberId, frozenAt,
            (tenant, subject, revision, actor, snapshotState, at) =>
                new ObservationPeriodCloseFrozen(tenant, subject, revision, actor, snapshotState, at,
                    live.AuthorizationId, live.Generation),
            "freeze");
    }

    public CommandFailure? AmendClose(long expectedRevision,
        TypeIIPeriodCloseSnapshotContent content, string reason,
        IReadOnlyList<Uuid> changedRecordIds, IReadOnlyList<Uuid> affectedExaminationMaterialIds,
        Uuid approverMemberId, string approverRole, string approverDisplay,
        DateTimeOffset amendedAt)
    {
        if (_current is not { } current || current.CloseSnapshots.Count == 0)
            return CommandFailure.MissingRecord("A frozen Type II period close was not found.");
        reason = Clean(reason);
        approverRole = Clean(approverRole);
        if (content is not null && current.CloseSnapshots.FirstOrDefault(item =>
                item.CloseSnapshotId == content.CloseSnapshotId) is { } existing)
            return SameAmendment(existing, content, reason, changedRecordIds,
                    affectedExaminationMaterialIds, approverMemberId, approverRole, approverDisplay, amendedAt)
                ? null
                : CommandFailure.StateConflict("A close amendment identity cannot be reused with different content.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status != "closed" || !ValidActor(approverMemberId, approverDisplay, amendedAt) ||
            amendedAt < current.CloseSnapshots[^1].RecordedAt ||
            !ExecutiveRoles.Contains(approverRole) || !Bounded(reason, 1, MaximumTextLength) ||
            NormalizeSnapshot(content) is not { } normalized ||
            NormalizeIds(changedRecordIds) is not { } changed ||
            NormalizeIds(affectedExaminationMaterialIds) is not { Length: > 0 } affected)
            return Invalid("A close amendment requires a new snapshot, exact change set, affected material, and attributed executive.");
        var latest = current.CloseSnapshots[^1];
        if (normalized.CloseSnapshotId == latest.CloseSnapshotId ||
            !normalized.ExpectedOccurrenceIds.SequenceEqual(latest.ExpectedOccurrenceIds) ||
            !normalized.AcknowledgedMaterialItemIds.SequenceEqual(latest.AcknowledgedMaterialItemIds))
            return CommandFailure.StateConflict("A close amendment preserves the approved occurrence population and acknowledgements.");
        var expectedChanged = ChangedRecordIds(latest.SourceReferences, normalized.SourceReferences);
        if (expectedChanged.Length == 0 || !expectedChanged.SequenceEqual(changed))
            return CommandFailure.InvalidContent("The close amendment must identify every changed source record exactly once.");
        if (current.CloseSnapshots.Count >= MaximumAmendments ||
            current.CloseSnapshots.Any(item => item.CloseSnapshotId == normalized.CloseSnapshotId))
            return CommandFailure.StateConflict("A close amendment requires a new identity within the supported lineage limit.");

        var snapshot = NewSnapshot(normalized, latest.Version + 1, approverMemberId,
            approverRole, approverDisplay.Trim(), amendedAt, latest.CloseSnapshotId,
            reason, changed, affected);
        var state = current with
        {
            Revision = Revision + 1,
            CloseSnapshots = Append(current.CloseSnapshots, snapshot),
            ActorMemberId = approverMemberId,
            ActorDisplay = approverDisplay.Trim(),
            UpdatedAt = amendedAt,
        };
        return Emit(state, approverMemberId, amendedAt,
            static (tenant, subject, revision, actor, snapshotState, at) =>
                new TypeIIPeriodCloseAmended(tenant, subject, revision, actor, snapshotState, at),
            "close_amendment");
    }

    void Accept(DomainEvent fact, EventStreamAddress owner, long revision, long generation,
        OperationProvenance operation, ImmutableContentRef? firstResponse, bool opening)
    {
        Acceptance.Observe(fact);
        if (_pending is not null || owner != Stream || operation.Owner != Stream ||
            operation.LogicalId != operation.RootOperationId || revision != Revision ||
            generation != TypeIIRangeEffects.Generation || firstResponse is null ||
            (opening ? _current is not null || _accepted.Count > 0 || operation.RootOperationId != Id
                : _current is null) ||
            !_accepted.TryAdd(operation.RootOperationId, fact))
            throw new InvalidOperationException(
                "A period accepts one new opening or amendment at a time, at its own revision.");
        _pending = operation.RootOperationId;
    }

    void Fence(DomainEvent fact, EventStreamAddress owner, OperationProvenance operation, ImmutableContentRef intent,
        bool opening)
    {
        Acceptance.Observe(fact);
        if (_pending is not { } pending || pending != operation.RootOperationId || owner != Stream ||
            operation.Owner != Stream || (_accepted[pending] is TypeIIPeriodOpeningAccepted) != opening ||
            IntentOf(_accepted[pending]) != intent)
            throw new InvalidOperationException("Only the exact pending opening or amendment can be rejected or canceled.");
        _terminal.Add(pending, fact);
        _facts.Add(fact);
        _pending = null;
    }

    void Apply(ObservationPeriodStarted ev)
    {
        Acceptance.Observe(ev);
        RequirePending(ev.Operation, ev.Change, ev.CalendarAuthorizationId, ev.CalendarGeneration, true);
        ApplyState(ev.TenantId, ev.SubjectId, ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, true, "start");
        Settle(ev, ev.Operation, ev.CalendarAuthorizationId, ev.CalendarGeneration);
    }

    void Apply(ObservationPeriodAmended ev)
    {
        Acceptance.Observe(ev);
        RequirePending(ev.Operation, ev.Change, ev.CalendarAuthorizationId, ev.CalendarGeneration, false);
        ApplyState(ev.TenantId, ev.SubjectId, ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false,
            "period_amendment");
        Settle(ev, ev.Operation, ev.CalendarAuthorizationId, ev.CalendarGeneration);
    }

    void RequirePending(OperationProvenance operation, ImmutableContentRef change, Uuid authorizationId,
        long generation, bool opening)
    {
        if (_pending is not { } pending || pending != operation.RootOperationId || operation.Owner != Stream ||
            (_accepted[pending] is TypeIIPeriodOpeningAccepted) != opening || IntentOf(_accepted[pending]) != change ||
            authorizationId != pending || generation <= 0)
            throw new InvalidOperationException(
                "A period range change must follow its accepted opening or amendment and carry its calendar permit.");
    }

    void Settle(DomainEvent fact, OperationProvenance operation, Uuid authorizationId, long generation)
    {
        _terminal.Add(operation.RootOperationId, fact);
        _facts.Add(fact);
        _live = new TypeIIPeriodCalendarAuthorization(authorizationId, generation);
        _pending = null;
    }

    void Apply(DueControlOccurrencesGenerated ev)
    {
        Acceptance.Observe(ev);
        if (_current is not { } current || ev.TenantId != _tenantId || ev.SubjectId != Id ||
            ev.Revision != Revision + 1 || ev.ActorMemberId is not { } actorMemberId ||
            actorMemberId == Uuid.Empty || ev.OccurredAt == default ||
            current.Status != "active" || ev.OccurredAt < current.UpdatedAt ||
            !Token(ev.GenerationKey, 200) || ev.GenerationKey != Clean(ev.GenerationKey) ||
            ev.ThroughDate < current.PeriodStart || ev.ThroughDate > current.PeriodEnd ||
            !ValidActor(actorMemberId, ev.ActorDisplay, ev.OccurredAt) || ev.Operation.Owner != Stream ||
            ev.Operation.LogicalId != GenerationLogicalId(Id, ev.GenerationKey) || ev.FirstResponse is null ||
            _generations.Any(generation => generation.GenerationKey == ev.GenerationKey))
            throw new InvalidOperationException("The occurrence generation event is invalid.");

        var next = current with { Revision = ev.Revision };
        if (ValidateState(next) is not null)
            throw new InvalidOperationException("The occurrence generation event is invalid.");
        _generations.Add(ev);
        _facts.Add(ev);
        _current = Freeze(next);
    }
    void Apply(ObservationPeriodCloseProposed ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "validation");
    void Apply(TypeIIPeriodCloseApproved ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "approval");
    void Apply(ObservationPeriodCloseFrozen ev)
    {
        if (_pending is not null || _live is not { } live || live.AuthorizationId != ev.CalendarAuthorizationId ||
            live.Generation != ev.CalendarGeneration)
            throw new InvalidOperationException(
                "A close freeze must name the exact live calendar reservation of a period without a pending change.");
        ApplyState(ev.TenantId, ev.SubjectId, ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "freeze");
        _facts.Add(ev);
    }
    void Apply(TypeIIPeriodCloseAmended ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "close_amendment");

    void ApplyState(Uuid tenantId, Uuid subjectId, long revision, Uuid? actorMemberId,
        TypeIIPeriodView state, DateTimeOffset occurredAt, bool creating, string transition)
    {
        if (tenantId != _tenantId || subjectId != Id || state is null ||
            state.TenantId != _tenantId || state.ProgramId != _programId || state.PeriodId != Id ||
            state.Revision != revision || occurredAt == default || revision != Revision + 1 ||
            creating != (_current is null) || actorMemberId == Uuid.Empty || state.UpdatedAt != occurredAt ||
            state.ActorMemberId != actorMemberId || ValidateState(state) is not null ||
            _current is null && (transition != "start" || !ValidOpeningState(state)) ||
            _current is { } prior && !ValidTransition(prior, state, actorMemberId, transition))
            throw new InvalidOperationException("The Type II period event is invalid.");
        _current = Freeze(state);
    }

    CommandFailure? Emit(TypeIIPeriodView state, Uuid actorMemberId, DateTimeOffset occurredAt,
        Func<Uuid, Uuid, long, Uuid?, TypeIIPeriodView, DateTimeOffset, DomainEvent> createEvent,
        string transition, bool creating = false)
    {
        if (ValidateState(state) is not null ||
            (creating ? _current is not null : _current is null ||
                !ValidTransition(_current, state, actorMemberId, transition)))
            return Invalid("The Type II period transition is invalid.");
        var ev = createEvent(_tenantId, Id, state.Revision, actorMemberId, Freeze(state), occurredAt);
        if (DomainEventPayload.Size(ev) > MaximumEventBytes)
            return Invalid("The complete Type II period state exceeds the bounded event payload.");
        RaiseEvent(ev);
        return null;
    }

    CommandFailure? ValidateState(TypeIIPeriodView state)
    {
        if (state.TenantId != _tenantId || state.ProgramId != _programId || state.PeriodId != Id ||
            state.Revision <= 0 || state.ExaminationId == Uuid.Empty || state.TypeIIPlanId == Uuid.Empty ||
            state.TypeIIPlanVersion <= 0 || !Sha256(state.TypeIIPlanContentSha256) ||
            state.PeriodStart == default || state.PeriodEnd < state.PeriodStart ||
            state.OpeningSnapshotId == Uuid.Empty || !Sha256(state.OpeningSnapshotSha256) ||
            NormalizeReferences(state.OpeningSourceReferences) is not { } openingRefs ||
            !SameReferences(openingRefs, state.OpeningSourceReferences) ||
            NormalizeIds(state.AcknowledgedOpeningGapIds) is not { } openingGaps ||
            !openingGaps.SequenceEqual(state.AcknowledgedOpeningGapIds) ||
            state.ExpectedOccurrences is null || state.ExpectedOccurrences.Count is < 1 or > MaximumOccurrences ||
            state.ExpectedOccurrences.Any(item => item is null || item.OccurrenceId == Uuid.Empty ||
                item.ControlId == Uuid.Empty || item.ControlVersionId == Uuid.Empty || item.Sequence <= 0 ||
                item.OwnerMemberId == Uuid.Empty || !Bounded(item.Cadence, 1, 80) ||
                item.ExpectedEvidenceDescriptions is { } evidence && !ValidExpectedEvidence(evidence)) ||
            state.ExpectedOccurrences.Select(item => item.OccurrenceId).Distinct().Count() != state.ExpectedOccurrences.Count ||
            state.Amendments is null || state.Amendments.Count > MaximumAmendments ||
            state.Amendments.Any(item => item is null || item.PriorPeriodEnd < item.PriorPeriodStart ||
                item.PeriodEnd < item.PeriodStart || !Bounded(item.Reason, 1, MaximumTextLength) ||
                !ValidActor(item.ActorMemberId, item.ActorDisplay, item.AmendedAt)) ||
            state.CloseApprovals is null || state.CloseSnapshots is null ||
            !ValidValidation(state.CloseValidation, state.ExpectedOccurrences) ||
            !ValidApprovals(state) || !ValidSnapshots(state) ||
            !ValidStatus(state) || !ValidActor(state.ActorMemberId, state.ActorDisplay, state.UpdatedAt))
            return Invalid("The Type II period event contains an invalid state snapshot.");
        return null;
    }

    static bool ValidTransition(TypeIIPeriodView prior, TypeIIPeriodView next,
        Uuid? actorMemberId, string transition)
    {
        if (next.Revision != prior.Revision + 1 || next.TenantId != prior.TenantId ||
            next.ProgramId != prior.ProgramId || next.PeriodId != prior.PeriodId ||
            next.ExaminationId != prior.ExaminationId || next.TypeIIPlanId != prior.TypeIIPlanId ||
            next.TypeIIPlanVersion != prior.TypeIIPlanVersion ||
            next.TypeIIPlanContentSha256 != prior.TypeIIPlanContentSha256 ||
            next.OpeningSnapshotId != prior.OpeningSnapshotId ||
            next.OpeningSnapshotSha256 != prior.OpeningSnapshotSha256 ||
            !SameReferences(next.OpeningSourceReferences, prior.OpeningSourceReferences) ||
            !next.AcknowledgedOpeningGapIds.SequenceEqual(prior.AcknowledgedOpeningGapIds) ||
            !SameOccurrences(next.ExpectedOccurrences, prior.ExpectedOccurrences) ||
            !Prefix(prior.Amendments, next.Amendments) ||
            !Prefix(prior.CloseSnapshots, next.CloseSnapshots) ||
            !Prefix(prior.CloseApprovals, next.CloseApprovals) ||
            transition != "period_amendment" &&
                (next.PeriodStart != prior.PeriodStart || next.PeriodEnd != prior.PeriodEnd))
            return false;
        return transition switch
        {
            "period_amendment" => prior.Status == "active" && next.Status == "active" &&
                next.Amendments.Count == prior.Amendments.Count + 1 &&
                SameValidation(prior.CloseValidation, next.CloseValidation) &&
                next.CloseApprovals.Count == prior.CloseApprovals.Count &&
                next.CloseSnapshots.Count == prior.CloseSnapshots.Count &&
                next.Amendments[^1].ActorMemberId == actorMemberId &&
                next.Amendments[^1].PriorPeriodStart == prior.PeriodStart &&
                next.Amendments[^1].PriorPeriodEnd == prior.PeriodEnd &&
                next.Amendments[^1].PeriodStart == next.PeriodStart &&
                next.Amendments[^1].PeriodEnd == next.PeriodEnd &&
                next.Amendments[^1].AmendedAt == next.UpdatedAt,
            "validation" => prior.Status is "active" or "close_ready" or "close_blocked" &&
                next.Status is "close_ready" or "close_blocked" &&
                next.CloseValidation is { } validation &&
                validation.ValidatorMemberId == actorMemberId &&
                !SameValidation(prior.CloseValidation, next.CloseValidation) &&
                next.CloseApprovals.Count == 0 && next.CloseSnapshots.Count == prior.CloseSnapshots.Count &&
                next.Amendments.Count == prior.Amendments.Count &&
                next.PeriodStart == prior.PeriodStart && next.PeriodEnd == prior.PeriodEnd,
            "approval" => next.Status == "close_approved" && prior.Status == "close_ready" &&
                next.CloseApprovals.Count == prior.CloseApprovals.Count + 1 &&
                next.CloseApprovals[^1].ApprovedByMemberId == actorMemberId &&
                SameValidation(prior.CloseValidation, next.CloseValidation) &&
                next.CloseSnapshots.Count == prior.CloseSnapshots.Count &&
                next.Amendments.Count == prior.Amendments.Count,
            "freeze" => prior.Status == "close_approved" && next.Status == "closed" &&
                next.CloseSnapshots.Count == prior.CloseSnapshots.Count + 1 &&
                next.CloseSnapshots[^1].ActorMemberId == actorMemberId &&
                SameValidation(prior.CloseValidation, next.CloseValidation) &&
                next.CloseApprovals.Count == prior.CloseApprovals.Count &&
                next.Amendments.Count == prior.Amendments.Count,
            "close_amendment" => prior.Status == "closed" && next.Status == "closed" &&
                next.CloseSnapshots.Count == prior.CloseSnapshots.Count + 1 &&
                next.CloseSnapshots[^1].ActorMemberId == actorMemberId &&
                SameValidation(prior.CloseValidation, next.CloseValidation) &&
                next.CloseApprovals.Count == prior.CloseApprovals.Count &&
                next.Amendments.Count == prior.Amendments.Count,
            _ => false,
        };
    }

    static bool ApprovedPlan(TypeIIPlanView? plan)
    {
        if (plan is null || plan.Status != "approved" || plan.TenantId == Uuid.Empty ||
            plan.ProgramId == Uuid.Empty || plan.TypeIIPlanId == Uuid.Empty || plan.Current is not { } current ||
            current.Version != plan.CurrentVersionNumber || current.Content is not { } content ||
            current.ContentSha256 != Digest(content) || plan.Approvals is null || plan.Reviews is null)
            return false;
        var approval = plan.Approvals.SingleOrDefault(item => item.PlanVersion == current.Version);
        var review = plan.Reviews.SingleOrDefault(item => item.PlanVersion == current.Version);
        return approval is not null && review?.Outcome == "approve" &&
               approval.ApprovedByMemberId != current.ProposedByMemberId &&
               approval.ApprovedByMemberId != review.ReviewerMemberId &&
               review.ReviewerMemberId != current.ProposedByMemberId &&
               ExecutiveRoles.Contains(approval.ApproverRole);
    }

    static TypeIIPeriodOpeningContent? NormalizeOpening(TypeIIPeriodOpeningContent? opening)
    {
        if (opening is null || opening.ExaminationId == Uuid.Empty ||
            opening.OpeningSnapshotId == Uuid.Empty || !Sha256(opening.OpeningSnapshotSha256) ||
            NormalizeReferences(opening.SourceReferences) is not { } refs ||
            NormalizeIds(opening.AcknowledgedOpeningGapIds) is not { } gaps)
            return null;
        return opening with
        {
            OpeningSnapshotSha256 = opening.OpeningSnapshotSha256.ToLowerInvariant(),
            SourceReferences = refs,
            AcknowledgedOpeningGapIds = gaps,
        };
    }

    static ReadOnlyCollection<TypeIIPeriodOccurrenceView>? BuildExpectedOccurrences(Uuid periodId,
        TypeIIPlanContent plan, DateOnly periodStart, DateOnly periodEnd)
    {
        var occurrences = new List<TypeIIPeriodOccurrenceView>();
        foreach (var control in plan.Controls)
        {
            if (control.OwnerMemberId is not { } owner || control.EffectiveFrom < periodStart ||
                control.EffectiveFrom > periodEnd)
                return null;
            var cadence = control.Cadence.Trim().ToLowerInvariant();
            if (cadence is "event_driven" or "ad_hoc")
            {
                if (control.ExpectedOccurrences != 1)
                    return null;
                occurrences.Add(NewExpectedOccurrence(periodId, control, 1, null, owner, cadence));
                if (occurrences.Count > MaximumOccurrences)
                    return null;
                continue;
            }

            var schedule = new ControlCadence(ControlCadenceSchedule.Recurring, cadence,
                control.EffectiveFrom, control.DueWithinDays ?? 0);
            if (ControlCadenceSchedule.Validate(schedule) is not null)
                return null;
            var until = control.EffectiveThrough?.AddDays(1);
            var periods = ControlCadenceSchedule.Periods(schedule, periodStart, until, periodEnd)
                .ToArray();
            if (periods.Length != control.ExpectedOccurrences ||
                occurrences.Count + periods.Length > MaximumOccurrences)
                return null;
            for (var index = 0; index < periods.Length; index++)
                occurrences.Add(NewExpectedOccurrence(periodId, control, index + 1,
                    periods[index].DueOn, owner, cadence));
        }
        return Freeze(occurrences.OrderBy(item => IdKey(item.ControlId), StringComparer.Ordinal)
            .ThenBy(item => item.Sequence).ToArray());
    }

    static TypeIIPeriodOccurrenceView NewExpectedOccurrence(Uuid periodId,
        TypeIIControlPlanEntry control, int sequence, DateOnly? dueOn, Uuid owner, string cadence)
    {
        var identity = $"expected:{control.ControlId}:{control.ControlVersionId}:{sequence}";
        return new(Uuid.CreateVersion5(periodId, identity), control.ControlId,
            control.ControlVersionId, sequence, dueOn, owner, cadence,
            control.ExpectedEvidenceDescriptions is { } evidence
                ? Freeze(evidence.ToArray())
                : null);
    }

    static bool ValidExpectedEvidence(IReadOnlyList<string> descriptions) =>
        descriptions.Count is >= 1 and <= 20 &&
        descriptions.All(value => Bounded(value, 1, MaximumTextLength));

    static TypeIIPeriodCloseValidationContent? NormalizeValidation(
        TypeIIPeriodCloseValidationContent? content,
        IReadOnlyList<TypeIIPeriodOccurrenceView> expectedOccurrences)
    {
        if (content is null || NormalizeReferences(content.SourceReferences) is not { } refs ||
            NormalizeIds(content.ExpectedOccurrenceIds) is not { } expected ||
            NormalizeIds(content.ActualOccurrenceIds, allowDuplicates: true) is not { } actual ||
            NormalizeIds(content.DuplicateOccurrenceIds) is not { } duplicates ||
            NormalizeIds(content.MissingWorkIds) is not { } missing ||
            NormalizeIds(content.UnresolvedMaterialItemIds) is not { } unresolved ||
            !expected.SequenceEqual(expectedOccurrences.Select(item => item.OccurrenceId).OrderBy(IdKey, StringComparer.Ordinal)))
            return null;
        return content with
        {
            SourceReferences = refs,
            ExpectedOccurrenceIds = expected,
            ActualOccurrenceIds = actual,
            DuplicateOccurrenceIds = duplicates,
            MissingWorkIds = missing,
            UnresolvedMaterialItemIds = unresolved,
        };
    }

    static TypeIIPeriodCloseSnapshotContent? NormalizeSnapshot(TypeIIPeriodCloseSnapshotContent? content)
    {
        if (content is null || content.CloseSnapshotId == Uuid.Empty ||
            NormalizeReferences(content.SourceReferences) is not { } refs ||
            NormalizeIds(content.ExpectedOccurrenceIds) is not { } expected ||
            NormalizeIds(content.AcknowledgedMaterialItemIds) is not { } acknowledged)
            return null;
        return content with
        {
            SourceReferences = refs,
            ExpectedOccurrenceIds = expected,
            AcknowledgedMaterialItemIds = acknowledged,
        };
    }

    static TypeIIPeriodCloseSnapshotView NewSnapshot(TypeIIPeriodCloseSnapshotContent content,
        int version, Uuid actor, string role, string display, DateTimeOffset at,
        Uuid? amendsId, string? reason, IReadOnlyList<Uuid> changed,
        IReadOnlyList<Uuid> affected) => new(content.CloseSnapshotId, version,
        content.SourceReferences, content.ExpectedOccurrenceIds,
        content.AcknowledgedMaterialItemIds, Digest(content), amendsId, reason,
        changed, affected, actor, role, display, at);

    static bool ValidValidation(TypeIIPeriodCloseValidationView? validation,
        IReadOnlyList<TypeIIPeriodOccurrenceView> expected) => validation is null ||
        NormalizeReferences(validation.SourceReferences) is { } refs &&
        SameReferences(refs, validation.SourceReferences) &&
        NormalizeIds(validation.ExpectedOccurrenceIds) is { } expectedIds &&
        expectedIds.SequenceEqual(expected.Select(item => item.OccurrenceId).OrderBy(IdKey, StringComparer.Ordinal)) &&
        NormalizeIds(validation.ActualOccurrenceIds, allowDuplicates: true) is { } actual &&
        actual.SequenceEqual(validation.ActualOccurrenceIds) &&
        NormalizeIds(validation.DuplicateOccurrenceIds) is { } duplicates &&
        duplicates.SequenceEqual(validation.DuplicateOccurrenceIds) &&
        NormalizeIds(validation.MissingWorkIds) is { } missing && missing.SequenceEqual(validation.MissingWorkIds) &&
        NormalizeIds(validation.UnresolvedMaterialItemIds) is { } unresolved &&
        unresolved.SequenceEqual(validation.UnresolvedMaterialItemIds) &&
        ValidActor(validation.ValidatorMemberId, validation.ValidatorDisplay, validation.ValidatedAt) &&
        validation.Status == (expectedIds.SequenceEqual(actual) && duplicates.Length == 0 && missing.Length == 0
            ? "ready" : "blocked");

    static bool ValidApprovals(TypeIIPeriodView state) =>
        state.CloseApprovals.Count <= 1 && state.CloseApprovals.All(item =>
            NormalizeIds(item.AcknowledgedMaterialItemIds) is { } ids &&
            ids.SequenceEqual(item.AcknowledgedMaterialItemIds) &&
            state.CloseValidation is { } validation && ids.SequenceEqual(validation.UnresolvedMaterialItemIds) &&
            Bounded(item.Rationale, 1, MaximumTextLength) && item.ApprovedByMemberId != Uuid.Empty &&
            ExecutiveRoles.Contains(item.ApproverRole) &&
            item.ApprovedByMemberId != validation.ValidatorMemberId &&
            Bounded(item.ApproverDisplay, 1, 200) && item.ApprovedAt != default &&
            item.ValidatedRevision > 0 && item.ValidatedRevision < state.Revision);

    static bool ValidSnapshots(TypeIIPeriodView state)
    {
        if (state.CloseSnapshots.Count > MaximumAmendments ||
            state.CloseSnapshots.Select(item => item.CloseSnapshotId).Distinct().Count() != state.CloseSnapshots.Count)
            return false;
        for (var index = 0; index < state.CloseSnapshots.Count; index++)
        {
            var item = state.CloseSnapshots[index];
            var content = new TypeIIPeriodCloseSnapshotContent(item.CloseSnapshotId,
                item.SourceReferences, item.ExpectedOccurrenceIds, item.AcknowledgedMaterialItemIds);
            if (item.CloseSnapshotId == Uuid.Empty || item.Version != index + 1 ||
                NormalizeSnapshot(content) is not { } normalized || Digest(normalized) != item.ContentSha256 ||
                !Bounded(item.ActorRole, 1, 100) || !ExecutiveRoles.Contains(item.ActorRole) ||
                item.ActorMemberId == Uuid.Empty ||
                !Bounded(item.ActorDisplay, 1, 200) || item.RecordedAt == default ||
                index == 0 && (item.AmendsCloseSnapshotId is not null || item.AmendmentReason is not null ||
                    item.ChangedRecordIds.Count != 0 || item.AffectedExaminationMaterialIds.Count != 0) ||
                index > 0 && (item.AmendsCloseSnapshotId != state.CloseSnapshots[index - 1].CloseSnapshotId ||
                    !Bounded(item.AmendmentReason, 1, MaximumTextLength) ||
                    item.ChangedRecordIds.Count == 0 || item.AffectedExaminationMaterialIds.Count == 0 ||
                    NormalizeIds(item.ChangedRecordIds) is not { } changed ||
                    !item.ChangedRecordIds.SequenceEqual(changed) ||
                    NormalizeIds(item.AffectedExaminationMaterialIds) is not { } affected ||
                    !item.AffectedExaminationMaterialIds.SequenceEqual(affected) ||
                    !item.ExpectedOccurrenceIds.SequenceEqual(state.CloseSnapshots[index - 1].ExpectedOccurrenceIds) ||
                    !item.AcknowledgedMaterialItemIds.SequenceEqual(state.CloseSnapshots[index - 1].AcknowledgedMaterialItemIds) ||
                    !item.ChangedRecordIds.SequenceEqual(ChangedRecordIds(
                        state.CloseSnapshots[index - 1].SourceReferences, item.SourceReferences))))
                return false;
        }
        return true;
    }

    static bool ValidStatus(TypeIIPeriodView state) => state.Status switch
    {
        "active" => state.CloseValidation is null && state.CloseApprovals.Count == 0 && state.CloseSnapshots.Count == 0,
        "close_blocked" => state.CloseValidation?.Status == "blocked" && state.CloseApprovals.Count == 0 && state.CloseSnapshots.Count == 0,
        "close_ready" => state.CloseValidation?.Status == "ready" && state.CloseApprovals.Count == 0 && state.CloseSnapshots.Count == 0,
        "close_approved" => state.CloseValidation?.Status == "ready" && state.CloseApprovals.Count == 1 && state.CloseSnapshots.Count == 0,
        "closed" => state.CloseSnapshots.Count > 0 && state.CloseApprovals.Count == 1 &&
            state.CloseSnapshots[^1].ExpectedOccurrenceIds.SequenceEqual(
                state.ExpectedOccurrences.Select(item => item.OccurrenceId).OrderBy(IdKey, StringComparer.Ordinal)) &&
            state.CloseSnapshots[^1].AcknowledgedMaterialItemIds.SequenceEqual(
                state.CloseApprovals[0].AcknowledgedMaterialItemIds),
        _ => false,
    };

    static bool ValidOpeningState(TypeIIPeriodView state) => state.Status == "active" &&
        state.Revision == 1 && state.Amendments.Count == 0 && state.CloseValidation is null &&
        state.CloseApprovals.Count == 0 && state.CloseSnapshots.Count == 0;

    static bool SameOpening(TypeIIPeriodView state, TypeIIPlanView plan,
        TypeIIPeriodOpeningContent opening, Uuid actor, string display) =>
        ApprovedPlan(plan) && plan.TenantId == state.TenantId && plan.ProgramId == state.ProgramId &&
        NormalizeOpening(opening) is { } normalized &&
        state.TypeIIPlanId == plan.TypeIIPlanId && state.TypeIIPlanVersion == plan.CurrentVersionNumber &&
        state.TypeIIPlanContentSha256 == plan.Current!.ContentSha256 &&
        state.ExaminationId == normalized.ExaminationId && state.OpeningSnapshotId == normalized.OpeningSnapshotId &&
        state.OpeningSnapshotSha256 == normalized.OpeningSnapshotSha256 &&
        SameReferences(state.OpeningSourceReferences, normalized.SourceReferences) &&
        state.AcknowledgedOpeningGapIds.SequenceEqual(normalized.AcknowledgedOpeningGapIds) &&
        state.ActorMemberId == actor && state.ActorDisplay == display.Trim();

    static bool SameValidation(TypeIIPeriodCloseValidationView? existing,
        TypeIIPeriodCloseValidationContent content, Uuid actor, string display, DateTimeOffset at) =>
        existing is not null && content is not null && NormalizeReferences(content.SourceReferences) is { } refs &&
        NormalizeIds(content.ExpectedOccurrenceIds) is { } expected &&
        NormalizeIds(content.ActualOccurrenceIds, allowDuplicates: true) is { } actual &&
        NormalizeIds(content.DuplicateOccurrenceIds) is { } duplicates &&
        NormalizeIds(content.MissingWorkIds) is { } missing &&
        NormalizeIds(content.UnresolvedMaterialItemIds) is { } unresolved &&
        SameReferences(existing.SourceReferences, refs) && existing.ExpectedOccurrenceIds.SequenceEqual(expected) &&
        existing.ActualOccurrenceIds.SequenceEqual(actual) && existing.DuplicateOccurrenceIds.SequenceEqual(duplicates) &&
        existing.MissingWorkIds.SequenceEqual(missing) && existing.UnresolvedMaterialItemIds.SequenceEqual(unresolved) &&
        existing.ValidatorMemberId == actor && existing.ValidatorDisplay == display.Trim() && existing.ValidatedAt == at;

    static bool SameSnapshot(TypeIIPeriodCloseSnapshotView existing,
        TypeIIPeriodCloseSnapshotContent content, Uuid actor, string role) =>
        NormalizeSnapshot(content) is { } normalized && existing.ContentSha256 == Digest(normalized) &&
        existing.ActorMemberId == actor && existing.ActorRole == role;

    static bool SameAmendment(TypeIIPeriodCloseSnapshotView existing,
        TypeIIPeriodCloseSnapshotContent content, string reason, IReadOnlyList<Uuid> changed,
        IReadOnlyList<Uuid> affected, Uuid actor, string role, string display, DateTimeOffset at) =>
        NormalizeSnapshot(content) is { } normalized && NormalizeIds(changed) is { } normalizedChanged &&
        NormalizeIds(affected) is { } normalizedAffected && existing.ContentSha256 == Digest(normalized) &&
        existing.AmendmentReason == reason && existing.ChangedRecordIds.SequenceEqual(normalizedChanged) &&
        existing.AffectedExaminationMaterialIds.SequenceEqual(normalizedAffected) &&
        existing.ActorMemberId == actor && existing.ActorRole == role && existing.ActorDisplay == display.Trim() &&
        existing.RecordedAt == at;

    static bool SameOccurrences(IReadOnlyList<TypeIIPeriodOccurrenceView> left,
        IReadOnlyList<TypeIIPeriodOccurrenceView> right) => left.Count == right.Count &&
        left.Zip(right).All(pair => SameOccurrence(pair.First, pair.Second));

    static bool SameOccurrence(TypeIIPeriodOccurrenceView left,
        TypeIIPeriodOccurrenceView right) => left.OccurrenceId == right.OccurrenceId &&
        left.ControlId == right.ControlId && left.ControlVersionId == right.ControlVersionId &&
        left.Sequence == right.Sequence && left.DueOn == right.DueOn &&
        left.OwnerMemberId == right.OwnerMemberId && left.Cadence == right.Cadence &&
        (left.ExpectedEvidenceDescriptions is null && right.ExpectedEvidenceDescriptions is null ||
         left.ExpectedEvidenceDescriptions is not null && right.ExpectedEvidenceDescriptions is not null &&
         left.ExpectedEvidenceDescriptions.SequenceEqual(right.ExpectedEvidenceDescriptions,
             StringComparer.Ordinal));

    static bool SameValidation(TypeIIPeriodCloseValidationView? left,
        TypeIIPeriodCloseValidationView? right) => left == right || left is not null && right is not null &&
        SameReferences(left.SourceReferences, right.SourceReferences) &&
        left.ExpectedOccurrenceIds.SequenceEqual(right.ExpectedOccurrenceIds) &&
        left.ActualOccurrenceIds.SequenceEqual(right.ActualOccurrenceIds) &&
        left.DuplicateOccurrenceIds.SequenceEqual(right.DuplicateOccurrenceIds) &&
        left.MissingWorkIds.SequenceEqual(right.MissingWorkIds) &&
        left.UnresolvedMaterialItemIds.SequenceEqual(right.UnresolvedMaterialItemIds) &&
        left.Status == right.Status && left.ValidatorMemberId == right.ValidatorMemberId &&
        left.ValidatorDisplay == right.ValidatorDisplay && left.ValidatedAt == right.ValidatedAt;

    static bool SameReferences(IReadOnlyList<ExaminationContentReference> left,
        IReadOnlyList<ExaminationContentReference> right) => left.Count == right.Count &&
        left.Zip(right).All(pair => pair.First == pair.Second);

    static bool Prefix<T>(IReadOnlyList<T> prior, IReadOnlyList<T> next) =>
        prior.Count <= next.Count && prior.Zip(next).All(pair => Digest(pair.First) == Digest(pair.Second));

    static ExaminationContentReference[]? NormalizeReferences(
        IReadOnlyList<ExaminationContentReference>? references)
    {
        if (references is null || references.Count is < 1 or > MaximumSources || references.Any(item =>
                item is null || !Token(item.SourceType, 80) || item.RecordId == Uuid.Empty ||
                item.VersionId == Uuid.Empty || item.Version <= 0 || !Sha256(item.ContentSha256)) ||
            references.Select(item => (item.SourceType, item.RecordId)).Distinct().Count() != references.Count)
            return null;
        return references.Select(item => item with
        {
            SourceType = item.SourceType.Trim(),
            ContentSha256 = item.ContentSha256.ToLowerInvariant(),
        })
            .OrderBy(item => item.SourceType, StringComparer.Ordinal)
            .ThenBy(item => IdKey(item.RecordId), StringComparer.Ordinal)
            .ThenBy(item => item.Version).ToArray();
    }

    static Uuid[]? NormalizeIds(IReadOnlyList<Uuid>? values, bool allowDuplicates = false)
    {
        if (values is null || values.Count > MaximumOccurrences || values.Any(static id => id == Uuid.Empty) ||
            !allowDuplicates && values.Distinct().Count() != values.Count)
            return null;
        return values.OrderBy(IdKey, StringComparer.Ordinal).ToArray();
    }

    static Uuid[] ChangedRecordIds(IReadOnlyList<ExaminationContentReference> prior,
        IReadOnlyList<ExaminationContentReference> next)
    {
        var oldByKey = prior.ToDictionary(item => (item.SourceType, item.RecordId));
        var newByKey = next.ToDictionary(item => (item.SourceType, item.RecordId));
        return oldByKey.Keys.Union(newByKey.Keys).Where(key =>
            !oldByKey.TryGetValue(key, out var oldValue) || !newByKey.TryGetValue(key, out var newValue) ||
            oldValue != newValue).Select(key => key.RecordId).Distinct()
            .OrderBy(IdKey, StringComparer.Ordinal).ToArray();
    }

    static bool Bounded(string? value, int minimum, int maximum) => value is not null &&
        value.Trim().Length >= minimum && value.Trim().Length <= maximum;
    static bool Token(string? value, int maximum) => Bounded(value, 1, maximum) &&
        value!.Trim().All(static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');
    static string Clean(string? value) => value?.Trim() ?? string.Empty;
    static bool Sha256(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
    static string IdKey(Uuid id) => id.ToString();
    static string Digest<T>(T value) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();
    bool ValidIdentity() => _tenantId != Uuid.Empty && _programId != Uuid.Empty && Id != Uuid.Empty;
    static bool ValidActor(Uuid id, string? display, DateTimeOffset at) => id != Uuid.Empty &&
        Bounded(display, 1, 200) && at != default;
    CommandFailure StaleRevision() => CommandFailure.ForVersion(
        VersionedRecordRules.StaleRevision("Type II period", Revision));
    static CommandFailure Invalid(string message) => CommandFailure.InvalidContent(message);
    static ReadOnlyCollection<T> Freeze<T>(T[] values) => Array.AsReadOnly(values);
    static ReadOnlyCollection<T> Append<T>(IReadOnlyList<T> values, T value) =>
        Freeze(values.Append(value).ToArray());
    static TypeIIPeriodView Freeze(TypeIIPeriodView state) => state with
    {
        OpeningSourceReferences = Freeze(state.OpeningSourceReferences.ToArray()),
        AcknowledgedOpeningGapIds = Freeze(state.AcknowledgedOpeningGapIds.ToArray()),
        ExpectedOccurrences = Freeze(state.ExpectedOccurrences.Select(item => item with
        {
            ExpectedEvidenceDescriptions = item.ExpectedEvidenceDescriptions is { } evidence
                ? Freeze(evidence.ToArray())
                : null,
        }).ToArray()),
        Amendments = Freeze(state.Amendments.ToArray()),
        CloseValidation = state.CloseValidation is { } validation ? validation with
        {
            SourceReferences = Freeze(validation.SourceReferences.ToArray()),
            ExpectedOccurrenceIds = Freeze(validation.ExpectedOccurrenceIds.ToArray()),
            ActualOccurrenceIds = Freeze(validation.ActualOccurrenceIds.ToArray()),
            DuplicateOccurrenceIds = Freeze(validation.DuplicateOccurrenceIds.ToArray()),
            MissingWorkIds = Freeze(validation.MissingWorkIds.ToArray()),
            UnresolvedMaterialItemIds = Freeze(validation.UnresolvedMaterialItemIds.ToArray()),
        } : null,
        CloseApprovals = Freeze(state.CloseApprovals.Select(item => item with
        {
            AcknowledgedMaterialItemIds = Freeze(item.AcknowledgedMaterialItemIds.ToArray()),
        }).ToArray()),
        CloseSnapshots = Freeze(state.CloseSnapshots.Select(item => item with
        {
            SourceReferences = Freeze(item.SourceReferences.ToArray()),
            ExpectedOccurrenceIds = Freeze(item.ExpectedOccurrenceIds.ToArray()),
            AcknowledgedMaterialItemIds = Freeze(item.AcknowledgedMaterialItemIds.ToArray()),
            ChangedRecordIds = Freeze(item.ChangedRecordIds.ToArray()),
            AffectedExaminationMaterialIds = Freeze(item.AffectedExaminationMaterialIds.ToArray()),
        }).ToArray()),
    };
}
