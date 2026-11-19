using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Aggregates;

/// <summary>
/// One edition identifier across both kinds and all incarnations. This owner
/// holds one pending structural operation and at most one publication witness;
/// source ordering, parent admission and callback provenance are verified by
/// the invoking process, not by a projection or this owner's local state.
/// </summary>
public sealed class CriteriaEntry : Aggregate
{
    readonly Uuid _editionId;
    readonly string _identifier;
    readonly Uuid _selectedOperationId;
    readonly Uuid _selectedPublicationAttemptId;
    Uuid _draftGeneration;
    PendingProposal? _pending;
    PendingProposal? _selectedProposal;
    DomainEvent? _selectedOutcome;
    CriterionOperationAttemptFenced? _selectedFence;
    CriterionSummaryCorrected? _selectedSummary;
    CriterionSummaryUnchanged? _selectedSummaryUnchanged;
    CriterionSummaryRefused? _selectedSummaryRefused;
    CriterionSummaryAttemptFenced? _selectedSummaryFence;
    CriterionWitnessPinned? _selectedPin;
    CriterionWitnessPinFenced? _selectedPinFence;
    CriterionWitnessRefused? _selectedWitnessRefusal;
    CriterionWitnessPinned? _activePin;
    long _fencedExecutionGeneration;
    long _fencedAttemptOrdinal;
    Uuid _currentIncarnationId;
    Uuid _currentAcceptedEventId;
    string? _kind;
    string? _category;
    string? _parentIdentifier;
    Uuid? _parentIncarnationId;
    Uuid? _parentAcceptedEventId;
    string? _summary;
    long _revision;

    /// <summary>Opens one entry and selects the operation and publication attempt whose historical results are needed.</summary>
    public CriteriaEntry(Uuid editionId, string identifier, Uuid selectedOperationId = default,
        Uuid selectedPublicationAttemptId = default)
        : base(IdentityOf(editionId, identifier), StreamFor(editionId, identifier))
    {
        _editionId = editionId;
        _identifier = identifier;
        _selectedOperationId = selectedOperationId;
        _selectedPublicationAttemptId = selectedPublicationAttemptId;
        On<CriterionChangeProposed>(Apply);
        On<CriterionSeedProposed>(Apply);
        On<CriterionChanged>(Apply);
        On<CriterionChangeRefused>(Apply);
        On<CriterionOperationAttemptFenced>(Apply);
        On<CriterionWitnessPinned>(Apply);
        On<CriterionWitnessPinFenced>(Apply);
        On<CriterionWitnessRefused>(Apply);
        On<CriterionSummaryCorrected>(Apply);
        On<CriterionSummaryUnchanged>(Apply);
        On<CriterionSummaryRefused>(Apply);
        On<CriterionSummaryAttemptFenced>(Apply);
    }

    /// <summary>Stable identity shared by criteria, points of focus and all their incarnations.</summary>
    public static Uuid IdentityOf(Uuid editionId, string identifier)
    {
        if (!CriteriaContentBounds.Identifier(identifier))
            throw new ArgumentException("A criteria identifier must be nonempty and at most 128 UTF-8 bytes.", nameof(identifier));
        return AggregateIdentity.Create("CriteriaEntry", editionId, identifier);
    }

    /// <summary>Address in the same edition-scoped ordered area as the edition owner.</summary>
    public static EventStreamAddress StreamFor(Uuid editionId, string identifier) =>
        new("bdgrz", $"criteria-{editionId}", IdentityOf(editionId, identifier).ToString());

    /// <summary>Deterministic incarnation for a structural operation.</summary>
    public static Uuid IncarnationOf(Uuid editionId, string identifier, Uuid operationId) =>
        AggregateIdentity.Create("CriteriaEntryIncarnation", editionId, identifier, operationId);

    /// <summary>Current accepted revision, including summary corrections.</summary>
    public long Revision => _revision;
    /// <summary>Whether an entry is currently present on this owner.</summary>
    public bool Exists => _currentIncarnationId != Uuid.Empty;
    /// <summary>Whether an exact structural outcome is still pending.</summary>
    public bool HasPendingChange => _pending is not null;
    /// <summary>Whether one exact publication attempt currently protects this witness.</summary>
    public bool IsWitnessPinned => _activePin is not null;
    /// <summary>Current accepted incarnation, or empty when removed or not created.</summary>
    public Uuid CurrentIncarnationId => _currentIncarnationId;
    /// <summary>Current accepted structural fact identity, or empty.</summary>
    public Uuid CurrentAcceptedEventId => _currentAcceptedEventId;
    /// <summary>Current accepted kind.</summary>
    public string? CurrentKind => _kind;
    /// <summary>Current accepted category.</summary>
    public string? CurrentCategory => _category;
    /// <summary>Current point-of-focus parent identifier, if any.</summary>
    public string? CurrentParentIdentifier => _parentIdentifier;
    /// <summary>Current exact point-of-focus parent incarnation, if any.</summary>
    public Uuid? CurrentParentIncarnationId => _parentIncarnationId;
    /// <summary>Source-verified parent acceptance fact identity, if any.</summary>
    public Uuid? CurrentParentAcceptedEventId => _parentAcceptedEventId;
    /// <summary>Current original product-written summary, if present.</summary>
    public string? CurrentSummary => _summary;
    /// <summary>The draft generation to which this owner is bound, if observed.</summary>
    public Uuid DraftGeneration => _draftGeneration;
    /// <summary>The next execution generation that cannot be rejected by an older attempt fence.</summary>
    public long MinimumExecutionGeneration => checked(_fencedExecutionGeneration + 1);
    /// <summary>Exact currently active witness fact, if present.</summary>
    public CriterionWitnessPinned? ActiveWitnessPin => _activePin;
    /// <summary>Selected historical witness-pin outcome, if present.</summary>
    public CriterionWitnessPinned? SelectedWitnessPin => _selectedPin;
    /// <summary>Selected historical witness-fence outcome, if present.</summary>
    public CriterionWitnessPinFenced? SelectedWitnessFence => _selectedPinFence;
    /// <summary>The selected attempt's committed target-side refusal, when present.</summary>
    public CriterionWitnessRefused? SelectedWitnessRefusal => _selectedWitnessRefusal;

    /// <summary>Returns the selected operation's durable status without retaining a roster of earlier operations.</summary>
    public CriteriaOperationReceipt? Receipt(Uuid operationId)
    {
        if (operationId == Uuid.Empty || operationId != _selectedOperationId)
            return null;
        if (_selectedSummaryRefused is not null)
            return new CriteriaOperationReceipt(_editionId, operationId, "refused", "revision_mismatch");
        if (HasSelectedSummary || _selectedOutcome is CriterionChanged)
            return new CriteriaOperationReceipt(_editionId, operationId, "accepted");
        if (_selectedOutcome is CriterionChangeRefused refused)
            return new CriteriaOperationReceipt(_editionId, operationId, "refused", refused.Reason);
        return _selectedProposal is not null || _selectedFence is not null || _selectedSummaryFence is not null
            ? new CriteriaOperationReceipt(_editionId, operationId, "pending") : null;
    }

    /// <summary>
    /// Checks a structural intent without claiming an operation or emitting a fact.
    /// </summary>
    public Result PreviewProposal(CriterionChangeIntent intent, long executionGeneration, ActorAttribution actor,
        AccessAdmissionEvidence admission, Uuid? parentAcceptedEventId)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (!CriteriaContentBounds.StructuralIntent(intent) || !CriteriaContentBounds.AdmissionFields(actor, admission))
            return Failure(RequestErrorKind.Validation,
                "The entry fields and admission evidence must fit the criteria UTF-8 and 8192-byte JSON payload limits.");
        if (!AccessAdmissionEvidenceBinding.IsCurrentFormat(admission) || !Selected(intent.OperationId) || !ValidAdmission(intent, executionGeneration, actor, admission) || executionGeneration <= 0)
            return Failure(RequestErrorKind.Validation, "The selected operation and its trusted, exact admission are required.");
        if (HasSelectedSummary || _selectedSummaryFence is not null)
            return Failure(RequestErrorKind.Conflict, "This operation identity is already a summary correction.");
        if (_selectedProposal is { } previous)
            return previous.Source is CriterionChangeProposed prior && prior.Intent == intent && prior.ExecutionGeneration == executionGeneration &&
                   prior.Actor == actor && prior.ParentAcceptedEventId == parentAcceptedEventId
                ? Result.Success : Failure(RequestErrorKind.Conflict, "The operation already has different intent.");
        if (_selectedFence is { } selectedFence &&
            (selectedFence.Intent != intent || selectedFence.Actor != actor || executionGeneration <= selectedFence.ExecutionGeneration))
            return Failure(RequestErrorKind.Conflict, "The fenced operation cannot be changed or reused by an old execution.");
        if (_pending is not null || executionGeneration <= _fencedExecutionGeneration)
            return Failure(RequestErrorKind.Conflict, "Another change is pending or this execution has been fenced.");
        if (!ValidIntent(intent, parentAcceptedEventId))
            return Failure(RequestErrorKind.Validation, "The change, revision, entry or exact parent evidence is invalid.");
        if (_activePin is not null && (intent.Action is "replace" or "remove"))
            return Failure(RequestErrorKind.Conflict, "A publication witness cannot be removed or replaced while pinned.");

        return Result.Success;
    }

    /// <summary>Records a proposal bound to one source-verified operation claim.</summary>
    /// <remarks>The caller must verify the exact claim, draft, and parent source facts before invoking.</remarks>
    public Result Propose(CriterionChangeIntent intent, long executionGeneration, ActorAttribution actor,
        AccessAdmissionEvidence admission, Uuid? parentAcceptedEventId, CriterionEntryOperationClaimed claim)
    {
        var preview = PreviewProposal(intent, executionGeneration, actor, admission, parentAcceptedEventId);
        if (!preview.IsSuccess)
            return preview;
        if (!ValidClaim(intent, executionGeneration, actor, admission, claim))
            return Failure(RequestErrorKind.Validation, "The exact committed operation claim is required.");
        if (_selectedProposal is { } previous)
            return previous.Source is CriterionChangeProposed prior && prior.OperationClaimEventId == claim.Metadata.EventId
                ? Result.Success : Failure(RequestErrorKind.Conflict, "The proposal names another operation claim.");
        var incarnation = intent.Action == "remove" ? _currentIncarnationId :
            IncarnationOf(_editionId, _identifier, intent.OperationId);
        var proposed = new CriterionChangeProposed(intent, executionGeneration, incarnation, actor, admission,
            parentAcceptedEventId, claim.Metadata.EventId);
        if (!CriteriaContentBounds.Payload(proposed))
            return Failure(RequestErrorKind.Validation, "A criteria event's JSON business payload must fit within 8192 bytes.");
        RaiseEvent(proposed);
        return Result.Success;
    }

    /// <summary>
    /// Proposes one compiled seed fact. The internal caller must authenticate a
    /// system principal and independently verify the exact committed request,
    /// current draft and parent source facts before invoking this method.
    /// </summary>
    internal Result ProposeSeed(CriterionChangeIntent intent, long executionGeneration,
        Uuid? parentAcceptedEventId, CriteriaEditionDraftRequested request, Uuid operationClaimEventId)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(request);
        if (operationClaimEventId == Uuid.Empty || !ValidSeed(intent, executionGeneration, parentAcceptedEventId, request.Metadata.EventId,
                request.SeedVersion, request.SeedFingerprint) || request.EditionId != _editionId || request.Framework != "tsc" ||
            request.DraftGeneration != intent.DraftGeneration || request.OperationId != intent.DraftGeneration ||
            request.ExecutionGeneration <= 0 || request.Actor != new ActorAttribution("criteria-seed-bootstrap", "system") ||
            request.Admission is not null || request.Label != PlatformCriteriaCatalog.SeedEdition().EditionLabel ||
            request.Metadata.AggregateId != new CriteriaEdition(_editionId).Id)
            return Failure(RequestErrorKind.Validation, "The exact configured seed request and entry are required.");
        if (!Selected(intent.OperationId))
            return Failure(RequestErrorKind.Validation, "The seed operation must be selected.");
        if (_selectedProposal is { } previous)
            return previous.Source is CriterionSeedProposed prior && prior.Intent == intent &&
                prior.ExecutionGeneration == executionGeneration && prior.ParentAcceptedEventId == parentAcceptedEventId &&
                prior.EditionRequestEventId == request.Metadata.EventId && prior.OperationClaimEventId == operationClaimEventId && prior.SeedVersion == request.SeedVersion &&
                prior.SeedFingerprint == request.SeedFingerprint
                ? Result.Success : Failure(RequestErrorKind.Conflict, "The operation already has different intent.");
        if (_selectedFence is not null || HasSelectedSummary || _selectedSummaryFence is not null ||
            _pending is not null || executionGeneration <= _fencedExecutionGeneration || !ValidIntent(intent, parentAcceptedEventId))
            return Failure(RequestErrorKind.Conflict, "The entry cannot accept this seed proposal.");
        var proposed = new CriterionSeedProposed(intent, executionGeneration,
            IncarnationOf(_editionId, _identifier, intent.OperationId), parentAcceptedEventId,
            request.Metadata.EventId, operationClaimEventId, request.SeedVersion!, request.SeedFingerprint!);
        if (!CriteriaContentBounds.Payload(proposed))
            return Failure(RequestErrorKind.Validation, "A criteria event's JSON business payload must fit within 8192 bytes.");
        RaiseEvent(proposed);
        return Result.Success;
    }

    /// <summary>
    /// Records the exact participant outcome. The caller must first prove source
    /// admission and publication ordering; this method does not infer either.
    /// </summary>
    public Result Decide(Uuid operationId, long executionGeneration, Uuid proposalEventId, bool accepted,
        string? refusalReason, CriteriaEntryDecisionEvidence evidence)
    {
        if (operationId == Uuid.Empty || executionGeneration <= 0 || proposalEventId == Uuid.Empty ||
            (accepted ? refusalReason is not null : !CriteriaContentBounds.Text(refusalReason, CriteriaContentBounds.SummaryBytes)) ||
            !DecisionShape(evidence, accepted, refusalReason))
            return Failure(RequestErrorKind.Validation, "An exact proposal and accepted or refused outcome are required.");
        if (_selectedOperationId == operationId && _selectedOutcome is { } prior)
            return SameOutcome(prior, operationId, executionGeneration, proposalEventId, accepted, refusalReason, evidence)
                ? Result.Success : Failure(RequestErrorKind.Conflict, "The proposal already has a different outcome.");
        if (_pending is null || _pending.Intent.OperationId != operationId ||
            _pending.ExecutionGeneration != executionGeneration || _pending.Source.Metadata.EventId != proposalEventId)
            return Failure(RequestErrorKind.Conflict, "The exact proposed operation is not pending.");
        if (_pending.Source is CriterionChangeProposed human && !AccessAdmissionEvidenceBinding.IsCurrentFormat(human.Admission))
            return Failure(RequestErrorKind.Conflict, "The legacy proposal requires an explicitly qualified recovery policy.");
        DomainEvent outcome = accepted
            ? new CriterionChanged(_editionId, _identifier, operationId, executionGeneration, proposalEventId, evidence)
            : new CriterionChangeRefused(_editionId, _identifier, operationId, executionGeneration,
                proposalEventId, refusalReason!, evidence);
        if (!CriteriaContentBounds.Payload(outcome))
            return Failure(RequestErrorKind.Validation, "A criteria event's JSON business payload must fit within 8192 bytes.");
        RaiseEvent(outcome);
        return Result.Success;
    }

    /// <summary>Appends a real same-stream fence for a recovering request; the handler supplies trusted admission.</summary>
    public Result FenceOperation(CriterionChangeIntent intent, long executionGeneration, ActorAttribution actor,
        AccessAdmissionEvidence admission)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (!CriteriaContentBounds.StructuralIntent(intent) || !CriteriaContentBounds.AdmissionFields(actor, admission))
            return Failure(RequestErrorKind.Validation,
                "The entry fields and admission evidence must fit the criteria UTF-8 and 8192-byte JSON payload limits.");
        if (!AccessAdmissionEvidenceBinding.IsCurrentFormat(admission) || !Selected(intent.OperationId) || executionGeneration <= 0 || !ValidAdmission(intent, executionGeneration, actor, admission))
            return Failure(RequestErrorKind.Validation, "The selected recovering intent and trusted admission are required.");
        if (_selectedProposal is not null || HasSelectedSummary || _selectedSummaryFence is not null || _pending is not null)
            return Failure(RequestErrorKind.Conflict, "An existing proposal must be reconciled instead of fenced as absent.");
        if (_selectedFence is { } prior)
        {
            if (prior.Intent != intent || prior.Actor != actor)
                return Failure(RequestErrorKind.Conflict, "The fenced identity is bound to another intent.");
            if (executionGeneration == prior.ExecutionGeneration)
                return Result.Success;
        }
        if (executionGeneration <= _fencedExecutionGeneration)
            return Failure(RequestErrorKind.Conflict, "The execution generation was already fenced.");
        var fenced = new CriterionOperationAttemptFenced(intent, executionGeneration, actor, admission);
        if (!CriteriaContentBounds.Payload(fenced))
            return Failure(RequestErrorKind.Validation, "A criteria event's JSON business payload must fit within 8192 bytes.");
        RaiseEvent(fenced);
        return Result.Success;
    }

    /// <summary>
    /// Pins this accepted criterion from an exact committed edition-start fact.
    /// The caller must read and authenticate that source fact before invoking.
    /// </summary>
    public Result PinWitness(CriteriaPublicationStarted started)
    {
        ArgumentNullException.ThrowIfNull(started);
        if (!ValidStarted(started) || !SelectedAttempt(started.PublicationAttemptId))
            return Failure(RequestErrorKind.Validation, "The selected, source-verified publication attempt is required.");
        if (_selectedPin is { } previous)
            return Matches(previous, started) ? Result.Success :
                Failure(RequestErrorKind.Conflict, "The publication attempt already pinned different intent.");
        if (started.AttemptOrdinal <= _fencedAttemptOrdinal || _selectedPinFence is not null)
            return Failure(RequestErrorKind.Conflict, "The publication attempt was already fenced.");
        if (_activePin is not null || _pending is not null || !Exists || _kind != "criterion" ||
            _currentIncarnationId != started.WitnessIncarnationId || _draftGeneration != started.DraftGeneration)
            return Failure(RequestErrorKind.Conflict, "The exact accepted criterion is not an available witness.");
        RaiseEvent(new CriterionWitnessPinned(_editionId, _draftGeneration, _identifier, started.PublicationAttemptId,
            started.AttemptOrdinal, _currentIncarnationId, started.Metadata.EventId));
        return Result.Success;
    }

    /// <summary>Fences an exact attempt when its witness cannot satisfy the pin at this target position.</summary>
    /// <remarks>The caller must authenticate the committed start and qualify the exact target head and conditional append.</remarks>
    public Result RefuseWitness(CriteriaPublicationStarted started)
    {
        ArgumentNullException.ThrowIfNull(started);
        if (!ValidStarted(started) || !SelectedAttempt(started.PublicationAttemptId))
            return Failure(RequestErrorKind.Validation, "The selected source-verified publication attempt is required.");
        if (_selectedWitnessRefusal is { } previous)
            return Matches(previous, started) ? Result.Success :
                Failure(RequestErrorKind.Conflict, "The witness refusal already names a different attempt.");
        if (_selectedPin is not null || _selectedPinFence is not null || started.AttemptOrdinal <= _fencedAttemptOrdinal ||
            _pending is not null || _activePin is not null ||
            _draftGeneration != Uuid.Empty && _draftGeneration != started.DraftGeneration)
            return Failure(RequestErrorKind.Conflict, "The witness attempt or entry state requires reconciliation.");
        if (Exists && _kind == "criterion" && _currentIncarnationId == started.WitnessIncarnationId &&
            _draftGeneration == started.DraftGeneration)
            return Failure(RequestErrorKind.Conflict, "The exact criterion can still be pinned.");
        var fact = new CriterionWitnessRefused(_editionId, started.DraftGeneration, _identifier, started.PublicationAttemptId,
            started.AttemptOrdinal, started.WitnessIncarnationId, started.Metadata.EventId);
        if (!CriteriaContentBounds.Payload(fact))
            return Failure(RequestErrorKind.Validation, "A criteria event's JSON business payload must fit within 8192 bytes.");
        RaiseEvent(fact);
        return Result.Success;
    }

    /// <summary>
    /// Fences an exact committed edition-abort fact, including when the pin has
    /// not arrived. The caller must authenticate the source event before invoking.
    /// </summary>
    public Result FenceWitness(CriteriaPublicationAborted aborted)
    {
        ArgumentNullException.ThrowIfNull(aborted);
        if (!ValidAborted(aborted) || !SelectedAttempt(aborted.PublicationAttemptId))
            return Failure(RequestErrorKind.Validation, "The selected, source-verified abort is required.");
        if (_selectedPinFence is { } previous)
            return Matches(previous, aborted) ? Result.Success :
                Failure(RequestErrorKind.Conflict, "The attempt was fenced by different source evidence.");
        if (_draftGeneration != Uuid.Empty && _draftGeneration != aborted.DraftGeneration)
            return Failure(RequestErrorKind.Conflict, "The abort names another draft generation.");
        if (_activePin is { } active && active.AttemptOrdinal == aborted.AttemptOrdinal &&
            (active.PublicationAttemptId != aborted.PublicationAttemptId || active.IncarnationId != aborted.WitnessIncarnationId))
            return Failure(RequestErrorKind.Conflict, "The attempt ordinal is owned by another witness pin.");
        if (_activePin is { } earlier && earlier.AttemptOrdinal < aborted.AttemptOrdinal)
            return Failure(RequestErrorKind.Conflict, "A prior witness pin must be reconciled before fencing a later attempt.");
        RaiseEvent(new CriterionWitnessPinFenced(_editionId, aborted.DraftGeneration, _identifier,
            aborted.PublicationAttemptId, aborted.AttemptOrdinal, aborted.WitnessIncarnationId, aborted.Metadata.EventId));
        return Result.Success;
    }

    /// <summary>Checks a summary correction without claiming an operation or emitting a fact.</summary>
    public Result PreviewSummary(CriterionSummaryIntent intent, long executionGeneration, ActorAttribution actor, AccessAdmissionEvidence admission)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (!CriteriaContentBounds.SummaryIntent(intent) || !CriteriaContentBounds.AdmissionFields(actor, admission))
            return Failure(RequestErrorKind.Validation,
                "The entry fields and admission evidence must fit the criteria UTF-8 and 8192-byte JSON payload limits.");
        if (!AccessAdmissionEvidenceBinding.IsCurrentFormat(admission) || !Selected(intent.OperationId) || executionGeneration <= 0 || !ValidAdmission(intent, executionGeneration, actor, admission))
            return Failure(RequestErrorKind.Validation, "The selected operation and its trusted, exact admission are required.");
        if (_selectedProposal is not null || _selectedFence is not null)
            return Failure(RequestErrorKind.Conflict, "This identity already names a structural operation.");
        if (_selectedSummary is { } prior)
            return prior.Intent == intent && prior.ExecutionGeneration == executionGeneration && prior.Actor == actor ? Result.Success :
                Failure(RequestErrorKind.Conflict, "The operation already has different intent.");
        if (_selectedSummaryUnchanged is { } unchanged)
            return unchanged.Intent == intent && unchanged.ExecutionGeneration == executionGeneration && unchanged.Actor == actor ? Result.Success :
                Failure(RequestErrorKind.Conflict, "The operation already has different intent.");
        if (_selectedSummaryRefused is { } refused)
            return refused.Intent == intent && refused.ExecutionGeneration == executionGeneration && refused.Actor == actor ? Result.Success :
                Failure(RequestErrorKind.Conflict, "The operation already has different intent.");
        if ((_selectedSummaryFence is { } summaryFence &&
            (summaryFence.Intent != intent || summaryFence.Actor != actor || executionGeneration <= summaryFence.ExecutionGeneration)) ||
            executionGeneration <= _fencedExecutionGeneration)
            return Failure(RequestErrorKind.Conflict, "The summary execution was fenced or has changed intent.");
        if (!Exists || _pending is not null || intent.ExpectedRevision != _revision ||
            string.IsNullOrWhiteSpace(intent.Summary))
            return Failure(RequestErrorKind.Conflict, "The entry, current revision and original product summary are required.");
        return Result.Success;
    }

    /// <summary>Corrects original product text with a source-verified operation claim.</summary>
    public Result CorrectSummary(CriterionSummaryIntent intent, long executionGeneration, ActorAttribution actor,
        AccessAdmissionEvidence admission, CriterionEntryOperationClaimed claim)
    {
        var preview = PreviewSummary(intent, executionGeneration, actor, admission);
        if (!preview.IsSuccess)
            return preview;
        if (!ValidSummaryClaim(intent, executionGeneration, actor, admission, claim))
            return Failure(RequestErrorKind.Validation, "The exact committed summary operation claim is required.");
        if (_selectedSummary is { } prior)
            return prior.OperationClaimEventId == claim.Metadata.EventId ? Result.Success :
                Failure(RequestErrorKind.Conflict, "The summary names another operation claim.");
        if (_selectedSummaryUnchanged is { } priorUnchanged)
            return priorUnchanged.OperationClaimEventId == claim.Metadata.EventId ? Result.Success :
                Failure(RequestErrorKind.Conflict, "The summary names another operation claim.");
        if (_selectedSummaryRefused is not null)
            return Failure(RequestErrorKind.Conflict, "The summary operation was refused.");
        if (string.Equals(intent.Summary, _summary, StringComparison.Ordinal))
        {
            var unchanged = new CriterionSummaryUnchanged(intent, executionGeneration, actor, admission, claim.Metadata.EventId);
            if (!CriteriaContentBounds.Payload(unchanged))
                return Failure(RequestErrorKind.Validation, "A criteria event's JSON business payload must fit within 8192 bytes.");
            RaiseEvent(unchanged);
            return Result.Success;
        }
        var corrected = new CriterionSummaryCorrected(intent, executionGeneration, actor, admission, claim.Metadata.EventId);
        if (!CriteriaContentBounds.Payload(corrected))
            return Failure(RequestErrorKind.Validation, "A criteria event's JSON business payload must fit within 8192 bytes.");
        RaiseEvent(corrected);
        return Result.Success;
    }

    /// <summary>Refuses one claimed summary operation at an exact, different target revision.</summary>
    public Result RefuseSummaryRevision(CriterionSummaryIntent intent, long executionGeneration, ActorAttribution actor,
        AccessAdmissionEvidence admission, CriterionEntryOperationClaimed claim)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (!CriteriaContentBounds.SummaryIntent(intent) || !CriteriaContentBounds.AdmissionFields(actor, admission) ||
            !AccessAdmissionEvidenceBinding.IsCurrentFormat(admission) || !Selected(intent.OperationId) || executionGeneration <= 0 || !ValidAdmission(intent, executionGeneration, actor, admission) ||
            !ValidSummaryClaim(intent, executionGeneration, actor, admission, claim))
            return Failure(RequestErrorKind.Validation, "The exact admitted summary claim is required.");
        if (_selectedSummaryRefused is { } prior)
            return prior.Intent == intent && prior.ExecutionGeneration == executionGeneration && prior.Actor == actor &&
                   prior.OperationClaimEventId == claim.Metadata.EventId ? Result.Success :
                Failure(RequestErrorKind.Conflict, "The operation already has different intent.");
        if (HasSelectedSummary || _selectedProposal is not null || _selectedFence is not null ||
            executionGeneration <= _fencedExecutionGeneration || intent.ExpectedRevision == _revision)
            return Failure(RequestErrorKind.Conflict, "The target revision does not authorize a refusal.");
        var refused = new CriterionSummaryRefused(intent, executionGeneration, actor, admission, claim.Metadata.EventId, _revision);
        if (!CriteriaContentBounds.Payload(refused))
            return Failure(RequestErrorKind.Validation, "A criteria event's JSON business payload must fit within 8192 bytes.");
        RaiseEvent(refused);
        return Result.Success;
    }

    /// <summary>Durably fences an unresolved same-position summary correction attempt.</summary>
    public Result FenceSummary(CriterionSummaryIntent intent, long executionGeneration, ActorAttribution actor,
        AccessAdmissionEvidence admission)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (!CriteriaContentBounds.SummaryIntent(intent) || !CriteriaContentBounds.AdmissionFields(actor, admission))
            return Failure(RequestErrorKind.Validation,
                "The entry fields and admission evidence must fit the criteria UTF-8 and 8192-byte JSON payload limits.");
        if (!Selected(intent.OperationId) || executionGeneration <= 0 ||
            !AccessAdmissionEvidenceBinding.IsCurrentFormat(admission) || !ValidAdmission(intent, executionGeneration, actor, admission))
            return Failure(RequestErrorKind.Validation, "The selected recovering summary and trusted admission are required.");
        if (_selectedProposal is not null || _selectedFence is not null || HasSelectedSummary || _pending is not null)
            return Failure(RequestErrorKind.Conflict, "An existing operation must be reconciled first.");
        if (_selectedSummaryFence is { } prior)
        {
            if (prior.Intent != intent || prior.Actor != actor)
                return Failure(RequestErrorKind.Conflict, "The fenced identity is bound to another intent.");
            if (executionGeneration == prior.ExecutionGeneration)
                return Result.Success;
        }
        if (executionGeneration <= _fencedExecutionGeneration)
            return Failure(RequestErrorKind.Conflict, "The execution generation was already fenced.");
        var fenced = new CriterionSummaryAttemptFenced(intent, executionGeneration, actor, admission);
        if (!CriteriaContentBounds.Payload(fenced))
            return Failure(RequestErrorKind.Validation, "A criteria event's JSON business payload must fit within 8192 bytes.");
        RaiseEvent(fenced);
        return Result.Success;
    }

    void Apply(CriterionChangeProposed ev)
    {
        if (ev.Intent is null || ev.OperationClaimEventId == Uuid.Empty ||
            !ValidAdmission(ev.Intent, ev.ExecutionGeneration, ev.Actor, ev.Admission) ||
            !CriteriaContentBounds.Payload(ev) || ev.ExecutionGeneration <= 0 ||
            ev.ExecutionGeneration <= _fencedExecutionGeneration || _pending is not null ||
            !ValidIntent(ev.Intent, ev.ParentAcceptedEventId) ||
            ev.IncarnationId != (ev.Intent.Action == "remove" ? _currentIncarnationId :
                IncarnationOf(_editionId, _identifier, ev.Intent.OperationId)) ||
            (_activePin is not null && (ev.Intent.Action is "replace" or "remove")))
            throw new InvalidOperationException("An entry proposal must preserve its exact owner, operation, revision and witness boundary.");
        BindDraft(ev.Intent.DraftGeneration);
        _pending = new PendingProposal(ev.Intent, ev.ExecutionGeneration, ev.IncarnationId, ev.ParentAcceptedEventId, ev);
        if (ev.Intent.OperationId == _selectedOperationId)
        {
            if (_selectedProposal is not null || HasSelectedSummary || _selectedSummaryFence is not null ||
                (_selectedFence is not null && (_selectedFence.Intent != ev.Intent ||
                 _selectedFence.ExecutionGeneration >= ev.ExecutionGeneration)))
                throw new InvalidOperationException("A selected operation cannot change intent or bypass its execution fence.");
            _selectedProposal = _pending;
        }
    }

    void Apply(CriterionSeedProposed ev)
    {
        if (ev.Intent is null || ev.OperationClaimEventId == Uuid.Empty || !ValidSeed(ev.Intent, ev.ExecutionGeneration, ev.ParentAcceptedEventId,
                ev.EditionRequestEventId, ev.SeedVersion, ev.SeedFingerprint) ||
            !CriteriaContentBounds.Payload(ev) || ev.ExecutionGeneration <= _fencedExecutionGeneration ||
            _pending is not null || !ValidIntent(ev.Intent, ev.ParentAcceptedEventId) ||
            ev.IncarnationId != IncarnationOf(_editionId, _identifier, ev.Intent.OperationId))
            throw new InvalidOperationException("A seed proposal must match its fixed release and exact entry owner.");
        BindDraft(ev.Intent.DraftGeneration);
        _pending = new PendingProposal(ev.Intent, ev.ExecutionGeneration, ev.IncarnationId, ev.ParentAcceptedEventId, ev);
        if (ev.Intent.OperationId == _selectedOperationId)
        {
            if (_selectedProposal is not null || _selectedFence is not null || HasSelectedSummary ||
                _selectedSummaryFence is not null)
                throw new InvalidOperationException("A selected seed operation cannot replace another intent.");
            _selectedProposal = _pending;
        }
    }

    void Apply(CriterionChanged ev)
    {
        if (!Owns(ev.EditionId, ev.Identifier) || !DecisionShape(ev.Evidence, true, null) || !CriteriaContentBounds.Payload(ev))
            throw new InvalidOperationException("A criterion change must match its entry and JSON business payload limit.");
        var proposal = RequirePending(ev.EditionId, ev.Identifier, ev.OperationId, ev.ExecutionGeneration, ev.ProposalEventId);
        var intent = proposal.Intent;
        if (intent.Action == "remove")
        {
            _currentIncarnationId = Uuid.Empty;
            _currentAcceptedEventId = Uuid.Empty;
            _kind = _category = _parentIdentifier = _summary = null;
            _parentIncarnationId = _parentAcceptedEventId = null;
        }
        else
        {
            _currentIncarnationId = proposal.IncarnationId;
            _currentAcceptedEventId = ev.Metadata.EventId;
            _kind = intent.Kind;
            _category = intent.Category;
            _parentIdentifier = intent.ParentIdentifier;
            _parentIncarnationId = intent.ParentIncarnationId;
            _parentAcceptedEventId = proposal.ParentAcceptedEventId;
            _summary = intent.Summary;
        }
        _revision = checked(_revision + 1);
        _pending = null;
        if (ev.OperationId == _selectedOperationId)
            _selectedOutcome = ev;
    }

    void Apply(CriterionChangeRefused ev)
    {
        _ = RequirePending(ev.EditionId, ev.Identifier, ev.OperationId, ev.ExecutionGeneration, ev.ProposalEventId);
        if (!CriteriaContentBounds.Text(ev.Reason, CriteriaContentBounds.SummaryBytes) ||
            !DecisionShape(ev.Evidence, false, ev.Reason) || !CriteriaContentBounds.Payload(ev))
            throw new InvalidOperationException("A refusal must explain the exact proposal's terminal result.");
        _pending = null;
        if (ev.OperationId == _selectedOperationId)
            _selectedOutcome = ev;
    }

    void Apply(CriterionOperationAttemptFenced ev)
    {
        if (ev.Intent is null || !ValidAdmission(ev.Intent, ev.ExecutionGeneration, ev.Actor, ev.Admission) ||
            !CriteriaContentBounds.Payload(ev) || ev.ExecutionGeneration <= 0 ||
            ev.ExecutionGeneration <= _fencedExecutionGeneration || _pending is not null)
            throw new InvalidOperationException("An operation fence must bind a new execution generation with no accepted proposal.");
        BindDraft(ev.Intent.DraftGeneration);
        _fencedExecutionGeneration = ev.ExecutionGeneration;
        if (ev.Intent.OperationId == _selectedOperationId)
        {
            if (_selectedProposal is not null || HasSelectedSummary || _selectedSummaryFence is not null ||
                (_selectedFence is not null && (_selectedFence.Intent != ev.Intent || _selectedFence.Actor != ev.Actor)))
                throw new InvalidOperationException("A selected operation fence cannot change its intent.");
            _selectedFence = ev;
        }
    }

    void Apply(CriterionWitnessPinned ev)
    {
        if (!Owns(ev.EditionId, ev.Identifier) || !CriteriaContentBounds.Payload(ev) ||
            ev.DraftGeneration == Uuid.Empty || ev.PublicationAttemptId == Uuid.Empty ||
            ev.AttemptOrdinal <= _fencedAttemptOrdinal || ev.IncarnationId == Uuid.Empty ||
            ev.PublicationStartedEventId == Uuid.Empty || _draftGeneration != ev.DraftGeneration ||
            _activePin is not null || _pending is not null || !Exists || _kind != "criterion" ||
            _currentIncarnationId != ev.IncarnationId)
            throw new InvalidOperationException("A witness pin must protect one exact accepted criterion and live attempt.");
        _activePin = ev;
        if (ev.PublicationAttemptId == _selectedPublicationAttemptId)
        {
            if (_selectedPin is not null || _selectedPinFence is not null)
                throw new InvalidOperationException("A fenced or selected witness attempt cannot be pinned again.");
            _selectedPin = ev;
        }
    }

    void Apply(CriterionWitnessPinFenced ev)
    {
        if (!Owns(ev.EditionId, ev.Identifier) || !CriteriaContentBounds.Payload(ev) ||
            ev.DraftGeneration == Uuid.Empty || ev.PublicationAttemptId == Uuid.Empty ||
            ev.AttemptOrdinal <= 0 || ev.IncarnationId == Uuid.Empty || ev.PublicationAbortedEventId == Uuid.Empty ||
            (_draftGeneration != Uuid.Empty && _draftGeneration != ev.DraftGeneration))
            throw new InvalidOperationException("A witness fence must name its exact committed edition abort.");
        if (_activePin is { } active && active.AttemptOrdinal == ev.AttemptOrdinal &&
            (active.PublicationAttemptId != ev.PublicationAttemptId || active.IncarnationId != ev.IncarnationId))
            throw new InvalidOperationException("A witness fence cannot clear another attempt.");
        if (_activePin is { } earlier && earlier.AttemptOrdinal < ev.AttemptOrdinal)
            throw new InvalidOperationException("A prior witness pin must be reconciled first.");
        BindDraft(ev.DraftGeneration);
        if (_activePin is { } pin && pin.PublicationAttemptId == ev.PublicationAttemptId &&
            pin.AttemptOrdinal == ev.AttemptOrdinal)
            _activePin = null;
        _fencedAttemptOrdinal = Math.Max(_fencedAttemptOrdinal, ev.AttemptOrdinal);
        if (ev.PublicationAttemptId == _selectedPublicationAttemptId)
        {
            if (_selectedPinFence is not null)
                throw new InvalidOperationException("A witness attempt cannot have two target fences.");
            _selectedPinFence = ev;
        }
    }

    void Apply(CriterionWitnessRefused ev)
    {
        if (!Owns(ev.EditionId, ev.Identifier) || !CriteriaContentBounds.Payload(ev) ||
            ev.DraftGeneration == Uuid.Empty || ev.PublicationAttemptId == Uuid.Empty || ev.IncarnationId == Uuid.Empty ||
            ev.PublicationStartedEventId == Uuid.Empty || ev.AttemptOrdinal <= _fencedAttemptOrdinal ||
            _pending is not null || _activePin is not null ||
            _draftGeneration != Uuid.Empty && _draftGeneration != ev.DraftGeneration ||
            Exists && _kind == "criterion" && _currentIncarnationId == ev.IncarnationId && _draftGeneration == ev.DraftGeneration)
            throw new InvalidOperationException("A witness refusal requires an exact unavailable witness without a pending change.");
        if (ev.PublicationAttemptId == _selectedPublicationAttemptId)
        {
            if (_selectedPin is not null || _selectedPinFence is not null || _selectedWitnessRefusal is not null)
                throw new InvalidOperationException("The selected attempt already has a witness outcome.");
            _selectedWitnessRefusal = ev;
        }
        BindDraft(ev.DraftGeneration);
        _fencedAttemptOrdinal = ev.AttemptOrdinal;
    }

    void Apply(CriterionSummaryCorrected ev)
    {
        if (ev.Intent is null || ev.OperationClaimEventId == Uuid.Empty ||
            !ValidAdmission(ev.Intent, ev.ExecutionGeneration, ev.Actor, ev.Admission) ||
            !CriteriaContentBounds.Payload(ev) ||
            ev.ExecutionGeneration <= 0 || ev.ExecutionGeneration <= _fencedExecutionGeneration || !Exists ||
            _pending is not null || ev.Intent.ExpectedRevision != _revision || string.IsNullOrWhiteSpace(ev.Intent.Summary) ||
            string.Equals(ev.Intent.Summary, _summary, StringComparison.Ordinal))
            throw new InvalidOperationException("A summary correction must revise an existing entry's exact revision.");
        _summary = ev.Intent.Summary;
        _revision = checked(_revision + 1);
        if (ev.Intent.OperationId == _selectedOperationId)
        {
            if (HasSelectedSummary || _selectedProposal is not null || _selectedFence is not null ||
                (_selectedSummaryFence is not null && (_selectedSummaryFence.Intent != ev.Intent ||
                 _selectedSummaryFence.Actor != ev.Actor || _selectedSummaryFence.ExecutionGeneration >= ev.ExecutionGeneration)))
                throw new InvalidOperationException("An operation identity cannot name multiple intents.");
            _selectedSummary = ev;
        }
    }

    void Apply(CriterionSummaryUnchanged ev)
    {
        if (ev.Intent is null || ev.OperationClaimEventId == Uuid.Empty ||
            !ValidAdmission(ev.Intent, ev.ExecutionGeneration, ev.Actor, ev.Admission) ||
            !CriteriaContentBounds.Payload(ev) || ev.ExecutionGeneration <= 0 ||
            ev.ExecutionGeneration <= _fencedExecutionGeneration || !Exists || _pending is not null ||
            ev.Intent.ExpectedRevision != _revision || !string.Equals(ev.Intent.Summary, _summary, StringComparison.Ordinal))
            throw new InvalidOperationException("An unchanged summary must bind the exact current entry revision and text.");
        if (ev.Intent.OperationId == _selectedOperationId)
        {
            if (HasSelectedSummary || _selectedProposal is not null || _selectedFence is not null ||
                (_selectedSummaryFence is not null && (_selectedSummaryFence.Intent != ev.Intent ||
                 _selectedSummaryFence.Actor != ev.Actor || _selectedSummaryFence.ExecutionGeneration >= ev.ExecutionGeneration)))
                throw new InvalidOperationException("An operation identity cannot name multiple intents.");
            _selectedSummaryUnchanged = ev;
        }
    }

    void Apply(CriterionSummaryRefused ev)
    {
        if (ev.Intent is null || ev.OperationClaimEventId == Uuid.Empty ||
            !ValidAdmission(ev.Intent, ev.ExecutionGeneration, ev.Actor, ev.Admission) ||
            !CriteriaContentBounds.Payload(ev) || ev.ExecutionGeneration <= 0 ||
            ev.ExecutionGeneration <= _fencedExecutionGeneration || ev.ObservedRevision != _revision ||
            ev.Intent.ExpectedRevision == _revision)
            throw new InvalidOperationException("A summary refusal must bind a different current entry revision.");
        if (ev.Intent.OperationId == _selectedOperationId)
        {
            if (HasSelectedSummary || _selectedProposal is not null || _selectedFence is not null ||
                (_selectedSummaryFence is not null && (_selectedSummaryFence.Intent != ev.Intent ||
                 _selectedSummaryFence.Actor != ev.Actor || _selectedSummaryFence.ExecutionGeneration >= ev.ExecutionGeneration)))
                throw new InvalidOperationException("An operation identity cannot name multiple intents.");
            _selectedSummaryRefused = ev;
        }
    }

    void Apply(CriterionSummaryAttemptFenced ev)
    {
        if (ev.Intent is null || !ValidAdmission(ev.Intent, ev.ExecutionGeneration, ev.Actor, ev.Admission) ||
            !CriteriaContentBounds.Payload(ev) ||
            ev.ExecutionGeneration <= 0 || ev.ExecutionGeneration <= _fencedExecutionGeneration || _pending is not null)
            throw new InvalidOperationException("A summary fence must bind a new execution with no pending change.");
        _fencedExecutionGeneration = ev.ExecutionGeneration;
        if (ev.Intent.OperationId == _selectedOperationId)
        {
            if (_selectedProposal is not null || _selectedFence is not null || HasSelectedSummary ||
                (_selectedSummaryFence is not null && (_selectedSummaryFence.Intent != ev.Intent || _selectedSummaryFence.Actor != ev.Actor)))
                throw new InvalidOperationException("A selected summary fence cannot change its intent.");
            _selectedSummaryFence = ev;
        }
    }

    PendingProposal RequirePending(Uuid editionId, string identifier, Uuid operationId,
        long executionGeneration, Uuid proposalEventId)
    {
        if (!Owns(editionId, identifier) || _pending is null || _pending.Intent.OperationId != operationId ||
            _pending.ExecutionGeneration != executionGeneration || _pending.Source.Metadata.EventId != proposalEventId)
            throw new InvalidOperationException("An outcome must answer its exact pending entry proposal.");
        return _pending;
    }

    bool ValidClaim(CriterionChangeIntent intent, long generation, ActorAttribution actor,
        AccessAdmissionEvidence admission, CriterionEntryOperationClaimed? claim)
    {
        if (claim is null || claim.Structural != intent || claim.Summary is not null || claim.Actor != actor ||
            claim.ExecutionGeneration != generation || claim.Admission is null || !AccessAdmissionEvidenceBinding.IsCurrentFormat(claim.Admission) || claim.Admission.UserId != admission.UserId ||
            !CriteriaContentBounds.AdmissionFields(claim.Actor, claim.Admission) ||
            !AccessAdmissionEvidenceBinding.BindsCriteriaOperator(claim.Admission, admission.UserId, actor,
                intent.OperationId, Stream.ToString(), CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), generation)))
            return false;
        try
        {
            return !claim.Metadata.IsAudit && claim.Metadata.EventId != Uuid.Empty && claim.Metadata.AggregateVersion == 1 &&
                claim.Metadata.AggregateId == CriteriaEntryOperation.IdentityOf(_editionId, _identifier, intent.OperationId);
        }
        catch (InvalidOperationException)
        {
            // A constructed but unattached object is not a committed-source claim.
            return false;
        }
    }

    bool ValidSummaryClaim(CriterionSummaryIntent intent, long generation, ActorAttribution actor,
        AccessAdmissionEvidence admission, CriterionEntryOperationClaimed? claim)
    {
        if (claim is null || claim.Summary != intent || claim.Structural is not null || claim.Actor != actor ||
            claim.ExecutionGeneration != generation || claim.Admission is null || !AccessAdmissionEvidenceBinding.IsCurrentFormat(claim.Admission) || claim.Admission.UserId != admission.UserId ||
            !CriteriaContentBounds.AdmissionFields(claim.Actor, claim.Admission) ||
            !AccessAdmissionEvidenceBinding.BindsCriteriaOperator(claim.Admission, admission.UserId, actor,
                intent.OperationId, Stream.ToString(), CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), generation)))
            return false;
        try
        {
            return !claim.Metadata.IsAudit && claim.Metadata.EventId != Uuid.Empty && claim.Metadata.AggregateVersion == 1 &&
                claim.Metadata.AggregateId == CriteriaEntryOperation.IdentityOf(_editionId, _identifier, intent.OperationId);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    bool ValidSeed(CriterionChangeIntent intent, long executionGeneration, Uuid? parentAcceptedEventId,
        Uuid editionRequestEventId, string? version, string? fingerprint)
    {
        if (!Owns(intent.EditionId, intent.Identifier) || executionGeneration <= 0 || editionRequestEventId == Uuid.Empty ||
            !PlatformCriteriaSeedDefinition.MatchesBinding(_editionId, version, fingerprint) ||
            intent.DraftGeneration != PlatformCriteriaSeedDefinition.OperationOf(_editionId, "edition") ||
            intent.OperationId != PlatformCriteriaSeedDefinition.OperationOf(_editionId, "entry:" + _identifier) ||
            intent.ExpectedRevision != 0 || intent.Action != "create" ||
            intent.Kind is not ("criterion" or "point_of_focus") || intent.Category is null || intent.Summary is null)
            return false;
        var entry = new Criterion(_editionId, _identifier, intent.Kind == "criterion" ? _identifier : null,
            intent.Category, intent.Kind, intent.ParentIdentifier, intent.Summary);
        if (!PlatformCriteriaSeedDefinition.MatchesEntry(_editionId, version, fingerprint, entry))
            return false;
        if (intent.Kind == "criterion")
            return intent.ParentIdentifier is null && intent.ParentIncarnationId is null && parentAcceptedEventId is null;
        return intent.ParentIdentifier is { Length: > 0 } parent &&
            intent.ParentIncarnationId == IncarnationOf(_editionId, parent,
                PlatformCriteriaSeedDefinition.OperationOf(_editionId, "entry:" + parent)) &&
            parentAcceptedEventId is { } accepted && accepted != Uuid.Empty;
    }

    sealed record PendingProposal(CriterionChangeIntent Intent, long ExecutionGeneration, Uuid IncarnationId,
        Uuid? ParentAcceptedEventId, DomainEvent Source);

    bool ValidIntent(CriterionChangeIntent intent, Uuid? parentAcceptedEventId)
    {
        if (!Owns(intent.EditionId, intent.Identifier) || intent.DraftGeneration == Uuid.Empty ||
            (_draftGeneration != Uuid.Empty && _draftGeneration != intent.DraftGeneration) ||
            intent.OperationId == Uuid.Empty || intent.ExpectedRevision != _revision)
            return false;
        if (intent.Action == "remove")
            return Exists && intent.Kind is null && intent.Category is null && intent.ParentIdentifier is null &&
                   intent.ParentIncarnationId is null && intent.Summary is null && parentAcceptedEventId is null;
        if (intent.Action is not ("create" or "replace") ||
            (intent.Action == "create" ? Exists : !Exists) || !CriteriaContentBounds.Summary(intent.Summary) ||
            intent.Category is not ("security" or "availability" or "confidentiality" or "processing_integrity" or "privacy"))
            return false;
        return intent.Kind switch
        {
            "criterion" => intent.ParentIdentifier is null && intent.ParentIncarnationId is null && parentAcceptedEventId is null,
            "point_of_focus" => !string.IsNullOrWhiteSpace(intent.ParentIdentifier) &&
                                !string.Equals(intent.ParentIdentifier, _identifier, StringComparison.Ordinal) &&
                                intent.ParentIncarnationId is { } parent && parent != Uuid.Empty &&
                                parentAcceptedEventId is { } source && source != Uuid.Empty,
            _ => false
        };
    }

    bool ValidAdmission(CriterionChangeIntent intent, long executionGeneration, ActorAttribution actor, AccessAdmissionEvidence admission) =>
        actor is not null && admission is not null && CriteriaContentBounds.StructuralIntent(intent) &&
        CriteriaContentBounds.AdmissionFields(actor, admission) && Owns(intent.EditionId, intent.Identifier) && intent.DraftGeneration != Uuid.Empty &&
        AccessAdmissionEvidenceBinding.BindsCriteriaOperator(admission, admission.UserId, actor, intent.OperationId,
            Stream.ToString(), CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), executionGeneration));

    bool ValidAdmission(CriterionSummaryIntent intent, long executionGeneration, ActorAttribution actor, AccessAdmissionEvidence admission) =>
        actor is not null && admission is not null && CriteriaContentBounds.SummaryIntent(intent) &&
        CriteriaContentBounds.AdmissionFields(actor, admission) && Owns(intent.EditionId, intent.Identifier) &&
        AccessAdmissionEvidenceBinding.BindsCriteriaOperator(admission, admission.UserId, actor, intent.OperationId,
            Stream.ToString(), CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), executionGeneration));

    bool Owns(Uuid editionId, string identifier) => _editionId != Uuid.Empty && editionId == _editionId &&
        !string.IsNullOrWhiteSpace(identifier) && string.Equals(identifier, _identifier, StringComparison.Ordinal);
    bool Selected(Uuid operationId) => operationId != Uuid.Empty && _selectedOperationId == operationId;
    bool HasSelectedSummary => _selectedSummary is not null || _selectedSummaryUnchanged is not null || _selectedSummaryRefused is not null;
    bool SelectedAttempt(Uuid attemptId) => attemptId != Uuid.Empty && _selectedPublicationAttemptId == attemptId;

    bool ValidStarted(CriteriaPublicationStarted ev) => Owns(ev.EditionId, ev.WitnessIdentifier) &&
        ev.DraftGeneration != Uuid.Empty && ev.PublicationAttemptId != Uuid.Empty && ev.AttemptOrdinal > 0 &&
        ev.WitnessIncarnationId != Uuid.Empty && ev.Metadata.EventId != Uuid.Empty;
    bool ValidAborted(CriteriaPublicationAborted ev) => Owns(ev.EditionId, ev.WitnessIdentifier) &&
        ev.DraftGeneration != Uuid.Empty && ev.PublicationAttemptId != Uuid.Empty && ev.AttemptOrdinal > 0 &&
        ev.WitnessIncarnationId != Uuid.Empty && ev.Metadata.EventId != Uuid.Empty;

    static bool Matches(CriterionWitnessPinned pin, CriteriaPublicationStarted started) =>
        pin.EditionId == started.EditionId && pin.DraftGeneration == started.DraftGeneration &&
        string.Equals(pin.Identifier, started.WitnessIdentifier, StringComparison.Ordinal) &&
        pin.PublicationAttemptId == started.PublicationAttemptId && pin.AttemptOrdinal == started.AttemptOrdinal &&
        pin.IncarnationId == started.WitnessIncarnationId && pin.PublicationStartedEventId == started.Metadata.EventId;

    static bool Matches(CriterionWitnessRefused refusal, CriteriaPublicationStarted started) =>
        refusal.EditionId == started.EditionId && refusal.DraftGeneration == started.DraftGeneration &&
        refusal.Identifier == started.WitnessIdentifier && refusal.PublicationAttemptId == started.PublicationAttemptId &&
        refusal.AttemptOrdinal == started.AttemptOrdinal && refusal.IncarnationId == started.WitnessIncarnationId &&
        refusal.PublicationStartedEventId == started.Metadata.EventId;

    static bool Matches(CriterionWitnessPinFenced fence, CriteriaPublicationAborted aborted) =>
        fence.EditionId == aborted.EditionId && fence.DraftGeneration == aborted.DraftGeneration &&
        string.Equals(fence.Identifier, aborted.WitnessIdentifier, StringComparison.Ordinal) &&
        fence.PublicationAttemptId == aborted.PublicationAttemptId && fence.AttemptOrdinal == aborted.AttemptOrdinal &&
        fence.IncarnationId == aborted.WitnessIncarnationId && fence.PublicationAbortedEventId == aborted.Metadata.EventId;

    static bool DecisionShape(CriteriaEntryDecisionEvidence? evidence, bool accepted, string? reason) =>
        evidence is not null && evidence.EditionEventId != Uuid.Empty &&
        evidence.EditionAreaOffset != evidence.ProposalAreaOffset && (evidence.Basis switch
        {
            "draft" => accepted && reason is null,
            "publication" => accepted && reason is null && evidence.ProposalAreaOffset < evidence.EditionAreaOffset ||
                !accepted && reason == "edition_published" && evidence.ProposalAreaOffset > evidence.EditionAreaOffset,
            "abandonment" => !accepted && reason == "edition_abandoned",
            _ => false,
        });

    static bool SameOutcome(DomainEvent prior, Uuid operationId, long generation, Uuid proposalId, bool accepted,
        string? reason, CriteriaEntryDecisionEvidence evidence) =>
        prior switch
        {
            CriterionChanged changed => accepted && changed.OperationId == operationId &&
                changed.ExecutionGeneration == generation && changed.ProposalEventId == proposalId && changed.Evidence == evidence,
            CriterionChangeRefused refused => !accepted && refused.OperationId == operationId &&
                refused.ExecutionGeneration == generation && refused.ProposalEventId == proposalId &&
                string.Equals(refused.Reason, reason, StringComparison.Ordinal) && refused.Evidence == evidence,
            _ => false
        };

    void BindDraft(Uuid draftGeneration)
    {
        if (_draftGeneration != Uuid.Empty && _draftGeneration != draftGeneration)
            throw new InvalidOperationException("An entry cannot change its edition draft generation.");
        _draftGeneration = draftGeneration;
    }

    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}
