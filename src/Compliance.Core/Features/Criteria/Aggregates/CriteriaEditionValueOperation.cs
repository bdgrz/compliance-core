using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Aggregates;

/// <summary>One immutable operation identity for one field or disclosure target.</summary>
public sealed class CriteriaEditionValueOperation : Aggregate
{
    readonly Uuid _editionId;
    readonly Uuid _targetId;
    readonly Uuid _operationId;
    CriteriaEditionValueOperationClaimed? _claim;

    /// <summary>Opens a target-scoped operation, without an edition-wide operation list.</summary>
    public CriteriaEditionValueOperation(Uuid editionId, string kind, string? field, string? category,
        string? code, Uuid operationId)
        : base(IdentityOf(editionId, kind, field, category, code, operationId),
            AddressOf(editionId, kind, field, category, code, operationId))
    {
        _editionId = editionId;
        _targetId = CriteriaEditionValue.IdentityOf(editionId, kind, field, category, code);
        _operationId = operationId;
        On<CriteriaEditionValueOperationClaimed>(Apply);
    }

    /// <summary>Returns the stable target-scoped operation identity.</summary>
    public static Uuid IdentityOf(Uuid editionId, string kind, string? field, string? category, string? code,
        Uuid operationId)
    {
        if (operationId == Uuid.Empty)
            throw new ArgumentException("An operation identity is required.", nameof(operationId));
        return AggregateIdentity.Create("CriteriaEditionValueOperation",
            CriteriaEditionValue.IdentityOf(editionId, kind, field, category, code), operationId);
    }

    /// <summary>Places the operation in its edition's ordered area.</summary>
    public static EventStreamAddress AddressOf(Uuid editionId, string kind, string? field, string? category,
        string? code, Uuid operationId) => new("bdgrz", $"criteria-{editionId}",
            $"edition-value-operation-{IdentityOf(editionId, kind, field, category, code, operationId)}");

    /// <summary>The claim already accepted on this owner.</summary>
    public CriteriaEditionValueOperationClaimed? ClaimFact => _claim;

    /// <summary>Claims one normalized intent under trusted operator admission.</summary>
    public Result Claim(CriteriaEditionValueIntent intent, long executionGeneration, ActorAttribution actor,
        AccessAdmissionEvidence admission)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (!AccessAdmissionEvidenceBinding.IsCurrentFormat(admission) || !Valid(intent, executionGeneration, actor, admission))
            return Failure(RequestErrorKind.Validation, "An exact target, bounded intent and trusted admission are required.");
        if (_claim is { } previous)
            return previous.Intent == intent && previous.Actor == actor && previous.Admission.UserId == admission.UserId
                ? Result.Success : Failure(RequestErrorKind.Conflict, "The operation identity already has different intent or actor.");
        var fact = new CriteriaEditionValueOperationClaimed(intent, executionGeneration, actor, admission);
        if (!CriteriaContentBounds.Payload(fact))
            return Failure(RequestErrorKind.Validation, "The event exceeds the criteria business payload limit.");
        RaiseEvent(fact);
        return Result.Success;
    }

    void Apply(CriteriaEditionValueOperationClaimed fact)
    {
        if (_claim is not null || !Valid(fact.Intent, fact.ExecutionGeneration, fact.Actor, fact.Admission) ||
            !CriteriaContentBounds.Payload(fact))
            throw new InvalidOperationException("A value operation can be claimed only once by its exact intent.");
        _claim = fact;
    }

    bool Valid(CriteriaEditionValueIntent intent, long generation, ActorAttribution actor, AccessAdmissionEvidence admission) =>
        CriteriaEditionValueRules.Shape(intent) && intent.EditionId == _editionId && intent.OperationId == _operationId &&
        CriteriaEditionValue.IdentityOf(intent.EditionId, intent.TargetKind, intent.Field, intent.Category, intent.Code) == _targetId &&
        CriteriaEditionValueRules.Admission(intent, generation, actor, admission);

    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}
