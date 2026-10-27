using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
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
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Services;
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
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Risks.Aggregates;

/// <summary>
///     One accountable treatment action: its work, the completions submitted for it and the independent reviews of those
///     submissions. The action belongs to one risk for its whole life, because the risk is part of its stream identity,
///     and only unfinished work can be revised or canceled. A review is accepted as a pending operation that seals the
///     exact submission for its generation, the submission ordinal; the decision, or the cancellation that fences it,
///     ends the review. A rejected completion reopens the work and can only be resubmitted as a new operation. The
///     logical revision counts every fact of this action.
/// </summary>
public sealed class RiskTreatmentAction : Aggregate
{
    /// <summary>Stream area of every treatment action (the realm is the tenant).</summary>
    public const string Area = "risk-treatment-actions";

    /// <summary>The identity owner type of a treatment action.</summary>
    public const string OwnerType = "RiskTreatmentAction";

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _riskId;
    readonly Uuid _actionId;
    readonly Uuid _selectedReviewRoot;
    RiskTreatmentActionCompletionSubmitted? _submission;
    RiskTreatmentActionCompletionSubmitted? _selectedSubmission;
    RiskTreatmentActionCompletionReviewAccepted? _selectedAccepted;
    DomainEvent? _selectedTerminal;
    DomainEvent? _latest;
    long _generation;
    RiskTreatmentActionAdded? _added;
    RiskTreatmentActionCancelled? _cancelled;
    RiskTreatmentActionCompletionReviewAccepted? _pending;
    string _status = RiskStates.ActionOpen;
    long _revision;

    /// <summary>Opens one action, selecting the logical operation to resolve.</summary>
    public RiskTreatmentAction(Uuid tenantId, Uuid programId, Uuid riskId, Uuid actionId, Uuid selectedLogicalId = default,
        Uuid selectedReviewRoot = default)
        : base(IdentityOf(tenantId, programId, riskId, actionId), new EventStreamAddress(tenantId.ToString(), Area,
            IdentityOf(tenantId, programId, riskId, actionId).ToString()))
    {
        (_tenantId, _programId, _riskId, _actionId) = (tenantId, programId, riskId, actionId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        _selectedReviewRoot = selectedReviewRoot;
        On<RiskTreatmentActionAdded>(Apply);
        On<RiskTreatmentActionRevised>(Apply);
        On<RiskTreatmentActionCancelled>(Apply);
        On<RiskTreatmentActionCompletionSubmitted>(Apply);
        On<RiskTreatmentActionCompletionReviewAccepted>(Apply);
        On<RiskTreatmentActionCompletionReviewed>(Apply);
        On<RiskTreatmentActionCompletionReviewCanceled>(Apply);
    }

    /// <summary>The identity of the action of a risk.</summary>
    public static Uuid IdentityOf(Uuid tenantId, Uuid programId, Uuid riskId, Uuid actionId) =>
        AggregateIdentity.Create(OwnerType, tenantId, programId, riskId, actionId);

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The action identity.</summary>
    public Uuid ActionId => _actionId;

    /// <summary>The risk this action belongs to.</summary>
    public Uuid RiskId => _riskId;

    /// <summary>The logical revision: zero before the action was added, one when added, then one per fact.</summary>
    public long Revision => _revision;

    /// <summary>True once the action was added.</summary>
    public bool IsAdded => _added is not null;

    /// <summary>The adding fact, once recorded.</summary>
    public RiskTreatmentActionAdded? Added => _added;

    /// <summary>The current status: open, completion submitted, completed or canceled.</summary>
    public string Status => _status;

    /// <summary>The tenant of this exact action owner.</summary>
    public Uuid TenantId => _tenantId;

    /// <summary>The program of this exact action owner.</summary>
    public Uuid ProgramId => _programId;

    /// <summary>The latest native fact at the captured command cut.</summary>
    public DomainEvent? Latest => _latest;

    /// <summary>The cancellation fact, once canceled.</summary>
    public RiskTreatmentActionCancelled? CancelledFact => _cancelled;

    /// <summary>The review accepted and not yet decided or canceled, which seals the submission.</summary>
    public RiskTreatmentActionCompletionReviewAccepted? PendingReview => _pending;

    /// <summary>The submission awaiting review, or null.</summary>
    public RiskTreatmentActionCompletionSubmitted? PendingSubmission =>
        _status == RiskStates.ActionSubmitted ? _submission : null;

    /// <summary>The accepted review selected for recovery, or the currently pending review.</summary>
    public RiskTreatmentActionCompletionReviewAccepted? AcceptedReview(Uuid rootOperationId) =>
        _pending?.Operation.RootOperationId == rootOperationId ? _pending :
        _selectedReviewRoot == rootOperationId ? _selectedAccepted : null;

    /// <summary>The exact submitted completion sealed by a selected or currently pending review.</summary>
    public RiskTreatmentActionCompletionSubmitted? SubmissionForReview(Uuid rootOperationId) =>
        _pending?.Operation.RootOperationId == rootOperationId ? _submission :
        _selectedReviewRoot == rootOperationId ? _selectedSubmission : null;

    /// <summary>The terminal fact of the selected review. Historical selections never change with current state.</summary>
    public DomainEvent? TerminalReview(Uuid rootOperationId) =>
        _selectedReviewRoot == rootOperationId ? _selectedTerminal : null;

    /// <summary>Checks that the action can be added.</summary>
    public Result PrepareAdd() => _added is null
        ? Result.Success
        : Fail(CommandFailure.StateConflict("A treatment action identity cannot be reused."));

    /// <summary>Checks that open work can be revised at the expected revision.</summary>
    public Result PrepareRevise(long expectedRevision)
    {
        if (_added is null)
            return Fail(CommandFailure.MissingRecord("The treatment action was not found."));
        if (_status != RiskStates.ActionOpen)
            return Fail(CommandFailure.StateConflict("Only open treatment actions can be edited."));
        return Stale(expectedRevision);
    }

    /// <summary>Checks that unfinished work can be canceled at the expected revision.</summary>
    public Result PrepareCancel(long expectedRevision)
    {
        if (_added is null)
            return Fail(CommandFailure.MissingRecord("The treatment action was not found."));
        if (_status == RiskStates.ActionCancelled)
            return Fail(CommandFailure.StateConflict("The treatment action is already cancelled."));
        if (_pending is not null)
            return Pending();
        var stale = Stale(expectedRevision);
        if (!stale.IsSuccess)
            return stale;
        return _status is RiskStates.ActionOpen or RiskStates.ActionSubmitted
            ? Result.Success
            : Fail(CommandFailure.StateConflict("Only unfinished treatment actions can be cancelled."));
    }

    /// <summary>Checks that a completion can be submitted at the expected revision.</summary>
    public Result PrepareSubmit(long expectedRevision)
    {
        if (_added is null)
            return Fail(CommandFailure.MissingRecord("The treatment action was not found."));
        if (_status != RiskStates.ActionOpen)
            return Fail(CommandFailure.StateConflict("The treatment action has no open work to complete."));
        return Stale(expectedRevision);
    }

    /// <summary>Checks that the pending completion can be taken into review at the expected revision.</summary>
    public Result PrepareReview(long expectedRevision)
    {
        if (_added is null)
            return Fail(CommandFailure.MissingRecord("The treatment action was not found."));
        if (_status != RiskStates.ActionSubmitted)
            return Fail(CommandFailure.StateConflict("The treatment action has no pending completion to review."));
        if (_pending is not null)
            return Pending();
        return Stale(expectedRevision);
    }

    /// <summary>Adds the action as the root native fact of its operation; the logical id is the action id.</summary>
    public Result Add(OperationProvenance operation, ImmutableContentRef actionContent, ImmutableContentRef firstResponse,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(actionContent);
        if (!NativeOwnerHistory.IsOwnRoot(this, operation, nameof(AddRiskTreatmentAction), 0) ||
            operation.LogicalId != _actionId)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An action is created by its own operation at expected revision zero."));
        var prepared = PrepareAdd();
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new RiskTreatmentActionAdded(_tenantId, _programId, _riskId, 1, actionContent, operation,
            firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Revises open work as the root native fact of its operation.</summary>
    public Result Revise(OperationProvenance operation, string title, string targetState, string expectedEvidence,
        DateOnly dueOn, Uuid accountableMemberId, ImmutableContentRef evidenceRequestIds, ActorReference revisedBy,
        DateTimeOffset revisedAt, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(revisedBy);
        var prepared = PrepareOwn(operation, PrepareRevise);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new RiskTreatmentActionRevised(_tenantId, _programId, _riskId, _revision + 1, _actionId,
            operation.LogicalId, (long)operation.OriginalExpectedRevision!.Value, title, targetState, expectedEvidence,
            dueOn, accountableMemberId, evidenceRequestIds, revisedBy, revisedAt, operation, firstResponse,
            operationResult));
        return Result.Success;
    }

    /// <summary>Cancels unfinished work as the root native fact of its operation.</summary>
    public Result Cancel(OperationProvenance operation, string rationale, ActorReference cancelledBy,
        DateTimeOffset cancelledAt, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(cancelledBy);
        var prepared = PrepareOwn(operation, PrepareCancel);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new RiskTreatmentActionCancelled(_tenantId, _programId, _riskId, _revision + 1, _actionId,
            operation.LogicalId, rationale, cancelledBy, cancelledAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Submits a completion as the root native fact of its operation; the logical id is the submission id.</summary>
    public Result Submit(OperationProvenance operation, string summary, ImmutableContentRef evidenceRequestIds,
        Uuid submitterMemberId, ActorReference submittedBy, DateTimeOffset submittedAt,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(submittedBy);
        var prepared = PrepareOwn(operation, PrepareSubmit);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new RiskTreatmentActionCompletionSubmitted(_tenantId, _programId, _riskId, _revision + 1,
            _actionId, operation.LogicalId, summary, evidenceRequestIds, submitterMemberId, submittedBy, submittedAt,
            operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>
    ///     Accepts a completion review as the root native fact of its operation. It seals the pending submission, whose
    ///     ordinal is the generation, until the decision or its cancellation.
    /// </summary>
    public Result AcceptReview(OperationProvenance operation, ImmutableContentRef intent,
        ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream || operation.RootOperationId != operation.LogicalId)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A review is a root operation of its own action."));
        var prepared = PrepareOwn(operation, PrepareReview);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new RiskTreatmentActionCompletionReviewAccepted(Stream, _revision + 1, _generation, intent,
            null, operation, firstResponse, null));
        return Result.Success;
    }

    /// <summary>Appends the decision of the pending review; the caller proved every held evidence request.</summary>
    public Result CompleteReview(OperationProvenance operation, string outcome, string rationale,
        ActorReference reviewer, DateTimeOffset reviewedAt, Uuid? waiverId, ImmutableContentRef intent,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(reviewer);
        if (_pending is null || _pending.Operation.RootOperationId != operation.RootOperationId ||
            _pending.Intent != intent || !IsReviewChild(operation, nameof(CompleteRiskTreatmentActionReview)))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only the exact pending review can decide the completion.", false));
        RaiseEvent(new RiskTreatmentActionCompletionReviewed(_tenantId, _programId, _riskId, _revision + 1, _actionId,
            operation.RootOperationId, outcome, rationale, reviewer, reviewedAt, waiverId, operation, intent, null,
            operationResult));
        return Result.Success;
    }

    /// <summary>Cancels the pending review of the operation and fences its delayed decision.</summary>
    public Result CancelReview(OperationProvenance operation, ImmutableContentRef failureProof)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_pending is null || _pending.Operation.RootOperationId != operation.RootOperationId ||
            !IsReviewChild(operation, nameof(AbortRiskTreatmentActionReview)))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only the exact pending review can be canceled.", false));
        RaiseEvent(new RiskTreatmentActionCompletionReviewCanceled(Stream, _revision + 1, _pending.Generation,
            _pending.Intent, failureProof, operation, null, null));
        return Result.Success;
    }

    void Apply(RiskTreatmentActionAdded ev)
    {
        Acceptance.Observe(ev);
        if (_added is not null || ev.TenantId != _tenantId || ev.ProgramId != _programId || ev.RiskId != _riskId ||
            ev.Revision != 1 || !NativeOwnerHistory.IsOwnRoot(this, ev.Operation, nameof(AddRiskTreatmentAction), 0) ||
            ev.Operation.LogicalId != _actionId)
            throw new InvalidOperationException("An action is added once, at revision one, by its own operation.");
        _added = ev;
        Advance(ev);
    }

    void Apply(RiskTreatmentActionRevised ev)
    {
        Acceptance.Observe(ev);
        EnsureNext(ev.TenantId, ev.ProgramId, ev.RiskId, ev.ActionId, ev.Revision, ev.Operation);
        if (_status != RiskStates.ActionOpen || !NativeOwnerHistory.IsOwnRoot(this, ev.Operation,
                nameof(ReviseRiskTreatmentAction), (ulong)_revision) || ev.RequestId != ev.Operation.LogicalId ||
            ev.ExpectedRevision != _revision || ev.RevisedBy != ev.Operation.Actor || ev.RevisedAt != ev.Operation.AcceptedAt)
            throw new InvalidOperationException("Only the exact attributed open work is revised.");
        Advance(ev);
    }

    void Apply(RiskTreatmentActionCancelled ev)
    {
        Acceptance.Observe(ev);
        EnsureNext(ev.TenantId, ev.ProgramId, ev.RiskId, ev.ActionId, ev.Revision, ev.Operation);
        if (_status is not (RiskStates.ActionOpen or RiskStates.ActionSubmitted) || _pending is not null ||
            !NativeOwnerHistory.IsOwnRoot(this, ev.Operation, nameof(CancelRiskTreatmentAction), (ulong)_revision) ||
            ev.CancellationId != ev.Operation.LogicalId || ev.CancelledBy != ev.Operation.Actor ||
            ev.CancelledAt != ev.Operation.AcceptedAt)
            throw new InvalidOperationException("Only unfinished work without a pending review is canceled.");
        _cancelled = ev;
        _status = RiskStates.ActionCancelled;
        Advance(ev);
    }

    void Apply(RiskTreatmentActionCompletionSubmitted ev)
    {
        Acceptance.Observe(ev);
        EnsureNext(ev.TenantId, ev.ProgramId, ev.RiskId, ev.ActionId, ev.Revision, ev.Operation);
        if (_status != RiskStates.ActionOpen || !NativeOwnerHistory.IsOwnRoot(this, ev.Operation,
                nameof(SubmitRiskTreatmentActionCompletion), (ulong)_revision) || ev.SubmissionId != ev.Operation.LogicalId ||
            ev.SubmittedBy != ev.Operation.Actor || ev.SubmittedAt != ev.Operation.AcceptedAt ||
            ev.SubmitterMemberId == Uuid.Empty || ev.SubmitterMemberId.ToString() != ev.Operation.Actor.Id)
            throw new InvalidOperationException("An exact attributed completion is submitted for open work only.");
        _submission = ev;
        _generation++;
        _status = RiskStates.ActionSubmitted;
        Advance(ev);
    }

    void Apply(RiskTreatmentActionCompletionReviewAccepted ev)
    {
        Acceptance.Observe(ev);
        if (_added is null || _status != RiskStates.ActionSubmitted || _pending is not null || ev.Owner != Stream ||
            ev.Revision != _revision + 1 || ev.Generation != _generation || ev.Operation.Owner != Stream ||
            !NativeOwnerHistory.IsOwnRoot(this, ev.Operation, nameof(ReviewRiskTreatmentActionCompletion), (ulong)_revision) ||
            _selectedReviewRoot == ev.Operation.RootOperationId && _selectedAccepted is not null)
            throw new InvalidOperationException(
                "A review is accepted once for the pending submission of the current generation.");
        _pending = ev;
        if (_selectedReviewRoot == ev.Operation.RootOperationId)
        {
            _selectedAccepted = ev;
            _selectedSubmission = _submission;
        }
        Advance(ev);
    }

    void Apply(RiskTreatmentActionCompletionReviewed ev)
    {
        Acceptance.Observe(ev);
        if (_pending is null || ev.TenantId != _tenantId || ev.ProgramId != _programId || ev.RiskId != _riskId ||
            ev.ActionId != _actionId || ev.Revision != _revision + 1 || ev.Intent != _pending.Intent ||
            !IsReviewChild(ev.Operation, nameof(CompleteRiskTreatmentActionReview)) ||
            ev.DecisionId != _pending.Operation.RootOperationId || ev.Reviewer != _pending.Operation.Actor ||
            ev.ReviewedAt != _pending.Operation.AcceptedAt || ev.Outcome is not (RiskStates.Accept or RiskStates.Reject))
            throw new InvalidOperationException("Only the exact pending review decides the completion.");
        if (_selectedReviewRoot == ev.Operation.RootOperationId)
            _selectedTerminal = ev;
        _status = ev.Outcome == RiskStates.Accept ? RiskStates.ActionCompleted : RiskStates.ActionOpen;
        _pending = null;
        Advance(ev);
    }

    void Apply(RiskTreatmentActionCompletionReviewCanceled ev)
    {
        Acceptance.Observe(ev);
        if (_pending is null || ev.Owner != Stream || ev.Revision != _revision + 1 ||
            ev.Generation != _pending.Generation || ev.Intent != _pending.Intent ||
            !IsReviewChild(ev.Operation, nameof(AbortRiskTreatmentActionReview)) || ev.SourceProofs is null)
            throw new InvalidOperationException("Only the exact pending review is canceled.");
        if (_selectedReviewRoot == ev.Operation.RootOperationId)
            _selectedTerminal = ev;
        _pending = null;
        Advance(ev);
    }

    void EnsureNext(Uuid tenantId, Uuid programId, Uuid riskId, Uuid actionId, long revision, OperationProvenance operation)
    {
        if (_added is null || _pending is not null || _status == RiskStates.ActionCancelled ||
            tenantId != _tenantId || programId != _programId || riskId != _riskId || actionId != _actionId ||
            revision != _revision + 1 || operation.Owner != Stream)
            throw new InvalidOperationException("An action fact follows the previous one at the next revision of unfinished work.");
    }

    void Advance(DomainEvent fact)
    {
        _revision++;
        _latest = fact;
    }

    bool IsReviewChild(OperationProvenance operation, string kind) => _pending is { } accepted &&
        operation.LogicalId != Uuid.Empty && operation.Owner == Stream &&
        (operation.FirstOwner ?? operation.Owner) == Stream && operation.AffectedOwner == Stream &&
        operation.ExpectedRevisionOwner == Stream && operation.RootOperationId == accepted.Operation.RootOperationId &&
        operation.CausationEventId == accepted.Metadata.EventId && operation.CommandKind == kind &&
        operation.Actor == accepted.Operation.Actor && operation.Episode == accepted.Operation.Episode &&
        operation.OriginalExpectedRevision == accepted.Operation.OriginalExpectedRevision &&
        operation.AuthoredIntentSha256 == accepted.Operation.AuthoredIntentSha256 && operation.AcceptedAt != default;

    Result PrepareOwn(OperationProvenance operation, Func<long, Result> prepare)
    {
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected ||
            operation.RootOperationId != operation.LogicalId || operation.LogicalId == Uuid.Empty ||
            operation.CausationEventId is not null || (operation.FirstOwner ?? operation.Owner) != Stream ||
            operation.AffectedOwner != Stream || operation.ExpectedRevisionOwner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An action write is a root operation of its own action with an expected revision."));
        return prepare((long)expected);
    }

    Result Stale(long expectedRevision) => expectedRevision != _revision
        ? Fail(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("risk treatment action", _revision)))
        : Result.Success;

    static Result Pending() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "A completion review of this treatment action is in progress; retry when it settles.", true));

    static Result Fail(CommandFailure failure) => Result.Failure(failure.ToRequestError());
}
