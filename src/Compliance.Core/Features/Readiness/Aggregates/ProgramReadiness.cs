using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
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
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Rules;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Readiness.Aggregates;

/// <summary>
///     The compact readiness authority of one program: the latest adopted assessment, the one pending latest-adoption
///     generation, the one pending management decision and the decisions of the current assessment. The
///     logical revision counts every fact of this stream. A run reserves the pending adoption generation before any
///     computation, and a decision seals the latest adoption; while either is pending the old latest cannot be decided
///     and no second run is admitted. Full assessment and decision content stays in immutable content, and reducers
///     never read content, another owner or a projection.
/// </summary>
public sealed class ProgramReadiness : Aggregate
{
    /// <summary>Stream area of every program readiness owner (the realm is the tenant).</summary>
    public const string Area = "program-readiness";

    /// <summary>The identity owner type of the program readiness.</summary>
    public const string OwnerType = "ProgramReadiness";

    /// <summary>The proceed readiness outcome.</summary>
    public const string Proceed = "proceed";

    /// <summary>The do_not_proceed readiness outcome.</summary>
    public const string DoNotProceed = "do_not_proceed";

    /// <summary>The approve Type I entry outcome.</summary>
    public const string Approve = "approve";

    /// <summary>The approve_with_exceptions Type I entry outcome.</summary>
    public const string ApproveWithExceptions = "approve_with_exceptions";

    /// <summary>The defer Type I entry outcome; it never consumes the approved entry slot.</summary>
    public const string Defer = "defer";

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _selectedRoot;
    readonly Uuid _selectedAssessment;
    ReadinessAssessmentRequested? _selectedRun;
    ReadinessAssessmentRequested? _latestRun;
    DomainEvent? _latestAccepted;
    DomainEvent? _latestTerminal;
    ReadinessAssessmentAdopted? _selectedAdoption;
    DomainEvent? _selectedDecision;
    DomainEvent? _selectedTerminal;
    ReadinessAssessmentAdopted? _latestAdoption;
    ReadinessDecisionRecorded? _latestReadinessDecision;
    TypeIEntryDecisionRecorded? _latestEntryDecision;
    ReadinessAssessmentRequested? _pendingAdoption;
    DomainEvent? _pendingDecision;
    long _revision;
    long _generation;

    /// <summary>Opens the readiness of one program, selecting the logical operation to resolve.</summary>
    public ProgramReadiness(Uuid tenantId, Uuid programId, Uuid selectedLogicalId = default,
        Uuid selectedRootOperationId = default, Uuid selectedAssessmentId = default)
        : base(IdentityOf(tenantId, programId),
            new EventStreamAddress(tenantId.ToString(), Area, IdentityOf(tenantId, programId).ToString()))
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty)
            throw new ArgumentException("A program readiness stream needs tenant and program IDs.");
        (_tenantId, _programId) = (tenantId, programId);
        _selectedRoot = selectedRootOperationId == Uuid.Empty ? selectedLogicalId : selectedRootOperationId;
        _selectedAssessment = selectedAssessmentId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<ReadinessAssessmentRequested>(Apply);
        On<ReadinessAssessmentAdopted>(Apply);
        On<ReadinessDecisionAccepted>(ev => ApplyAccepted(ev, ev.Owner, ev.Revision, ev.Generation, ev.Operation));
        On<TypeIEntryDecisionAccepted>(ev => ApplyAccepted(ev, ev.Owner, ev.Revision, ev.Generation, ev.Operation));
        On<ReadinessDecisionRecorded>(Apply);
        On<TypeIEntryDecisionRecorded>(Apply);
        On<ReadinessDecisionCanceled>(Apply);
    }

    /// <summary>The identity of the readiness owner of a program.</summary>
    public static Uuid IdentityOf(Uuid tenantId, Uuid programId) =>
        AggregateIdentity.Create(OwnerType, tenantId, programId);

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The program.</summary>
    public Uuid ProgramId => _programId;

    /// <summary>The logical revision: the number of facts of this program readiness.</summary>
    public long Revision => _revision;

    /// <summary>The last adoption generation reserved by a run; zero before the first run.</summary>
    public long AdoptionGeneration => _generation;

    /// <summary>The tenant of this command authority.</summary>
    public Uuid TenantId => _tenantId;

    /// <summary>The last fact at the captured native and physical cut.</summary>
    public DomainEvent? Latest { get; private set; }

    /// <summary>The run admitted and not yet adopted.</summary>
    public ReadinessAssessmentRequested? PendingAdoption => _pendingAdoption;

    /// <summary>The one accepted decision still awaiting completion or cancellation.</summary>
    public DomainEvent? PendingDecision => _pendingDecision;

    /// <summary>The latest adopted assessment identity.</summary>
    public Uuid? LatestAssessmentId => _latestAdoption?.Operation.RootOperationId;

    /// <summary>The latest adoption, retained as current command authority.</summary>
    public ReadinessAssessmentAdopted? LatestAdoption => _latestAdoption;

    /// <summary>The adoption of the latest or explicitly selected assessment only.</summary>
    public ReadinessAssessmentAdopted? AdoptionOf(Uuid assessmentId) =>
        LatestAssessmentId == assessmentId ? _latestAdoption :
        _selectedAdoption?.Operation.RootOperationId == assessmentId ? _selectedAdoption : null;

    /// <summary>The latest, pending or explicitly selected root run.</summary>
    public ReadinessAssessmentRequested? RequestedRun(Uuid rootOperationId) =>
        _pendingAdoption?.Operation.RootOperationId == rootOperationId ? _pendingAdoption :
        _selectedRun?.Operation.RootOperationId == rootOperationId ? _selectedRun :
        _latestRun?.Operation.RootOperationId == rootOperationId ? _latestRun : null;

    /// <summary>The latest, pending or explicitly selected root decision.</summary>
    public DomainEvent? AcceptedDecision(Uuid rootOperationId) =>
        _pendingDecision is not null && PendingRoot(_pendingDecision) == rootOperationId ? _pendingDecision :
        _selectedDecision is not null && PendingRoot(_selectedDecision) == rootOperationId ? _selectedDecision :
        _latestAccepted is not null && PendingRoot(_latestAccepted) == rootOperationId ? _latestAccepted : null;

    /// <summary>The latest terminal fact or the terminal fact of the explicitly selected decision.</summary>
    public DomainEvent? TerminalDecision(Uuid rootOperationId) =>
        _selectedTerminal is not null && PendingRoot(_selectedTerminal) == rootOperationId ? _selectedTerminal :
        _latestTerminal is not null && PendingRoot(_latestTerminal) == rootOperationId ? _latestTerminal : null;

    /// <summary>The readiness decision of the current assessment; historical composition lives in ReadinessReader.</summary>
    public ReadinessDecisionRecorded? DecisionOf(Uuid assessmentId) =>
        _latestReadinessDecision?.AssessmentId == assessmentId ? _latestReadinessDecision : null;

    /// <summary>The current assessment's Type I entry decision.</summary>
    public TypeIEntryDecisionRecorded? CurrentEntryDecision => _latestEntryDecision;

    /// <summary>The frozen decision intent and generation of an accepted decision fact.</summary>
    public static (ImmutableContentRef Intent, long Generation, ReadinessDecisionKind Kind)? DecisionTerms(DomainEvent? fact) =>
        fact switch
        {
            ReadinessDecisionAccepted accepted => (accepted.Intent, accepted.Generation, ReadinessDecisionKind.Readiness),
            TypeIEntryDecisionAccepted accepted => (accepted.Intent, accepted.Generation, ReadinessDecisionKind.TypeIEntry),
            _ => null,
        };

    /// <summary>Checks that a run can be admitted at the expected program revision.</summary>
    public Result PrepareRun(long expectedRevision)
    {
        if (expectedRevision != _revision)
            return Fail(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("program readiness", _revision)));
        if (_pendingAdoption is not null)
            return Transient("A readiness assessment is being prepared; retry when it is adopted.");
        return _pendingDecision is not null
            ? Transient("A readiness decision is in progress; retry when it settles.")
            : Result.Success;
    }

    /// <summary>Checks that the latest assessment can take a new decision of the kind at the expected revision.</summary>
    public Result PrepareDecision(long expectedRevision, Uuid assessmentId, ReadinessDecisionKind kind)
    {
        if (AdoptionOf(assessmentId) is null)
            return Fail(CommandFailure.MissingRecord("The readiness assessment was not found."));
        if (expectedRevision != _revision)
            return Fail(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("program readiness", _revision)));
        if (_pendingAdoption is not null)
            return Transient("A newer readiness assessment is being prepared; retry when it is adopted.");
        if (_pendingDecision is not null)
            return Transient("A readiness decision is in progress; retry when it settles.");
        if (LatestAssessmentId != assessmentId)
            return Fail(CommandFailure.StateConflict(kind == ReadinessDecisionKind.Readiness
                ? "A later assessment exists; decide the latest assessment."
                : "A later assessment exists; decide Type I entry on the latest assessment."));
        if (kind == ReadinessDecisionKind.Readiness && _latestReadinessDecision is not null)
            return Fail(CommandFailure.StateConflict("The readiness assessment was already decided."));
        return kind == ReadinessDecisionKind.TypeIEntry && _latestEntryDecision is not null
            ? Fail(CommandFailure.StateConflict(
                "The assessment already has a Type I entry decision; run a new assessment."))
            : Result.Success;
    }

    /// <summary>Admits a run as the root native fact of its operation; it reserves the pending adoption generation.</summary>
    public Result RequestRun(OperationProvenance operation, ImmutableContentRef intent, ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A run is a root operation of its own program readiness with an expected revision."));
        var prepared = PrepareRun((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new ReadinessAssessmentRequested(Stream, _revision + 1, _generation + 1, intent, null, operation,
            firstResponse, null));
        return Result.Success;
    }

    /// <summary>Adopts the exact pending assessment as the latest; the caller proved the computation.</summary>
    public Result Adopt(OperationProvenance operation, ImmutableContentRef intent, long generation,
        ImmutableContentRef sourceProofs, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_pendingAdoption is null || _pendingAdoption.Operation.RootOperationId != operation.RootOperationId ||
            _pendingAdoption.Generation != generation || _pendingAdoption.Intent != intent || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only the exact pending adoption generation of this run can be adopted.", false));
        RaiseEvent(new ReadinessAssessmentAdopted(Stream, _revision + 1, generation, intent, sourceProofs, operation,
            null, operationResult));
        return Result.Success;
    }

    /// <summary>Accepts a decision as the root native fact of its operation; it seals the latest adoption.</summary>
    public Result AcceptDecision(ReadinessDecisionKind kind, Uuid assessmentId, OperationProvenance operation,
        ImmutableContentRef intent, ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A decision is a root operation of its own program readiness with an expected revision."));
        var prepared = PrepareDecision((long)expected, assessmentId, kind);
        if (!prepared.IsSuccess)
            return prepared;
        var generation = _latestAdoption!.Generation;
        RaiseEvent(kind == ReadinessDecisionKind.Readiness
            ? new ReadinessDecisionAccepted(Stream, _revision + 1, generation, intent, null, operation, firstResponse, null)
            : new TypeIEntryDecisionAccepted(Stream, _revision + 1, generation, intent, null, operation, firstResponse, null));
        return Result.Success;
    }

    /// <summary>Records the pending readiness decision; the caller proved every held gap plan.</summary>
    public Result RecordReadinessDecision(OperationProvenance operation, ImmutableContentRef decisionContent,
        Uuid assessmentId, ImmutableContentRef decisionProofs, ImmutableContentRef intent, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (PendingFailure<ReadinessDecisionAccepted>(operation, intent, assessmentId) is { } failure)
            return failure;
        RaiseEvent(new ReadinessDecisionRecorded(_tenantId, _programId, _revision + 1, decisionContent, operation,
            assessmentId, operation.RootOperationId, decisionProofs, intent, null, operationResult));
        return Result.Success;
    }

    /// <summary>Records the pending Type I entry decision; the caller proved every held gap plan.</summary>
    public Result RecordTypeIEntryDecision(OperationProvenance operation, ImmutableContentRef decisionContent,
        Uuid assessmentId, ImmutableContentRef decisionProofs, ImmutableContentRef intent, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (PendingFailure<TypeIEntryDecisionAccepted>(operation, intent, assessmentId) is { } failure)
            return failure;
        RaiseEvent(new TypeIEntryDecisionRecorded(_tenantId, _programId, _revision + 1, decisionContent, operation,
            assessmentId, operation.RootOperationId, decisionProofs, intent, null, operationResult));
        return Result.Success;
    }

    /// <summary>Cancels the pending decision of the operation and fences its delayed completion.</summary>
    public Result CancelDecision(OperationProvenance operation, ImmutableContentRef failureProof)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_pendingDecision is null || PendingRoot(_pendingDecision) != operation.RootOperationId ||
            operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only the exact pending decision can be canceled.", false));
        var terms = DecisionTerms(_pendingDecision)!.Value;
        RaiseEvent(new ReadinessDecisionCanceled(Stream, _revision + 1, terms.Generation, terms.Intent, failureProof,
            operation, null, null));
        return Result.Success;
    }

    Result? PendingFailure<TAccepted>(OperationProvenance operation, ImmutableContentRef intent, Uuid assessmentId)
        where TAccepted : DomainEvent =>
        _pendingDecision is TAccepted pending && PendingRoot(pending) == operation.RootOperationId &&
        DecisionTerms(pending)!.Value.Intent == intent && operation.Owner == Stream &&
        LatestAssessmentId == assessmentId
            ? null
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only the exact pending decision can be recorded.", false));

    static Uuid PendingRoot(DomainEvent accepted) => ((INativeOperationFact)accepted).Operation.RootOperationId;

    void Apply(ReadinessAssessmentRequested ev)
    {
        Acceptance.Observe(ev);
        if (_pendingAdoption is not null || _pendingDecision is not null || ev.Owner != Stream ||
            ev.Revision != _revision + 1 || ev.Generation != _generation + 1 || ev.Operation.Owner != Stream ||
            ev.Operation.RootOperationId != ev.Operation.LogicalId)
            throw new InvalidOperationException(
                "A run is admitted once, with no pending adoption or decision, at the next revision and generation.");
        if (ev.Operation.RootOperationId == _selectedRoot)
            _selectedRun = ev;
        _latestRun = ev;
        _pendingAdoption = ev;
        _generation = ev.Generation;
        Advance(ev);
    }

    void Apply(ReadinessAssessmentAdopted ev)
    {
        Acceptance.Observe(ev);
        if (_pendingAdoption is null || ev.Owner != Stream || ev.Revision != _revision + 1 ||
            ev.Generation != _pendingAdoption.Generation || ev.Intent != _pendingAdoption.Intent ||
            ev.Operation.RootOperationId != _pendingAdoption.Operation.RootOperationId || ev.Operation.Owner != Stream)
            throw new InvalidOperationException("Only the exact pending adoption generation is adopted.");
        _latestAdoption = ev;
        _latestReadinessDecision = null;
        _latestEntryDecision = null;
        if (ev.Operation.RootOperationId == _selectedRoot || ev.Operation.RootOperationId == _selectedAssessment)
            _selectedAdoption = ev;
        _pendingAdoption = null;
        Advance(ev);
    }

    void ApplyAccepted(DomainEvent ev, EventStreamAddress owner, long revision, long generation, OperationProvenance operation)
    {
        Acceptance.Observe(ev);
        if (_latestAdoption is null || _pendingAdoption is not null || _pendingDecision is not null || owner != Stream ||
            revision != _revision + 1 || generation != _latestAdoption.Generation ||
            operation.Owner != Stream || operation.RootOperationId != operation.LogicalId)
            throw new InvalidOperationException(
                "A decision is accepted once, on the latest adopted assessment, with nothing pending.");
        if (operation.RootOperationId == _selectedRoot)
            _selectedDecision = ev;
        _latestAccepted = ev;
        _pendingDecision = ev;
        Advance(ev);
    }

    void Apply(ReadinessDecisionRecorded ev)
    {
        Acceptance.Observe(ev);
        if (_pendingDecision is not ReadinessDecisionAccepted pending || ev.TenantId != _tenantId ||
            ev.ProgramId != _programId || ev.Revision != _revision + 1 || ev.Intent != pending.Intent ||
            ev.Operation.RootOperationId != pending.Operation.RootOperationId || ev.Operation.Owner != Stream ||
            ev.AssessmentId != LatestAssessmentId || ev.DecisionId != pending.Operation.RootOperationId ||
            _latestReadinessDecision is not null)
            throw new InvalidOperationException(
                "Only the exact pending readiness decision of the latest assessment is recorded.");
        if (ev.Operation.RootOperationId == _selectedRoot)
            _selectedTerminal = ev;
        _latestTerminal = ev;
        _latestReadinessDecision = ev;
        _pendingDecision = null;
        Advance(ev);
    }

    void Apply(TypeIEntryDecisionRecorded ev)
    {
        Acceptance.Observe(ev);
        if (_pendingDecision is not TypeIEntryDecisionAccepted pending || ev.TenantId != _tenantId ||
            ev.ProgramId != _programId || ev.Revision != _revision + 1 || ev.Intent != pending.Intent ||
            ev.Operation.RootOperationId != pending.Operation.RootOperationId || ev.Operation.Owner != Stream ||
            ev.AssessmentId != LatestAssessmentId || ev.DecisionId != pending.Operation.RootOperationId)
            throw new InvalidOperationException(
                "Only the exact pending Type I entry decision of the latest assessment is recorded.");
        _latestEntryDecision = ev;
        if (ev.Operation.RootOperationId == _selectedRoot)
            _selectedTerminal = ev;
        _latestTerminal = ev;
        _pendingDecision = null;
        Advance(ev);
    }

    void Apply(ReadinessDecisionCanceled ev)
    {
        Acceptance.Observe(ev);
        if (_pendingDecision is null || ev.Owner != Stream || ev.Revision != _revision + 1 ||
            ev.Intent != DecisionTerms(_pendingDecision)!.Value.Intent ||
            ev.Operation.RootOperationId != PendingRoot(_pendingDecision) || ev.Operation.Owner != Stream)
            throw new InvalidOperationException("Only the exact pending decision is canceled.");
        if (ev.Operation.RootOperationId == _selectedRoot)
            _selectedTerminal = ev;
        _latestTerminal = ev;
        _pendingDecision = null;
        Advance(ev);
    }

    void Advance(DomainEvent fact)
    {
        _revision++;
        Latest = fact;
    }

    static Result Transient(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Conflict, message, true));

    static Result Fail(CommandFailure failure) => Result.Failure(failure.ToRequestError());
}
