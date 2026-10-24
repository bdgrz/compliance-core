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
///     kind that a late older decision can never regress.
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

    /// <summary>The only generation of a personal decision.</summary>
    public const long DecisionGeneration = 1;

    readonly Uuid _tenantId;
    readonly List<ServiceEngagementMutationRecorded> _changes = [];
    readonly Dictionary<Uuid, EngagementPersonalDecisionRecord> _decisions = [];
    readonly Dictionary<string, EngagementPersonalDecisionRecord> _pointers = [];
    ulong _revision;
    string _status = "none";

    /// <summary>Opens one engagement, selecting the logical operation to resolve.</summary>
    public ServiceEngagementIndependence(Uuid tenantId, Uuid engagementId, Uuid selectedLogicalId = default)
        : base(AggregateIdentity.Create(nameof(ServiceEngagementIndependence), tenantId, engagementId),
            new EventStreamAddress(tenantId.ToString(), Area, engagementId.ToString()))
    {
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
                creating != (_revision == 0) || _status == "closed" ||
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
                revoked.Revision != _revision + 1 || _status != "accepted" || revoked.FirstResponse is null ||
                revoked.OperationResult is null)
                throw new InvalidOperationException(
                    "An actual assignment is revoked on an accepted engagement of its own tenant, in order.");
            _status = revoked.StaffMemberId is null ? "closed" : _status;
            _revision = revoked.Revision;
        });
        On<EngagementPersonalDecisionAdmitted>(admitted =>
        {
            Acceptance.Observe(admitted);
            if (admitted.Owner != Stream || admitted.Operation.Owner != Stream ||
                admitted.Operation.LogicalId != admitted.Operation.RootOperationId ||
                admitted.Operation.CommandKind is not (ManagementKind or PartnerKind) ||
                admitted.Revision != (long)_revision || admitted.Generation != DecisionGeneration ||
                _status != "draft" || admitted.FirstResponse is null ||
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

    /// <summary>The latest revised acceptance content, once an acceptance exists.</summary>
    public ImmutableContentRef? AcceptanceContent { get; private set; }

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
        return _status == "draft"
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only an open draft engagement can be changed."));
    }

    /// <summary>Records one lifecycle change as the root native fact of its operation.</summary>
    public Result Change(string kind, OperationProvenance operation, ImmutableContentRef change,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
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
        return _status == "accepted"
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only an accepted engagement has actual assignments to revoke."));
    }

    /// <summary>Revokes one actual assignment, or closes the accepted engagement when no staff member is named.</summary>
    public Result Revoke(OperationProvenance operation, Uuid? staffMemberId, string reason, ActorReference actor,
        DateTimeOffset recordedAt, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
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
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(authorizationProofs);
        if (!_decisions.TryGetValue(operation.RootOperationId, out var decision) || operation.Owner != Stream)
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
        ArgumentNullException.ThrowIfNull(operation);
        if (!_decisions.TryGetValue(operation.RootOperationId, out var decision) || operation.Owner != Stream)
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
        ArgumentNullException.ThrowIfNull(operation);
        if (!_decisions.TryGetValue(operation.RootOperationId, out var decision) || operation.Owner != Stream ||
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

    static Result PendingDecisionFailure() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "A personal decision holds the engagement content; retry after it is materialized or canceled.", true));
}
