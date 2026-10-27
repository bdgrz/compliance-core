using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Aggregates;

/// <summary>Bounded scope-decision authority for one instance, independent of its immutable effective-date history.</summary>
public sealed class SystemInstanceAccessReviewScope : Aggregate
{
    public const string Included = "included";
    public const string Excluded = "excluded";
    readonly Uuid _tenantId;
    AccessReviewScopeDecided? _latest;

    public SystemInstanceAccessReviewScope(Uuid tenantId, Uuid systemInstanceId, Uuid selectedLogicalId = default)
        : base(systemInstanceId, new EventStreamAddress(tenantId.ToString(), AccessReviewScopeStreams.Area,
            systemInstanceId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<AccessReviewScopeDecided>(ApplyFact);
    }

    public NativeAcceptance Acceptance { get; }
    public long Revision => _latest?.Sequence ?? 0;
    public AccessReviewScopeDecided? Latest => _latest;

    /// <summary>The stable operation identity for the existing personal contract's original decision count.</summary>
    public static Uuid OperationId(Uuid tenant, Uuid application, Uuid instance, long expectedCount) =>
        SystemInstanceAccessReviewScopeIdentity.For(tenant, application, instance, expectedCount);

    public Result PrepareDecide(long expectedCount, DateTimeOffset effectiveFrom)
    {
        if (expectedCount != Revision)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The instance has {Revision} scope decisions; expected {expectedCount}."));
        return _latest is { } prior && effectiveFrom < prior.EffectiveFrom
            ? Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A new scope decision cannot take effect before the decision it supersedes."))
            : Result.Success;
    }

    public Result Record(OperationProvenance operation, Uuid applicationId, long instanceRevision,
        DateTimeOffset effectiveFrom, ImmutableContentRef content, ImmutableContentRef firstResponse,
        ImmutableContentRef operationResult)
    {
        if (_latest is not null && _latest.ApplicationId != applicationId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The system instance was not found."));
        if (operation.OriginalExpectedRevision is not { } expected || expected > (ulong)long.MaxValue)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The original native revision is required."));
        var prepared = PrepareDecide((long)expected, effectiveFrom);
        if (!prepared.IsSuccess)
            return prepared;
        if (!NativeOwnerHistory.IsOwnRoot(this, operation, nameof(DecideAccessReviewScope), (ulong)Revision))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The decision has another native owner or parent."));
        RaiseEvent(new AccessReviewScopeDecided(_tenantId, applicationId, Id, instanceRevision,
            operation.LogicalId, Revision + 1, effectiveFrom, content, operation, firstResponse, operationResult));
        return Result.Success;
    }

    void ApplyFact(AccessReviewScopeDecided fact)
    {
        Acceptance.Observe(fact);
        if (fact.TenantId != _tenantId || fact.SystemInstanceId != Id || fact.ApplicationId == Uuid.Empty ||
            fact.SystemInstanceRevision < 1 || fact.Sequence != Revision + 1 ||
            _latest is not null && (fact.ApplicationId != _latest.ApplicationId || fact.EffectiveFrom < _latest.EffectiveFrom) ||
            fact.DecisionId != fact.Operation.LogicalId || fact.FirstResponse is null || fact.OperationResult is null ||
            !NativeOwnerHistory.IsOwnRoot(this, fact.Operation, nameof(DecideAccessReviewScope), (ulong)Revision))
            throw new InvalidOperationException("A scope decision must advance its own instance's exact native effective-date line.");
        _latest = fact;
    }
}
