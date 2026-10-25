using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>Bounded current authority of one control's evaluation-plan line; history is immutable read composition.</summary>
public sealed class ControlEvaluationPlan : Aggregate
{
    public const string Area = "control-evaluation-plans";
    readonly Uuid _tenantId;
    ControlEvaluationPlanVersionDefined? _latest;

    public ControlEvaluationPlan(Uuid tenantId, Uuid programId, Uuid controlId, Uuid selectedLogicalId = default)
        : base(controlId, new EventStreamAddress(tenantId.ToString(), Area, controlId.ToString()))
    {
        _tenantId = tenantId;
        RequestedProgramId = programId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<ControlEvaluationPlanVersionDefined>(ApplyFact);
    }

    public Uuid TenantId => _tenantId;
    public Uuid RequestedProgramId { get; }
    public Uuid ProgramId => _latest?.ProgramId ?? RequestedProgramId;
    public long Revision => _latest?.Version ?? 0;
    public ControlEvaluationPlanVersionDefined? Latest => _latest;
    public NativeAcceptance Acceptance { get; }

    /// <summary>The stable operation identity for the existing wire contract's original expected version.</summary>
    public static Uuid OperationId(Uuid tenant, Uuid program, Uuid control, long expectedVersion) =>
        ControlEvaluationPlanIdentity.For(tenant, program, control, expectedVersion);

    public Result PrepareDefine(long expectedVersion) => ProgramId != RequestedProgramId
        ? Result.Failure(new RequestError(RequestErrorKind.NotFound, "The control evaluation plan was not found."))
        : expectedVersion != Revision
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"Reload the control evaluation plan. Current version: {Revision}."))
            : Result.Success;

    public Result Define(OperationProvenance operation, Uuid controlVersionId, ImmutableContentRef content,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        if (operation.OriginalExpectedRevision is not { } expected || expected > (ulong)long.MaxValue)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The original native revision is required."));
        var prepared = PrepareDefine((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        if (!NativeOwnerHistory.IsOwnRoot(this, operation, nameof(DefineControlEvaluationPlan), (ulong)Revision))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The plan definition has another native root."));
        RaiseEvent(new ControlEvaluationPlanVersionDefined(_tenantId, ProgramId, Id, controlVersionId,
            operation.LogicalId, Revision + 1, content, operation, firstResponse, operationResult));
        return Result.Success;
    }

    void ApplyFact(ControlEvaluationPlanVersionDefined fact)
    {
        Acceptance.Observe(fact);
        if (fact.TenantId != _tenantId || fact.ControlId != Id || fact.ProgramId == Uuid.Empty ||
            _latest is not null && fact.ProgramId != ProgramId || fact.ControlVersionId == Uuid.Empty ||
            fact.PlanVersionId != fact.Operation.LogicalId || fact.Version != Revision + 1 ||
            fact.FirstResponse is null || fact.OperationResult is null ||
            !NativeOwnerHistory.IsOwnRoot(this, fact.Operation, nameof(DefineControlEvaluationPlan), (ulong)Revision))
            throw new InvalidOperationException("A plan definition must advance its own exact native version line.");
        _latest = fact;
    }
}
