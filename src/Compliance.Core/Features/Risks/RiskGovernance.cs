using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     One risk's owner and reassessment obligations. The logical revision counts the owner assignments and the
///     reassessment triggers of this risk only, so risks, control treatment lines and actions are independent owners
///     and no program-wide ledger revision or cap exists. A trigger is recognized by the deterministic content id of its
///     trigger identity, so a duplicate source never appends twice.
/// </summary>
public sealed class RiskGovernance : Aggregate
{
    /// <summary>Stream area of every risk governance owner (the realm is the tenant).</summary>
    public const string Area = "risk-governance";

    /// <summary>The identity owner type of a risk's governance.</summary>
    public const string OwnerType = "RiskGovernance";

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _riskId;
    readonly List<RiskOwnerAssigned> _owners = [];
    readonly List<RiskReassessmentTriggered> _triggers = [];
    readonly List<DomainEvent> _facts = [];
    long _revision;

    /// <summary>Opens one risk's governance, selecting the logical operation to resolve.</summary>
    public RiskGovernance(Uuid tenantId, Uuid programId, Uuid riskId, Uuid selectedLogicalId = default)
        : base(IdentityOf(tenantId, programId, riskId),
            new EventStreamAddress(tenantId.ToString(), Area, IdentityOf(tenantId, programId, riskId).ToString()))
    {
        (_tenantId, _programId, _riskId) = (tenantId, programId, riskId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<RiskOwnerAssigned>(Apply);
        On<RiskReassessmentTriggered>(Apply);
    }

    /// <summary>The identity of the governance of a risk.</summary>
    public static Uuid IdentityOf(Uuid tenantId, Uuid programId, Uuid riskId) =>
        AggregateIdentity.Create(OwnerType, tenantId, programId, riskId);

    /// <summary>The deterministic stable identity of a reassessment trigger, so a replayed reaction never raises it twice.</summary>
    public static Uuid TriggerIdFor(Uuid programId, Uuid riskId, string triggerKind, string sourceReference) =>
        Uuid.CreateVersion5(Uuid.CreateVersion5(programId, "risk-reassessment-trigger"),
            riskId + "|" + triggerKind + "|" + sourceReference);

    /// <summary>The content id the trigger's content is stored under, which is how the owner recognizes it.</summary>
    public static Uuid TriggerContentId(Uuid triggerId) => Uuid.CreateVersion5(triggerId, RiskContent.TriggerSchema);

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The risk this governance belongs to.</summary>
    public Uuid RiskId => _riskId;

    /// <summary>The logical revision: the number of owner assignments and triggers of this risk.</summary>
    public long Revision => _revision;

    /// <summary>The latest owner assignment, or null when no owner was assigned.</summary>
    public RiskOwnerAssigned? Owner => _owners.Count == 0 ? null : _owners[^1];

    /// <summary>Every reassessment trigger in the order raised.</summary>
    public IReadOnlyList<RiskReassessmentTriggered> Triggers => _triggers;

    /// <summary>Every fact in order.</summary>
    public IReadOnlyList<DomainEvent> Facts => _facts;

    /// <summary>True when the trigger with this identity was already raised.</summary>
    public bool HasTrigger(Uuid triggerId)
    {
        var contentId = TriggerContentId(triggerId);
        return _triggers.Any(trigger => trigger.TriggerContent.ContentId == contentId);
    }

    /// <summary>Checks that the owner can be assigned at the expected revision.</summary>
    public Result PrepareAssignOwner(long expectedRevision) => expectedRevision != _revision
        ? Result.Failure(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("risk governance", _revision))
            .ToRequestError())
        : Result.Success;

    /// <summary>Checks that the trigger can be raised: a duplicate is a retryable conflict the reaction resolves on replay.</summary>
    public Result PrepareTrigger(Uuid triggerId) => HasTrigger(triggerId)
        ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
            "The reassessment trigger was already raised; retry to confirm it.", true))
        : Result.Success;

    /// <summary>Assigns the owner as the root native fact of its operation.</summary>
    public Result AssignOwner(OperationProvenance operation, ImmutableContentRef ownerContent,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(ownerContent);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An owner assignment is a root operation of its own risk governance with an expected revision."));
        var prepared = PrepareAssignOwner((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new RiskOwnerAssigned(_tenantId, _programId, _riskId, _revision + 1, ownerContent, operation,
            firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Raises a reassessment trigger as the root native fact of its operation.</summary>
    public Result RaiseTrigger(OperationProvenance operation, Uuid triggerId, ImmutableContentRef triggerContent,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(triggerContent);
        if (operation.Owner != Stream || triggerContent.ContentId != TriggerContentId(triggerId))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A trigger is a root operation of its own risk governance stored under its trigger identity."));
        var prepared = PrepareTrigger(triggerId);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new RiskReassessmentTriggered(_tenantId, _programId, _riskId, _revision + 1, triggerContent,
            operation, firstResponse, operationResult));
        return Result.Success;
    }

    void Apply(RiskOwnerAssigned ev)
    {
        Acceptance.Observe(ev);
        EnsureNext(ev.TenantId, ev.ProgramId, ev.RiskId, ev.Revision, ev.Operation);
        _owners.Add(ev);
        Advance(ev);
    }

    void Apply(RiskReassessmentTriggered ev)
    {
        Acceptance.Observe(ev);
        EnsureNext(ev.TenantId, ev.ProgramId, ev.RiskId, ev.Revision, ev.Operation);
        _triggers.Add(ev);
        Advance(ev);
    }

    void EnsureNext(Uuid tenantId, Uuid programId, Uuid riskId, long revision, OperationProvenance operation)
    {
        if (tenantId != _tenantId || programId != _programId || riskId != _riskId || revision != _revision + 1 ||
            operation.Owner != Stream)
            throw new InvalidOperationException("A risk governance fact follows the previous one at the next revision.");
    }

    void Advance(DomainEvent fact)
    {
        _revision++;
        _facts.Add(fact);
    }
}
