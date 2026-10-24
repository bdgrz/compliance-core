using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The lifecycle of one service engagement: its draft, tentative staff proposals and closure. The logical revision
///     counts accepted changes of this engagement only and never depends on another engagement or on the client's
///     service history. Acceptance, actual assignments and personal decisions are attached by their own facts; this
///     owner keeps the acceptance content of the latest acceptance so a revocation can revise it.
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

    readonly Uuid _tenantId;
    readonly List<ServiceEngagementMutationRecorded> _changes = [];
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
}
