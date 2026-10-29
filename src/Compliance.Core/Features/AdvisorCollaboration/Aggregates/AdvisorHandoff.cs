using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.History;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;

/// <summary>
///     One handoff: its small selection and readiness state, its revocation and closure, and the admission authority
///     for feedback. The logical revision counts every fact of this handoff stream and the generation starts at one
///     and advances on every selection revision. Feedback is admitted here, against an exactly selected version while
///     the handoff is ready, unrevoked and unclosed, and materialized as its own <see cref="AdvisorFeedbackThread" />;
///     revocation, closure and feedback admission share this one stream, so an admission either precedes a revocation
///     or is refused by it. The selection is verified content referenced by the latest selection fact; reducers never
///     read content or another owner.
/// </summary>
public sealed class AdvisorHandoff : Aggregate
{
    /// <summary>Stream area of every handoff (the realm is the tenant).</summary>
    public const string Area = "advisor-handoffs";

    /// <summary>The largest selection one handoff can hold.</summary>
    public const int MaximumSelectionCount = 100;

    /// <summary>The handoff status vocabulary.</summary>
    public const string Draft = "draft";
    public const string ReadyForReview = "ready_for_review";
    public const string Closed = "closed";
    public const string Revoked = "revoked";

    readonly Uuid _tenantId;
    readonly AdvisorReplayCut _cut = new();
    readonly Uuid _handoffId;
    AdvisorHandoffCreated? _createdFact;
    ImmutableContentRef? _manifest;
    Uuid _programId;
    string _status = "missing";
    long _revision;
    long _generation;
    long _admissionCount;
    DateTimeOffset? _updatedAt;
    bool _wasReadyForReview;

    /// <summary>Opens one handoff, selecting the logical operation to resolve.</summary>
    public AdvisorHandoff(Uuid tenantId, Uuid programId, Uuid handoffId, Uuid selectedLogicalId = default)
        : base(handoffId, new EventStreamAddress(tenantId.ToString(), Area, handoffId.ToString()))
    {
        (_tenantId, _programId, _handoffId) = (tenantId, programId, handoffId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<AdvisorHandoffCreated>(Apply);
        On<AdvisorHandoffSelectionRevised>(Apply);
        On<AdvisorHandoffReadiedForReview>(Apply);
        On<AdvisorHandoffClosed>(Apply);
        On<AdvisorHandoffRevoked>(Apply);
        On<AdvisorFeedbackAdmitted>(Apply);
    }

    internal AdvisorReplayCut ReplayCut => _cut;

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>True once the handoff was created.</summary>
    public bool IsCreated => _createdFact is not null;

    /// <summary>The program the handoff belongs to.</summary>
    public Uuid ProgramId => _programId;

    /// <summary>The current status: draft, ready for review, closed or revoked.</summary>
    public string Status => _status;

    /// <summary>The logical revision: the number of facts of this handoff.</summary>
    public long Revision => _revision;

    /// <summary>The selection generation: one at creation, plus one for every selection revision.</summary>
    public long Generation => _generation;

    /// <summary>The last day the handoff stays shared.</summary>
    public DateOnly ExpiresOn => _createdFact?.ExpiresOn ?? default;

    /// <summary>Who created the handoff.</summary>
    public ActorReference? CreatedBy => _createdFact?.CreatedBy;

    /// <summary>When the handoff was created.</summary>
    public DateTimeOffset? CreatedAt => _createdFact?.CreatedAt;

    /// <summary>When the latest fact was recorded.</summary>
    public DateTimeOffset? UpdatedAt => _updatedAt;

    /// <summary>The ended handoff this selection is compared with.</summary>
    public Uuid? ComparedToHandoffId => _createdFact?.ComparedToHandoffId;

    /// <summary>True once the handoff was opened for review at least once.</summary>
    public bool WasReadyForReview => _wasReadyForReview;

    /// <summary>The verified content of the current selection, once created.</summary>
    public ImmutableContentRef? SelectionManifest => _manifest;

    /// <summary>The count of accepted admissions, independent of selection generation.</summary>
    public long AdmissionCount => _admissionCount;
    public Uuid TenantId => _tenantId;
    internal AdvisorHandoffCreated? CreatedFact => _createdFact;

    /// <summary>True while the handoff can still change: it was created and is neither closed nor revoked.</summary>
    public bool IsOpen => IsCreated && _status is Draft or ReadyForReview;

    /// <summary>Checks that the handoff does not exist yet.</summary>
    public Result PrepareCreate() => IsCreated
        ? Result.Failure(new RequestError(RequestErrorKind.Conflict, "The handoff already exists with different content."))
        : Result.Success;

    /// <summary>Checks that the selection of an open handoff can be revised at the expected revision.</summary>
    public Result PrepareRevise(long expectedRevision)
    {
        if (!IsCreated)
            return Missing();
        if (!IsOpen)
            return Conflict("Only an open handoff can be revised.");
        return expectedRevision == _revision ? Result.Success : Stale();
    }

    /// <summary>Checks that a draft handoff can be opened for review at the expected revision.</summary>
    public Result PrepareReady(long expectedRevision, DateOnly today)
    {
        if (!IsCreated)
            return Missing();
        if (!IsOpen || _status != Draft)
            return Conflict("Only a draft handoff can be marked ready for review.");
        if (expectedRevision != _revision)
            return Stale();
        return ExpiresOn < today
            ? Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A handoff requires an active exact selection and valid actor before it can be shared."))
            : Result.Success;
    }

    /// <summary>Checks that an open handoff can be closed or revoked at the expected revision.</summary>
    public Result PrepareEnd(long expectedRevision)
    {
        if (!IsCreated)
            return Missing();
        if (!IsOpen)
            return Conflict("Only an open handoff can be closed or revoked.");
        return expectedRevision == _revision ? Result.Success : Stale();
    }

    /// <summary>
    ///     Checks that feedback can be admitted at the expected revision: the handoff is ready, unrevoked, unclosed and
    ///     unexpired, and the feedback identity was not admitted before. The caller proves the source is selected at
    ///     exactly this revision.
    /// </summary>
    public Result PrepareAdmit(long expectedRevision, DateOnly today)
    {
        if (!IsCreated)
            return Missing();
        if (expectedRevision != _revision)
            return Stale();
        if (_status != ReadyForReview || !IsOpen)
            return Conflict("Feedback is accepted only while the handoff is ready for review.");
        return ExpiresOn < today
            ? Conflict("Feedback is accepted only while the handoff is shared and unexpired.")
            : Result.Success;
    }

    /// <summary>Creates the handoff as the root native fact of its operation.</summary>
    public Result Create(OperationProvenance operation, ImmutableContentRef manifest, ImmutableContentRef authored,
        DateOnly expiresOn, ActorReference createdBy, DateTimeOffset createdAt, Uuid? comparedToHandoffId,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(manifest);
        if (operation.Owner != Stream || operation.LogicalId != _handoffId)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A creation is the logical operation of its own handoff."));
        if (_programId == Uuid.Empty || comparedToHandoffId is { } comparedTo &&
            (comparedTo == Uuid.Empty || comparedTo == _handoffId))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A handoff belongs to a program and compares only with another handoff."));
        var prepared = PrepareCreate();
        if (!prepared.IsSuccess)
            return prepared;
        var append = _cut.Require(this, Acceptance, operation);
        if (!append.IsSuccess)
            return append;
        RaiseEvent(new AdvisorHandoffCreated(_tenantId, _programId, _handoffId, manifest, authored, expiresOn,
            createdBy, createdAt, comparedToHandoffId, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Revises the selection as the root native fact of its operation; the generation advances.</summary>
    public Result ReviseSelection(OperationProvenance operation, ImmutableContentRef manifest, ImmutableContentRef authored,
        ActorReference revisedBy, DateTimeOffset revisedAt, ImmutableContentRef firstResponse,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(manifest);
        var prepared = PrepareOwn(operation, PrepareRevise);
        if (!prepared.IsSuccess)
            return prepared;
        var append = _cut.Require(this, Acceptance, operation);
        if (!append.IsSuccess)
            return append;
        RaiseEvent(new AdvisorHandoffSelectionRevised(_tenantId, _programId, _handoffId, _revision + 1, manifest,
            authored, revisedBy, revisedAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Opens the draft handoff for review as the root native fact of its operation.</summary>
    public Result MarkReady(OperationProvenance operation, ActorReference readiedBy, DateTimeOffset readiedAt,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var prepared = PrepareOwn(operation, expected => PrepareReady(expected, DateOnly.FromDateTime(readiedAt.UtcDateTime)));
        if (!prepared.IsSuccess)
            return prepared;
        var append = _cut.Require(this, Acceptance, operation);
        if (!append.IsSuccess)
            return append;
        RaiseEvent(new AdvisorHandoffReadiedForReview(_tenantId, _programId, _handoffId, _revision + 1, readiedBy,
            readiedAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Closes the handoff as the root native fact of its operation.</summary>
    public Result Close(OperationProvenance operation, ImmutableContentRef authored, ActorReference closedBy,
        DateTimeOffset closedAt, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var prepared = PrepareOwn(operation, PrepareEnd);
        if (!prepared.IsSuccess)
            return prepared;
        var append = _cut.Require(this, Acceptance, operation);
        if (!append.IsSuccess)
            return append;
        RaiseEvent(new AdvisorHandoffClosed(_tenantId, _programId, _handoffId, _revision + 1, authored, closedBy,
            closedAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Revokes the handoff as the root native fact of its operation.</summary>
    public Result Revoke(OperationProvenance operation, ImmutableContentRef authored, ActorReference revokedBy,
        DateTimeOffset revokedAt, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var prepared = PrepareOwn(operation, PrepareEnd);
        if (!prepared.IsSuccess)
            return prepared;
        var append = _cut.Require(this, Acceptance, operation);
        if (!append.IsSuccess)
            return append;
        RaiseEvent(new AdvisorHandoffRevoked(_tenantId, _programId, _handoffId, _revision + 1, authored, revokedBy,
            revokedAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Admits one feedback contribution for the current generation as the root native fact of its operation.</summary>
    public Result AdmitFeedback(OperationProvenance operation, ImmutableContentRef intent, DateOnly today,
        ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        var prepared = PrepareOwn(operation, expected => PrepareAdmit(expected, today));
        if (!prepared.IsSuccess)
            return prepared;
        var append = _cut.Require(this, Acceptance, operation);
        if (!append.IsSuccess)
            return append;
        RaiseEvent(new AdvisorFeedbackAdmitted(Stream, _revision + 1, _generation, intent, null, operation,
            firstResponse, null));
        return Result.Success;
    }

    protected override void Apply(DomainEvent ev)
    {
        _cut.Observe(ev);
        base.Apply(ev);
    }

    void Apply(AdvisorHandoffCreated ev)
    {
        if (ev.Operation is null)
            throw new ImmutableContentIntegrityException("The advisor native fact lacks operation provenance.");
        if (Acceptance.SelectedFact is not null && Acceptance.SelectedLogicalId == ev.Operation.LogicalId)
            throw new ImmutableContentIntegrityException("The selected advisor native operation occurs more than once.");
        Acceptance.Observe(ev);
        if (_createdFact is not null || ev.TenantId != _tenantId || ev.HandoffId != _handoffId ||
            ev.ProgramId == Uuid.Empty || ev.Operation.Owner != Stream || ev.Operation.LogicalId != _handoffId ||
            ev.ComparedToHandoffId is { } comparedTo && (comparedTo == Uuid.Empty || comparedTo == _handoffId))
            throw new ImmutableContentIntegrityException("A handoff stream is created once, by its own operation.");
        _createdFact = ev;
        _programId = ev.ProgramId;
        _manifest = ev.RecordVersionsManifest;
        _revision = 1;
        _generation = 1;
        _status = Draft;
        _updatedAt = ev.CreatedAt;
    }

    void Apply(AdvisorHandoffSelectionRevised ev)
    {
        if (ev.Operation is null)
            throw new ImmutableContentIntegrityException("The advisor native fact lacks operation provenance.");
        if (Acceptance.SelectedFact is not null && Acceptance.SelectedLogicalId == ev.Operation.LogicalId)
            throw new ImmutableContentIntegrityException("The selected advisor native operation occurs more than once.");
        Acceptance.Observe(ev);
        EnsureOpen(ev.TenantId, ev.ProgramId, ev.HandoffId, ev.Revision, ev.Operation);
        _manifest = ev.RecordVersionsManifest;
        _generation++;
        _status = Draft;
        Advance(ev.RevisedAt);
    }

    void Apply(AdvisorHandoffReadiedForReview ev)
    {
        if (ev.Operation is null)
            throw new ImmutableContentIntegrityException("The advisor native fact lacks operation provenance.");
        if (Acceptance.SelectedFact is not null && Acceptance.SelectedLogicalId == ev.Operation.LogicalId)
            throw new ImmutableContentIntegrityException("The selected advisor native operation occurs more than once.");
        Acceptance.Observe(ev);
        EnsureOpen(ev.TenantId, ev.ProgramId, ev.HandoffId, ev.Revision, ev.Operation);
        if (_status != Draft)
            throw new ImmutableContentIntegrityException("Only a draft handoff is readied for review.");
        _status = ReadyForReview;
        _wasReadyForReview = true;
        Advance(ev.ReadiedAt);
    }

    void Apply(AdvisorHandoffClosed ev)
    {
        if (ev.Operation is null)
            throw new ImmutableContentIntegrityException("The advisor native fact lacks operation provenance.");
        if (Acceptance.SelectedFact is not null && Acceptance.SelectedLogicalId == ev.Operation.LogicalId)
            throw new ImmutableContentIntegrityException("The selected advisor native operation occurs more than once.");
        Acceptance.Observe(ev);
        EnsureOpen(ev.TenantId, ev.ProgramId, ev.HandoffId, ev.Revision, ev.Operation);
        _status = Closed;
        Advance(ev.ClosedAt);
    }

    void Apply(AdvisorHandoffRevoked ev)
    {
        if (ev.Operation is null)
            throw new ImmutableContentIntegrityException("The advisor native fact lacks operation provenance.");
        if (Acceptance.SelectedFact is not null && Acceptance.SelectedLogicalId == ev.Operation.LogicalId)
            throw new ImmutableContentIntegrityException("The selected advisor native operation occurs more than once.");
        Acceptance.Observe(ev);
        EnsureOpen(ev.TenantId, ev.ProgramId, ev.HandoffId, ev.Revision, ev.Operation);
        _status = Revoked;
        Advance(ev.RevokedAt);
    }

    void Apply(AdvisorFeedbackAdmitted ev)
    {
        if (ev.Operation is null)
            throw new ImmutableContentIntegrityException("The advisor native fact lacks operation provenance.");
        if (Acceptance.SelectedFact is not null && Acceptance.SelectedLogicalId == ev.Operation.LogicalId)
            throw new ImmutableContentIntegrityException("The selected advisor native operation occurs more than once.");
        Acceptance.Observe(ev);
        if (!IsCreated || _status != ReadyForReview || ev.Owner != Stream || ev.Revision != _revision + 1 ||
            ev.Generation != _generation || ev.Operation.Owner != Stream)
            throw new ImmutableContentIntegrityException(
                "Feedback is admitted once, for the current ready generation, at the next revision.");
        _admissionCount++;
        Advance(ev.Operation.AcceptedAt);
    }

    void EnsureOpen(Uuid tenantId, Uuid programId, Uuid handoffId, long revision, OperationProvenance operation)
    {
        if (!IsOpen || tenantId != _tenantId || programId != _programId || handoffId != _handoffId ||
            revision != _revision + 1 || operation.Owner != Stream)
            throw new ImmutableContentIntegrityException("A handoff fact follows the creation at the next revision of an open handoff.");
    }

    void Advance(DateTimeOffset occurredAt)
    {
        _revision++;
        _updatedAt = occurredAt;
    }

    Result PrepareOwn(OperationProvenance operation, Func<long, Result> prepare)
    {
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A handoff write is a root operation of its own handoff with an expected revision."));
        return prepare((long)expected);
    }

    static Result Missing() => Result.Failure(new RequestError(RequestErrorKind.NotFound,
        "The advisor handoff was not found in the requested program."));

    static Result Conflict(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message));

    Result Stale() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        $"The handoff revision is stale; the current revision is {_revision}."));
}
