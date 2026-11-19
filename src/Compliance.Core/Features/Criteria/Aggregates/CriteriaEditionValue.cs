using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Aggregates;

/// <summary>One metadata field or one independently addressed disclosure note.</summary>
/// <remarks>
/// The process must source-verify the exact request and operation claim, current
/// draft and source positions before proposing. It must source-verify the
/// publication or abandonment ordering before deciding. IDs, actor fields and
/// this aggregate's local state do not establish any cross-stream authority.
/// </remarks>
public sealed class CriteriaEditionValue : Aggregate
{
    readonly Uuid _editionId;
    readonly Uuid _selectedOperationId;
    Uuid _draftGeneration;
    CriteriaEditionValueProposed? _pending;
    CriteriaEditionValueProposed? _selectedProposal;
    DomainEvent? _selectedOutcome;
    long _revision;
    string? _textValue;
    bool? _booleanValue;

    /// <summary>Opens exactly one field or one category/code disclosure.</summary>
    public CriteriaEditionValue(Uuid editionId, string kind, string? field, string? category, string? code,
        Uuid selectedOperationId = default)
        : base(IdentityOf(editionId, kind, field, category, code), AddressOf(editionId, kind, field, category, code))
    {
        _editionId = editionId;
        _selectedOperationId = selectedOperationId;
        On<CriteriaEditionValueProposed>(Apply);
        On<CriteriaEditionValueAccepted>(Apply);
        On<CriteriaEditionValueRefused>(Apply);
    }

    /// <summary>Derives an independent stable owner for exactly one target.</summary>
    public static Uuid IdentityOf(Uuid editionId, string kind, string? field, string? category, string? code)
    {
        if (editionId == Uuid.Empty || !CriteriaEditionValueRules.Target(kind, field, category, code))
            throw new ArgumentException("A valid edition and bounded value target are required.");
        return AggregateIdentity.Create("CriteriaEditionValue", editionId, kind,
            field ?? string.Empty, category ?? string.Empty, code ?? string.Empty);
    }

    /// <summary>Places one target in the edition's ordered source area.</summary>
    public static EventStreamAddress AddressOf(Uuid editionId, string kind, string? field, string? category, string? code) =>
        new("bdgrz", $"criteria-{editionId}", $"edition-value-{IdentityOf(editionId, kind, field, category, code)}");

    /// <summary>Accepted target revision, beginning at zero.</summary>
    public long Revision => _revision;
    /// <summary>Whether an exact source proposal remains unsettled.</summary>
    public bool HasPendingChange => _pending is not null;
    /// <summary>Current accepted text, if the target is a text field or disclosure.</summary>
    public string? TextValue => _textValue;
    /// <summary>Current accepted boolean, if the target is the numbered-criteria declaration.</summary>
    public bool? BooleanValue => _booleanValue;

    /// <summary>Returns a selected durable receipt without retaining earlier operation history.</summary>
    public CriteriaOperationReceipt? Receipt(Uuid operationId)
    {
        if (operationId == Uuid.Empty || operationId != _selectedOperationId)
            return null;
        return _selectedOutcome switch
        {
            CriteriaEditionValueAccepted => new CriteriaOperationReceipt(_editionId, operationId, "accepted"),
            CriteriaEditionValueRefused refused => new CriteriaOperationReceipt(_editionId, operationId, "refused", refused.Reason),
            _ when _selectedProposal is not null => new CriteriaOperationReceipt(_editionId, operationId, "pending"),
            _ => null,
        };
    }

    /// <summary>Proposes a value after trusted, exact source verification by the process.</summary>
    public Result Propose(CriteriaEditionValueIntent intent, long executionGeneration, ActorAttribution actor,
        AccessAdmissionEvidence admission, CriteriaEditionDraftRequested request, CriteriaEditionValueOperationClaimed claim)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(claim);
        if (!AccessAdmissionEvidenceBinding.IsCurrentFormat(admission) || !AccessAdmissionEvidenceBinding.IsCurrentFormat(claim.Admission) ||
            !ValidProposal(intent, executionGeneration, actor, admission, request, claim) ||
            intent.OperationId != _selectedOperationId)
            return Failure(RequestErrorKind.Validation, "The selected intent, exact source facts and operator admission are required.");
        if (_selectedProposal is { } previous)
            return previous.Intent == intent && previous.ExecutionGeneration == executionGeneration && previous.Actor == actor &&
                previous.EditionRequestEventId == request.Metadata.EventId && previous.OperationClaimEventId == claim.Metadata.EventId
                ? Result.Success : Failure(RequestErrorKind.Conflict, "The operation already has different intent or source binding.");
        if (_pending is not null || intent.ExpectedRevision != _revision ||
            (_draftGeneration != Uuid.Empty && _draftGeneration != intent.DraftGeneration))
            return Failure(RequestErrorKind.Conflict, "The target revision changed or another proposal is pending.");
        var proposed = new CriteriaEditionValueProposed(intent, executionGeneration, request.Metadata.EventId,
            claim.Metadata.EventId, actor, admission);
        if (!CriteriaContentBounds.Payload(proposed))
            return Failure(RequestErrorKind.Validation, "The event exceeds the criteria business payload limit.");
        RaiseEvent(proposed);
        return Result.Success;
    }

    /// <summary>Records an outcome only after the process verifies its exact source cut and attempt.</summary>
    public Result Decide(Uuid operationId, long executionGeneration, Uuid proposalEventId, bool accepted, string? reason,
        CriteriaEditionValueDecisionEvidence evidence)
    {
        if (operationId == Uuid.Empty || executionGeneration <= 0 || proposalEventId == Uuid.Empty ||
            (accepted ? reason is not null : !CriteriaContentBounds.Text(reason, CriteriaContentBounds.SummaryBytes)) ||
            !DecisionShape(evidence, accepted, reason))
            return Failure(RequestErrorKind.Validation, "An exact proposal and outcome are required.");
        if (operationId == _selectedOperationId && _selectedOutcome is { } prior)
            return Matches(prior, operationId, executionGeneration, proposalEventId, accepted, reason, evidence)
                ? Result.Success : Failure(RequestErrorKind.Conflict, "The proposal already has a different outcome.");
        if (_pending is null || _pending.Intent.OperationId != operationId ||
            _pending.ExecutionGeneration != executionGeneration || _pending.Metadata.EventId != proposalEventId)
            return Failure(RequestErrorKind.Conflict, "The exact proposal is not pending.");
        if (!AccessAdmissionEvidenceBinding.IsCurrentFormat(_pending.Admission))
            return Failure(RequestErrorKind.Conflict, "The legacy proposal requires an explicitly qualified recovery policy.");
        DomainEvent outcome = accepted
            ? new CriteriaEditionValueAccepted(_editionId, operationId, executionGeneration, proposalEventId,
                evidence)
            : new CriteriaEditionValueRefused(_editionId, operationId, executionGeneration, proposalEventId,
                reason!, evidence);
        if (!CriteriaContentBounds.Payload(outcome))
            return Failure(RequestErrorKind.Validation, "The event exceeds the criteria business payload limit.");
        RaiseEvent(outcome);
        return Result.Success;
    }

    void Apply(CriteriaEditionValueProposed fact)
    {
        if (!MatchesTarget(fact.Intent) || !CriteriaEditionValueRules.Admission(fact.Intent, fact.ExecutionGeneration, fact.Actor, fact.Admission) ||
            fact.EditionRequestEventId == Uuid.Empty || fact.OperationClaimEventId == Uuid.Empty ||
            !CriteriaContentBounds.Payload(fact) || _pending is not null || fact.Intent.ExpectedRevision != _revision ||
            (_draftGeneration != Uuid.Empty && _draftGeneration != fact.Intent.DraftGeneration))
            throw new InvalidOperationException("A value proposal must match its exact target and draft revision.");
        _draftGeneration = fact.Intent.DraftGeneration;
        _pending = fact;
        if (fact.Intent.OperationId == _selectedOperationId)
        {
            if (_selectedProposal is not null)
                throw new InvalidOperationException("An operation cannot change its proposal.");
            _selectedProposal = fact;
        }
    }

    void Apply(CriteriaEditionValueAccepted fact)
    {
        var pending = RequirePending(fact.EditionId, fact.OperationId, fact.ExecutionGeneration, fact.ProposalEventId);
        if (!DecisionShape(fact.Evidence, true, null) || !CriteriaContentBounds.Payload(fact))
            throw new InvalidOperationException("The outcome needs its exact edition source and must fit its payload limit.");
        _textValue = pending.Intent.TextValue;
        _booleanValue = pending.Intent.BooleanValue;
        _revision = checked(_revision + 1);
        _pending = null;
        if (fact.OperationId == _selectedOperationId)
            _selectedOutcome = fact;
    }

    void Apply(CriteriaEditionValueRefused fact)
    {
        _ = RequirePending(fact.EditionId, fact.OperationId, fact.ExecutionGeneration, fact.ProposalEventId);
        if (!DecisionShape(fact.Evidence, false, fact.Reason) ||
            !CriteriaContentBounds.Text(fact.Reason, CriteriaContentBounds.SummaryBytes) || !CriteriaContentBounds.Payload(fact))
            throw new InvalidOperationException("A refused outcome needs a bounded reason.");
        _pending = null;
        if (fact.OperationId == _selectedOperationId)
            _selectedOutcome = fact;
    }

    CriteriaEditionValueProposed RequirePending(Uuid editionId, Uuid operationId, long generation, Uuid proposalId)
    {
        if (editionId != _editionId || _pending is null || _pending.Intent.OperationId != operationId ||
            _pending.ExecutionGeneration != generation || _pending.Metadata.EventId != proposalId)
            throw new InvalidOperationException("The outcome must identify the exact pending proposal.");
        return _pending;
    }

    bool MatchesTarget(CriteriaEditionValueIntent intent) => CriteriaEditionValueRules.Shape(intent) &&
        intent.EditionId == _editionId && IdentityOf(intent.EditionId, intent.TargetKind, intent.Field, intent.Category, intent.Code) == Id;

    bool ValidProposal(CriteriaEditionValueIntent intent, long generation, ActorAttribution actor,
        AccessAdmissionEvidence admission, CriteriaEditionDraftRequested request, CriteriaEditionValueOperationClaimed claim) =>
        MatchesTarget(intent) && CriteriaEditionValueRules.Admission(intent, generation, actor, admission) &&
        request.Metadata.EventId != Uuid.Empty && !request.Metadata.IsAudit && request.Metadata.AggregateId == new CriteriaEdition(_editionId).Id &&
        request.EditionId == _editionId && request.DraftGeneration == intent.DraftGeneration &&
        request.OperationId == request.DraftGeneration && request.ExecutionGeneration > 0 && request.Framework == "tsc" &&
        request.SeedVersion is null && request.SeedFingerprint is null && request.Admission is not null &&
        claim.Metadata.EventId != Uuid.Empty && !claim.Metadata.IsAudit &&
        claim.Metadata.AggregateId == CriteriaEditionValueOperation.IdentityOf(_editionId, intent.TargetKind, intent.Field,
            intent.Category, intent.Code, intent.OperationId) && claim.Intent == intent && claim.Actor == actor &&
        claim.Admission is not null && claim.Admission.UserId == admission.UserId;

    static bool DecisionShape(CriteriaEditionValueDecisionEvidence? evidence, bool accepted, string? reason) =>
        evidence is not null && evidence.EditionEventId != Uuid.Empty && evidence.EditionAreaOffset != evidence.ProposalAreaOffset &&
        (evidence.Basis switch
        {
            "draft" => accepted && reason is null,
            "publication" => accepted && reason is null && evidence.ProposalAreaOffset < evidence.EditionAreaOffset ||
                !accepted && reason == "edition_published" && evidence.ProposalAreaOffset > evidence.EditionAreaOffset,
            "abandonment" => !accepted && reason == "edition_abandoned",
            _ => false,
        });

    static bool Matches(DomainEvent prior, Uuid operationId, long generation, Uuid proposalId, bool accepted, string? reason,
        CriteriaEditionValueDecisionEvidence evidence) => prior switch
        {
            CriteriaEditionValueAccepted value => accepted && value.OperationId == operationId &&
                value.ExecutionGeneration == generation && value.ProposalEventId == proposalId && value.Evidence == evidence,
            CriteriaEditionValueRefused value => !accepted && value.OperationId == operationId &&
                value.ExecutionGeneration == generation && value.ProposalEventId == proposalId && value.Reason == reason &&
                value.Evidence == evidence,
            _ => false,
        };

    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}
