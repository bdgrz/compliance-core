using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The complete nonattest service history of one client and its service epoch. Every recorded service is an
///     immutable fact in record order and advances the epoch by one; no count bounds the history. An admitted preview
///     evaluation is a root fact at the current epoch and leaves the epoch unchanged, so evaluations and later read
///     uses never contend with service records. The owner embeds <see cref="SourceUseReservations" />: a personal
///     decision that holds the exact epoch does not advance it, but a held epoch refuses further services until the
///     use is settled.
/// </summary>
public sealed class ClientServiceIndependence : Aggregate, ISourceUseOwner
{
    /// <summary>Stream area of every client service owner.</summary>
    public const string Area = "client-service-independence";

    /// <summary>The only generation of an evaluation admission.</summary>
    public const long Generation = 1;

    readonly Uuid _tenantId;
    NonattestServiceRecorded? _latest;
    ClientIndependenceEvaluationAdmitted? _selectedAdmission;
    Uuid _lastAdmissionId;
    ulong _epoch;

    /// <summary>Opens the service owner of one client tenant, selecting the logical operation to resolve.</summary>
    public ClientServiceIndependence(Uuid tenantId, Uuid selectedLogicalId = default, Uuid selectedEvaluationId = default)
        : base(AggregateIdentity.Create(nameof(ClientServiceIndependence), tenantId),
            new EventStreamAddress(tenantId.ToString(), Area, tenantId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        SelectedEvaluationId = selectedEvaluationId == Uuid.Empty ? selectedLogicalId : selectedEvaluationId;
        Uses = new SourceUseReservations(Stream);
        On<NonattestServiceRecorded>(recorded =>
        {
            Acceptance.Observe(recorded);
            if (recorded.TenantId != _tenantId || recorded.Operation.Owner != Stream ||
                recorded.OperationId != recorded.Operation.LogicalId || recorded.OperationId == Uuid.Empty ||
                recorded.Operation.LogicalId != recorded.Operation.RootOperationId ||
                recorded.Revision != _epoch + 1 || recorded.FirstResponse is null ||
                recorded.OperationResult is null || recorded.OperationId == _latest?.OperationId || recorded.OperationId == _lastAdmissionId)
                throw new InvalidOperationException(
                    "A service is appended once, to its own client, at the next service epoch.");
            _latest = recorded;
            _epoch++;
        });
        On<ClientIndependenceEvaluationAdmitted>(admitted =>
        {
            Acceptance.Observe(admitted);
            if (admitted.Owner != Stream || admitted.Operation.Owner != Stream ||
                admitted.Operation.LogicalId != admitted.Operation.RootOperationId ||
                admitted.Revision != (long)_epoch || admitted.Generation != Generation ||
                admitted.FirstResponse is null || admitted.Operation.LogicalId == _lastAdmissionId ||
                admitted.Operation.LogicalId == _latest?.OperationId)
                throw new InvalidOperationException(
                    "An evaluation is admitted once, at the current service epoch of its own client.");
            _lastAdmissionId = admitted.Operation.LogicalId;
            if (admitted.Operation.LogicalId == SelectedEvaluationId)
                _selectedAdmission = admitted;
        });
        On<ClientServicePersonalDecisionUseAuthorized>(granted =>
        {
            Acceptance.Observe(granted);
            if (granted.Revision != (long)_epoch)
                throw new InvalidOperationException("A service epoch is held exactly at its current epoch.");
            Uses.ApplyGranted(SourceUsePurpose.EngagementPersonalDecision, granted);
        });
        On<ClientServicePersonalDecisionUseSettled>(settled =>
        {
            Acceptance.Observe(settled);
            Uses.ApplySettled(SourceUsePurpose.EngagementPersonalDecision, settled);
        });
        On<EngagementServiceEpochAuthorized>(granted =>
        {
            Acceptance.Observe(granted);
            if (granted.Revision != (long)_epoch)
                throw new InvalidOperationException("A service epoch is held exactly at its current epoch.");
            Uses.ApplyGranted(SourceUsePurpose.EngagementAcceptance, granted);
        });
        On<EngagementServiceEpochSettled>(settled =>
        {
            Acceptance.Observe(settled);
            Uses.ApplySettled(SourceUsePurpose.EngagementAcceptance, settled);
        });
    }

    /// <summary>The active grants, receiver generation and fences of this owner's source uses.</summary>
    public SourceUseReservations Uses { get; }

    /// <summary>The client tenant this owner serves.</summary>
    public Uuid TenantId => _tenantId;

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The latest recorded service, whose event pins the complete history; null at epoch zero.</summary>
    public NonattestServiceRecorded? LatestService => _latest;

    /// <summary>The service epoch: the number of services ever recorded for this client.</summary>
    public ulong Epoch => _epoch;

    /// <summary>The one evaluation deliberately selected for this hydration.</summary>
    public Uuid SelectedEvaluationId { get; }

    /// <summary>The explicitly selected original evaluation admission.</summary>
    public ClientIndependenceEvaluationAdmitted? AdmissionOf(Uuid evaluationId) =>
        evaluationId == SelectedEvaluationId ? _selectedAdmission : null;

    internal FirmIndependenceReplayCut ReplayCut { get; } = new();
    internal Uuid LastAdmissionId => _lastAdmissionId;

    /// <summary>Checks the service guards without raising anything.</summary>
    public Result PrepareService(Uuid serviceRecordId, long expectedEpoch)
    {
        if (Acceptance.SelectedLogicalId == serviceRecordId && Acceptance.SelectedFact is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Service identities are immutable and cannot be reused."));
        if (Uses.HasActiveUse)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A personal decision holds the service epoch of this client; retry after it settles.", true));
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
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new NonattestServiceRecorded(_tenantId, operation.LogicalId, serviceContent, operation, Epoch + 1,
            firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Checks the evaluation admission guards without raising anything.</summary>
    public Result PrepareEvaluation(Uuid evaluationId, long expectedEpoch)
    {
        if (Acceptance.SelectedLogicalId == evaluationId && Acceptance.SelectedFact is not null)
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
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new ClientIndependenceEvaluationAdmitted(Stream, (long)Epoch, Generation, intent, null, operation,
            firstResponse, null));
        return Result.Success;
    }

    /// <summary>Holds the exact complete service history for a personal decision; the epoch does not advance.</summary>
    public Result AuthorizePersonalDecisionUse(OperationProvenance operation, ImmutableContentRef intent,
        long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A service history is held on its own client stream."));
        Uses.RequireForAppend(SourceUsePurpose.EngagementPersonalDecision, operation, generation, Version, CommittedStreamPosition);
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new ClientServicePersonalDecisionUseAuthorized(Stream, (long)Epoch, generation, intent, null,
            operation, null, null));
        return Result.Success;
    }

    /// <summary>Settles a personal decision use, or fences its key when no grant exists yet.</summary>
    public Result SettlePersonalDecisionUse(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A settlement is recorded on the held client stream."));
        Uses.RequireForAppend(SourceUsePurpose.EngagementPersonalDecision, operation, generation, Version, CommittedStreamPosition);
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new ClientServicePersonalDecisionUseSettled(Stream, (long)Epoch, generation, intent, null,
            operation, null, null));
        return Result.Success;
    }

    /// <summary>Holds the exact complete service history for an acceptance or staff assignment; the epoch does not advance.</summary>
    public Result AuthorizeEngagementEpoch(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A service history is held on its own client stream."));
        Uses.RequireForAppend(SourceUsePurpose.EngagementAcceptance, operation, generation, Version, CommittedStreamPosition);
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new EngagementServiceEpochAuthorized(Stream, (long)Epoch, generation, intent, null, operation,
            null, null));
        return Result.Success;
    }

    /// <summary>Settles an engagement epoch hold, or fences its key when no grant exists yet.</summary>
    public Result SettleEngagementEpoch(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A settlement is recorded on the held client stream."));
        Uses.RequireForAppend(SourceUsePurpose.EngagementAcceptance, operation, generation, Version, CommittedStreamPosition);
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new EngagementServiceEpochSettled(Stream, (long)Epoch, generation, intent, null, operation, null,
            null));
        return Result.Success;
    }
    protected override void Apply(DomainEvent ev)
    {
        if (!ev.Metadata.IsAudit)
            ReplayCut.Observe(ev);
        base.Apply(ev);
        Uses.ObserveOwnerFact(ev);
    }
}
