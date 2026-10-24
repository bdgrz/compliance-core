using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     One immutable historical preview of a client, identified by its evaluation. It holds the single materialized
///     <see cref="ClientIndependenceEvaluated" /> fact, which carries the frozen intent it was computed from. The
///     preview never grants production acceptance and is never rewritten.
/// </summary>
public sealed class ClientIndependenceDecision : Aggregate
{
    /// <summary>Stream area of every preview decision.</summary>
    public const string Area = "client-independence-decisions";

    readonly Uuid _tenantId;
    ClientIndependenceEvaluated? _evaluated;

    /// <summary>Opens the preview of one evaluation, selecting the logical operation to resolve.</summary>
    public ClientIndependenceDecision(Uuid tenantId, Uuid evaluationId, Uuid selectedLogicalId = default)
        : base(AggregateIdentity.Create(nameof(ClientIndependenceDecision), tenantId, evaluationId),
            new EventStreamAddress(tenantId.ToString(), Area, evaluationId.ToString()))
    {
        _tenantId = tenantId;
        EvaluationId = evaluationId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<ClientIndependenceEvaluated>(evaluated =>
        {
            Acceptance.Observe(evaluated);
            if (_evaluated is not null || evaluated.TenantId != _tenantId || evaluated.OperationId != EvaluationId ||
                evaluated.Operation.Owner != Stream || evaluated.Revision != 1 || evaluated.OperationResult is null)
                throw new InvalidOperationException(
                    "A preview is materialized exactly once, for its own evaluation of its own client.");
            _evaluated = evaluated;
        });
    }

    /// <summary>The evaluation this decision materializes.</summary>
    public Uuid EvaluationId { get; }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The materialized preview fact, once recorded.</summary>
    public ClientIndependenceEvaluated? Evaluated => _evaluated;

    /// <summary>Records the preview computed from the frozen intent; a second preview is never recorded.</summary>
    public Result Materialize(OperationProvenance operation, ImmutableContentRef evaluationContent,
        ImmutableContentRef intent, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(evaluationContent);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(operationResult);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A preview is recorded on the decision owner of its evaluation."));
        if (_evaluated is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "This evaluation already has its immutable preview."));
        RaiseEvent(new ClientIndependenceEvaluated(_tenantId, EvaluationId, evaluationContent, operation, 1, intent,
            null, operationResult));
        return Result.Success;
    }
}
