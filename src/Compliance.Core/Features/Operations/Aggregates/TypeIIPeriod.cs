using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Compact native period authority; full populations, source bodies and histories are composed by TypeIIPeriodReader.</summary>
public sealed class TypeIIPeriod : Aggregate
{
    public const string Area = "type-ii-periods";
    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _selectedRoot;
    readonly Uuid _selectedEvent;
    DomainEvent? _pendingFact;
    DomainEvent? _selectedAccepted;
    DomainEvent? _selectedTerminal;
    DomainEvent? _selectedSource;
    bool _openingAccepted;
    Uuid _boundProgramId;

    public TypeIIPeriod(Uuid tenantId, Uuid programId, Uuid periodId, Uuid selectedLogicalId = default,
        Uuid selectedRootId = default, Uuid selectedEventId = default)
        : base(periodId, new EventStreamAddress(tenantId.ToString(), Area, periodId.ToString()))
    {
        (_tenantId, _programId, _selectedRoot, _selectedEvent) = (tenantId, programId,
            selectedRootId == Uuid.Empty ? selectedLogicalId : selectedRootId, selectedEventId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<TypeIIPeriodOpeningAccepted>(ApplyFact);
        On<TypeIIPeriodAmendmentAccepted>(ApplyFact);
        On<TypeIIPeriodOpeningRejected>(ApplyFact);
        On<TypeIIPeriodAmendmentCanceled>(ApplyFact);
        On<ObservationPeriodStarted>(ApplyFact);
        On<ObservationPeriodAmended>(ApplyFact);
        On<DueControlOccurrencesGenerated>(ApplyFact);
        On<ObservationPeriodCloseProposed>(ApplyFact);
        On<TypeIIPeriodCloseApproved>(ApplyFact);
        On<ObservationPeriodCloseFrozen>(ApplyFact);
        On<TypeIIPeriodCloseAmended>(ApplyFact);
    }
    public NativeAcceptance Acceptance { get; }
    public long NativeSequence { get; private set; }
    public TypeIIPeriodAuthorityHeader? Header { get; private set; }
    public ImmutableContentRef? CurrentContent { get; private set; }
    public long Revision => Header?.Revision ?? 0;
    public Uuid TenantId => _tenantId;
    public Uuid ProgramId => _boundProgramId != Uuid.Empty ? _boundProgramId : Header?.ProgramId ?? _programId;
    public Uuid? PendingOperationId => (_pendingFact as INativeOperationFact)?.Operation.RootOperationId;
    public DomainEvent? Pending => _pendingFact;
    public TypeIIPeriodCalendarAuthorization? CalendarAuthorization { get; private set; }
    public ImmutableContentRef? CalendarIntent { get; private set; }
    public DomainEvent? AcceptedFor(Uuid operationId) => PendingOperationId == operationId ? _pendingFact :
        operationId == _selectedRoot ? _selectedAccepted : null;
    public DomainEvent? TerminalFor(Uuid operationId) => operationId == _selectedRoot ? _selectedTerminal : null;
    public bool IsFenced(Uuid operationId) => TerminalFor(operationId) is TypeIIPeriodOpeningRejected or TypeIIPeriodAmendmentCanceled;
    public bool IsApplied(Uuid operationId) => TerminalFor(operationId) is ObservationPeriodStarted or ObservationPeriodAmended;
    public DomainEvent? FactById(Uuid eventId) => eventId == _selectedEvent ? _selectedSource : null;
    public static Uuid GenerationLogicalId(Uuid periodId, string generationKey) =>
        TypeIIPeriodRules.GenerationLogicalId(periodId, generationKey);
    public static ImmutableContentRef? IntentOf(DomainEvent? accepted) => accepted switch
    {
        TypeIIPeriodOpeningAccepted opening => opening.Intent,
        TypeIIPeriodAmendmentAccepted amendment => amendment.Intent,
        _ => null,
    };

    public Result PrepareOpeningAcceptance() => Header is null && !_openingAccepted
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
            operation, firstResponse, null, ProgramId));
        return Result.Success;
    }

    public Result AcceptAmendment(long expectedRevision, OperationProvenance operation, ImmutableContentRef intent,
        ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.Owner != Stream || operation.LogicalId != operation.RootOperationId)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A period amendment is the logical operation of its own command."));
        if (Header is null)
            return Result.Failure(CommandFailure.MissingRecord("The Type II period was not found.").ToRequestError());
        if (_pendingFact is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Another opening or amendment is still being applied; retry after it ends.", true));
        if (expectedRevision != Revision)
            return Result.Failure(StaleRevision().ToRequestError());
        RaiseEvent(new TypeIIPeriodAmendmentAccepted(Stream, Revision, TypeIIRangeEffects.Generation, intent, null,
            operation, firstResponse, null, ProgramId));
        return Result.Success;
    }

    /// <summary>Checks that a pending, not yet applied amendment can be canceled at the expected revision.</summary>
    public Result PrepareCancel(Uuid amendmentId, long expectedRevision)
    {
        if (PendingOperationId != amendmentId || _pendingFact is not TypeIIPeriodAmendmentAccepted)
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
    public Result CancelAmendment(Uuid amendmentId, OperationProvenance operation, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.Owner != Stream || operation.RootOperationId != amendmentId)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A cancellation names the amendment it ends."));
        if (PendingOperationId != amendmentId || _pendingFact is not TypeIIPeriodAmendmentAccepted accepted)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only a pending amendment can be canceled; an applied amendment is irreversible."));
        RaiseEvent(new TypeIIPeriodAmendmentCanceled(Stream, Revision, TypeIIRangeEffects.Generation,
            accepted.Intent, null, operation, firstResponse, operationResult, ProgramId));
        return Result.Success;
    }

    /// <summary>Rejects the pending opening that the calendar proved conflicts; an applied opening is never rejected.</summary>
    public Result RejectOpening(OperationProvenance operation, ImmutableContentRef failureProof)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(failureProof);
        if (PendingOperationId != operation.RootOperationId || operation.Owner != Stream ||
            _pendingFact is not TypeIIPeriodOpeningAccepted accepted)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only the exact pending opening can be rejected."));
        RaiseEvent(new TypeIIPeriodOpeningRejected(Stream, Revision, TypeIIRangeEffects.Generation, accepted.Intent,
            failureProof, operation, null, null, ProgramId));
        return Result.Success;
    }

    /// <summary>Rejects the pending amendment that the calendar proved conflicts; the applied range is preserved.</summary>
    public Result RejectAmendment(OperationProvenance operation, ImmutableContentRef failureProof)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(failureProof);
        if (PendingOperationId != operation.RootOperationId || operation.Owner != Stream ||
            _pendingFact is not TypeIIPeriodAmendmentAccepted accepted)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only the exact pending amendment can be rejected."));
        RaiseEvent(new TypeIIPeriodAmendmentCanceled(Stream, Revision, TypeIIRangeEffects.Generation,
            accepted.Intent, failureProof, operation, null, null, ProgramId));
        return Result.Success;
    }


    public Result AtCut(long nativeSequence, ulong physicalPosition) => NativeSequence == nativeSequence &&
        CommittedStreamPosition == physicalPosition ? Result.Success : Failure("The exact period preparation cut changed.", true);

    /// <summary>Rechecks the exact physical/native cut and verified local transition inside the owning OCC append.</summary>
    public Result Append(DomainEvent value, TypeIIPeriodView? before, TypeIIPeriodView after,
        long preparedSequence, ulong preparedPosition)
    {
        if (NativeSequence != preparedSequence || CommittedStreamPosition != preparedPosition)
            return Failure("The period changed while its exact immutable content was prepared.", true);
        if (value is not ITypeIIPeriodStateFact fact ||
            (before is null ? CurrentContent is not null || Header is not null :
                CurrentContent is null || ContentDigest.OfJson(before) != CurrentContent.Sha256 ||
                TypeIIPeriodContent.Header(before) != Header) ||
            fact.CurrentContent.Schema != TypeIIPeriodContent.StateSchema ||
            ContentDigest.OfJson(after) != fact.CurrentContent.Sha256 ||
            TypeIIPeriodContent.Header(after) != fact.Header)
            return Failure("The period's prepared content does not match its exact native authority.");
        var rules = new TypeIIPeriodRules(_tenantId, _programId, Id);
        var transition = TypeIIPeriodContent.Transition(value);
        if (rules.ValidateState(after) is not null ||
            (before is null ? transition != "start" || !TypeIIPeriodRules.ValidOpeningState(after) :
                transition == "generation" ? !TypeIIPeriodContent.SameGenerationState(before, after) :
                !TypeIIPeriodRules.ValidTransition(before, after, fact.ActorMemberId, transition)))
            return Failure("The prepared Type II period transition is invalid.");
        try
        {
            ValidateStateFact(value, fact);
            if (DomainEventPayload.Size(value) > DomainEventPayload.MaximumBytes)
                return Failure("The compact period event exceeds the bounded payload size.");
        }
        catch (InvalidOperationException)
        {
            return Failure("The period mutation differs from its exact local native authority.");
        }
        RaiseEvent(value);
        return Result.Success;
    }

    void ApplyFact(DomainEvent value)
    {
        if (value is ITypeIIPeriodStateFact fact)
        {
            ValidateStateFact(value, fact);
            if (fact.Operation.RootOperationId == _selectedRoot && value is ObservationPeriodStarted or ObservationPeriodAmended)
                _selectedTerminal = value;
            Header = fact.Header;
            CurrentContent = fact.CurrentContent;
            if (value is ObservationPeriodStarted started)
                Settle(started.Change, started.CalendarAuthorizationId, started.CalendarGeneration);
            else if (value is ObservationPeriodAmended amended)
                Settle(amended.Change, amended.CalendarAuthorizationId, amended.CalendarGeneration);
            else if (value is ObservationPeriodCloseFrozen)
            {
                CalendarAuthorization = null;
                CalendarIntent = null;
            }
        }
        else if (value is TypeIIPeriodOpeningAccepted opening)
            Accept(opening, opening.Owner, opening.Revision, opening.Generation, opening.Operation, opening.FirstResponse, opening.ProgramId, true);
        else if (value is TypeIIPeriodAmendmentAccepted amendment)
            Accept(amendment, amendment.Owner, amendment.Revision, amendment.Generation, amendment.Operation, amendment.FirstResponse, amendment.ProgramId, false);
        else if (value is TypeIIPeriodOpeningRejected rejected)
            Fence(rejected, rejected.Owner, rejected.Operation, rejected.Intent, rejected.ProgramId, true);
        else if (value is TypeIIPeriodAmendmentCanceled canceled)
            Fence(canceled, canceled.Owner, canceled.Operation, canceled.Intent, canceled.ProgramId, false);
        else
            throw new InvalidOperationException("The Type II period has another native fact kind.");
        NativeSequence++;
        Acceptance.Observe(value);
        if (value.Metadata?.EventId == _selectedEvent)
            _selectedSource = value;
    }

    void Accept(DomainEvent fact, EventStreamAddress owner, long revision, long generation,
        OperationProvenance operation, ImmutableContentRef? firstResponse, Uuid programId, bool opening)
    {
        if (programId == Uuid.Empty || _boundProgramId != Uuid.Empty && _boundProgramId != programId ||
            _pendingFact is not null || owner != Stream || operation.Owner != Stream ||
            operation.FirstOwner is { } first && first != Stream || operation.AffectedOwner != Stream ||
            operation.ExpectedRevisionOwner != Stream || operation.LogicalId != operation.RootOperationId ||
            revision != Revision || generation != TypeIIRangeEffects.Generation || firstResponse?.Schema != FirstResponses.Schema ||
            (opening ? Header is not null || _openingAccepted || operation.RootOperationId != Id : Header is null))
            throw new InvalidOperationException("A period accepts one exact pending root at its own revision.");
        _boundProgramId = programId;
        _pendingFact = fact;
        _openingAccepted |= opening;
        if (operation.RootOperationId == _selectedRoot)
            _selectedAccepted = fact;
    }

    void Fence(DomainEvent fact, EventStreamAddress owner, OperationProvenance operation,
        ImmutableContentRef intent, Uuid programId, bool opening)
    {
        if (programId != ProgramId || _pendingFact is null || PendingOperationId != operation.RootOperationId || owner != Stream ||
            operation.Owner != Stream || (_pendingFact is TypeIIPeriodOpeningAccepted) != opening || IntentOf(_pendingFact) != intent)
            throw new InvalidOperationException("Only the exact pending period operation can be fenced.");
        if (operation.RootOperationId == _selectedRoot)
            _selectedTerminal = fact;
        _pendingFact = null;
    }

    void Settle(ImmutableContentRef intent, Uuid authorizationId, long generation)
    {
        _pendingFact = null;
        CalendarAuthorization = new TypeIIPeriodCalendarAuthorization(authorizationId, generation);
        CalendarIntent = intent;
    }

    void ValidateStateFact(DomainEvent value, ITypeIIPeriodStateFact fact)
    {
        var after = fact.Header;
        var operation = fact.Operation;
        if (after is null || fact.TenantId != _tenantId || fact.SubjectId != Id ||
            after.TenantId != _tenantId || after.ProgramId != ProgramId || after.PeriodId != Id ||
            after.Revision != Revision + 1 || fact.Revision != after.Revision || fact.NativeSequence != NativeSequence + 1 ||
            after.ExpectedOccurrenceCount is < 1 or > 10000 || after.CloseSnapshotCount is < 0 or > 64 ||
            after.PeriodStart == default || after.PeriodEnd < after.PeriodStart ||
            fact.CurrentContent.Schema != TypeIIPeriodContent.StateSchema || fact.CurrentContent.Bytes <= 0 ||
            operation.Owner != Stream || operation.AffectedOwner != Stream || operation.ExpectedRevisionOwner != Stream ||
            operation.FirstOwner is { } first && first != Stream || operation.Actor.Kind != "member" ||
            !Uuid.TryParse(operation.Actor.Id, out var member) || member == Uuid.Empty || member != fact.ActorMemberId ||
            fact.OperationResult?.Schema != "mutation-result-content.v1")
            throw new InvalidOperationException("The compact Type II period native authority is invalid.");
        if (value is not DueControlOccurrencesGenerated && (after.ActorMemberId != member || after.UpdatedAt != fact.OccurredAt ||
            Header is { } current && fact.OccurredAt < current.UpdatedAt) ||
            value is not (ObservationPeriodStarted or ObservationPeriodAmended or DueControlOccurrencesGenerated) && operation.AcceptedAt != fact.OccurredAt)
            throw new InvalidOperationException("A period decision must retain its exact actor and decision time.");
        var change = value switch
        {
            ObservationPeriodStarted startedFact => startedFact.Change,
            ObservationPeriodAmended amended => amended.Change,
            ObservationPeriodCloseProposed validation => validation.Change,
            TypeIIPeriodCloseApproved approval => approval.Change,
            ObservationPeriodCloseFrozen frozenFact => frozenFact.Change,
            TypeIIPeriodCloseAmended amended => amended.Change,
            _ => null,
        };
        var schema = value is ObservationPeriodStarted or ObservationPeriodAmended ? TypeIIRangeIntent.Schema :
            value is ObservationPeriodCloseProposed ? TypeIIPeriodCloseValidationChange.Schema : TypeIIPeriodMutationContent.Schema;
        if (value is not DueControlOccurrencesGenerated && change?.Schema != schema ||
            fact.CurrentContent.MediaType != ImmutableContentStoreExtensions.JsonMediaType ||
            after.Status is not ("active" or "close_ready" or "close_blocked" or "close_approved" or "closed") ||
            after.ExpectedOccurrencesSha256.Length != 64 || after.TypeIIPlanContentSha256.Length != 64 || after.OpeningSnapshotSha256.Length != 64)
            throw new InvalidOperationException("The compact period authority has another state or immutable content schema.");
        if (Header is { } prior && (after.ProgramId != prior.ProgramId || after.ExaminationId != prior.ExaminationId || after.TypeIIPlanId != prior.TypeIIPlanId ||
            after.TypeIIPlanVersion != prior.TypeIIPlanVersion || after.TypeIIPlanContentSha256 != prior.TypeIIPlanContentSha256 ||
            after.OpeningSnapshotId != prior.OpeningSnapshotId || after.OpeningSnapshotSha256 != prior.OpeningSnapshotSha256 ||
            after.ExpectedOccurrenceCount != prior.ExpectedOccurrenceCount || after.ExpectedOccurrencesSha256 != prior.ExpectedOccurrencesSha256 ||
            after.CloseSnapshotCount < prior.CloseSnapshotCount || after.CloseSnapshotCount > prior.CloseSnapshotCount + 1))
            throw new InvalidOperationException("The period cannot rewrite its admitted identity or expected population.");
        if (value is ObservationPeriodStarted started)
            RequirePending(started.Operation, started.Change, started.CalendarAuthorizationId, started.CalendarGeneration, true);
        else if (value is ObservationPeriodAmended amended)
            RequirePending(amended.Operation, amended.Change, amended.CalendarAuthorizationId, amended.CalendarGeneration, false);
        else
        {
            if (Header is null || _pendingFact is not null || operation.RootOperationId != operation.LogicalId ||
                operation.CausationEventId is not null || fact.FirstResponse?.Schema != FirstResponses.Schema ||
                value is not DueControlOccurrencesGenerated && operation.OriginalExpectedRevision != (ulong)Revision)
                throw new InvalidOperationException("A period decision requires its exact current root and no pending range change.");
            var kind = value switch
            {
                DueControlOccurrencesGenerated => nameof(GenerateDueControlOccurrences),
                ObservationPeriodCloseProposed => nameof(RecordTypeIIPeriodCloseValidation),
                TypeIIPeriodCloseApproved => nameof(ApproveTypeIIPeriodClose),
                ObservationPeriodCloseFrozen => nameof(FreezeTypeIIPeriodClose),
                TypeIIPeriodCloseAmended => nameof(AmendTypeIIPeriodClose),
                _ => throw new InvalidOperationException("The period decision has another kind."),
            };
            if (operation.CommandKind != kind)
                throw new InvalidOperationException("The period decision has another authored command kind.");
        }
        if (value is DueControlOccurrencesGenerated generation && (Header?.Status != "active" || after.Status != "active" ||
            operation.LogicalId != GenerationLogicalId(Id, generation.GenerationKey) ||
            generation.ThroughDate < after.PeriodStart || generation.ThroughDate > after.PeriodEnd ||
            after.UpdatedAt != Header.UpdatedAt || after.ActorMemberId != Header.ActorMemberId))
            throw new InvalidOperationException("A generation must preserve its admitted period authority.");
        if (value is ObservationPeriodCloseProposed && (Header?.Status is not ("active" or "close_ready" or "close_blocked") ||
            after.Status is not ("close_ready" or "close_blocked") || after.ValidatorMemberId != member ||
            after.ValidatedAt != operation.AcceptedAt || DateOnly.FromDateTime(operation.AcceptedAt.UtcDateTime) <= after.PeriodEnd))
            throw new InvalidOperationException("A close reconciliation must follow the complete observation interval.");
        if (value is TypeIIPeriodCloseApproved && (Header?.Status != "close_ready" || after.Status != "close_approved" ||
            Header.ValidatorMemberId == member || after.ApproverMemberId != member || after.ValidationSha256 != Header.ValidationSha256))
            throw new InvalidOperationException("A close approval requires an independent executive over the exact ready validation.");
        if (value is ObservationPeriodCloseFrozen frozen && (Header?.Status != "close_approved" || after.Status != "closed" ||
            Header.ApproverMemberId != member || ExecutiveRole(after.CloseActorRole) != ExecutiveRole(Header.ApproverRole) || CalendarAuthorization is not { } live ||
            live.AuthorizationId != frozen.CalendarAuthorizationId || live.Generation != frozen.CalendarGeneration ||
            CalendarIntent != frozen.CalendarIntent || after.CloseSnapshotCount != 1 || after.LatestCloseVersion != 1 ||
            after.ValidationSha256 != Header.ValidationSha256 || after.ApprovalSha256 != Header.ApprovalSha256))
            throw new InvalidOperationException("A freeze requires its exact approving executive and live calendar permit.");
        if (value is TypeIIPeriodCloseAmended && (Header?.Status != "closed" || after.Status != "closed" ||
            after.CloseSnapshotCount != Header.CloseSnapshotCount + 1 || after.LatestCloseVersion != Header.LatestCloseVersion + 1 ||
            after.CloseOriginalExpectedRevision != Revision))
            throw new InvalidOperationException("A close amendment advances the immutable terminal lineage.");
    }

    void RequirePending(OperationProvenance operation, ImmutableContentRef change, Uuid authorizationId,
        long generation, bool opening)
    {
        if (_pendingFact is null || PendingOperationId != operation.RootOperationId ||
            (_pendingFact is TypeIIPeriodOpeningAccepted) != opening || IntentOf(_pendingFact) != change ||
            authorizationId != operation.RootOperationId || generation <= 0 ||
            opening != (Header is null))
            throw new InvalidOperationException("A period apply must answer its exact pending range admission and calendar permit.");
        if (operation.RootOperationId == _selectedRoot)
            _selectedTerminal = null;
    }

    static string ExecutiveRole(string? role) => role?.Trim().ToLowerInvariant() switch
    {
        "ceo" or "chief executive" or "chief executive officer" => "ceo",
        "ciso" or "chief information security officer" => "ciso",
        _ => string.Empty,
    };

    CommandFailure StaleRevision() => CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("Type II period", Revision));
    static Result Failure(string message, bool transient = false) =>
        Result.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
