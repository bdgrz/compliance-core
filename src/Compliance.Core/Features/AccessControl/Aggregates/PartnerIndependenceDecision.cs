using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Aggregates;

/// <summary>
///     One immutable personal partner evaluation, identified by its evaluation. It holds the single materialized
///     <see cref="PartnerIndependenceEvaluationRecorded" /> fact, which carries the frozen intent it was computed from
///     and the ratified rules it relied on. The evaluation is never rewritten and never approves production
///     acceptance by itself.
/// </summary>
public sealed class PartnerIndependenceDecision : Aggregate
{
    /// <summary>Stream area of every partner evaluation.</summary>
    public const string Area = "partner-independence-decisions";

    PartnerIndependenceEvaluationRecorded? _recorded;

    /// <summary>Opens the evaluation of one client, selecting the logical operation to resolve.</summary>
    public PartnerIndependenceDecision(Uuid tenantId, Uuid evaluationId, Uuid selectedLogicalId = default)
        : base(AggregateIdentity.Create(nameof(PartnerIndependenceDecision), tenantId, evaluationId),
            new EventStreamAddress(tenantId.ToString(), Area, evaluationId.ToString()))
    {
        TenantId = tenantId;
        EvaluationId = evaluationId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<PartnerIndependenceEvaluationRecorded>(recorded =>
        {
            Acceptance.Observe(recorded);
            if (_recorded is not null || recorded.OperationId != EvaluationId || recorded.Operation.Owner != Stream ||
                recorded.Revision != 1 || recorded.OperationResult is null)
                throw new InvalidOperationException(
                    "A partner evaluation is materialized exactly once, for its own evaluation on its own stream.");
            _recorded = recorded;
        });
    }

    /// <summary>The client tenant of the evaluation.</summary>
    public Uuid TenantId { get; }

    /// <summary>The evaluation this owner materializes.</summary>
    public Uuid EvaluationId { get; }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The materialized evaluation fact, once recorded.</summary>
    public PartnerIndependenceEvaluationRecorded? Recorded => _recorded;

    /// <summary>Records the evaluation computed from the frozen intent; a second evaluation is never recorded.</summary>
    public Result Materialize(OperationProvenance operation, ImmutableContentRef evaluationContent,
        ImmutableContentRef ratifiedRulesContent, ImmutableContentRef intent, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(evaluationContent);
        ArgumentNullException.ThrowIfNull(ratifiedRulesContent);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(operationResult);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A partner evaluation is recorded on the decision owner of its evaluation."));
        if (_recorded is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "This evaluation is already immutable."));
        RaiseEvent(new PartnerIndependenceEvaluationRecorded(EvaluationId, evaluationContent, ratifiedRulesContent,
            operation, 1, intent, null, operationResult));
        return Result.Success;
    }
}
