using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Examinations;
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
    const int MaximumSources = 1000;
    const int MaximumOccurrences = 10000;
    const int MaximumAmendments = 64;
    const int MaximumOccurrenceGenerationBatches = 256;
    const int MaximumEventBytes = 512 * 1024;
    const int MaximumTextLength = 2000;
    static readonly HashSet<string> ExecutiveRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "ceo", "ciso", "chief executive", "chief executive officer",
        "chief information security officer"
    };

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    TypeIIPeriodView? _current;
    IReadOnlyList<OccurrenceGenerationBatchView> _occurrenceGenerationBatches =
        Array.Empty<OccurrenceGenerationBatchView>();

    public long Revision => _current?.Revision ?? 0;
    public TypeIIPeriodView? Current => _current;
    public IReadOnlyList<OccurrenceGenerationBatchView> OccurrenceGenerationBatches =>
        _occurrenceGenerationBatches;

    public TypeIIPeriod(Uuid tenantId, Uuid programId, Uuid periodId)
        : base(periodId, new EventStreamAddress(tenantId.ToString(), "type-ii-periods",
            periodId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        On<ObservationPeriodStarted>(Apply);
        On<ObservationPeriodAmended>(Apply);
        On<DueControlOccurrencesGenerated>(Apply);
        On<ObservationPeriodCloseProposed>(Apply);
        On<TypeIIPeriodCloseApproved>(Apply);
        On<ObservationPeriodCloseFrozen>(Apply);
        On<TypeIIPeriodCloseAmended>(Apply);
    }

    public CommandFailure? Start(TypeIIPlanView approvedPlan, TypeIIPeriodOpeningContent opening,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset startedAt)
    {
        if (_current is { } existing)
            return SameOpening(existing, approvedPlan, opening, actorMemberId, actorDisplay)
                ? null
                : CommandFailure.StateConflict("The Type II period already exists with a different opening.");
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
        return Emit(state, actorMemberId, startedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new ObservationPeriodStarted(tenant, subject, revision, actor, snapshot, at),
            "start", creating: true);
    }

    public CommandFailure? Amend(long expectedRevision, DateOnly periodStart, DateOnly periodEnd,
        string reason, Uuid actorMemberId, string actorDisplay, DateTimeOffset amendedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The Type II period was not found.");
        reason = Clean(reason);
        if (current.Amendments.Count > 0 && current.Amendments[^1] is { } prior &&
            prior.PeriodStart == periodStart &&
            prior.PeriodEnd == periodEnd && prior.Reason == reason && prior.ActorMemberId == actorMemberId &&
            prior.ActorDisplay == actorDisplay.Trim())
            return null;
        if (expectedRevision != Revision)
            return StaleRevision();
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
        if (_occurrenceGenerationBatches.Any(batch => batch.ThroughDate < periodStart ||
                batch.ThroughDate > periodEnd))
            return CommandFailure.StateConflict(
                "A period amendment cannot move its boundaries outside a recorded generation batch.");

        var amendment = new TypeIIPeriodAmendmentView(current.PeriodStart, current.PeriodEnd,
            periodStart, periodEnd, reason, actorMemberId, actorDisplay.Trim(), amendedAt);
        var state = current with
        {
            Revision = Revision + 1,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Amendments = Append(current.Amendments, amendment),
            ActorMemberId = actorMemberId,
            ActorDisplay = actorDisplay.Trim(),
            UpdatedAt = amendedAt,
        };
        return Emit(state, actorMemberId, amendedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new ObservationPeriodAmended(tenant, subject, revision, actor, snapshot, at),
            "period_amendment");
    }

    /// <summary>
    /// Records the exact due expected occurrence identities selected by a durable generation key.
    /// The downstream scheduler can safely materialize each identity from this retained batch.
    /// </summary>
    public CommandFailure? GenerateDueOccurrences(string generationKey, DateOnly throughDate,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset generatedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The Type II observation period was not found.");

        generationKey = Clean(generationKey);
        var prior = _occurrenceGenerationBatches.FirstOrDefault(batch =>
            batch.GenerationKey == generationKey);
        if (prior is not null)
            return prior.ThroughDate == throughDate && prior.ActorMemberId == actorMemberId &&
                   prior.ActorDisplay == Clean(actorDisplay)
                ? null
                : CommandFailure.StateConflict(
                    "A generation key cannot be reused for another date or actor.");

        if (current.Status != "active")
            return CommandFailure.StateConflict("Occurrences can only be generated for an active period.");
        if (!Token(generationKey, 200) || throughDate < current.PeriodStart ||
            throughDate > current.PeriodEnd || !ValidActor(actorMemberId, actorDisplay, generatedAt) ||
            generatedAt < current.UpdatedAt ||
            _occurrenceGenerationBatches.Count >= MaximumOccurrenceGenerationBatches)
            return Invalid("Occurrence generation requires a bounded key, an in-period date, and an attributed actor.");

        var alreadyGenerated = _occurrenceGenerationBatches
            .SelectMany(static batch => batch.OccurrenceIds).ToHashSet();
        var ids = DueOccurrenceIds(current, throughDate, alreadyGenerated);
        var batch = new OccurrenceGenerationBatchView(current.Revision + 1, generationKey,
            throughDate, ids, actorMemberId, actorDisplay.Trim(), generatedAt);
        var ev = new DueControlOccurrencesGenerated(_tenantId, Id, current.Revision + 1,
            generationKey, throughDate, ids, actorMemberId, actorDisplay.Trim(), generatedAt);
        if (JsonSerializer.SerializeToUtf8Bytes(ev).Length > MaximumEventBytes)
            return Invalid("The occurrence generation batch exceeds the bounded event payload.");

        RaiseEvent(ev);
        return null;
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
            return SameSnapshot(prior, content, actorMemberId, actorRole, frozenAt) ? null :
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
            frozenAt, null, null, [], []);
        var state = current with
        {
            Status = "closed",
            Revision = Revision + 1,
            CloseSnapshots = Append(current.CloseSnapshots, snapshot),
            ActorMemberId = actorMemberId,
            ActorDisplay = actorRole,
            UpdatedAt = frozenAt,
        };
        return Emit(state, actorMemberId, frozenAt,
            static (tenant, subject, revision, actor, snapshotState, at) =>
                new ObservationPeriodCloseFrozen(tenant, subject, revision, actor, snapshotState, at),
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

    void Apply(ObservationPeriodStarted ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, true, "start");
    void Apply(ObservationPeriodAmended ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "period_amendment");
    void Apply(DueControlOccurrencesGenerated ev)
    {
        if (_current is not { } current || ev.TenantId != _tenantId || ev.SubjectId != Id ||
            ev.Revision != Revision + 1 || ev.ActorMemberId is not { } actorMemberId ||
            actorMemberId == Uuid.Empty || ev.OccurredAt == default ||
            current.Status != "active" || ev.OccurredAt < current.UpdatedAt ||
            !Token(ev.GenerationKey, 200) || ev.GenerationKey != Clean(ev.GenerationKey) ||
            ev.ThroughDate < current.PeriodStart || ev.ThroughDate > current.PeriodEnd ||
            !ValidActor(actorMemberId, ev.ActorDisplay, ev.OccurredAt) ||
            _occurrenceGenerationBatches.Count >= MaximumOccurrenceGenerationBatches ||
            _occurrenceGenerationBatches.Any(batch => batch.GenerationKey == ev.GenerationKey))
            throw new InvalidOperationException("The occurrence generation event is invalid.");

        var alreadyGenerated = _occurrenceGenerationBatches
            .SelectMany(static batch => batch.OccurrenceIds).ToHashSet();
        var ids = NormalizeIds(ev.OccurrenceIds);
        if (ids is null || !ids.SequenceEqual(ev.OccurrenceIds) ||
            !ids.SequenceEqual(DueOccurrenceIds(current, ev.ThroughDate, alreadyGenerated)))
            throw new InvalidOperationException("The occurrence generation event does not match its due period population.");

        var batch = new OccurrenceGenerationBatchView(ev.Revision, ev.GenerationKey,
            ev.ThroughDate, ids, actorMemberId, ev.ActorDisplay.Trim(), ev.OccurredAt);
        var next = current with { Revision = ev.Revision };
        if (ValidateState(next) is not null)
            throw new InvalidOperationException("The occurrence generation event is invalid.");
        _occurrenceGenerationBatches = Append(_occurrenceGenerationBatches, batch);
        _current = Freeze(next);
    }
    void Apply(ObservationPeriodCloseProposed ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "validation");
    void Apply(TypeIIPeriodCloseApproved ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "approval");
    void Apply(ObservationPeriodCloseFrozen ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "freeze");
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
        if (JsonSerializer.SerializeToUtf8Bytes(ev).Length > MaximumEventBytes)
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
                item.OwnerMemberId == Uuid.Empty || !Bounded(item.Cadence, 1, 80)) ||
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
            for (var index = 0; index < control.ExpectedOccurrences; index++)
            {
                var due = cadence switch
                {
                    "daily" => control.EffectiveFrom.AddDays(index),
                    "weekly" => control.EffectiveFrom.AddDays(checked(7 * index)),
                    "monthly" => control.EffectiveFrom.AddMonths(index),
                    "quarterly" => control.EffectiveFrom.AddMonths(checked(3 * index)),
                    "semiannual" or "semi_annual" => control.EffectiveFrom.AddMonths(checked(6 * index)),
                    "annual" or "annually" => control.EffectiveFrom.AddYears(index),
                    "event_driven" or "ad_hoc" => null,
                    _ => (DateOnly?)null,
                };
                if (cadence is not ("event_driven" or "ad_hoc") && due is null ||
                    due is { } dueOn && (dueOn < periodStart || dueOn > periodEnd ||
                        control.EffectiveThrough is { } through && dueOn > through))
                    return null;
                var identity = $"expected:{control.ControlId}:{control.ControlVersionId}:{index + 1}";
                occurrences.Add(new TypeIIPeriodOccurrenceView(Uuid.CreateVersion5(periodId, identity),
                    control.ControlId, control.ControlVersionId, index + 1, due, owner, cadence));
                if (occurrences.Count > MaximumOccurrences)
                    return null;
            }
        }
        return Freeze(occurrences.OrderBy(item => IdKey(item.ControlId), StringComparer.Ordinal)
            .ThenBy(item => item.Sequence).ToArray());
    }

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
        TypeIIPeriodCloseSnapshotContent content, Uuid actor, string role, DateTimeOffset at) =>
        NormalizeSnapshot(content) is { } normalized && existing.ContentSha256 == Digest(normalized) &&
        existing.ActorMemberId == actor && existing.ActorRole == role && existing.RecordedAt == at;

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
        left.Zip(right).All(pair => pair.First == pair.Second);

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
        ExpectedOccurrences = Freeze(state.ExpectedOccurrences.ToArray()),
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

    static Uuid[] DueOccurrenceIds(TypeIIPeriodView state, DateOnly throughDate,
        HashSet<Uuid> alreadyGenerated) => state.ExpectedOccurrences
        .Where(item => item.DueOn is { } due && due <= throughDate &&
            !alreadyGenerated.Contains(item.OccurrenceId))
        .Select(static item => item.OccurrenceId)
        .OrderBy(IdKey, StringComparer.Ordinal)
        .ToArray();
}
