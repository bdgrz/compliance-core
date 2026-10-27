using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>
///     One evidence request, identified by its program and request id: opened at revision one and then fulfilled with a
///     captured artifact or canceled with a rationale, both attributed and final. Risk action completion reviews hold a
///     fulfilled request through <see cref="SourceUseReservations" />: an active use does not advance the public revision
///     but blocks a conflicting write until it is settled. The logical revision counts content facts of this request
///     only.
/// </summary>
public sealed class EvidenceRequest : Aggregate, ISourceUseOwner
{
    /// <summary>Stream area of every evidence request (the realm is the tenant).</summary>
    public const string Area = "evidence-requests";

    /// <summary>The identity owner type of an evidence request.</summary>
    public const string OwnerType = "EvidenceRequest";

    /// <summary>The status of a request that can still be fulfilled or canceled.</summary>
    public const string Open = "open";

    /// <summary>The status of a request answered by an artifact.</summary>
    public const string Fulfilled = "fulfilled";

    /// <summary>The status of a request canceled with a rationale.</summary>
    public const string Cancelled = "cancelled";

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _requestId;
    EvidenceRequestOpened? _opened;
    EvidenceRequestFulfilled? _fulfilled;
    EvidenceRequestCancelled? _cancelled;
    long _revision;

    /// <summary>Opens one request, selecting the logical operation to resolve.</summary>
    public EvidenceRequest(Uuid tenantId, Uuid programId, Uuid requestId, Uuid selectedLogicalId = default)
        : base(IdentityOf(tenantId, programId, requestId),
            new EventStreamAddress(tenantId.ToString(), Area, IdentityOf(tenantId, programId, requestId).ToString()))
    {
        (_tenantId, _programId, _requestId) = (tenantId, programId, requestId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        Uses = new SourceUseReservations(Stream);
        On<EvidenceRequestOpened>(Apply);
        On<EvidenceRequestFulfilled>(Apply);
        On<EvidenceRequestCancelled>(Apply);
        On<EvidenceRequestActionCompletionUseAuthorized>(granted =>
        {
            Acceptance.Observe(granted);
            if (_fulfilled is null || granted.Revision != _revision)
                throw new InvalidOperationException(
                    "An action completion use is granted on a fulfilled request at its held revision.");
            Uses.ApplyGranted(SourceUsePurpose.ActionCompletionReview, granted);
        });
        On<EvidenceRequestActionCompletionUseSettled>(settled =>
        {
            Acceptance.Observe(settled);
            Uses.ApplySettled(SourceUsePurpose.ActionCompletionReview, settled);
        });
    }

    /// <summary>The identity of the request of a program.</summary>
    public static Uuid IdentityOf(Uuid tenantId, Uuid programId, Uuid requestId) =>
        AggregateIdentity.Create(OwnerType, tenantId, programId, requestId);

    /// <inheritdoc />
    public SourceUseReservations Uses { get; }

    /// <inheritdoc />
    public NativeAcceptance Acceptance { get; }

    /// <summary>The request identity.</summary>
    public Uuid RequestId => _requestId;

    /// <summary>The logical revision: zero before the request opened, one when open, two once fulfilled or canceled.</summary>
    public long Revision => _revision;

    /// <summary>True once the request was opened.</summary>
    public bool IsOpened => _opened is not null;

    /// <summary>The opening fact, once recorded.</summary>
    public EvidenceRequestOpened? Opened => _opened;

    /// <summary>The fulfilment fact, once recorded.</summary>
    public EvidenceRequestFulfilled? FulfilledFact => _fulfilled;

    /// <summary>The cancellation fact, once recorded.</summary>
    public EvidenceRequestCancelled? CancelledFact => _cancelled;

    /// <summary>The latest content fact: the fulfilment or cancellation when decided, else the opening.</summary>
    public DomainEvent? Latest => (DomainEvent?)_fulfilled ?? (DomainEvent?)_cancelled ?? _opened;

    /// <summary>The current status, or null before the request opened.</summary>
    public string? Status => _opened is null ? null : _fulfilled is not null ? Fulfilled : _cancelled is not null ? Cancelled : Open;

    /// <summary>The exact original lifecycle content fact; technical source-use history belongs to its verified selection.</summary>
    public DomainEvent? ContentFactById(Uuid eventId) => _opened?.Metadata.EventId == eventId ? _opened
        : _fulfilled?.Metadata.EventId == eventId ? _fulfilled
        : _cancelled?.Metadata.EventId == eventId ? _cancelled : null;

    /// <summary>The public view of the request, or null before it opened.</summary>
    public EvidenceRequestView? View() => _opened is not { } opened
        ? null
        : new EvidenceRequestView(opened.TenantId, opened.ProgramId, opened.EvidenceRequestId, _revision, opened.Title,
            opened.Instructions, opened.OwnerMemberId, opened.DueOn, opened.ControlId, Status!, opened.RequestedBy,
            opened.OpenedAt, _fulfilled?.ArtifactId, _fulfilled?.FulfilledBy, _fulfilled?.FulfilledAt,
            _cancelled?.Rationale, _cancelled?.CancelledBy, _cancelled?.CancelledAt);

    /// <summary>Checks that the request does not exist yet.</summary>
    public Result PrepareOpen() => _opened is null
        ? Result.Success
        : Result.Failure(CommandFailure.StateConflict("The evidence request already exists with different details.")
            .ToRequestError());

    /// <summary>Checks that a fulfilment or cancellation can be recorded at the expected revision.</summary>
    public Result PrepareDecision(long expectedRevision)
    {
        if (_opened is null)
            return Fail(CommandFailure.MissingRecord("The evidence request was not found."));
        if (Uses.HasActiveUse)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A risk action completion review holds this evidence request; retry when it settles.", true));
        if (expectedRevision != _revision)
            return Fail(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("evidence request", _revision)));
        return Status == Open
            ? Result.Success
            : Fail(CommandFailure.StateConflict($"The evidence request is already {Status}."));
    }

    /// <summary>Opens the request as the root native fact of its operation.</summary>
    public Result OpenRequest(OperationProvenance operation, EvidenceRequestTerms terms, ImmutableContentRef firstResponse,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(terms);
        if (operation.Owner != Stream || operation.LogicalId != _requestId)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An opening is the logical operation of its own evidence request."));
        var prepared = PrepareOpen();
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new EvidenceRequestOpened(_tenantId, _programId, _requestId, terms.Title, terms.Instructions,
            terms.OwnerMemberId, terms.DueOn, terms.ControlId, terms.RequestedBy, terms.OpenedAt, operation,
            firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Fulfils the request as the root native fact of its operation.</summary>
    public Result Fulfil(OperationProvenance operation, Uuid artifactId, ActorReference fulfilledBy,
        DateTimeOffset fulfilledAt, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(fulfilledBy);
        var prepared = PrepareOwnDecision(operation);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new EvidenceRequestFulfilled(_tenantId, _programId, _requestId, _revision + 1, artifactId,
            fulfilledBy, fulfilledAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Cancels the request as the root native fact of its operation.</summary>
    public Result Cancel(OperationProvenance operation, string rationale, ActorReference cancelledBy,
        DateTimeOffset cancelledAt, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(cancelledBy);
        var prepared = PrepareOwnDecision(operation);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new EvidenceRequestCancelled(_tenantId, _programId, _requestId, _revision + 1, rationale,
            cancelledBy, cancelledAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Holds this exact fulfilled request for a risk action completion review; the revision does not advance.</summary>
    public Result AuthorizeActionCompletionUse(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (_fulfilled is null || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only a fulfilled evidence request can be held, on its own stream.", true));
        Uses.RequireForAppend(SourceUsePurpose.ActionCompletionReview, operation, generation, Version, CommittedStreamPosition);
        RaiseEvent(new EvidenceRequestActionCompletionUseAuthorized(Stream, _revision, generation, intent, null,
            operation, null, null));
        return Result.Success;
    }

    /// <summary>Settles an action completion use, or fences its key when no grant exists yet.</summary>
    public Result SettleActionCompletionUse(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A settlement is recorded on the request's own stream."));
        Uses.RequireForAppend(SourceUsePurpose.ActionCompletionReview, operation, generation, Version, CommittedStreamPosition);
        RaiseEvent(new EvidenceRequestActionCompletionUseSettled(Stream, _revision, generation, intent, null,
            operation, null, null));
        return Result.Success;
    }

    Result PrepareOwnDecision(OperationProvenance operation)
    {
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A decision is a root operation of its own evidence request with an expected revision."));
        return PrepareDecision((long)expected);
    }

    void Apply(EvidenceRequestOpened ev)
    {
        Acceptance.Observe(ev);
        if (_opened is not null || ev.TenantId != _tenantId || ev.ProgramId != _programId ||
            ev.EvidenceRequestId != _requestId || ev.Operation.Owner != Stream)
            throw new InvalidOperationException("An evidence request is opened once, by its own operation.");
        _opened = ev;
        _revision = 1;
    }

    void Apply(EvidenceRequestFulfilled ev)
    {
        Acceptance.Observe(ev);
        EnsureDecidable(ev.TenantId, ev.ProgramId, ev.EvidenceRequestId, ev.Revision, ev.Operation);
        _fulfilled = ev;
        _revision = ev.Revision;
    }

    void Apply(EvidenceRequestCancelled ev)
    {
        Acceptance.Observe(ev);
        EnsureDecidable(ev.TenantId, ev.ProgramId, ev.EvidenceRequestId, ev.Revision, ev.Operation);
        _cancelled = ev;
        _revision = ev.Revision;
    }

    void EnsureDecidable(Uuid tenantId, Uuid programId, Uuid requestId, long revision, OperationProvenance operation)
    {
        if (_opened is null || _fulfilled is not null || _cancelled is not null || tenantId != _tenantId ||
            programId != _programId || requestId != _requestId || revision != _revision + 1 ||
            operation.Owner != Stream)
            throw new InvalidOperationException("An evidence request is decided once, at the next revision after it opened.");
    }

    static Result Fail(CommandFailure failure) => Result.Failure(failure.ToRequestError());
    protected override void Apply(DomainEvent ev)
    {
        base.Apply(ev);
        Uses.ObserveOwnerFact(ev);
    }
}
