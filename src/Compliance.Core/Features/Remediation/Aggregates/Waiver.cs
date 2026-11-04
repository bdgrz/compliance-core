using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Aggregates;

/// <summary>One independently reviewed, time-bounded exception to one product rule for exact records.</summary>
public sealed class Waiver : Aggregate
{
    public const string Area = "waivers";
    static readonly string[] Rules = ["data_flow_encryption", "control_evaluation_minor_deviation", "control_occurrence"];
    readonly Uuid _tenantId;
    WaiverRequested? _requested;
    WaiverApproved? _approved;
    WaiverRejected? _rejected;
    WaiverRevoked? _revoked;

    public Waiver(Uuid tenantId, Uuid waiverId)
        : base(waiverId, new EventStreamAddress(tenantId.ToString(), Area, waiverId.ToString()))
    {
        _tenantId = tenantId;
        On<WaiverRequested>(ApplyRequested);
        On<WaiverApproved>(ApplyApproved);
        On<WaiverRejected>(ApplyRejected);
        On<WaiverRevoked>(ApplyRevoked);
    }

    public long Revision { get; private set; }

    public Result<WaiverRegistration> Request(RequestWaiver request, ActorReference requester, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Owns(request.TenantId, request.WaiverId))
            return Result<WaiverRegistration>.Failure(Invalid("The request must identify this tenant's waiver."));
        var scope = CanonicalScope(request.Scope);
        if (_requested is { } original)
            return scope is not null && original.Rule == request.Rule && original.Scope.SequenceEqual(scope) &&
                   original.Rationale == request.Rationale?.Trim() && original.CompensatingAction == request.CompensatingAction?.Trim() &&
                   original.ExpiresAt == request.ExpiresAt && SameMember(original.RequestedBy, requester)
                ? Result<WaiverRegistration>.Success(new WaiverRegistration(Id))
                : Result<WaiverRegistration>.Failure(Conflict("The waiver identity already accepted different terms or requester."));
        var error = ValidateRequest(request.Rule, scope, request.Rationale, request.CompensatingAction,
            request.ExpiresAt, requester, now);
        if (error is not null)
            return Result<WaiverRegistration>.Failure(error);
        RaiseEvent(new WaiverRequested(_tenantId, Id, request.Rule, Snapshot(scope!), request.Rationale.Trim(),
            request.CompensatingAction.Trim(), request.ExpiresAt, requester, now));
        return Result<WaiverRegistration>.Success(new WaiverRegistration(Id));
    }

    public Result Review(ReviewWaiver request, ActorReference reviewer, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Owns(request.TenantId, request.WaiverId) || request.CommandId == Uuid.Empty || request.CommandId == Id)
            return Invalid("A review requires this waiver and a distinct command identity.");
        if (_approved is { } approved && approved.CommandId == request.CommandId)
            return request.Approve && approved.OriginalExpectedRevision == request.ExpectedRevision &&
                   approved.Comments == CleanOptional(request.Comments) && SameMember(approved.ApprovedBy, reviewer)
                ? Result.Success : Conflict("The review identity already accepted another decision or reviewer.");
        if (_rejected is { } rejected && rejected.CommandId == request.CommandId)
            return !request.Approve && rejected.OriginalExpectedRevision == request.ExpectedRevision &&
                   rejected.Comments == CleanOptional(request.Comments) && SameMember(rejected.ReviewedBy, reviewer)
                ? Result.Success : Conflict("The review identity already accepted another decision or reviewer.");
        var error = ReviewError(request.ExpectedRevision, reviewer, now);
        if (error is not null)
            return error;
        if (request.Approve)
            RaiseEvent(new WaiverApproved(request.CommandId, _tenantId, Id, Revision + 1,
                request.ExpectedRevision, CleanOptional(request.Comments), reviewer, now));
        else
            RaiseEvent(new WaiverRejected(request.CommandId, _tenantId, Id, Revision + 1,
                request.ExpectedRevision, CleanOptional(request.Comments), reviewer, now));
        return Result.Success;
    }

    public Result Revoke(RevokeWaiver request, ActorReference revokedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Owns(request.TenantId, request.WaiverId) || request.CommandId == Uuid.Empty ||
            request.CommandId == Id || request.CommandId == _approved?.CommandId || request.CommandId == _rejected?.CommandId)
            return Invalid("A revocation requires this waiver and a distinct command identity.");
        if (_revoked is { } original && original.CommandId == request.CommandId)
            return original.OriginalExpectedRevision == request.ExpectedRevision &&
                   original.Reason == request.Reason?.Trim() && SameMember(original.RevokedBy, revokedBy)
                ? Result.Success : Conflict("The revocation identity already accepted another reason or actor.");
        var error = RevocationError(request.ExpectedRevision, request.Reason, revokedBy, now);
        if (error is not null)
            return error;
        RaiseEvent(new WaiverRevoked(request.CommandId, _tenantId, Id, Revision + 1,
            request.ExpectedRevision, request.Reason.Trim(), revokedBy, now));
        return Result.Success;
    }

    public bool Covers(string rule, IReadOnlyList<WaiverScopeRecord> scope, DateTimeOffset at) =>
        _requested is { } requested && _approved is { } approved && _revoked is null &&
        requested.Rule == rule && at >= approved.ApprovedAt && at < requested.ExpiresAt &&
        CanonicalScope(scope) is { } canonical && requested.Scope.SequenceEqual(canonical);

    public WaiverView? ViewOrNull(DateTimeOffset now)
    {
        if (_requested is not { } requested)
            return null;
        var state = _revoked is not null ? "revoked" : _rejected is not null ? "rejected" :
            now >= requested.ExpiresAt ? "expired" : _approved is not null ? "approved" : "requested";
        return new WaiverView(_tenantId, Id, Revision, requested.Rule, Snapshot(requested.Scope),
            requested.Rationale, requested.CompensatingAction, requested.ExpiresAt,
            requested.RequestedBy, requested.RequestedAt, state, Covers(requested.Rule, requested.Scope, now),
            _approved?.ApprovedBy ?? _rejected?.ReviewedBy, _approved?.ApprovedAt ?? _rejected?.ReviewedAt,
            _approved?.Comments ?? _rejected?.Comments, _revoked?.RevokedBy, _revoked?.RevokedAt, _revoked?.Reason);
    }

    RequestError? ReviewError(long expectedRevision, ActorReference reviewer, DateTimeOffset now)
    {
        if (_requested is null)
            return new RequestError(RequestErrorKind.NotFound, "The waiver was not found.");
        if (_approved is not null || _rejected is not null || _revoked is not null)
            return Conflict("Only a requested waiver can be reviewed.");
        if (expectedRevision != Revision)
            return Conflict("The waiver changed before this review.");
        if (!Member(reviewer))
            return new RequestError(RequestErrorKind.Forbidden, "A waiver reviewer requires a member identity.");
        if (SameMember(_requested.RequestedBy, reviewer))
            return new RequestError(RequestErrorKind.Forbidden, "The requester cannot review their own waiver.");
        return now < _requested.RequestedAt || now >= _requested.ExpiresAt
            ? Conflict("The waiver is outside its review window.") : null;
    }

    RequestError? RevocationError(long expectedRevision, string reason, ActorReference actor, DateTimeOffset now)
    {
        if (_approved is null || _revoked is not null)
            return Conflict("Only an approved waiver can be revoked.");
        if (expectedRevision != Revision)
            return Conflict("The waiver changed before this revocation.");
        if (!Member(actor))
            return new RequestError(RequestErrorKind.Forbidden, "A waiver revocation requires a member identity.");
        if (string.IsNullOrWhiteSpace(reason))
            return Invalid("A waiver revocation requires a reason.");
        return now < _approved.ApprovedAt || now >= _requested!.ExpiresAt
            ? Conflict("The waiver is outside its revocation window.") : null;
    }

    static RequestError? ValidateRequest(string rule, IReadOnlyList<WaiverScopeRecord>? scope, string rationale,
        string compensation, DateTimeOffset expiry, ActorReference actor, DateTimeOffset now)
    {
        if (!Rules.Contains(rule, StringComparer.Ordinal))
            return Invalid("The product rule is not waivable.");
        if (scope is null)
            return Invalid("A waiver requires distinct exact record identities and positive revisions.");
        if (string.IsNullOrWhiteSpace(rationale) || string.IsNullOrWhiteSpace(compensation))
            return Invalid("A waiver requires its rationale and compensating action.");
        if (!Member(actor))
            return new RequestError(RequestErrorKind.Forbidden, "A waiver requester requires a member identity.");
        return now == default || expiry <= now ? Invalid("A waiver expiry must be after its request time.") : null;
    }

    void ApplyRequested(WaiverRequested fact)
    {
        var scope = CanonicalScope(fact.Scope);
        if (!Owns(fact.TenantId, fact.WaiverId) || _requested is not null ||
            ValidateRequest(fact.Rule, scope, fact.Rationale, fact.CompensatingAction, fact.ExpiresAt,
                fact.RequestedBy, fact.RequestedAt) is not null ||
            scope is null || !fact.Scope.SequenceEqual(scope) ||
            fact.Rationale != fact.Rationale.Trim() || fact.CompensatingAction != fact.CompensatingAction.Trim())
            throw new InvalidOperationException("A waiver request must create its exact tenant owner with canonical valid terms.");
        _requested = fact with { Scope = Snapshot(scope) };
        Revision = 1;
    }

    void ApplyApproved(WaiverApproved fact)
    {
        if (!Owns(fact.TenantId, fact.WaiverId) || fact.CommandId == Uuid.Empty || fact.CommandId == Id ||
            fact.Revision != Revision + 1 || fact.Comments != CleanOptional(fact.Comments) ||
            ReviewError(fact.OriginalExpectedRevision, fact.ApprovedBy, fact.ApprovedAt) is not null)
            throw new InvalidOperationException("A waiver approval must independently review its current unexpired request.");
        _approved = fact;
        Revision = fact.Revision;
    }

    void ApplyRejected(WaiverRejected fact)
    {
        if (!Owns(fact.TenantId, fact.WaiverId) || fact.CommandId == Uuid.Empty || fact.CommandId == Id ||
            fact.Revision != Revision + 1 || fact.Comments != CleanOptional(fact.Comments) ||
            ReviewError(fact.OriginalExpectedRevision, fact.ReviewedBy, fact.ReviewedAt) is not null)
            throw new InvalidOperationException("A waiver rejection must independently review its current unexpired request.");
        _rejected = fact;
        Revision = fact.Revision;
    }

    void ApplyRevoked(WaiverRevoked fact)
    {
        if (!Owns(fact.TenantId, fact.WaiverId) || fact.CommandId == Uuid.Empty || fact.CommandId == Id ||
            fact.CommandId == _approved?.CommandId || fact.Revision != Revision + 1 ||
            string.IsNullOrWhiteSpace(fact.Reason) || fact.Reason != fact.Reason.Trim() ||
            RevocationError(fact.OriginalExpectedRevision, fact.Reason, fact.RevokedBy, fact.RevokedAt) is not null)
            throw new InvalidOperationException("A waiver revocation must end its current unexpired approval with a reason.");
        _revoked = fact;
        Revision = fact.Revision;
    }

    bool Owns(Uuid tenant, Uuid waiver) => tenant != Uuid.Empty && waiver != Uuid.Empty && tenant == _tenantId && waiver == Id;

    static bool Member(ActorReference? actor) => actor is { Kind: "member" } &&
        Uuid.TryParse(actor.Id, null, out var id) && id != Uuid.Empty;

    static bool SameMember(ActorReference? first, ActorReference? second) =>
        Member(first) && Member(second) && Uuid.TryParse(first!.Id, null, out var firstId) &&
        Uuid.TryParse(second!.Id, null, out var secondId) && firstId == secondId;

    static string? CleanOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    static WaiverScopeRecord[]? CanonicalScope(IReadOnlyList<WaiverScopeRecord>? scope)
    {
        if (scope is not { Count: > 0 } || scope.Any(item => item is null ||
                string.IsNullOrWhiteSpace(item.RecordType) || item.RecordId == Uuid.Empty || item.Revision < 1))
            return null;
        var canonical = scope.Select(item => item with { RecordType = item.RecordType.Trim() })
            .OrderBy(item => item.RecordType, StringComparer.Ordinal)
            .ThenBy(item => item.RecordId.ToString(), StringComparer.Ordinal).ThenBy(item => item.Revision).ToArray();
        return canonical.Distinct().Count() == canonical.Length ? canonical : null;
    }

    static System.Collections.ObjectModel.ReadOnlyCollection<WaiverScopeRecord> Snapshot(IEnumerable<WaiverScopeRecord> scope) =>
        Array.AsReadOnly(scope.ToArray());

    static RequestError Invalid(string message) => new(RequestErrorKind.Validation, message);

    static RequestError Conflict(string message) => new(RequestErrorKind.Conflict, message);
}
