using System.Globalization;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     One control occurrence for one canonical business window: its single materialization, every attestation
///     version and every review decision. The identity derives from the tenant, program, control and window only, so
///     the stream exists once however many generators select the window. The logical revision counts the facts of
///     this occurrence only. The rules over attestation and review content are decided by the caller from
///     <see cref="ControlOccurrenceState" /> and re-checked here against the revision.
/// </summary>
public sealed class ControlOccurrence : Aggregate
{
    /// <summary>Stream area of every control occurrence (the realm is the tenant).</summary>
    public const string Area = "control-occurrences";

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _controlId;
    readonly List<DomainEvent> _facts = [];
    ControlOccurrenceOpened? _opened;
    long _revision;
    int _attestations;

    /// <summary>Opens the occurrence with a known id, selecting the logical operation to resolve.</summary>
    public ControlOccurrence(Uuid tenantId, Uuid programId, Uuid controlId, Uuid occurrenceId,
        Uuid selectedLogicalId = default)
        : base(occurrenceId, new EventStreamAddress(tenantId.ToString(), Area, occurrenceId.ToString()))
    {
        (_tenantId, _programId, _controlId) = (tenantId, programId, controlId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<ControlOccurrenceOpened>(Apply);
        On<ControlOccurrenceAttested>(Apply);
        On<ControlOccurrenceReviewed>(Apply);
    }

    /// <summary>Opens the occurrence of a business window.</summary>
    public ControlOccurrence(Uuid tenantId, Uuid programId, Uuid controlId, ControlBusinessWindow window,
        Uuid selectedLogicalId = default)
        : this(tenantId, programId, controlId, ControlOccurrenceIdentity.Of(tenantId, programId, controlId, window),
            selectedLogicalId)
    {
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The program this occurrence belongs to.</summary>
    public Uuid ProgramId => _programId;

    /// <summary>The control this occurrence belongs to.</summary>
    public Uuid ControlId => _controlId;

    /// <summary>The logical revision: the number of facts of this occurrence.</summary>
    public long Revision => _revision;

    /// <summary>The materialization fact, once recorded.</summary>
    public ControlOccurrenceOpened? Opened => _opened;

    /// <summary>The number of attestation versions recorded so far.</summary>
    public int AttestationCount => _attestations;

    /// <summary>Every fact of the occurrence in order.</summary>
    public IReadOnlyList<DomainEvent> Facts => _facts;

    /// <summary>The deterministic id of the next attestation version.</summary>
    public Uuid NextAttestationId => Uuid.CreateVersion5(Id, "attestation-" +
        (_attestations + 1).ToString(CultureInfo.InvariantCulture));

    /// <summary>Checks the direct opening guards without raising anything.</summary>
    public Result PrepareOpen(long expectedRevision)
    {
        if (_opened is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The occurrence for this business window is already materialized."));
        return expectedRevision == _revision ? Result.Success : Stale();
    }

    /// <summary>Opens the occurrence as a direct root native fact of its operation.</summary>
    public Result Open(OperationProvenance operation, ControlOccurrenceTerms terms, ImmutableContentRef creationIntent,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(creationIntent);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An opening is a root operation of its own occurrence with an expected revision."));
        var prepared = PrepareOpen((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseOpened(operation, terms, creationIntent, firstResponse, operationResult);
        return Result.Success;
    }

    /// <summary>Materializes the occurrence as a receiver fact; the caller proved it was not materialized.</summary>
    public Result Materialize(OperationProvenance operation, ControlOccurrenceTerms terms,
        ImmutableContentRef creationIntent)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(creationIntent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A materialization is recorded on the occurrence's own stream."));
        if (_opened is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The occurrence for this business window is already materialized."));
        RaiseOpened(operation, terms, creationIntent, null, null);
        return Result.Success;
    }

    /// <summary>Checks the attestation guards without raising anything; an unmaterialized occurrence is at revision zero.</summary>
    public Result PrepareProof(long expectedRevision, bool mayMaterialize)
    {
        if (_opened is null && !mayMaterialize)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The control occurrence was not found."));
        return expectedRevision == _revision ? Result.Success : Stale();
    }

    /// <summary>
    ///     Records an attestation or correction as the root native fact of its operation. An expected occurrence that
    ///     is not materialized yet is materialized first, in the same batch, from <paramref name="materialize" />.
    /// </summary>
    public Result Attest(OperationProvenance operation, ImmutableContentRef attestation,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult, ControlOccurrenceMaterialization? materialize)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(attestation);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A proof is a root operation of its own occurrence with an expected revision."));
        var prepared = PrepareProof((long)expected, materialize is not null);
        if (!prepared.IsSuccess)
            return prepared;
        if (_opened is null)
            RaiseOpened(materialize!.Operation, materialize.Terms, materialize.CreationIntent, null, null);
        RaiseEvent(new ControlOccurrenceAttested(_tenantId, _programId, _controlId, Id, _revision + 1, attestation,
            operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Records a review decision as the root native fact of its operation.</summary>
    public Result Review(OperationProvenance operation, ImmutableContentRef review, ImmutableContentRef firstResponse,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(review);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A review is a root operation of its own occurrence with an expected revision."));
        var prepared = PrepareProof((long)expected, false);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new ControlOccurrenceReviewed(_tenantId, _programId, _controlId, Id, _revision + 1, review,
            operation, firstResponse, operationResult));
        return Result.Success;
    }

    void RaiseOpened(OperationProvenance operation, ControlOccurrenceTerms terms, ImmutableContentRef creationIntent,
        ImmutableContentRef? firstResponse, ImmutableContentRef? operationResult) =>
        RaiseEvent(new ControlOccurrenceOpened(_tenantId, _programId, _controlId, Id, _revision + 1, terms.Kind,
            terms.ControlVersionId, terms.PlanVersionId, terms.PeriodStart, terms.PeriodEnd, terms.DueOn,
            terms.Trigger, terms.Assignee, terms.OpenedBy, terms.OpenedAt, operation, terms.Window, creationIntent,
            creationIntent, firstResponse, operationResult));

    void Apply(ControlOccurrenceOpened ev)
    {
        Acceptance.Observe(ev);
        if (_opened is not null || ev.TenantId != _tenantId || ev.ProgramId != _programId ||
            ev.ControlId != _controlId || ev.OccurrenceId != Id || ev.Revision != _revision + 1 ||
            ev.Operation.Owner != Stream || !ev.BusinessWindow.IsValid ||
            ControlOccurrenceIdentity.Of(_tenantId, _programId, _controlId, ev.BusinessWindow) != Id)
            throw new InvalidOperationException(
                "An occurrence is materialized once, on the stream its tenant, program, control and window derive.");
        _opened = ev;
        _revision = ev.Revision;
        _facts.Add(ev);
    }

    void Apply(ControlOccurrenceAttested ev)
    {
        Acceptance.Observe(ev);
        if (_opened is null || ev.TenantId != _tenantId || ev.ProgramId != _programId ||
            ev.ControlId != _controlId || ev.OccurrenceId != Id || ev.Revision != _revision + 1 ||
            ev.Operation.Owner != Stream)
            throw new InvalidOperationException("An attestation follows the materialization at the next revision.");
        _attestations++;
        _revision = ev.Revision;
        _facts.Add(ev);
    }

    void Apply(ControlOccurrenceReviewed ev)
    {
        Acceptance.Observe(ev);
        if (_opened is null || _attestations == 0 || ev.TenantId != _tenantId || ev.ProgramId != _programId ||
            ev.ControlId != _controlId || ev.OccurrenceId != Id || ev.Revision != _revision + 1 ||
            ev.Operation.Owner != Stream)
            throw new InvalidOperationException("A review follows an attestation at the next revision.");
        _revision = ev.Revision;
        _facts.Add(ev);
    }

    Result Stale() => Result.Failure(VersionedRecordRules.StaleRevision("control occurrence", _revision)
        .ToRequestError());
}
