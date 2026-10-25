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
sealed class ServiceEngagementHistoryModel
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

    public EventStreamAddress Stream { get; }
    readonly Uuid _tenantId;
    readonly List<ServiceEngagementMutationRecorded> _changes = [];
    readonly Dictionary<Uuid, EngagementPersonalDecisionRecord> _decisions = [];
    readonly Dictionary<string, EngagementPersonalDecisionRecord> _pointers = [];
    readonly Dictionary<Uuid, EngagementAcceptanceRecord> _operations = [];
    readonly List<ImmutableContentRef> _acceptanceHistory = [];
    readonly List<DirectoryIndependenceReevaluated> _receipts = [];
    ImmutableContentRef? _acceptanceResult;
    ulong _revision;
    string _status = "none";

    /// <summary>Opens one engagement, selecting the logical operation to resolve.</summary>
    public ServiceEngagementHistoryModel(Uuid tenantId, Uuid engagementId)
    {
        Stream = new ServiceEngagementIndependence(tenantId, engagementId).Stream;
        _tenantId = tenantId;
        EngagementId = engagementId;
        Acceptance = new NativeAcceptance(Uuid.Empty);
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
            _changes.Add(change);
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
            _acceptanceHistory.Add(revoked.OperationResult);
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
                    new EngagementPersonalDecisionRecord(admitted, _decisions.Count)))
                throw new InvalidOperationException(
                    "A personal decision is admitted once, on an open draft, at its current content revision.");
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
        });
        On<EngagementPersonalDecisionMaterialized>(materialized =>
        {
            Acceptance.Observe(materialized);
            if (!_decisions.TryGetValue(materialized.Operation.RootOperationId, out var decision) ||
                materialized.Owner != Stream || materialized.Intent != decision.Admitted.Intent ||
                decision.Authorized is null || decision.Materialized is not null)
                throw new InvalidOperationException("Only an authorized personal decision is materialized once.");
            decision.Materialized = materialized;
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
            _status = "accepted";
            _revision = recorded.Revision;
            _acceptanceResult = recorded.OperationResult;
            _acceptanceHistory.Add(recorded.OperationResult!);
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
            _revision = assigned.Revision;
            _acceptanceResult = assigned.OperationResult;
            _acceptanceHistory.Add(assigned.OperationResult!);
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
            _receipts.Add(reevaluated);
            _revision = reevaluated.Revision;
        });
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

    /// <summary>Every accepted lifecycle change of this engagement, in order; the result of each is the view after it.</summary>
    public IReadOnlyList<ServiceEngagementMutationRecorded> Changes => _changes;

    /// <summary>The operation result of the latest lifecycle change, which holds the current draft view.</summary>
    public ImmutableContentRef? LatestResult => _changes.LastOrDefault()?.OperationResult;

    /// <summary>
    ///     The retained result of the latest acceptance, staff assignment or revocation, whose view is the current
    ///     accepted snapshot; null until the engagement is accepted.
    /// </summary>
    public ImmutableContentRef? AcceptanceResult => _acceptanceResult;

    /// <summary>The retained result of every acceptance, staff assignment and revocation, in order.</summary>
    public IReadOnlyList<ImmutableContentRef> AcceptanceHistory => _acceptanceHistory;

    /// <summary>Every directory receipt of this engagement, in order.</summary>
    public IReadOnlyList<DirectoryIndependenceReevaluated> DirectoryReceipts => _receipts;

    /// <summary>Every acceptance and staff assignment ever admitted on this engagement, by operation identity.</summary>
    public IReadOnlyDictionary<Uuid, EngagementAcceptanceRecord> Operations => _operations;

    /// <summary>The acceptance or staff assignment of one identity, once admitted.</summary>
    public EngagementAcceptanceRecord? OperationOf(Uuid operationId) => _operations.GetValueOrDefault(operationId);

    /// <summary>True while an admitted acceptance or staff assignment is neither recorded nor rejected.</summary>
    public bool HasPendingOperation => _operations.Values.Any(operation => operation.IsPending);

    /// <summary>Every personal decision ever admitted on this engagement, by decision identity.</summary>
    public IReadOnlyDictionary<Uuid, EngagementPersonalDecisionRecord> Decisions => _decisions;

    /// <summary>True while an admitted decision is neither materialized nor canceled; it pins the content revision.</summary>
    public bool HasPendingPersonalDecision => _decisions.Values.Any(decision => decision.IsPending);

    /// <summary>The decision of one identity, once admitted.</summary>
    public EngagementPersonalDecisionRecord? DecisionOf(Uuid decisionId) => _decisions.GetValueOrDefault(decisionId);

    /// <summary>
    ///     The materialized decision that currently stands for a kind: the one admitted last among those materialized,
    ///     never an older one delivered late.
    /// </summary>
    public EngagementPersonalDecisionRecord? CurrentDecision(EngagementPersonalDecisionKind kind) =>
        _pointers.GetValueOrDefault(kind == EngagementPersonalDecisionKind.ManagementAcknowledgement
            ? ManagementKind
            : PartnerKind);

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
    }

    void ApplyRejected(DomainEvent fact, EventStreamAddress owner, ImmutableContentRef intent,
        EngagementIndependenceOperationKind kind)
    {
        Acceptance.Observe(fact);
        var operation = ((INativeOperationFact)fact).Operation;
        if (!_operations.TryGetValue(operation.RootOperationId, out var record) || owner != Stream ||
            record.Kind != kind || intent != record.Intent || record.Authorized is not null || !record.IsPending)
            throw new InvalidOperationException("Only an unauthorized, pending operation is rejected once.");
        record.Rejected = fact;
    }

    EngagementAcceptanceRecord CompleteOperation(OperationProvenance operation, Uuid tenantId, Uuid operationId,
        ulong revision, ImmutableContentRef? result, EngagementIndependenceOperationKind kind, string requiredStatus)
    {
        if (tenantId != _tenantId || operation.Owner != Stream || operationId != operation.RootOperationId ||
            !_operations.TryGetValue(operation.RootOperationId, out var record) || record.Kind != kind ||
            record.Authorized is null || !record.IsPending || revision != _revision + 1 || _status != requiredStatus ||
            result is null)
            throw new InvalidOperationException(
                "Only an authorized, pending operation of its own engagement is recorded once, in order.");
        return record;
    }

    readonly Dictionary<Type, Action<DomainEvent>> _handlers = [];
    void On<T>(Action<T> handler) where T : DomainEvent => _handlers.Add(typeof(T), value => handler((T)value));
    public void Observe(DomainEvent fact)
    {
        if (!_handlers.TryGetValue(fact.GetType(), out var apply))
            throw new InvalidOperationException("The engagement history contains an unsupported native fact.");
        apply(fact);
    }
}
