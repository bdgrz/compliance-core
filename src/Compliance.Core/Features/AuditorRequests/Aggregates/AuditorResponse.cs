using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Aggregates;

/// <summary>
///     One response of a request, identified by its request and response. It is materialized from the request's
///     admission at revision one; draft revision, submission, disclosure review, delivery and external disposition are
///     its own facts, each at the next revision. A resolution holds it through <see cref="SourceUseReservations" />: an
///     active use does not advance the public revision but blocks changes until it is settled, and a settlement for a
///     resolved parent installs the closed-generation fence that releasing the temporary use never removes. Reducers never
///     read content or another owner.
/// </summary>
public sealed class AuditorResponse : Aggregate, ISourceUseOwner
{
    /// <summary>Stream area of every auditor response (the realm is the tenant).</summary>
    public const string Area = "auditor-responses";

    /// <summary>The identity owner type of an auditor response.</summary>
    public const string OwnerType = "AuditorResponse";

    const string Draft = "draft";
    const string Submitted = "submitted";
    const string Approved = "approved";
    const string Rejected = "rejected";
    const string Delivered = "delivered";

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _requestId;
    readonly Uuid _responseId;
    long? _closedByGeneration;
    AuditResponseDraftCreated? _created;
    DomainEvent? _latest;
    AuditorResponseDispositionRecorded? _disposition;
    AuditorResponseDelivered? _delivery;
    string _status = string.Empty;
    Uuid _authorMemberId = Uuid.Empty;
    long _version;
    long _revision;

    /// <summary>Opens one response, selecting the logical operation to resolve.</summary>
    public AuditorResponse(Uuid tenantId, Uuid programId, Uuid requestId, Uuid responseId, Uuid selectedLogicalId = default)
        : base(IdentityOf(tenantId, programId, requestId, responseId),
            new EventStreamAddress(tenantId.ToString(), Area,
                IdentityOf(tenantId, programId, requestId, responseId).ToString()))
    {
        (_tenantId, _programId, _requestId, _responseId) = (tenantId, programId, requestId, responseId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        Uses = new SourceUseReservations(Stream);
        On<AuditResponseDraftCreated>(Apply);
        On<AuditResponseDraftRevised>(Apply);
        On<AuditorResponseSubmitted>(Apply);
        On<AuditorResponseDisclosureReviewed>(Apply);
        On<AuditorResponseDelivered>(Apply);
        On<AuditorResponseDispositionRecorded>(Apply);
        On<AuditorResponseRequestResolutionUseAuthorized>(granted =>
        {
            Acceptance.Observe(granted);
            if (_created is null || granted.Revision != _revision)
                throw new InvalidOperationException(
                    "A resolution use is granted on a materialized response at its held revision.");
            Uses.ApplyGranted(SourceUsePurpose.RequestResolution, granted);
        });
        On<AuditorResponseRequestResolutionUseSettled>(settled =>
        {
            Acceptance.Observe(settled);
            Uses.ApplySettled(SourceUsePurpose.RequestResolution, settled);
            if (settled.Disposition == SourceUseSettlementDisposition.ParentClosed)
                _closedByGeneration ??= settled.Generation;

        });
    }

    /// <summary>The identity of the response of a request.</summary>
    public static Uuid IdentityOf(Uuid tenantId, Uuid programId, Uuid requestId, Uuid responseId) =>
        AggregateIdentity.Create(OwnerType, tenantId, programId, requestId, responseId);

    /// <inheritdoc />
    public SourceUseReservations Uses { get; }

    /// <inheritdoc />
    public NativeAcceptance Acceptance { get; }

    /// <summary>The program the response belongs to.</summary>
    public Uuid ProgramId => _programId;

    /// <summary>The request this response belongs to.</summary>
    public Uuid RequestId => _requestId;

    /// <summary>The response identity.</summary>
    public Uuid ResponseId => _responseId;

    /// <summary>The logical revision: zero before materialization, one at the draft, plus one per content fact.</summary>
    public long Revision => _revision;

    /// <summary>True once the response was materialized.</summary>
    public bool IsMaterialized => _created is not null;

    /// <summary>The materialization fact, once recorded.</summary>
    public AuditResponseDraftCreated? Created => _created;

    /// <summary>The business version of the current draft, submission or delivery; zero before materialization.</summary>
    public long BusinessVersion => _version;

    /// <summary>The status of the current version: draft, submitted, approved, rejected or delivered.</summary>
    public string Status => _status;

    /// <summary>The member who authored the current version.</summary>
    public Uuid AuthorMemberId => _authorMemberId;

    /// <summary>The intentional delivery of the current version, once recorded.</summary>
    public AuditorResponseDelivered? Delivery => _delivery;

    /// <summary>The external disposition of the delivered version, once recorded.</summary>
    public AuditorResponseDispositionRecorded? Disposition => _disposition;

    /// <summary>The latest content fact: the last draft, submission, review, delivery or disposition.</summary>
    public DomainEvent? Latest => _latest;

    /// <summary>The first resolved parent generation whose verified settlement permanently fenced this response.</summary>
    public long? ClosedByGeneration => _closedByGeneration;

    /// <summary>The tenant of this response owner.</summary>
    public Uuid TenantId => _tenantId;

    /// <summary>The exact latest immutable content fact that a resolution may pin.</summary>
    public DomainEvent? CurrentContentFactById(Uuid eventId) => _latest?.Metadata.EventId == eventId ? _latest : null;

    /// <summary>Checks that the response can be materialized.</summary>
    public Result PrepareMaterialize() => _created is null
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict, "The auditor response is already materialized."));

    /// <summary>Materializes the admitted response draft as a receiver fact at revision one.</summary>
    public Result Materialize(OperationProvenance operation, ImmutableContentRef intent, Uuid authorMemberId,
        DateTimeOffset at, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        var prepared = PrepareMaterialize();
        if (!prepared.IsSuccess)
            return prepared;
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A materialization is recorded on the response's own stream."));
        RaiseEvent(new AuditResponseDraftCreated(_tenantId, _responseId, 1, authorMemberId, intent, at, operation, null,
            operationResult));
        return Result.Success;
    }

    /// <summary>Checks that a draft or rejected version can be revised at the expected revision.</summary>
    public Result PrepareRevise(long expectedRevision)
    {
        var guard = PrepareWrite(expectedRevision);
        if (!guard.IsSuccess)
            return guard;
        return _delivery is not null || _status is not (Draft or Rejected)
            ? Fail(CommandFailure.StateConflict(
                "Only a draft or rejected response version can be revised; delivered versions are immutable."))
            : Result.Success;
    }

    /// <summary>Checks that the current draft version can be submitted at the expected revision.</summary>
    public Result PrepareSubmit(long expectedRevision, long version)
    {
        var guard = PrepareVersioned(expectedRevision, version);
        if (!guard.IsSuccess)
            return guard;
        return _status != Draft
            ? Fail(CommandFailure.StateConflict("Only a draft with a bounded submission note can be submitted."))
            : Result.Success;
    }

    /// <summary>Checks that the current submitted version can be reviewed independently at the expected revision.</summary>
    public Result PrepareReview(long expectedRevision, long version, Uuid reviewerMemberId)
    {
        var guard = PrepareVersioned(expectedRevision, version);
        if (!guard.IsSuccess)
            return guard;
        if (_authorMemberId == reviewerMemberId)
            return Fail(CommandFailure.StateConflict("A response author cannot review their own response."));
        return _status != Submitted
            ? Fail(CommandFailure.StateConflict("Only an unreviewed submitted version can be reviewed."))
            : Result.Success;
    }

    /// <summary>Checks that the exact current version can be delivered at the expected revision.</summary>
    public Result PrepareDeliver(long expectedRevision, long version, bool requiresInternalReview)
    {
        var guard = PrepareVersioned(expectedRevision, version);
        if (!guard.IsSuccess)
            return guard;
        if (_delivery is not null)
            return Fail(CommandFailure.StateConflict("A response delivery receipt is immutable."));
        return _status is not (Submitted or Approved) || requiresInternalReview && _status != Approved
            ? Fail(CommandFailure.StateConflict(
                "The exact response version must be submitted and approved when internal review is required."))
            : Result.Success;
    }

    /// <summary>Checks that the delivered version can take its external disposition at the expected revision.</summary>
    public Result PrepareDisposition(long expectedRevision, long version)
    {
        var guard = PrepareWrite(expectedRevision);
        if (!guard.IsSuccess)
            return guard;
        if (_delivery is null || _delivery.Version != version)
            return Fail(CommandFailure.StateConflict(
                "A disposition must refer to the exact intentionally delivered response version."));
        return _disposition is not null
            ? Fail(CommandFailure.StateConflict("A response disposition is immutable."))
            : Result.Success;
    }

    /// <summary>Records the next draft version as the root native fact of its operation.</summary>
    public Result Revise(OperationProvenance operation, ImmutableContentRef version, Uuid authorMemberId,
        DateTimeOffset at, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        var prepared = PrepareOwnWrite(operation, PrepareRevise);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AuditResponseDraftRevised(_tenantId, _responseId, _revision + 1, authorMemberId, version, at,
            operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Records the submission of the exact draft version as the root native fact of its operation.</summary>
    public Result Submit(OperationProvenance operation, long version, ImmutableContentRef submission,
        Uuid actorMemberId, DateTimeOffset at, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        var prepared = PrepareOwnWrite(operation, expected => PrepareSubmit(expected, version));
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AuditorResponseSubmitted(_tenantId, _responseId, _revision + 1, actorMemberId, submission, at,
            operation, version, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Records the independent disclosure review of the exact submitted version as a root native fact.</summary>
    public Result Review(OperationProvenance operation, long version, string outcome, ImmutableContentRef review,
        Uuid reviewerMemberId, DateTimeOffset at, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        var prepared = PrepareOwnWrite(operation, expected => PrepareReview(expected, version, reviewerMemberId));
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AuditorResponseDisclosureReviewed(_tenantId, _responseId, _revision + 1, reviewerMemberId, review,
            at, operation, version, outcome, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Records the intentional delivery of the exact version as the root native fact of its operation.</summary>
    public Result Deliver(OperationProvenance operation, long version, bool requiresInternalReview,
        ImmutableContentRef delivery, Uuid actorMemberId, DateTimeOffset at, ImmutableContentRef firstResponse,
        ImmutableContentRef operationResult)
    {
        var prepared = PrepareOwnWrite(operation, expected => PrepareDeliver(expected, version, requiresInternalReview));
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AuditorResponseDelivered(_tenantId, _responseId, _revision + 1, actorMemberId, delivery, at,
            operation, version, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Records the external disposition of the delivered version as the root native fact of its operation.</summary>
    public Result RecordDisposition(OperationProvenance operation, long version, string outcome,
        ImmutableContentRef disposition, Uuid actorMemberId, DateTimeOffset at, ImmutableContentRef firstResponse,
        ImmutableContentRef operationResult)
    {
        var prepared = PrepareOwnWrite(operation, expected => PrepareDisposition(expected, version));
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AuditorResponseDispositionRecorded(_tenantId, _responseId, _revision + 1, actorMemberId,
            disposition, at, operation, version, outcome, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Holds this exact response for a request resolution; the revision does not advance.</summary>
    public Result AuthorizeResolutionUse(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (_created is null || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only a materialized response can be held, on its own stream.", true));
        Uses.RequireForAppend(SourceUsePurpose.RequestResolution, operation, generation, Version, CommittedStreamPosition);
        RaiseEvent(new AuditorResponseRequestResolutionUseAuthorized(Stream, _revision, generation, intent, null,
            operation, null, null));
        return Result.Success;
    }

    /// <summary>Settles a resolution use, or fences its key when no grant exists yet.</summary>
    public Result SettleResolutionUse(OperationProvenance operation, ImmutableContentRef intent, long generation,
        SourceUseSettlementDisposition disposition)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A settlement is recorded on the response's own stream."));
        Uses.RequireForAppend(SourceUsePurpose.RequestResolution, operation, generation, Version, CommittedStreamPosition);
        RaiseEvent(new AuditorResponseRequestResolutionUseSettled(Stream, _revision, generation, intent, null,
            operation, null, null, disposition));
        return Result.Success;
    }

    void Apply(AuditResponseDraftCreated ev)
    {
        Acceptance.Observe(ev);
        if (_created is not null || ev.TenantId != _tenantId || ev.SubjectId != _responseId || ev.Revision != 1 ||
            ev.Operation.Owner != Stream || ev.ActorMemberId is not { } author)
            throw new InvalidOperationException("A response is materialized once, at revision one, on its own stream.");
        _created = ev;
        _status = Draft;
        _authorMemberId = author;
        _version = 1;
        Advance(ev, 1);
    }

    void Apply(AuditResponseDraftRevised ev)
    {
        Acceptance.Observe(ev);
        EnsureContent(ev.TenantId, ev.SubjectId, ev.Revision, ev.Operation);
        if (_delivery is not null || _status is not (Draft or Rejected) || ev.ActorMemberId is not { } author)
            throw new InvalidOperationException("Only a draft or rejected version is revised into a new draft.");
        _status = Draft;
        _authorMemberId = author;
        _version++;
        Advance(ev, ev.Revision);
    }

    void Apply(AuditorResponseSubmitted ev)
    {
        Acceptance.Observe(ev);
        EnsureContent(ev.TenantId, ev.SubjectId, ev.Revision, ev.Operation);
        if (_status != Draft || ev.Version != _version)
            throw new InvalidOperationException("Only the exact draft version is submitted.");
        _status = Submitted;
        Advance(ev, ev.Revision);
    }

    void Apply(AuditorResponseDisclosureReviewed ev)
    {
        Acceptance.Observe(ev);
        EnsureContent(ev.TenantId, ev.SubjectId, ev.Revision, ev.Operation);
        if (_status != Submitted || ev.Version != _version || ev.Outcome is not (Approved or Rejected) ||
            ev.ActorMemberId == _authorMemberId)
            throw new InvalidOperationException("Only the exact submitted version is reviewed, by an independent reviewer.");
        _status = ev.Outcome;
        Advance(ev, ev.Revision);
    }

    void Apply(AuditorResponseDelivered ev)
    {
        Acceptance.Observe(ev);
        EnsureContent(ev.TenantId, ev.SubjectId, ev.Revision, ev.Operation);
        if (_delivery is not null || _status is not (Submitted or Approved) || ev.Version != _version)
            throw new InvalidOperationException("Only the exact submitted or approved version is delivered, once.");
        _status = Delivered;
        _delivery = ev;
        Advance(ev, ev.Revision);
    }

    void Apply(AuditorResponseDispositionRecorded ev)
    {
        Acceptance.Observe(ev);
        EnsureContent(ev.TenantId, ev.SubjectId, ev.Revision, ev.Operation);
        if (_delivery is null || _disposition is not null || ev.Version != _delivery.Version)
            throw new InvalidOperationException("Only the delivered version takes its disposition, once.");
        _disposition = ev;
        Advance(ev, ev.Revision);
    }

    void EnsureContent(Uuid tenantId, Uuid subjectId, long revision, OperationProvenance operation)
    {
        if (_created is null || tenantId != _tenantId || subjectId != _responseId || revision != _revision + 1 ||
            operation.Owner != Stream)
            throw new InvalidOperationException("A response fact follows the materialization at the next revision.");
    }

    void Advance(DomainEvent fact, long revision)
    {
        _revision = revision;
        _latest = fact;

    }

    Result PrepareWrite(long expectedRevision)
    {
        if (_created is null)
            return Fail(CommandFailure.MissingRecord("The auditor response was not found."));
        if (_closedByGeneration is not null)
            return Fail(CommandFailure.StateConflict(
                "The auditor response is fenced by a resolved request generation."));
        if (Uses.HasActiveUse)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A request resolution holds this auditor response; retry when it settles.", true));
        return expectedRevision != _revision
            ? Fail(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("auditor response", _revision)))
            : Result.Success;
    }

    Result PrepareVersioned(long expectedRevision, long version)
    {
        var guard = PrepareWrite(expectedRevision);
        if (!guard.IsSuccess)
            return guard;
        return version != _version
            ? Fail(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("auditor response", _version)))
            : Result.Success;
    }

    Result PrepareOwnWrite(OperationProvenance operation, Func<long, Result> prepare)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A response write is a root operation of its own response with an expected revision."));
        return prepare((long)expected);
    }

    static Result Fail(CommandFailure failure) => Result.Failure(failure.ToRequestError());
    protected override void Apply(DomainEvent ev)
    {
        base.Apply(ev);
        Uses.ObserveOwnerFact(ev);
    }
}
