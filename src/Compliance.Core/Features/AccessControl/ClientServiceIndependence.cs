using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The complete nonattest service history of one client and its service epoch. Every recorded service is an
///     immutable fact in record order and advances the epoch by one; no count bounds the history. An admitted preview
///     evaluation is a root fact at the current epoch and leaves the epoch unchanged, so evaluations and later read
///     uses never contend with service records.
/// </summary>
public sealed class ClientServiceIndependence : Aggregate
{
    /// <summary>Stream area of every client service owner.</summary>
    public const string Area = "client-service-independence";

    /// <summary>The only generation of an evaluation admission.</summary>
    public const long Generation = 1;

    readonly Uuid _tenantId;
    readonly List<NonattestServiceRecorded> _services = [];
    readonly Dictionary<Uuid, int> _serviceIndex = [];
    readonly List<ClientIndependenceEvaluationAdmitted> _admissions = [];
    readonly Dictionary<Uuid, ClientIndependenceEvaluationAdmitted> _admissionIndex = [];

    /// <summary>Opens the service owner of one client tenant, selecting the logical operation to resolve.</summary>
    public ClientServiceIndependence(Uuid tenantId, Uuid selectedLogicalId = default)
        : base(AggregateIdentity.Create(nameof(ClientServiceIndependence), tenantId),
            new EventStreamAddress(tenantId.ToString(), Area, tenantId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<NonattestServiceRecorded>(recorded =>
        {
            Acceptance.Observe(recorded);
            if (recorded.TenantId != _tenantId || recorded.Operation.Owner != Stream ||
                recorded.OperationId != recorded.Operation.LogicalId || recorded.OperationId == Uuid.Empty ||
                recorded.Operation.LogicalId != recorded.Operation.RootOperationId ||
                recorded.Revision != (ulong)_services.Count + 1 || recorded.FirstResponse is null ||
                recorded.OperationResult is null || !_serviceIndex.TryAdd(recorded.OperationId, _services.Count))
                throw new InvalidOperationException(
                    "A service is appended once, to its own client, at the next service epoch.");
            _services.Add(recorded);
        });
        On<ClientIndependenceEvaluationAdmitted>(admitted =>
        {
            Acceptance.Observe(admitted);
            if (admitted.Owner != Stream || admitted.Operation.Owner != Stream ||
                admitted.Operation.LogicalId != admitted.Operation.RootOperationId ||
                admitted.Revision != _services.Count || admitted.Generation != Generation ||
                admitted.FirstResponse is null ||
                !_admissionIndex.TryAdd(admitted.Operation.LogicalId, admitted))
                throw new InvalidOperationException(
                    "An evaluation is admitted once, at the current service epoch of its own client.");
            _admissions.Add(admitted);
        });
    }

    /// <summary>The client tenant this owner serves.</summary>
    public Uuid TenantId => _tenantId;

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The service epoch: the number of services ever recorded for this client.</summary>
    public ulong Epoch => (ulong)_services.Count;

    /// <summary>Every recorded service fact, in record order.</summary>
    public IReadOnlyList<NonattestServiceRecorded> Services => _services;

    /// <summary>Every admitted evaluation root fact, in admission order.</summary>
    public IReadOnlyList<ClientIndependenceEvaluationAdmitted> Admissions => _admissions;

    /// <summary>Finds the admission of one evaluation.</summary>
    public ClientIndependenceEvaluationAdmitted? AdmissionOf(Uuid evaluationId) =>
        _admissionIndex.GetValueOrDefault(evaluationId);

    /// <summary>Checks the service guards without raising anything.</summary>
    public Result PrepareService(Uuid serviceRecordId, long expectedEpoch)
    {
        if (_serviceIndex.ContainsKey(serviceRecordId) || _admissionIndex.ContainsKey(serviceRecordId))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Service identities are immutable and cannot be reused."));
        return expectedEpoch == (long)Epoch
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Reload the service epoch of this client before recording another service."));
    }

    /// <summary>Appends one service as the root native fact of its operation and advances the epoch.</summary>
    public Result RecordService(OperationProvenance operation, ImmutableContentRef serviceContent,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(serviceContent);
        ArgumentNullException.ThrowIfNull(firstResponse);
        ArgumentNullException.ThrowIfNull(operationResult);
        if (operation.Owner != Stream || operation.LogicalId != operation.RootOperationId ||
            operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A service is the logical operation of its own record on its client's owner."));
        var prepared = PrepareService(operation.LogicalId, (long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new NonattestServiceRecorded(_tenantId, operation.LogicalId, serviceContent, operation, Epoch + 1,
            firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Checks the evaluation admission guards without raising anything.</summary>
    public Result PrepareEvaluation(Uuid evaluationId, long expectedEpoch)
    {
        if (_admissionIndex.ContainsKey(evaluationId) || _serviceIndex.ContainsKey(evaluationId))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Evaluation identities are immutable and cannot be reused."));
        return expectedEpoch == (long)Epoch
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Reload the service epoch of this client before evaluating it."));
    }

    /// <summary>
    ///     Admits the evaluation as the root native fact of its operation. The frozen intent must have captured exactly
    ///     the epoch this owner holds, otherwise a service was recorded while the intent was prepared.
    /// </summary>
    public Result AdmitEvaluation(OperationProvenance operation, ImmutableContentRef intent, ulong intentEpoch,
        ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.Owner != Stream || operation.LogicalId != operation.RootOperationId ||
            operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An evaluation is the logical operation of its own preview on its client's owner."));
        var prepared = PrepareEvaluation(operation.LogicalId, (long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        if (intentEpoch != Epoch)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A service was recorded while the evaluation was prepared; reload and retry.", true));
        RaiseEvent(new ClientIndependenceEvaluationAdmitted(Stream, (long)Epoch, Generation, intent, null, operation,
            firstResponse, null));
        return Result.Success;
    }
}
