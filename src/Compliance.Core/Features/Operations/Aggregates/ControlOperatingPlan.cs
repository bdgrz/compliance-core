using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Aggregates;

/// <summary>
///     One control's operating plan line in one program: proposals and independent approvals of immutable plan
///     versions. Its logical revision counts the facts of this line only and is independent of every occurrence. The
///     complete plan content of each fact is verified immutable content, so the line's rules over that content are
///     decided by the caller from <see cref="ControlOperatingPlanLine" /> and re-checked here against the revision.
/// </summary>
public sealed class ControlOperatingPlan : Aggregate
{
    /// <summary>Stream area of every control operating plan line (the realm is the tenant).</summary>
    public const string Area = "control-operating-plans";

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _controlId;
    DomainEvent? _latest;
    long _revision;
    Uuid? _pending;

    /// <summary>Opens the plan line of one control, selecting the logical operation to resolve.</summary>
    public ControlOperatingPlan(Uuid tenantId, Uuid programId, Uuid controlId, Uuid selectedLogicalId = default)
        : base(ControlOccurrenceIdentity.PlanLineOf(tenantId, programId, controlId),
            new EventStreamAddress(tenantId.ToString(), Area,
                ControlOccurrenceIdentity.PlanLineOf(tenantId, programId, controlId).ToString()))
    {
        (_tenantId, _programId, _controlId) = (tenantId, programId, controlId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<ControlOperatingPlanProposed>(Apply);
        On<ControlOperatingPlanApproved>(Apply);
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The tenant of this exact owner.</summary>
    public Uuid TenantId => _tenantId;

    /// <summary>The program this line belongs to.</summary>
    public Uuid ProgramId => _programId;

    /// <summary>The control this line plans.</summary>
    public Uuid ControlId => _controlId;

    /// <summary>The logical revision: the number of facts on this line.</summary>
    public long Revision => _revision;

    /// <summary>The plan version waiting for approval, once proposed.</summary>
    public Uuid? PendingPlanVersionId => _pending;

    /// <summary>The latest exact native fact at this command cut.</summary>
    public DomainEvent? Latest => _latest;

    /// <summary>Checks the proposal guards without raising anything.</summary>
    public Result PrepareProposal(long expectedRevision)
    {
        if (expectedRevision != _revision)
            return Stale();
        return _pending is null
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "An operating plan is already pending approval for this control."));
    }

    /// <summary>Proposes a plan version as the root native fact of its operation.</summary>
    public Result Propose(OperationProvenance operation, ImmutableContentRef planContent,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(planContent);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A proposal is a root operation of its own plan line with an expected revision."));
        var prepared = PrepareProposal((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new ControlOperatingPlanProposed(_tenantId, _programId, _controlId, _revision + 1, planContent,
            operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Checks the approval guards without raising anything.</summary>
    public Result PrepareApproval(long expectedRevision, Uuid planVersionId)
    {
        if (_pending != planVersionId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The operating plan is not pending approval."));
        return expectedRevision == _revision ? Result.Success : Stale();
    }

    /// <summary>Approves the pending plan version as the root native fact of its operation.</summary>
    public Result Approve(OperationProvenance operation, Uuid planVersionId, Uuid approverMemberId,
        ActorReference approvedBy, DateTimeOffset approvedAt, string rationale, Uuid? waiverId,
        ImmutableContentRef reassignmentsManifest, ImmutableContentRef firstResponse,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(reassignmentsManifest);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An approval is a root operation of its own plan line with an expected revision."));
        var prepared = PrepareApproval((long)expected, planVersionId);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new ControlOperatingPlanApproved(_tenantId, _programId, _controlId, _revision + 1, planVersionId,
            approverMemberId, approvedBy, approvedAt, rationale, waiverId, reassignmentsManifest, operation,
            firstResponse, operationResult));
        return Result.Success;
    }

    void Apply(ControlOperatingPlanProposed ev)
    {
        Acceptance.Observe(ev);
        if (ev.TenantId != _tenantId || ev.ProgramId != _programId || ev.ControlId != _controlId ||
            ev.Revision != _revision + 1 || ev.Operation.Owner != Stream || ev.Operation.LogicalId == Uuid.Empty ||
            ev.FirstResponse is null || _pending is not null)
            throw new InvalidOperationException(
                "A plan proposal follows the previous revision of its own line while no plan is pending.");
        _revision = ev.Revision;
        _pending = ev.Operation.LogicalId;
        _latest = ev;
    }

    void Apply(ControlOperatingPlanApproved ev)
    {
        Acceptance.Observe(ev);
        if (ev.TenantId != _tenantId || ev.ProgramId != _programId || ev.ControlId != _controlId ||
            ev.Revision != _revision + 1 || ev.Operation.Owner != Stream || ev.FirstResponse is null ||
            _pending != ev.PlanVersionId)
            throw new InvalidOperationException(
                "A plan approval follows the previous revision of its own line and names the pending plan version.");
        _revision = ev.Revision;
        _pending = null;
        _latest = ev;
    }

    Result Stale() => Result.Failure(VersionedRecordRules.StaleRevision("control operating plan", _revision)
        .ToRequestError());
}
