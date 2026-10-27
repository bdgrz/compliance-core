using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The lifecycle of one service engagement: its draft, tentative staff proposals and closure. The logical revision
///     counts accepted changes of this engagement only and never depends on another engagement or on the client's
///     service history. Acceptance and actual assignments are attached by their own facts. Personal decisions
///     (management acknowledgement, partner evaluation) are admitted here against the exact content revision, which
///     they pin without advancing it until they are materialized or canceled; the owner keeps one pointer per decision
///     kind that a late older decision can never regress. An acceptance or a positive actual staff assignment is
///     admitted here as a root fact against the exact content revision, pins that content until the ordered process
///     records or rejects it, and advances the revision only when it is recorded. The permanent practice walls it
///     commits live on <see cref="PracticeIndependenceCommitment" />, never here.
/// </summary>
public sealed class ServiceEngagementIndependence : Aggregate
{
    /// <summary>Stream area of every engagement owner.</summary>
    public const string Area = "service-engagements";

    /// <summary>Stable command kinds that tag lifecycle changes.</summary>
    public const string CreateKind = "CreateServiceEngagement";
    public const string AmendKind = "AmendServiceEngagement";
    public const string ProposeKind = "ProposeServiceEngagementStaff";
    public const string WithdrawKind = "WithdrawServiceEngagementStaffProposal";
    public const string CloseKind = "CloseServiceEngagement";

    /// <summary>Stable command kinds that tag personal decision admissions.</summary>
    public const string ManagementKind = "AcknowledgeEngagementManagement";
    public const string PartnerKind = "RecordPartnerIndependenceEvaluation";

    /// <summary>Stable command kinds that tag acceptance and staff assignment admissions.</summary>
    public const string AcceptKind = "AcceptServiceEngagement";
    public const string AssignKind = "AssignServiceEngagementStaff";

    /// <summary>The only generation of a personal decision.</summary>
    public const long DecisionGeneration = 1;

    /// <summary>The only generation of an acceptance or staff assignment.</summary>
    public const long AcceptanceGeneration = 1;

    readonly Uuid _tenantId;
    ServiceEngagementMutationRecorded? _latestChange;
    readonly Uuid _selectedRootId;
    EngagementPersonalDecisionRecord? _selectedDecision;
    EngagementAcceptanceRecord? _selectedOperation;
    long _decisionOrdinal;
    readonly Dictionary<Uuid, EngagementPersonalDecisionRecord> _decisions = [];
    readonly Dictionary<string, EngagementPersonalDecisionRecord> _pointers = [];
    readonly Dictionary<Uuid, EngagementAcceptanceRecord> _operations = [];
    ImmutableContentRef? _acceptanceResult;
    ulong _revision;
    string _status = "none";

    /// <summary>Opens one engagement, selecting the logical operation to resolve.</summary>
    public ServiceEngagementIndependence(Uuid tenantId, Uuid engagementId, Uuid selectedLogicalId = default,
        Uuid selectedRootId = default)
        : base(AggregateIdentity.Create(nameof(ServiceEngagementIndependence), tenantId, engagementId),
            new EventStreamAddress(tenantId.ToString(), Area, engagementId.ToString()))
    {
        _selectedRootId = selectedRootId == Uuid.Empty ? selectedLogicalId : selectedRootId;
        _tenantId = tenantId;
        EngagementId = engagementId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<ServiceEngagementMutationRecorded>(change =>
        {
            Acceptance.Observe(change);
            var creating = change.Intent == CreateKind;
            if (change.TenantId != _tenantId || change.Operation.Owner != Stream ||
                change.OperationId != change.Operation.LogicalId ||
                change.Operation.LogicalId != change.Operation.RootOperationId ||
                change.Operation.CommandKind != change.Intent || change.Revision != _revision + 1 ||
                change.FirstResponse is null || change.OperationResult is null ||
                change.Intent is not (CreateKind or AmendKind or ProposeKind or WithdrawKind or CloseKind) ||
                creating != (_revision == 0) || _status == "closed" || HasPendingPersonalDecision || HasPendingOperation ||
                creating && change.OperationId != EngagementId)
                throw new InvalidOperationException(
                    "An engagement draft changes once per command, in order, until it is closed.");
            _status = change.Intent == CloseKind ? "closed" : "draft";
            _revision = change.Revision;
            _latestChange = change;
        });
        On<ServiceEngagementAssignmentRevoked>(revoked =>
        {
            Acceptance.Observe(revoked);
            if (revoked.TenantId != _tenantId || revoked.EngagementId != EngagementId ||
                revoked.Operation.Owner != Stream || revoked.OperationId != revoked.Operation.LogicalId ||
                revoked.Revision != _revision + 1 || _status != "accepted" || HasPendingPersonalDecision || HasPendingOperation || revoked.FirstResponse is null ||
                revoked.OperationResult is null)
                throw new InvalidOperationException(
                    "An actual assignment is revoked on an accepted engagement of its own tenant, in order.");
            _status = revoked.StaffMemberId is null ? "closed" : _status;
            _revision = revoked.Revision;
            _acceptanceResult = revoked.OperationResult;
        });
        On<EngagementPersonalDecisionAdmitted>(admitted =>
        {
            Acceptance.Observe(admitted);
            if (admitted.Owner != Stream || admitted.Operation.Owner != Stream ||
                admitted.Operation.LogicalId != admitted.Operation.RootOperationId ||
                admitted.Operation.CommandKind is not (ManagementKind or PartnerKind) ||
                admitted.Revision != (long)_revision || admitted.Generation != DecisionGeneration ||
                _status != "draft" || HasPendingOperation || admitted.FirstResponse is null ||
                !_decisions.TryAdd(admitted.Operation.RootOperationId,
                    new EngagementPersonalDecisionRecord(admitted, _decisionOrdinal++)))
                throw new InvalidOperationException(
                    "A personal decision is admitted once, on an open draft, at its current content revision.");
            if (admitted.Operation.RootOperationId == _selectedRootId)
                _selectedDecision = _decisions[admitted.Operation.RootOperationId];
        });
        On<EngagementPersonalDecisionAuthorized>(authorized =>
        {
            Acceptance.Observe(authorized);
            if (!_decisions.TryGetValue(authorized.Operation.RootOperationId, out var decision) ||
                authorized.Owner != Stream || authorized.Intent != decision.Admitted.Intent ||
                decision.Authorized is not null || decision.Canceled is not null)
                throw new InvalidOperationException("Only an admitted, uncanceled personal decision is authorized once.");
            decision.Authorized = authorized;
        });
        On<EngagementPersonalDecisionCanceled>(canceled =>
        {
            Acceptance.Observe(canceled);
            if (!_decisions.TryGetValue(canceled.Operation.RootOperationId, out var decision) ||
                canceled.Owner != Stream || canceled.Intent != decision.Admitted.Intent ||
                decision.Authorized is not null || decision.Canceled is not null)
                throw new InvalidOperationException("Only an unauthorized personal decision is canceled once.");
            decision.Canceled = canceled;
            _decisions.Remove(canceled.Operation.RootOperationId);
        });
        On<EngagementPersonalDecisionMaterialized>(materialized =>
        {
            Acceptance.Observe(materialized);
            if (!_decisions.TryGetValue(materialized.Operation.RootOperationId, out var decision) ||
                materialized.Owner != Stream || materialized.Intent != decision.Admitted.Intent ||
                decision.Authorized is null || decision.Materialized is not null)
                throw new InvalidOperationException("Only an authorized personal decision is materialized once.");
            decision.Materialized = materialized;
            _decisions.Remove(materialized.Operation.RootOperationId);
            var kind = decision.Admitted.Operation.CommandKind;
            if (!_pointers.TryGetValue(kind, out var current) || current.Ordinal < decision.Ordinal)
                _pointers[kind] = decision;
        });
        On<ServiceEngagementAcceptanceRequested>(requested => ApplyAdmitted(requested, requested.Owner,
            requested.Revision, requested.Generation, requested.Intent, requested.FirstResponse, AcceptKind,
            "draft", EngagementIndependenceOperationKind.Acceptance));
        On<ServiceEngagementStaffAssignmentRequested>(requested => ApplyAdmitted(requested, requested.Owner,
            requested.Revision, requested.Generation, requested.Intent, requested.FirstResponse, AssignKind,
            "accepted", EngagementIndependenceOperationKind.StaffAssignment));
        On<EngagementPracticeCommitmentsAuthorized>(authorized =>
        {
            Acceptance.Observe(authorized);
            if (!_operations.TryGetValue(authorized.Operation.RootOperationId, out var operation) ||
                authorized.Owner != Stream || authorized.Intent != operation.Intent ||
                authorized.Generation != AcceptanceGeneration || !operation.IsPending ||
                operation.Authorized is not null)
                throw new InvalidOperationException(
                    "Only an admitted, pending acceptance or staff assignment is authorized once.");
            operation.Authorized = authorized;
        });
        On<ServiceEngagementAcceptanceRejected>(rejected => ApplyRejected(rejected, rejected.Owner,
            rejected.Intent, EngagementIndependenceOperationKind.Acceptance));
        On<ServiceEngagementStaffAssignmentRejected>(rejected => ApplyRejected(rejected, rejected.Owner,
            rejected.Intent, EngagementIndependenceOperationKind.StaffAssignment));
        On<ServiceEngagementAcceptanceRecorded>(recorded =>
        {
            Acceptance.Observe(recorded);
            var operation = CompleteOperation(recorded.Operation, recorded.TenantId, recorded.OperationId,
                recorded.Revision, recorded.OperationResult, EngagementIndependenceOperationKind.Acceptance, "draft");
            operation.Completed = recorded;
            _operations.Remove(recorded.Operation.RootOperationId);
            _status = "accepted";
            _revision = recorded.Revision;
            _acceptanceResult = recorded.OperationResult;
        });
        On<ServiceEngagementStaffAssigned>(assigned =>
        {
            Acceptance.Observe(assigned);
            if (assigned.EngagementId != EngagementId)
                throw new InvalidOperationException("A staff assignment is recorded on its own engagement.");
            var operation = CompleteOperation(assigned.Operation, assigned.TenantId, assigned.OperationId,
                assigned.Revision, assigned.OperationResult, EngagementIndependenceOperationKind.StaffAssignment,
                "accepted");
            operation.Completed = assigned;
            _operations.Remove(assigned.Operation.RootOperationId);
            _revision = assigned.Revision;
            _acceptanceResult = assigned.OperationResult;
        });
        On<DirectoryIndependenceReevaluated>(reevaluated =>
        {
            Acceptance.Observe(reevaluated);
            if (reevaluated.TenantId != _tenantId || reevaluated.Operation.Owner != Stream ||
                reevaluated.Operation.LogicalId != reevaluated.Operation.RootOperationId ||
                reevaluated.Revision != _revision + 1 || _status != "accepted" || reevaluated.FirstResponse is null ||
                reevaluated.OperationResult is null)
                throw new InvalidOperationException(
                    "A directory receipt is recorded once, on an accepted engagement of its own tenant, in order.");
            _revision = reevaluated.Revision;
        });
    }

    internal FirmIndependenceReplayCut ReplayCut { get; } = new();
    internal Uuid SelectedRootId => _selectedRootId;
    internal IReadOnlyDictionary<Uuid, EngagementPersonalDecisionRecord> LiveDecisions => _decisions;
    internal IReadOnlyDictionary<Uuid, EngagementAcceptanceRecord> LiveOperations => _operations;
    protected override void Apply(DomainEvent ev)
    {
        if (!ev.Metadata.IsAudit)
            ReplayCut.Observe(ev);
        base.Apply(ev);
    }

    /// <summary>The engagement this owner serves.</summary>
    public Uuid EngagementId { get; }

    /// <summary>The client tenant of the engagement.</summary>
    public Uuid TenantId => _tenantId;

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The logical revision: the number of accepted changes of this engagement.</summary>
    public ulong Revision => _revision;

    /// <summary>One of none, draft, accepted or closed.</summary>
    public string Status => _status;

    /// <summary>True once a draft was created.</summary>
    public bool Exists => _revision > 0;



    /// <summary>The operation result of the latest lifecycle change, which holds the current draft view.</summary>
    public ImmutableContentRef? LatestResult => _latestChange?.OperationResult;

    /// <summary>
    ///     The retained result of the latest acceptance, staff assignment or revocation, whose view is the current
    ///     accepted snapshot; null until the engagement is accepted.
    /// </summary>
    public ImmutableContentRef? AcceptanceResult => _acceptanceResult;







    /// <summary>The acceptance or staff assignment of one identity, once admitted.</summary>
    public EngagementAcceptanceRecord? OperationOf(Uuid operationId) =>
        _operations.GetValueOrDefault(operationId) ?? (operationId == _selectedRootId ? _selectedOperation : null);

    /// <summary>True while an admitted acceptance or staff assignment is neither recorded nor rejected.</summary>
    public bool HasPendingOperation => _operations.Values.Any(operation => operation.IsPending);



    /// <summary>True while an admitted decision is neither materialized nor canceled; it pins the content revision.</summary>
    public bool HasPendingPersonalDecision => _decisions.Values.Any(decision => decision.IsPending);

    /// <summary>The decision of one identity, once admitted.</summary>
    public EngagementPersonalDecisionRecord? DecisionOf(Uuid decisionId) =>
        _decisions.GetValueOrDefault(decisionId) ?? (decisionId == _selectedRootId ? _selectedDecision :
            _pointers.Values.FirstOrDefault(value => value.Admitted.Operation.RootOperationId == decisionId));

    /// <summary>
    ///     The materialized decision that currently stands for a kind: the one admitted last among those materialized,
    ///     never an older one delivered late.
    /// </summary>
    public EngagementPersonalDecisionRecord? CurrentDecision(EngagementPersonalDecisionKind kind) =>
        _pointers.GetValueOrDefault(kind == EngagementPersonalDecisionKind.ManagementAcknowledgement
            ? ManagementKind
            : PartnerKind);

    /// <summary>Checks the lifecycle guards of a command without raising anything.</summary>
    public Result PrepareChange(string kind, long expectedRevision)
    {
        if (kind == CreateKind)
        {
            if (Exists)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "Engagement identities are immutable and cannot be created twice."));
            return expectedRevision == 0
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "A new engagement is created at revision zero."));
        }

        if (!Exists)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The client service engagement was not found."));
        if (expectedRevision != (long)_revision)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Reload the engagement revision before changing it."));
        if (HasPendingPersonalDecision)
            return PendingDecisionFailure();
        if (HasPendingOperation)
            return PendingOperationFailure();
        return _status == "draft"
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only an open draft engagement can be changed."));
    }

    /// <summary>Records one lifecycle change as the root native fact of its operation.</summary>
    public Result Change(string kind, OperationProvenance operation, ImmutableContentRef change,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(firstResponse);
        ArgumentNullException.ThrowIfNull(operationResult);
        if (operation.Owner != Stream || operation.CommandKind != kind ||
            operation.LogicalId != operation.RootOperationId || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An engagement change is the logical operation of its own command on its own engagement."));
        var prepared = PrepareChange(kind, (long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new ServiceEngagementMutationRecorded(_tenantId, operation.LogicalId, kind, change, operation,
            _revision + 1, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Checks the guards of revoking an actual assignment without raising anything.</summary>
    public Result PrepareRevocation(long expectedRevision)
    {
        if (!Exists)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The client service engagement was not found."));
        if (expectedRevision != (long)_revision)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Reload the engagement revision before revoking an actual assignment."));
        if (HasPendingPersonalDecision)
            return PendingDecisionFailure();
        if (HasPendingOperation)
            return PendingOperationFailure();
        return _status == "accepted"
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only an accepted engagement has actual assignments to revoke."));
    }

    /// <summary>Revokes one actual assignment, or closes the accepted engagement when no staff member is named.</summary>
    public Result Revoke(OperationProvenance operation, Uuid? staffMemberId, string reason, ActorReference actor,
        DateTimeOffset recordedAt, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        ArgumentNullException.ThrowIfNull(operationResult);
        if (operation.Owner != Stream || operation.LogicalId != operation.RootOperationId ||
            operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A revocation is the logical operation of its own command on its own engagement."));
        var prepared = PrepareRevocation((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new ServiceEngagementAssignmentRevoked(_tenantId, operation.LogicalId, EngagementId, staffMemberId,
            reason, operation.CommandKind, actor, recordedAt, operation, _revision + 1, firstResponse,
            operationResult));
        return Result.Success;
    }

    /// <summary>Checks the personal decision admission guards without raising anything.</summary>
    public Result PreparePersonalDecision(Uuid decisionId, long expectedRevision)
    {
        if (!Exists)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The client service engagement was not found."));
        if (_decisions.ContainsKey(decisionId))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Personal decision identities are immutable and cannot be reused."));
        if (expectedRevision != (long)_revision)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Reload the engagement revision before deciding on it."));
        if (HasPendingOperation)
            return PendingOperationFailure();
        return _status == "draft"
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only an open draft engagement accepts a personal decision."));
    }

    /// <summary>
    ///     Admits the decision as the root native fact of its operation. The frozen intent must have captured exactly
    ///     the content revision this owner holds, otherwise the engagement changed while the intent was prepared.
    /// </summary>
    public Result AdmitPersonalDecision(OperationProvenance operation, ImmutableContentRef intent, ulong intentRevision,
        ImmutableContentRef firstResponse)
    {
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.Owner != Stream || operation.LogicalId != operation.RootOperationId ||
            operation.CommandKind is not (ManagementKind or PartnerKind) ||
            operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A personal decision is the logical operation of its own command on its own engagement."));
        var prepared = PreparePersonalDecision(operation.LogicalId, (long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        if (intentRevision != _revision)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The engagement changed while the decision was prepared; reload and retry.", true));
        RaiseEvent(new EngagementPersonalDecisionAdmitted(Stream, (long)_revision, DecisionGeneration, intent, null,
            operation, firstResponse, null));
        return Result.Success;
    }

    /// <summary>Authorizes the admitted decision irreversibly; the caller proved every required source use.</summary>
    public Result AuthorizePersonalDecision(OperationProvenance operation, DateTimeOffset decisionAt,
        ImmutableContentRef authorizationProofs)
    {
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(authorizationProofs);
        if (DecisionOf(operation.RootOperationId) is not { } decision || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only an admitted personal decision of this engagement can be authorized.", true));
        if (decision.Canceled is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The personal decision was canceled; authorization is fenced.", false));
        if (decision.Authorized is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The personal decision is already authorized with other terms.", false));
        RaiseEvent(new EngagementPersonalDecisionAuthorized(Stream, (long)_revision, DecisionGeneration,
            decision.Admitted.Intent, null, operation, decisionAt, authorizationProofs, null, null));
        return Result.Success;
    }

    /// <summary>Cancels the admitted decision before authorization; an authorized decision recovers forward.</summary>
    public Result CancelPersonalDecision(OperationProvenance operation)
    {
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        ArgumentNullException.ThrowIfNull(operation);
        if (DecisionOf(operation.RootOperationId) is not { } decision || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only an admitted personal decision of this engagement can be canceled.", true));
        if (decision.Authorized is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The personal decision is authorized and irreversible; it recovers forward.", false));
        if (decision.Canceled is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The personal decision is already canceled with other terms.", false));
        RaiseEvent(new EngagementPersonalDecisionCanceled(Stream, (long)_revision, DecisionGeneration,
            decision.Admitted.Intent, null, operation, null, null));
        return Result.Success;
    }

    /// <summary>Releases the content pin of the materialized decision and advances the decision pointer.</summary>
    public Result MaterializePersonalDecision(OperationProvenance operation)
    {
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        ArgumentNullException.ThrowIfNull(operation);
        if (DecisionOf(operation.RootOperationId) is not { } decision || operation.Owner != Stream ||
            decision.Authorized is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only an authorized personal decision of this engagement can be materialized.", true));
        if (decision.Materialized is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The personal decision is already materialized with other terms.", false));
        RaiseEvent(new EngagementPersonalDecisionMaterialized(Stream, (long)_revision, DecisionGeneration,
            decision.Admitted.Intent, null, operation, null, null));
        return Result.Success;
    }

    /// <summary>Checks the acceptance or staff assignment admission guards without raising anything.</summary>
    public Result PrepareOperation(Uuid operationId, EngagementIndependenceOperationKind kind, long expectedRevision)
    {
        if (!Exists)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The client service engagement was not found."));
        if (_operations.ContainsKey(operationId))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Acceptance identities are immutable and cannot be reused."));
        if (expectedRevision != (long)_revision)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Reload the engagement revision before accepting it or assigning its staff."));
        if (HasPendingPersonalDecision)
            return PendingDecisionFailure();
        if (HasPendingOperation)
            return PendingOperationFailure();
        return kind == EngagementIndependenceOperationKind.Acceptance
            ? _status == "draft"
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "Only an open draft engagement can be accepted."))
            : _status == "accepted"
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "Only an accepted engagement takes actual staff assignments."));
    }

    /// <summary>
    ///     Admits the acceptance or staff assignment as the root native fact of its operation. The frozen intent must
    ///     have captured exactly the content revision this owner holds, otherwise the engagement changed while the
    ///     intent was prepared.
    /// </summary>
    public Result AdmitOperation(OperationProvenance operation, ImmutableContentRef intent, ulong intentRevision,
        ImmutableContentRef firstResponse, EngagementIndependenceOperationKind kind)
    {
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(firstResponse);
        var commandKind = kind == EngagementIndependenceOperationKind.Acceptance ? AcceptKind : AssignKind;
        if (operation.Owner != Stream || operation.LogicalId != operation.RootOperationId ||
            operation.CommandKind != commandKind || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An acceptance or staff assignment is the logical operation of its own command on its own engagement."));
        var prepared = PrepareOperation(operation.LogicalId, kind, (long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        if (intentRevision != _revision)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The engagement changed while the operation was prepared; reload and retry.", true));
        if (kind == EngagementIndependenceOperationKind.Acceptance)
            RaiseEvent(new ServiceEngagementAcceptanceRequested(Stream, (long)_revision, AcceptanceGeneration, intent,
                null, operation, firstResponse, null));
        else
            RaiseEvent(new ServiceEngagementStaffAssignmentRequested(Stream, (long)_revision, AcceptanceGeneration,
                intent, null, operation, firstResponse, null));
        return Result.Success;
    }

    /// <summary>Authorizes the admitted operation irreversibly; the caller proved every reservation and source use.</summary>
    public Result AuthorizeOperation(OperationProvenance operation, DateTimeOffset decisionAt,
        ImmutableContentRef authorizationProofs)
    {
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(authorizationProofs);
        if (OperationOf(operation.RootOperationId) is not { } record || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only an admitted acceptance or staff assignment of this engagement can be authorized.", true));
        if (record.Rejected is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The operation was rejected; authorization is fenced.", false));
        if (record.Authorized is not null || record.Completed is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The operation is already authorized with other terms.", false));
        RaiseEvent(new EngagementPracticeCommitmentsAuthorized(Stream, (long)_revision, AcceptanceGeneration,
            record.Intent, null, operation, decisionAt, authorizationProofs, null, null));
        return Result.Success;
    }

    /// <summary>Rejects the admitted operation before authorization; an authorized operation recovers forward.</summary>
    public Result RejectOperation(OperationProvenance operation, EngagementIndependenceOperationKind kind)
    {
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        ArgumentNullException.ThrowIfNull(operation);
        if (OperationOf(operation.RootOperationId) is not { } record || operation.Owner != Stream ||
            record.Kind != kind)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only an admitted operation of this engagement can be rejected.", true));
        if (record.Authorized is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The operation is authorized and irreversible; it recovers forward.", false));
        if (record.Rejected is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The operation is already rejected with other terms.", false));
        if (kind == EngagementIndependenceOperationKind.Acceptance)
            RaiseEvent(new ServiceEngagementAcceptanceRejected(Stream, (long)_revision, AcceptanceGeneration,
                record.Intent, null, operation, null, null));
        else
            RaiseEvent(new ServiceEngagementStaffAssignmentRejected(Stream, (long)_revision, AcceptanceGeneration,
                record.Intent, null, operation, null, null));
        return Result.Success;
    }

    /// <summary>Records the accepted engagement after the irreversible authorization and advances the revision.</summary>
    public Result RecordAcceptance(OperationProvenance operation, ImmutableContentRef acceptanceContent,
        ImmutableContentRef operationResult)
    {
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(acceptanceContent);
        ArgumentNullException.ThrowIfNull(operationResult);
        var ready = ReadyToComplete(operation, EngagementIndependenceOperationKind.Acceptance);
        if (!ready.IsSuccess)
            return ready;
        RaiseEvent(new ServiceEngagementAcceptanceRecorded(_tenantId, operation.RootOperationId, AcceptKind,
            acceptanceContent, operation, _revision + 1, null, operationResult));
        return Result.Success;
    }

    /// <summary>Records the actual staff assignment after the irreversible authorization and advances the revision.</summary>
    public Result RecordStaffAssignment(OperationProvenance operation, ImmutableContentRef assignedStaffContent,
        DateOnly effectiveFrom, ImmutableContentRef partnerStaffContent, ImmutableContentRef partnerDesignationContent,
        long partnerDutyContinuityStartRevision, ActorReference actor, DateTimeOffset recordedAt,
        ImmutableContentRef operationResult)
    {
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(assignedStaffContent);
        ArgumentNullException.ThrowIfNull(partnerStaffContent);
        ArgumentNullException.ThrowIfNull(partnerDesignationContent);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(operationResult);
        var ready = ReadyToComplete(operation, EngagementIndependenceOperationKind.StaffAssignment);
        if (!ready.IsSuccess)
            return ready;
        RaiseEvent(new ServiceEngagementStaffAssigned(_tenantId, operation.RootOperationId, EngagementId,
            assignedStaffContent, effectiveFrom, partnerStaffContent, partnerDesignationContent,
            partnerDutyContinuityStartRevision, AssignKind, actor, recordedAt, operation, _revision + 1, null,
            operationResult));
        return Result.Success;
    }

    /// <summary>Checks the directory receipt guards without raising anything.</summary>
    public Result PrepareDirectoryReevaluation() => !Exists
        ? Result.Failure(new RequestError(RequestErrorKind.NotFound, "The client service engagement was not found."))
        : _status == "accepted"
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A directory receipt is recorded only against an accepted engagement.", false));

    /// <summary>Records one denial-only directory receipt as the root native fact of its operation.</summary>
    public Result RecordDirectoryReevaluation(OperationProvenance operation, ImmutableContentRef content,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(firstResponse);
        ArgumentNullException.ThrowIfNull(operationResult);
        if (operation.Owner != Stream || operation.LogicalId != operation.RootOperationId)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A directory receipt is the logical operation of its own command on its own engagement."));
        var prepared = PrepareDirectoryReevaluation();
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new DirectoryIndependenceReevaluated(_tenantId, content, operation, _revision + 1, firstResponse,
            operationResult));
        return Result.Success;
    }

    Result ReadyToComplete(OperationProvenance operation, EngagementIndependenceOperationKind kind)
    {
        if (OperationOf(operation.RootOperationId) is not { } record || operation.Owner != Stream ||
            record.Kind != kind)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only an admitted operation of this engagement can be recorded.", true));
        if (record.Authorized is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The operation is not authorized yet; it can never be recorded without the authorization.", true));
        if (record.Completed is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The operation is already recorded with other terms.", false));
        return _status == (kind == EngagementIndependenceOperationKind.Acceptance ? "draft" : "accepted")
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The engagement status no longer admits this operation.", false));
    }

    void ApplyAdmitted(DomainEvent fact, EventStreamAddress owner, long revision, long generation,
        ImmutableContentRef intent, ImmutableContentRef? firstResponse, string commandKind, string requiredStatus,
        EngagementIndependenceOperationKind kind)
    {
        Acceptance.Observe(fact);
        var operation = ((INativeOperationFact)fact).Operation;
        if (owner != Stream || operation.Owner != Stream || operation.LogicalId != operation.RootOperationId ||
            operation.CommandKind != commandKind || revision != (long)_revision || generation != AcceptanceGeneration ||
            _status != requiredStatus || firstResponse is null || HasPendingOperation || HasPendingPersonalDecision ||
            !_operations.TryAdd(operation.RootOperationId, new EngagementAcceptanceRecord(fact, kind) { Intent = intent }))
            throw new InvalidOperationException(
                "An acceptance or staff assignment is admitted once, alone, at the current content revision.");
        if (operation.RootOperationId == _selectedRootId)
            _selectedOperation = _operations[operation.RootOperationId];
    }

    void ApplyRejected(DomainEvent fact, EventStreamAddress owner, ImmutableContentRef intent,
        EngagementIndependenceOperationKind kind)
    {
        Acceptance.Observe(fact);
        var operation = ((INativeOperationFact)fact).Operation;
        if (OperationOf(operation.RootOperationId) is not { } record || owner != Stream ||
            record.Kind != kind || intent != record.Intent || record.Authorized is not null || !record.IsPending)
            throw new InvalidOperationException("Only an unauthorized, pending operation is rejected once.");
        record.Rejected = fact;
        _operations.Remove(operation.RootOperationId);
    }

    EngagementAcceptanceRecord CompleteOperation(OperationProvenance operation, Uuid tenantId, Uuid operationId,
        ulong revision, ImmutableContentRef? result, EngagementIndependenceOperationKind kind, string requiredStatus)
    {
        if (tenantId != _tenantId || operation.Owner != Stream || operationId != operation.RootOperationId ||
            OperationOf(operation.RootOperationId) is not { } record || record.Kind != kind ||
            record.Authorized is null || !record.IsPending || revision != _revision + 1 || _status != requiredStatus ||
            result is null)
            throw new InvalidOperationException(
                "Only an authorized, pending operation of its own engagement is recorded once, in order.");
        return record;
    }

    static Result PendingOperationFailure() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "An acceptance or staff assignment holds the engagement content; retry after it is recorded or rejected.",
        true));

    static Result PendingDecisionFailure() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "A personal decision holds the engagement content; retry after it is materialized or canceled.", true));
}
