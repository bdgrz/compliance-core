using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
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
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
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
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Remediation.Aggregates;

/// <summary>
///     One finding: its raised terms, triage, acceptance links, the admitted corrective actions of its generations
///     and the closure decision. The logical revision counts every fact of this finding stream, and the generation
///     starts at one and advances on every reopen. A corrective action is admitted here and materialized as its own
///     owner; a closure seals admission for its generation and only closes once the closure process appends
///     <see cref="FindingClosedV2" />. Reducers never read content or another owner.
/// </summary>
public sealed class FindingAggregate : Aggregate
{
    /// <summary>Stream area of every finding (the realm is the tenant).</summary>
    public const string Area = "findings";

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _findingId;
    readonly Uuid _selectedClosureRoot;
    CorrectiveActionAdmitted? _selectedAdmission;
    FindingClosureAccepted? _selectedClosure;
    DomainEvent? _selectedTerminal;
    DomainEvent? _latest;
    long _admittedCount;
    readonly Dictionary<Uuid, FindingCorrectiveActionResolved?> _actionOutcomes = [];
    FindingRaisedV2? _raised;
    FindingRevisedV2? _revised;
    FindingAnalysisRevised? _analysis;
    FindingClosureAccepted? _pending;
    FindingClosureProposed? _proposal;
    readonly Dictionary<Uuid, long> _proposalRevisions = [];
    readonly Dictionary<Uuid, HashSet<Uuid>> _proposalAuthors = [];
    FindingClosedV2? _closed;
    long _revision;
    long _generation;

    /// <summary>Opens one finding, selecting the logical operation to resolve.</summary>
    public FindingAggregate(Uuid tenantId, Uuid programId, Uuid findingId, Uuid selectedLogicalId = default,
        Uuid selectedClosureRoot = default)
        : base(StreamId(programId, findingId), new EventStreamAddress(tenantId.ToString(), Area,
            StreamId(programId, findingId).ToString()))
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || findingId == Uuid.Empty)
            throw new ArgumentException("A finding stream needs tenant, program, and finding IDs.");
        (_tenantId, _programId, _findingId) = (tenantId, programId, findingId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        _selectedClosureRoot = selectedClosureRoot;
        On<FindingRaisedV2>(Apply);
        On<FindingRevisedV2>(Apply);
        On<FindingAnalysisRevised>(Apply);
        On<CorrectiveActionAdmitted>(Apply);
        On<FindingCorrectiveActionResolved>(Apply);
        On<FindingAcceptanceLinkedV2>(Apply);
        On<FindingClosureAccepted>(Apply);
        On<FindingClosureProposed>(Apply);
        On<FindingClosureWithdrawn>(Apply);
        On<FindingClosureReturned>(Apply);
        On<FindingClosedV2>(Apply);
        On<FindingClosureCanceled>(Apply);
        On<FindingReopenedV2>(Apply);
    }

    /// <summary>The stream identity of a finding.</summary>
    public static Uuid StreamId(Uuid programId, Uuid findingId)
    {
        if (programId == Uuid.Empty || findingId == Uuid.Empty)
            throw new ArgumentException("A finding stream needs program and finding IDs.");
        return Uuid.CreateVersion5(programId, $"finding:{findingId}");
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The program the finding belongs to.</summary>
    public Uuid ProgramId => _programId;

    /// <summary>The finding identity.</summary>
    public Uuid FindingId => _findingId;

    /// <summary>True once the finding was raised.</summary>
    public bool IsRecorded => _raised is not null;

    /// <summary>The raise fact, once recorded.</summary>
    public FindingRaisedV2? Raised => _raised;

    /// <summary>The logical revision: the number of facts of this finding.</summary>
    public long Revision => _revision;

    /// <summary>The current generation: one at the raise, plus one for every reopen.</summary>
    public long Generation => _generation;

    /// <summary>The current severity.</summary>
    public string? Severity => _analysis is { } analysis && analysis.Revision > (_revised?.Revision ?? 0)
        ? analysis.Severity : _revised?.Severity ?? _raised?.Severity;

    /// <summary>The latest separately authored team analysis, never the source wording.</summary>
    public string? Analysis => _analysis?.Analysis;

    /// <summary>The exact affected records named by the latest analysis.</summary>
    public IReadOnlyList<FindingLink> AffectedRecords => _analysis?.AffectedRecords ?? [];

    /// <summary>The current owner.</summary>
    public Uuid OwnerMemberId => _revised?.OwnerMemberId ?? _raised?.OwnerMemberId ?? Uuid.Empty;

    /// <summary>The current due date.</summary>
    public DateOnly DueOn => _revised?.DueOn ?? _raised?.DueOn ?? default;

    /// <summary>The current affected scope.</summary>
    public string? AffectedScope => _revised?.AffectedScope ?? _raised?.AffectedScope;

    /// <summary>The current root cause.</summary>
    public string? RootCause => _revised?.RootCause;

    /// <summary>The number of admitted actions across this finding's generations.</summary>
    public long AdmittedCount => _admittedCount;

    /// <summary>The admitted actions whose terminal outcome has not been recorded in this finding.</summary>
    public long UnresolvedActionCount => _actionOutcomes.Count(static item => item.Value is null);

    /// <summary>The closure accepted and not yet closed or canceled, which seals action admission.</summary>
    public FindingClosureAccepted? PendingClosure => _pending;

    /// <summary>The current unclosed proposal revision.</summary>
    public FindingClosureProposed? ClosureProposal => _proposal;

    /// <summary>Every author of one closure's proposal revisions, for source-authoritative review eligibility.</summary>
    public IReadOnlyList<Uuid> ProposalAuthors(Uuid closureId) => _proposalAuthors.TryGetValue(closureId, out var authors)
        ? authors.OrderBy(static id => id.ToString(), StringComparer.Ordinal).ToArray() : [];

    /// <summary>The closure fact of the current generation; null while open or after a reopen.</summary>
    public FindingClosedV2? Closed => _closed;

    /// <summary>The latest committed native fact of this owner.</summary>
    public DomainEvent? Latest => _latest;

    /// <summary>The tenant of this finding.</summary>
    public Uuid TenantId => _tenantId;

    /// <summary>Whether the selected logical action identity has already been admitted.</summary>
    public bool HasAdmitted(Uuid actionId) => actionId == Acceptance.SelectedLogicalId
        ? _selectedAdmission is not null
        : throw new InvalidOperationException("Select the action identity before hydrating its admission guard.");

    /// <summary>The pending or explicitly selected historical closure admission.</summary>
    public FindingClosureAccepted? AcceptedClosure(Uuid rootOperationId) =>
        _pending?.Operation.RootOperationId == rootOperationId ? _pending
        : _selectedClosureRoot == rootOperationId ? _selectedClosure
        : throw new InvalidOperationException("Select the historical closure root before hydrating it.");

    /// <summary>The explicitly selected closure terminal fact; absence is relative to this exact hydrated cut.</summary>
    public DomainEvent? TerminalClosure(Uuid rootOperationId) => _selectedClosureRoot == rootOperationId
        ? _selectedTerminal
        : throw new InvalidOperationException("Select the historical closure root before hydrating its terminal fact.");

    /// <summary>Checks that the finding can take an ordinary write at the expected revision.</summary>
    public Result PrepareWrite(long expectedRevision)
    {
        if (!IsRecorded)
            return Fail(CommandFailure.MissingRecord("The finding was not found."));
        if (expectedRevision != _revision)
            return Fail(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("finding", _revision)));
        if (_pending is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A closure of this finding is in progress; retry when it settles.", true));
        if (_proposal is not null)
            return Fail(CommandFailure.StateConflict("A closure proposal is awaiting review; return or withdraw it before changing the finding."));
        return _closed is not null
            ? Fail(CommandFailure.StateConflict("The finding is closed. Reopen it to change it."))
            : Result.Success;
    }

    /// <summary>Checks closure against the finding's own admitted actions and their recorded outcomes.</summary>
    public Result PrepareClosure(long expectedRevision)
    {
        var prepared = PrepareWrite(expectedRevision);
        if (!prepared.IsSuccess)
            return prepared;
        return _admittedCount == 0
            ? Fail(CommandFailure.StateConflict(
                "Closing a finding requires completed corrective work; an acceptance is not remediation."))
            : UnresolvedActionCount != 0
                ? Fail(CommandFailure.StateConflict("Every planned corrective action must have its outcome recorded in this finding before closure."))
            : Result.Success;
    }

    /// <summary>Checks that a closed finding can be reopened at the expected revision.</summary>
    public Result PrepareReopen(long expectedRevision)
    {
        if (!IsRecorded)
            return Fail(CommandFailure.MissingRecord("The finding was not found."));
        if (expectedRevision != _revision)
            return Fail(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("finding", _revision)));
        return _closed is null
            ? Fail(CommandFailure.StateConflict("Only a closed finding can be reopened."))
            : Result.Success;
    }

    /// <summary>Checks proposal terms and the finding's own resolved action set without recording a fact.</summary>
    public Result PrepareProposal(long expectedRevision, Uuid closureId, long previousProposalRevision, FindingClosureTerms terms)
    {
        var prepared = PrepareWrite(expectedRevision);
        if (!prepared.IsSuccess)
            return prepared;
        if (UnresolvedActionCount != 0)
            return Fail(CommandFailure.StateConflict("Every planned action must have its outcome recorded before proposing closure."));
        if (closureId == Uuid.Empty || previousProposalRevision != _proposalRevisions.GetValueOrDefault(closureId))
            return Fail(CommandFailure.StateConflict("The closure proposal lineage changed before this proposal."));
        return FindingClosureProposalRules.ValidTerms(terms) ? Result.Success : InvalidProposal();
    }

    /// <summary>Records a proposal using the finding's own resolved action set.</summary>
    public Result ProposeClosure(OperationProvenance operation, Uuid closureId, long previousProposalRevision,
        FindingClosureTerms terms, ImmutableSelectionManifest actions, ImmutableContentRef firstResponse,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!OwnProposalOperation(operation, nameof(ProposeFindingClosure), out var expected))
            return InvalidProposal();
        var prepared = PrepareProposal(expected, closureId, previousProposalRevision, terms);
        if (!prepared.IsSuccess)
            return prepared;
        if (!FindingClosureProposalRules.ValidActions(actions, _admittedCount))
            return InvalidProposal();
        var frozen = terms with
        {
            ResolutionEvidence = Array.AsReadOnly(terms.ResolutionEvidence.ToArray()),
            VerificationRationale = terms.VerificationRationale.Trim(), Rationale = terms.Rationale.Trim(),
        };
        RaiseEvent(new FindingClosureProposed(operation.LogicalId, _tenantId, _programId, _findingId,
            _revision + 1, closureId, previousProposalRevision + 1, frozen, actions, operation.Actor,
            operation.AcceptedAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Checks the exact current proposer and any committed review source before withdrawal.</summary>
    public Result PrepareWithdrawal(OperationProvenance operation, Uuid closureId, long proposalRevision)
    {
        var prepared = PrepareProposalDecision(operation, nameof(WithdrawFindingClosure), closureId, proposalRevision, true);
        if (!prepared.IsSuccess)
            return prepared;
        if (FindingClosureProposalRules.MemberId(operation.Actor) != FindingClosureProposalRules.MemberId(_proposal!.ProposedBy))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Only this proposal revision's proposer may withdraw it."));
        return _pending is not null && (_pending.Metadata.EventId == Uuid.Empty || _pending.Metadata.AggregateVersion == 0)
            ? Fail(CommandFailure.StateConflict("The accepted review must be committed before withdrawal can bind its source."))
            : Result.Success;
    }

    /// <summary>The proposer withdraws the exact current proposal.</summary>
    public Result WithdrawClosure(OperationProvenance operation, Uuid closureId, long proposalRevision,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var prepared = PrepareWithdrawal(operation, closureId, proposalRevision);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new FindingClosureWithdrawn(operation.LogicalId, _tenantId, _programId, _findingId,
            _revision + 1, closureId, proposalRevision, operation.Actor, operation.AcceptedAt,
            operation, firstResponse, operationResult, _pending is { } pending ? CancellationOf(pending) : null));
        return Result.Success;
    }

    /// <summary>Checks the current proposal, reviewer and exact waiver without recording a review.</summary>
    public Result PrepareReview(OperationProvenance operation, Uuid closureId, long proposalRevision, bool approve,
        string? comments, SeparationOfDutiesWaiverProof? waiver)
    {
        var prepared = PrepareProposalDecision(operation, nameof(ReviewFindingClosure), closureId, proposalRevision);
        if (!prepared.IsSuccess)
            return prepared;
        var review = new FindingClosureReviewBinding(closureId, proposalRevision, FindingClosureProposalRules.MemberId(operation.Actor),
            operation.Actor, operation.AcceptedAt, waiver);
        if (!CanReview(review, approve))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "A proposal author needs a waiver for this exact proposal revision, reviewer and review action."));
        if (approve && UnresolvedActionCount != 0)
            return Fail(CommandFailure.StateConflict("Approval requires every planned action outcome in this finding."));
        return !approve && !FindingRules.IsBoundedText(comments, FindingRules.MaximumTextLength) ? InvalidProposal() : Result.Success;
    }

    /// <summary>Returns a proposal or admits its exact independent review to the existing final proof path.</summary>
    public Result ReviewClosure(OperationProvenance operation, Uuid closureId, long proposalRevision, bool approve,
        string? comments, SeparationOfDutiesWaiverProof? waiver, ImmutableContentRef? intent,
        ImmutableContentRef firstResponse, ImmutableContentRef? operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var prepared = PrepareReview(operation, closureId, proposalRevision, approve, comments, waiver);
        if (!prepared.IsSuccess)
            return prepared;
        var review = new FindingClosureReviewBinding(closureId, proposalRevision, FindingClosureProposalRules.MemberId(operation.Actor),
            operation.Actor, operation.AcceptedAt, waiver);
        if (approve)
        {
            if (UnresolvedActionCount != 0 || intent is not { Schema: ClosureIntent.Schema })
                return Fail(CommandFailure.StateConflict("Approval requires all planned outcomes and its frozen final-proof intent."));
            RaiseEvent(new FindingClosureAccepted(Stream, _revision + 1, _generation, intent, null,
                operation, firstResponse, null, review));
        }
        else
        {
            if (!FindingRules.IsBoundedText(comments, FindingRules.MaximumTextLength) || operationResult is null)
                return InvalidProposal();
            RaiseEvent(new FindingClosureReturned(operation.LogicalId, _tenantId, _programId, _findingId,
                _revision + 1, comments!.Trim(), review, operation, firstResponse, operationResult));
        }
        return Result.Success;
    }

    /// <summary>Checks that the finding does not exist yet.</summary>
    public Result PrepareRaise() => IsRecorded
        ? Fail(CommandFailure.StateConflict("This finding identity already has a different raised record."))
        : Result.Success;

    /// <summary>Raises the finding as the root native fact of its operation, with its initial action when routed.</summary>
    public Result Raise(OperationProvenance operation, FindingRaiseTerms terms, ImmutableContentRef links,
        ImmutableContentRef? firstResponse, ImmutableContentRef? operationResult, InitialActionAdmission? initial)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(links);
        if (operation.Owner != Stream || operation.LogicalId != _findingId)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A raise is the logical operation of its own finding."));
        if (initial is not null && initial.Operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The initial corrective action is admitted by its own finding."));
        var prepared = PrepareRaise();
        if (!prepared.IsSuccess)
            return prepared;
        if (!FindingRules.IsSeverity(terms.Severity))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A finding severity must be low, moderate, or high."));
        RaiseEvent(new FindingRaisedV2(operation.LogicalId, _tenantId, _programId, _findingId, 1, terms.Source,
            terms.Title, terms.Description, terms.Severity, terms.AffectedScope, terms.OwnerMemberId, terms.DueOn,
            links, terms.RaisedBy, terms.RaisedAt, terms.CreationKind, terms.InitialActionSha256, operation,
            firstResponse, operationResult));
        if (initial is not null)
            RaiseEvent(new CorrectiveActionAdmitted(Stream, 2, 1, initial.Intent, null, initial.Operation, null, null));
        return Result.Success;
    }

    /// <summary>Records a triage revision as the root native fact of its operation.</summary>
    public Result Revise(OperationProvenance operation, FindingRevision revision, ImmutableContentRef firstResponse,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(revision);
        var prepared = PrepareOwnWrite(operation);
        if (!prepared.IsSuccess)
            return prepared;
        if (!FindingRules.IsSeverity(revision.Severity))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A finding severity must be low, moderate, or high."));
        RaiseEvent(new FindingRevisedV2(operation.LogicalId, _tenantId, _programId, _findingId, _revision + 1,
            revision.Severity, revision.OwnerMemberId, revision.DueOn, revision.AffectedScope, revision.RootCause,
            revision.Reason, revision.RevisedBy, revision.RevisedAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Checks an analysis revision without changing source wording, ownership or due dates.</summary>
    public Result PrepareAnalysis(long expectedRevision, string analysis, string severity, IReadOnlyList<FindingLink> affectedRecords)
    {
        var prepared = PrepareWrite(expectedRevision);
        if (!prepared.IsSuccess)
            return prepared;
        if (FindingRules.ValidateAnalysis(analysis, severity, affectedRecords) is { } invalid)
            return Fail(invalid);
        return Analysis == analysis.Trim() && Severity == severity && AffectedRecords.SequenceEqual(FindingRules.Trimmed(affectedRecords))
            ? Fail(CommandFailure.InvalidContent("The finding analysis changes nothing.")) : Result.Success;
    }

    /// <summary>Records one named analysis fact; source wording and ownership remain unchanged.</summary>
    public Result ReviseAnalysis(OperationProvenance operation, string analysis, string severity,
        IReadOnlyList<FindingLink> affectedRecords, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!OwnProposalOperation(operation, nameof(ReviseFindingAnalysis), out var expected))
            return Fail(CommandFailure.InvalidContent("Analysis requires its exact member-authored native operation."));
        var prepared = PrepareAnalysis(expected, analysis, severity, affectedRecords);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new FindingAnalysisRevised(operation.LogicalId, _tenantId, _programId, _findingId, _revision + 1,
            analysis.Trim(), severity, FindingRules.Trimmed(affectedRecords), operation.Actor, operation.AcceptedAt,
            operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Links an acceptance as the root native fact of its operation.</summary>
    public Result LinkAcceptance(OperationProvenance operation, ImmutableContentRef acceptance,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(acceptance);
        var prepared = PrepareOwnWrite(operation);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new FindingAcceptanceLinkedV2(operation.LogicalId, _tenantId, _programId, _findingId,
            _revision + 1, acceptance, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Admits one corrective action for the current generation as the root native fact of its operation.</summary>
    public Result AdmitAction(OperationProvenance operation, ImmutableContentRef intent, ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        var prepared = PrepareOwnWrite(operation);
        if (!prepared.IsSuccess)
            return prepared;
        if (_actionOutcomes.ContainsKey(operation.LogicalId))
            return Fail(CommandFailure.StateConflict("A corrective action identity cannot be reused."));
        RaiseEvent(new CorrectiveActionAdmitted(Stream, _revision + 1, _generation, intent, null, operation,
            firstResponse, null));
        return Result.Success;
    }

    /// <summary>Records the exact terminal outcome of an action admitted by this finding.</summary>
    public Result RecordActionOutcome(OperationProvenance operation, Uuid actionId, string outcome,
        NativeFactReference source, ImmutableContentRef sourceProof)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!ValidOutcome(operation, actionId, outcome, source, sourceProof))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An outcome requires its exact terminal action source and stable system reporting operation."));
        if (_raised is null)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The finding is not visible yet.", true));
        if (!_actionOutcomes.TryGetValue(actionId, out var prior))
            return Fail(CommandFailure.StateConflict("The corrective action was not planned by this finding."));
        if (prior is not null)
            return prior.Outcome == outcome && prior.Source == source && prior.SourceProof == sourceProof
                ? Result.Success
                : Fail(CommandFailure.StateConflict("The corrective action already has a different recorded outcome."));
        if (_pending is not null || _closed is not null)
            return Fail(CommandFailure.StateConflict("A new action outcome cannot change a closing or closed finding."));
        RaiseEvent(new FindingCorrectiveActionResolved(operation.LogicalId, _tenantId, _programId, _findingId,
            _revision + 1, actionId, outcome, source, sourceProof, operation));
        return Result.Success;
    }

    /// <summary>Accepts a closure as the root native fact of its operation; it seals admission for the generation.</summary>
    public Result AcceptClosure(OperationProvenance operation, ImmutableContentRef intent, ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (!IsRecorded)
            return Fail(CommandFailure.MissingRecord("The finding was not found."));
        return Fail(CommandFailure.StateConflict("Finding closure requires a current proposal and its independent review."));
    }

    /// <summary>Appends the closure of the pending operation; the caller proved every held action.</summary>
    public Result CompleteClosure(OperationProvenance operation, ImmutableContentRef closureContent,
        ImmutableContentRef actionCompletionProofs, ImmutableContentRef intent, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_pending is null || _pending.Review is null || !CurrentProposal(_pending.Review.ClosureId, _pending.Review.ProposalRevision) ||
            UnresolvedActionCount != 0 || _pending.Operation.RootOperationId != operation.RootOperationId ||
            _pending.Intent != intent || operation.Owner != Stream || operation.Actor != _pending.Operation.Actor)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only the exact pending closure can close the finding.", false));
        RaiseEvent(new FindingClosedV2(operation.RootOperationId, _tenantId, _programId, _findingId, _revision + 1,
            closureContent, operation, _generation, actionCompletionProofs, intent, null, operationResult, _pending.Review));
        return Result.Success;
    }

    /// <summary>Cancels the pending closure of the operation and fences its delayed completion.</summary>
    public Result CancelClosure(OperationProvenance operation, ImmutableContentRef failureProof)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_pending is null || _pending.Operation.RootOperationId != operation.RootOperationId ||
            operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only the exact pending closure can be canceled.", false));
        RaiseEvent(new FindingClosureCanceled(Stream, _revision + 1, _generation, _pending.Intent, failureProof,
            operation, null, null));
        return Result.Success;
    }

    /// <summary>Reopens the closed finding as the root native fact of its operation; the generation advances.</summary>
    public Result Reopen(OperationProvenance operation, string reason, ActorReference reopenedBy,
        DateTimeOffset reopenedAt, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!OwnProposalOperation(operation, nameof(ReopenFinding), out var expected))
            return InvalidProposal();
        var prepared = PrepareReopen(expected);
        if (!prepared.IsSuccess)
            return prepared;
        if (!FindingRules.IsBoundedText(reason, FindingRules.MaximumTextLength) || FindingClosureProposalRules.MemberId(reopenedBy) == Uuid.Empty ||
            reopenedBy != operation.Actor || reopenedAt != operation.AcceptedAt)
            return InvalidProposal();
        RaiseEvent(new FindingReopenedV2(operation.LogicalId, _tenantId, _programId, _findingId, _revision + 1,
            reason.Trim(), reopenedBy, reopenedAt, operation, firstResponse, operationResult,
            _closed!.Review?.ClosureId ?? _closed.RequestId, _closed.Review?.ProposalRevision));
        return Result.Success;
    }

    void Apply(FindingRaisedV2 ev)
    {
        Acceptance.Observe(ev);
        if (_raised is not null || ev.TenantId != _tenantId || ev.ProgramId != _programId ||
            ev.FindingId != _findingId || ev.Revision != 1 || ev.Operation.Owner != Stream ||
            ev.Operation.LogicalId != _findingId || !FindingRules.IsSeverity(ev.Severity))
            throw new InvalidOperationException("A finding stream is raised once, at revision one, by its own operation.");
        _raised = ev;
        _revision = 1;
        _generation = 1;
        _latest = ev;
    }

    void Apply(FindingRevisedV2 ev)
    {
        Acceptance.Observe(ev);
        EnsureWritable(ev.TenantId, ev.ProgramId, ev.FindingId, ev.Revision, ev.Operation);
        if (!FindingRules.IsSeverity(ev.Severity))
            throw new InvalidOperationException("A finding revision must name low, moderate, or high severity.");
        _revised = ev;
        Advance(ev);
    }

    void Apply(FindingAnalysisRevised ev)
    {
        Acceptance.Observe(ev);
        EnsureWritable(ev.TenantId, ev.ProgramId, ev.FindingId, ev.Revision, ev.Operation);
        if (FindingRules.ValidateAnalysis(ev.Analysis, ev.Severity, ev.AffectedRecords) is not null ||
            ev.Analysis != ev.Analysis.Trim() || ev.RevisedBy != ev.Operation.Actor || ev.RevisedAt != ev.Operation.AcceptedAt ||
            !OwnProposalOperation(ev.Operation, nameof(ReviseFindingAnalysis), out var expected) || expected != _revision)
            throw new InvalidOperationException("Finding analysis must retain its exact scope, actor and predecessor.");
        _analysis = ev;
        Advance(ev);
    }

    void Apply(CorrectiveActionAdmitted ev)
    {
        Acceptance.Observe(ev);
        if (_raised is null || _pending is not null || _proposal is not null || _closed is not null || ev.Owner != Stream ||
            ev.Revision != _revision + 1 || ev.Generation != _generation || ev.Operation.Owner != Stream ||
            _actionOutcomes.ContainsKey(ev.Operation.LogicalId))
            throw new InvalidOperationException(
                "An action is admitted once, for the current open generation, at the next revision.");
        _admittedCount = checked(_admittedCount + 1);
        _actionOutcomes.Add(ev.Operation.LogicalId, null);
        if (ev.Operation.LogicalId == Acceptance.SelectedLogicalId)
            _selectedAdmission = ev;
        Advance(ev);
    }

    void Apply(FindingCorrectiveActionResolved ev)
    {
        Acceptance.Observe(ev);
        EnsureWritable(ev.TenantId, ev.ProgramId, ev.FindingId, ev.Revision, ev.Operation);
        if (ev.RequestId != ev.Operation.LogicalId ||
            !ValidOutcome(ev.Operation, ev.ActionId, ev.Outcome, ev.Source, ev.SourceProof) ||
            !_actionOutcomes.TryGetValue(ev.ActionId, out var prior) || prior is not null)
            throw new InvalidOperationException("An admitted action has one exact terminal outcome in its finding.");
        _actionOutcomes[ev.ActionId] = ev;
        Advance(ev);
    }

    void Apply(FindingAcceptanceLinkedV2 ev)
    {
        Acceptance.Observe(ev);
        EnsureWritable(ev.TenantId, ev.ProgramId, ev.FindingId, ev.Revision, ev.Operation);
        Advance(ev);
    }

    void Apply(FindingClosureProposed ev)
    {
        Acceptance.Observe(ev);
        EnsureWritable(ev.TenantId, ev.ProgramId, ev.FindingId, ev.Revision, ev.Operation);
        var author = FindingClosureProposalRules.MemberId(ev.ProposedBy);
        if (UnresolvedActionCount != 0 || ev.RequestId != ev.Operation.LogicalId || ev.ClosureId == Uuid.Empty ||
            ev.ProposalRevision != checked(_proposalRevisions.GetValueOrDefault(ev.ClosureId) + 1) ||
            !NativeOwnerHistory.IsOwnRoot(this, ev.Operation, nameof(ProposeFindingClosure), (ulong)_revision) ||
            author == Uuid.Empty || ev.ProposedBy != ev.Operation.Actor || ev.ProposedAt != ev.Operation.AcceptedAt ||
            !FindingClosureProposalRules.ValidTerms(ev.Terms) ||
            ev.Terms.VerificationRationale != ev.Terms.VerificationRationale.Trim() || ev.Terms.Rationale != ev.Terms.Rationale.Trim() ||
            !FindingClosureProposalRules.ValidActions(ev.ActionsReliedOn, _admittedCount))
            throw new InvalidOperationException("A proposal records the next revision of its lineage over resolved admitted work.");
        _proposalRevisions[ev.ClosureId] = ev.ProposalRevision;
        if (!_proposalAuthors.TryGetValue(ev.ClosureId, out var authors))
        {
            authors = [];
            _proposalAuthors.Add(ev.ClosureId, authors);
        }
        authors.Add(author);
        _proposal = ev;
        Advance(ev);
    }

    void Apply(FindingClosureWithdrawn ev)
    {
        Acceptance.Observe(ev);
        if (_closed is not null || !CurrentProposal(ev.ClosureId, ev.ProposalRevision) ||
            ev.TenantId != _tenantId || ev.ProgramId != _programId || ev.FindingId != _findingId ||
            ev.Revision != _revision + 1 || ev.RequestId != ev.Operation.LogicalId ||
            !NativeOwnerHistory.IsOwnRoot(this, ev.Operation, nameof(WithdrawFindingClosure), (ulong)_revision) ||
            ev.WithdrawnBy != ev.Operation.Actor || ev.WithdrawnAt != ev.Operation.AcceptedAt ||
            FindingClosureProposalRules.MemberId(ev.WithdrawnBy) != FindingClosureProposalRules.MemberId(_proposal!.ProposedBy) ||
            !MatchesPendingCancellation(ev.CanceledReview))
            throw new InvalidOperationException("Only the proposer withdraws the exact current proposal and fences its exact accepted review.");
        if (ev.CanceledReview?.RootOperationId == _selectedClosureRoot)
            _selectedTerminal = ev;
        _pending = null;
        _proposal = null;
        Advance(ev);
    }

    void Apply(FindingClosureReturned ev)
    {
        Acceptance.Observe(ev);
        if (_closed is not null || _pending is not null || !CurrentProposal(ev.Review.ClosureId, ev.Review.ProposalRevision) ||
            ev.TenantId != _tenantId || ev.ProgramId != _programId || ev.FindingId != _findingId ||
            ev.Revision != _revision + 1 || ev.RequestId != ev.Operation.LogicalId ||
            !NativeOwnerHistory.IsOwnRoot(this, ev.Operation, nameof(ReviewFindingClosure), (ulong)_revision) ||
            ev.Review.ReviewedBy != ev.Operation.Actor || ev.Review.ReviewedAt != ev.Operation.AcceptedAt ||
            !CanReview(ev.Review, false) || !FindingRules.IsBoundedText(ev.Comments, FindingRules.MaximumTextLength) ||
            ev.Comments != ev.Comments.Trim())
            throw new InvalidOperationException("An independent reviewer returns the exact proposal revision with comments.");
        _proposal = null;
        Advance(ev);
    }

    void Apply(FindingClosureAccepted ev)
    {
        Acceptance.Observe(ev);
        if (_raised is null || _pending is not null || _closed is not null || UnresolvedActionCount != 0 ||
            ev.Owner != Stream || ev.Revision != _revision + 1 || ev.Generation != _generation ||
            ev.Operation.Owner != Stream || ev.Operation.RootOperationId != ev.Operation.LogicalId ||
            ev.Operation.RootOperationId == _selectedClosureRoot && _selectedClosure is not null ||
            (ev.Review is null ? _proposal is not null || _admittedCount == 0 :
                !CurrentProposal(ev.Review.ClosureId, ev.Review.ProposalRevision) || !CanReview(ev.Review, true) ||
                ev.Review.ReviewedBy != ev.Operation.Actor || ev.Review.ReviewedAt != ev.Operation.AcceptedAt ||
                ev.Intent.Schema != ClosureIntent.Schema ||
                !NativeOwnerHistory.IsOwnRoot(this, ev.Operation, nameof(ReviewFindingClosure), (ulong)_revision)))
            throw new InvalidOperationException(
                "A closure is accepted once, with admitted actions, for the current open generation.");
        _pending = ev;
        if (ev.Operation.RootOperationId == _selectedClosureRoot)
            _selectedClosure = ev;
        Advance(ev);
    }

    void Apply(FindingClosedV2 ev)
    {
        Acceptance.Observe(ev);
        if (_pending is null || UnresolvedActionCount != 0 || ev.TenantId != _tenantId || ev.ProgramId != _programId || ev.FindingId != _findingId ||
            ev.Revision != _revision + 1 || ev.FindingGeneration != _generation || ev.Intent != _pending.Intent ||
            ev.Operation.RootOperationId != _pending.Operation.RootOperationId || ev.Operation.Owner != Stream ||
            ev.Operation.Actor != _pending.Operation.Actor ||
            ev.Review != _pending.Review || ev.Review is not null && !CurrentProposal(ev.Review.ClosureId, ev.Review.ProposalRevision))
            throw new InvalidOperationException("Only the exact pending closure of the current generation closes the finding.");
        if (ev.Operation.RootOperationId == _selectedClosureRoot)
            _selectedTerminal = ev;
        _closed = ev;
        _pending = null;
        _proposal = null;
        Advance(ev);
    }

    void Apply(FindingClosureCanceled ev)
    {
        Acceptance.Observe(ev);
        if (_pending is null || ev.Owner != Stream || ev.Revision != _revision + 1 || ev.Generation != _generation ||
            ev.Intent != _pending.Intent || ev.Operation.RootOperationId != _pending.Operation.RootOperationId ||
            ev.Operation.Owner != Stream)
            throw new InvalidOperationException("Only the exact pending closure of the current generation is canceled.");
        if (ev.Operation.RootOperationId == _selectedClosureRoot)
            _selectedTerminal = ev;
        _pending = null;
        Advance(ev);
    }

    void Apply(FindingReopenedV2 ev)
    {
        Acceptance.Observe(ev);
        if (_raised is null || _closed is null || ev.TenantId != _tenantId || ev.ProgramId != _programId ||
            ev.FindingId != _findingId || ev.Revision != _revision + 1 || ev.Operation.Owner != Stream ||
            !FindingRules.IsBoundedText(ev.Reason, FindingRules.MaximumTextLength) ||
            ev.ReopenedBy != ev.Operation.Actor || ev.ReopenedAt != ev.Operation.AcceptedAt ||
            ev.PreviousClosureId is not null && (FindingClosureProposalRules.MemberId(ev.ReopenedBy) == Uuid.Empty ||
                !NativeOwnerHistory.IsOwnRoot(this, ev.Operation, nameof(ReopenFinding), (ulong)_revision)) ||
            ev.PreviousClosureId is { } previous && previous != (_closed.Review?.ClosureId ?? _closed.RequestId) ||
            ev.PreviousProposalRevision is { } previousRevision && previousRevision != _closed.Review?.ProposalRevision)
            throw new InvalidOperationException("Only a closed finding is reopened, at the next revision.");
        _closed = null;
        _generation++;
        Advance(ev);
    }

    void EnsureWritable(Uuid tenantId, Uuid programId, Uuid findingId, long revision, OperationProvenance operation)
    {
        if (_raised is null || _pending is not null || _closed is not null || _proposal is not null || tenantId != _tenantId ||
            programId != _programId || findingId != _findingId || revision != _revision + 1 ||
            operation.Owner != Stream)
            throw new InvalidOperationException("A finding fact follows the raise at the next revision of an open finding.");
    }

    void Advance(DomainEvent fact)
    {
        _revision++;
        _latest = fact;
    }

    bool ValidOutcome(OperationProvenance operation, Uuid actionId, string? outcome,
        NativeFactReference? source, ImmutableContentRef? sourceProof) =>
        actionId != Uuid.Empty && outcome is "completed" or "cancelled" && source is not null &&
        source.EventId != Uuid.Empty && source.AggregateVersion > 0 &&
        source.Stream == new CorrectiveAction(_tenantId, _programId, _findingId, actionId).Stream &&
        sourceProof is { Schema: SourceFactProofs.RequiredFactsSchema } &&
        operation.Owner == Stream && operation.RootOperationId == operation.LogicalId &&
        operation.LogicalId == Uuid.CreateVersion5(source.EventId, nameof(RecordCorrectiveActionOutcome)) &&
        operation.CommandKind == nameof(RecordCorrectiveActionOutcome) && operation.OriginalExpectedRevision is null &&
        operation.CausationEventId == source.EventId && operation.Actor.Kind == "system_process";

    Result PrepareOwnWrite(OperationProvenance operation)
    {
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A finding write is a root operation of its own finding with an expected revision."));
        return PrepareWrite((long)expected);
    }

    bool OwnProposalOperation(OperationProvenance operation, string commandKind, out long expected)
    {
        expected = operation.OriginalExpectedRevision is { } revision && revision <= long.MaxValue ? (long)revision : -1;
        return expected >= 0 && FindingClosureProposalRules.MemberId(operation.Actor) != Uuid.Empty &&
            NativeOwnerHistory.IsOwnRoot(this, operation, commandKind, (ulong)expected);
    }

    Result PrepareProposalDecision(OperationProvenance operation, string commandKind, Uuid closureId, long proposalRevision,
        bool allowAcceptedReview = false)
    {
        if (!OwnProposalOperation(operation, commandKind, out var expected))
            return InvalidProposal();
        if (_raised is null)
            return Fail(CommandFailure.MissingRecord("The finding was not found."));
        if (expected != _revision)
            return Fail(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("finding", _revision)));
        if (_pending is not null && !allowAcceptedReview)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The accepted review must settle before another proposal decision.", true));
        return _closed is not null || !CurrentProposal(closureId, proposalRevision)
            ? Fail(CommandFailure.StateConflict("This is not the finding's current closure proposal revision."))
            : Result.Success;
    }

    bool CurrentProposal(Uuid closureId, long proposalRevision) => _proposal is not null &&
        _proposal.ClosureId == closureId && _proposal.ProposalRevision == proposalRevision;

    bool CanReview(FindingClosureReviewBinding review, bool approve) => _proposal is not null &&
        FindingClosureProposalRules.CanReview(_tenantId, _findingId, _proposal,
            _proposalAuthors[_proposal.ClosureId], review, approve);

    bool MatchesPendingCancellation(FindingClosureReviewCancellation? canceled) => _pending is null ? canceled is null :
        _pending.Review is not null && canceled is not null && canceled.Admission.EventId != Uuid.Empty &&
        canceled.Admission.AggregateVersion > 0 && canceled == CancellationOf(_pending);

    static FindingClosureReviewCancellation CancellationOf(FindingClosureAccepted accepted) => new(
        NativeFactReference.Of(accepted.Owner, accepted), accepted.Intent, accepted.Operation.RootOperationId, accepted.Generation);

    static Result InvalidProposal() => Fail(CommandFailure.InvalidContent(
        "A closure proposal requires its exact native member operation, immutable artifact versions and resolution rationales."));

    static Result Fail(CommandFailure failure) => Result.Failure(failure.ToRequestError());
}
