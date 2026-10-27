using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Services;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Services;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
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
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Responsibilities.Commands;
using Bdgrz.Compliance.Features.Responsibilities.Events;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Boundaries.Rules;

public sealed class BoundaryRules
{
    readonly Dictionary<Type, Action<IBoundaryMutationContent>> _reducers = [];
    readonly List<IBoundaryMutationContent> _pendingChanges = [];
    readonly List<IBoundaryMutationContent> _appliedChanges = [];
    readonly Dictionary<Uuid, SeparationOfDutiesWaiverProof> _waivers = [];
    public Uuid Id { get; }
    public Uuid TenantId => _tenantId;
    public ulong SourcePhysicalPosition { get; internal set; }
    public IReadOnlyList<IBoundaryMutationContent> PendingChanges => _pendingChanges.AsReadOnly();
    internal IReadOnlyList<IBoundaryMutationContent> AppliedChanges => _appliedChanges.AsReadOnly();
    public void ClearPendingChanges() => _pendingChanges.Clear();
    void On<T>(Action<T> reduce) where T : IBoundaryMutationContent => _reducers.Add(typeof(T), value => reduce((T)value));
    void RaiseEvent<T>(T value) where T : IBoundaryMutationContent
    {
        Apply(value);
        _pendingChanges.Add(value);
    }
    public void Apply(IBoundaryMutationContent value)
    {
        if (!_reducers.TryGetValue(value.GetType(), out var reduce))
            throw new InvalidOperationException("The boundary change has another content kind.");
        FoldRead(value);
        reduce(value);
        _appliedChanges.Add(value);
    }
    readonly Uuid _tenantId;
    bool _created;
    Uuid _programId;
    Uuid _initialDraftVersionId;
    BoundaryContent? _initialContent;
    Uuid _draftVersionId;
    long _draftRevision;
    BoundaryContent? _draftContent;
    Uuid _draftAuthorMemberId;
    bool _draftEverReviewed;
    Uuid _acceptedReviewDecisionId;
    string? _latestReviewOutcome;
    Uuid _latestApprovedVersionId;
    readonly Dictionary<Uuid, (long Revision, Uuid DecisionId)> _approvedVersions = [];
    readonly Dictionary<ResponsibilityScope, ResponsibilitySet> _responsibilitySets = [];
    readonly Dictionary<ResponsibilityScope, List<ResponsibilityDecisionFact>> _responsibilityDecisions = [];
    DateOnly? _latestApprovedEffectiveFrom;
    DateTimeOffset? _draftChangedAt;
    long _revision;

    public bool IsCreated => _created;
    public bool IsVisible => _draftContent is not null || _latestApprovedVersionId != Uuid.Empty;
    public Uuid ProgramId => _programId;
    public long Revision => _revision;
    public Uuid DraftVersionId => _draftVersionId;
    public long DraftRevision => _draftRevision;
    public Uuid DraftAuthorMemberId => _draftAuthorMemberId;
    public DateTimeOffset? DraftChangedAt => _draftChangedAt;
    public string? LatestReviewOutcome => _latestReviewOutcome;
    public Uuid LatestApprovedVersionId => _latestApprovedVersionId;
    public bool IsVersionApproved(Uuid versionId) => _approvedVersions.ContainsKey(versionId);

    public bool IsVersionApproved(Uuid versionId, long revision, Uuid decisionId) =>
        _approvedVersions.TryGetValue(versionId, out var approval) &&
        approval.Revision == revision && approval.DecisionId == decisionId;

    /// <summary>The initial approved version; any later approval changes the boundary.</summary>
    public Uuid FirstApprovedVersionId { get; private set; }

    public BoundaryRules(Uuid tenantId, Uuid boundaryId)
    {
        Id = boundaryId;
        _tenantId = tenantId;
        On<BoundaryDraftCreatedContent>(ev =>
        {
            _revision++;
            _created = true;
            _programId = ev.ProgramId;
            _initialDraftVersionId = ev.DraftVersionId;
            _initialContent = ev.Content;
            _draftVersionId = ev.DraftVersionId;
            _draftRevision = 1;
            _draftContent = ev.Content;
            _draftAuthorMemberId = ev.AuthorMemberId;
            _draftEverReviewed = false;
            _draftChangedAt = ev.ChangedAt;
            _latestReviewOutcome = null;
        });
        On<BoundaryDraftRevisedContent>(ev =>
        {
            _revision++;
            _draftRevision = ev.Revision;
            _draftContent = ev.Content;
            _draftAuthorMemberId = ev.AuthorMemberId;
            _acceptedReviewDecisionId = Uuid.Empty;
            _draftChangedAt = ev.ChangedAt;
            _latestReviewOutcome = null;
        });
        On<BoundaryReviewedContent>(ev =>
        {
            _revision++;
            _acceptedReviewDecisionId = ev.Outcome == "accept" ? ev.DecisionId : Uuid.Empty;
            _latestReviewOutcome = ev.Outcome;
            _draftEverReviewed = true;
            RecordResponsibilityDecision(new ResponsibilityScope("boundary", Id,
                    ev.DraftVersionId, ev.Revision), ev.ActorMemberId,
                ResponsibilityType.AssignedReviewer, ev.DecidedAt,
                ev.SeparationOfDutiesWaiverId);
        });
        On<BoundaryDraftDiscardedContent>(_ =>
        {
            _revision++;
            _draftVersionId = Uuid.Empty;
            _draftContent = null;
            _acceptedReviewDecisionId = Uuid.Empty;
            _latestReviewOutcome = null;
            _draftChangedAt = null;
        });
        On<BoundaryApprovedContent>(ev =>
        {
            _revision++;
            RecordResponsibilityDecision(new ResponsibilityScope("boundary", Id,
                    ev.DraftVersionId, ev.Revision), ev.ActorMemberId,
                ResponsibilityType.PolicyApprover, ev.DecidedAt,
                ev.SeparationOfDutiesWaiverId);
            if (_approvedVersions.Count == 0)
                FirstApprovedVersionId = ev.DraftVersionId;
            _approvedVersions.Add(ev.DraftVersionId, (ev.Revision, ev.ApprovalDecisionId));
            _latestApprovedVersionId = ev.DraftVersionId;
            _latestApprovedEffectiveFrom = ev.EffectiveFrom;
            _draftVersionId = Uuid.Empty;
            _draftContent = null;
            _acceptedReviewDecisionId = Uuid.Empty;
            _latestReviewOutcome = null;
            _draftChangedAt = null;
        });
        On<BoundarySuccessorProposedContent>(ev =>
        {
            _revision++;
            _draftVersionId = ev.DraftVersionId;
            _draftRevision = 1;
            _draftContent = ev.Content;
            _draftAuthorMemberId = ev.AuthorMemberId;
            _draftEverReviewed = false;
            _acceptedReviewDecisionId = Uuid.Empty;
            _draftChangedAt = ev.ChangedAt;
            _latestReviewOutcome = null;
        });
        On<ResponsibilityAssigned>(ev => GetResponsibilitySet(ev.Scope).Apply(ev));
        On<ResponsibilityRevoked>(ev => GetResponsibilitySet(ev.Scope).Apply(ev));
    }

    public ResponsibilitySet GetResponsibilitySet(ResponsibilityScope scope)
    {
        if (!StringComparer.Ordinal.Equals(scope.RecordType, "boundary") || scope.RecordId != Id)
            throw new ArgumentException("The responsibility scope must belong to this boundary.", nameof(scope));
        if (!_responsibilitySets.TryGetValue(scope, out var set))
        {
            set = new ResponsibilitySet(_tenantId, scope);
            _responsibilitySets.Add(scope, set);
        }
        return set;
    }

    public CommandFailure? AssignResponsibility(ResponsibilityScope scope, Uuid assignmentId,
        Uuid memberId, ResponsibilityType type, Uuid assignedByMemberId,
        string assignedByDisplay, DateTimeOffset assignedAt, DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveUntil, IReadOnlyList<SeparationOfDutiesWaiverProof> waivers)
    {
        if (!IsCurrentResponsibilityScope(scope))
            return CommandFailure.StateConflict(
                "Responsibilities must target the current exact boundary draft revision.");
        var set = GetResponsibilitySet(scope);
        var proposed = new ResponsibilityAssignmentView(_tenantId, assignmentId, memberId,
            type, scope, assignedAt, assignedByMemberId, effectiveFrom, effectiveUntil,
            null, Uuid.Empty, []);
        var historicalConflict = HasUnwaivedHistoricalConflict(scope,
            set.ReadAssignments().Append(proposed));
        if (historicalConflict)
            return CommandFailure.StateConflict(
                "The responsibility would create an unwaived conflict with a prior decision on this exact revision.");
        return set.AssignAuthorized(assignmentId, memberId, type,
            assignedByMemberId, assignedByDisplay, assignedAt, effectiveFrom, effectiveUntil,
            waivers, RaiseEvent);
    }

    public CommandFailure? RevokeResponsibility(ResponsibilityScope scope, Uuid assignmentId,
        Uuid memberId, string memberDisplay, DateTimeOffset revokedAt, string reason)
    {
        if (!IsCurrentResponsibilityScope(scope))
            return CommandFailure.StateConflict(
                "Responsibilities must target the current exact boundary draft revision.");
        return GetResponsibilitySet(scope).Revoke(assignmentId, memberId, memberDisplay,
            revokedAt, reason, RaiseEvent);
    }

    public bool IsCurrentResponsibilityScope(ResponsibilityScope scope) =>
        StringComparer.Ordinal.Equals(scope.RecordType, "boundary") && scope.RecordId == Id &&
        _created && _draftContent is not null && scope.VersionId == _draftVersionId &&
        scope.Revision == _draftRevision;

    void RecordResponsibilityDecision(ResponsibilityScope scope, Uuid memberId,
        ResponsibilityType type, DateTimeOffset at, Uuid? waiverId)
    {
        if (!_responsibilityDecisions.TryGetValue(scope, out var decisions))
        {
            decisions = [];
            _responsibilityDecisions.Add(scope, decisions);
        }
        decisions.Add(new ResponsibilityDecisionFact(memberId, type, at, waiverId));
    }

    bool HasUnwaivedHistoricalConflict(ResponsibilityScope scope,
        IEnumerable<ResponsibilityAssignmentView> assignments)
    {
        if (!_responsibilityDecisions.TryGetValue(scope, out var decisions))
            return false;
        foreach (var decision in decisions)
        {
            if (decision.WaiverId is not null)
                continue;
            var decidedAction = new ResponsibilityAssignmentView(_tenantId, Uuid.Empty,
                decision.MemberId, decision.Type, scope, decision.At, Uuid.Empty,
                decision.At, decision.At.AddTicks(1), null, Uuid.Empty, []);
            if (ResponsibilityConflictPolicy.FindConflicts(assignments, decidedAction).Count > 0)
                return true;
        }
        return false;
    }

    sealed record ResponsibilityDecisionFact(Uuid MemberId, ResponsibilityType Type,
        DateTimeOffset At, Uuid? WaiverId);

    public Result<BoundaryRegistration> Create(Uuid programId, Uuid draftVersionId,
        BoundaryContent content, Uuid authorMemberId, string authorDisplay,
        DateTimeOffset changedAt)
    {
        if (_created)
            return programId == _programId && draftVersionId == _initialDraftVersionId &&
                   Equivalent(content, _initialContent)
                ? Result<BoundaryRegistration>.Success(new BoundaryRegistration(Id, _initialDraftVersionId))
                : Result<BoundaryRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The boundary already exists with different content."));
        if (programId == Uuid.Empty || draftVersionId == Uuid.Empty || authorMemberId == Uuid.Empty)
            return Result<BoundaryRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "The boundary requires a program, draft version, and author."));
        var validation = Validate(content);
        if (validation is not null)
            return Result<BoundaryRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation, validation));
        RaiseEvent(new BoundaryDraftCreatedContent(_tenantId, Id, programId, draftVersionId,
            content, authorMemberId, authorDisplay, changedAt));
        return Result<BoundaryRegistration>.Success(new BoundaryRegistration(Id, draftVersionId));
    }

    public CommandFailure? Revise(Uuid draftVersionId, long expectedRevision,
        BoundaryContent content, Uuid authorMemberId, string authorDisplay,
        DateTimeOffset changedAt)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The boundary was not found.");
        if (_draftVersionId == Uuid.Empty)
            return CommandFailure.StateConflict(
                "The approved boundary is immutable. Propose a successor draft.");
        if (DraftVersionConflict(draftVersionId, expectedRevision) is { } conflict)
            return CommandFailure.ForVersion(conflict);
        var validation = Validate(content);
        if (validation is not null)
            return CommandFailure.InvalidContent(validation);
        RaiseEvent(new BoundaryDraftRevisedContent(_tenantId, Id, draftVersionId,
            _draftRevision + 1, content, authorMemberId, authorDisplay, changedAt));
        return null;
    }

    VersionConflict? DraftVersionConflict(Uuid draftVersionId, long expectedRevision) =>
        _created && _draftVersionId != Uuid.Empty &&
        (draftVersionId != _draftVersionId || expectedRevision != _draftRevision)
            ? VersionedRecordRules.StaleDraft("boundary", _draftVersionId.ToGuid(), _draftRevision)
            : null;

    public CommandFailure? Review(Uuid draftVersionId, long expectedRevision, Uuid decisionId,
        string outcome, string rationale, Uuid reviewerMemberId, string reviewerDisplay,
        DateTimeOffset decidedAt, SeparationOfDutiesWaiverProof? separationOfDutiesWaiver = null)
    {
        var current = CheckDraft(draftVersionId, expectedRevision);
        if (current is not null)
            return current;
        var responsibilityScope = new ResponsibilityScope("boundary", Id, draftVersionId,
            expectedRevision);
        var responsibilityFailure = ResponsibilityDecisionGuard.ValidateAuthorized(
            GetResponsibilitySet(responsibilityScope), responsibilityScope, reviewerMemberId,
            ResponsibilityType.AssignedReviewer, decidedAt,
            reviewerMemberId == _draftAuthorMemberId, separationOfDutiesWaiver);
        if (responsibilityFailure is not null)
            return responsibilityFailure;
        var reviewWaiverScope = new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.Boundary, Id, draftVersionId,
            expectedRevision, SeparationOfDutiesActions.Review);
        if (separationOfDutiesWaiver is not null &&
            (separationOfDutiesWaiver.TenantId != _tenantId ||
             !BoundaryWaiverProof.Allows(separationOfDutiesWaiver, _tenantId, reviewWaiverScope, reviewerMemberId, decidedAt)))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this reviewer and draft revision.");
        if (reviewerMemberId == _draftAuthorMemberId && separationOfDutiesWaiver is null)
            return CommandFailure.ActorProhibited(
                "A boundary author cannot review their own draft.");
        if (outcome is not ("accept" or "request_changes") || string.IsNullOrWhiteSpace(rationale))
            return CommandFailure.InvalidContent("A review requires an outcome and rationale.");
        BoundaryReviewedContent reviewed = new(_tenantId, Id, draftVersionId, expectedRevision,
            decisionId, outcome, reviewerMemberId, reviewerDisplay, rationale.Trim(), decidedAt,
            separationOfDutiesWaiver?.WaiverId)
        {
            StoredActor = ActorReference.ForMember(reviewerMemberId, reviewerDisplay),
        };
        Remember(separationOfDutiesWaiver);
        RaiseEvent(reviewed);
        return null;
    }

    public CommandFailure? DiscardDraft(Uuid draftVersionId, long expectedRevision,
        string rationale, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset discardedAt)
    {
        var current = CheckDraft(draftVersionId, expectedRevision);
        if (current is not null)
            return current;
        if (VersionedRecordRules.DraftDiscardConflict(_draftEverReviewed) is { } referenced)
            return CommandFailure.ForVersion(referenced);
        if (string.IsNullOrWhiteSpace(rationale))
            return CommandFailure.InvalidContent("Discarding a draft requires a rationale.");
        RaiseEvent(new BoundaryDraftDiscardedContent(_tenantId, Id, draftVersionId,
            expectedRevision, actorMemberId, actorDisplay, rationale.Trim(), discardedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return null;
    }

    public CommandFailure? Approve(Uuid draftVersionId, long expectedRevision,
        Uuid approvalDecisionId, Uuid acceptedReviewDecisionId, DateOnly effectiveFrom, string rationale,
        string impactDigest, Uuid approverMemberId, string approverDisplay, DateTimeOffset decidedAt,
        SeparationOfDutiesWaiverProof? separationOfDutiesWaiver = null)
    {
        var current = CheckDraft(draftVersionId, expectedRevision);
        if (current is not null)
            return current;
        var responsibilityScope = new ResponsibilityScope("boundary", Id, draftVersionId,
            expectedRevision);
        var responsibilityFailure = ResponsibilityDecisionGuard.ValidateAuthorized(
            GetResponsibilitySet(responsibilityScope), responsibilityScope, approverMemberId,
            ResponsibilityType.PolicyApprover, decidedAt,
            approverMemberId == _draftAuthorMemberId, separationOfDutiesWaiver);
        if (responsibilityFailure is not null)
            return responsibilityFailure;
        var approvalWaiverScope = new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.Boundary, Id, draftVersionId,
            expectedRevision, SeparationOfDutiesActions.Approve);
        if (separationOfDutiesWaiver is not null &&
            (separationOfDutiesWaiver.TenantId != _tenantId ||
             !BoundaryWaiverProof.Allows(separationOfDutiesWaiver, _tenantId, approvalWaiverScope, approverMemberId, decidedAt)))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this approver and draft revision.");
        if (approverMemberId == _draftAuthorMemberId && separationOfDutiesWaiver is null)
            return CommandFailure.ActorProhibited(
                "A boundary author cannot approve their own draft.");
        // Drafts recorded before a content rule existed must satisfy it before approval.
        if (Validate(_draftContent) is { } invalidContent)
            return CommandFailure.InvalidContent(invalidContent);
        if (acceptedReviewDecisionId == Uuid.Empty ||
            acceptedReviewDecisionId != _acceptedReviewDecisionId)
            return CommandFailure.StateConflict(
                "Approval requires the latest accepted review of this draft revision.");
        if (string.IsNullOrWhiteSpace(rationale))
            return CommandFailure.InvalidContent("Approval requires a rationale.");
        if (string.IsNullOrWhiteSpace(impactDigest))
            return CommandFailure.InvalidContent(
                "Approval requires the acknowledged impact preview digest.");
        if (_latestApprovedEffectiveFrom is { } prior &&
            !EffectiveInterval.CanFollow(prior, effectiveFrom))
            return CommandFailure.InvalidContent(
                "A successor must become effective after the previous approved version.");
        BoundaryApprovedContent approved = new(_tenantId, Id, draftVersionId, expectedRevision,
            approvalDecisionId, acceptedReviewDecisionId, approverMemberId, approverDisplay,
            rationale.Trim(), effectiveFrom, decidedAt, impactDigest,
            separationOfDutiesWaiver?.WaiverId)
        {
            StoredActor = ActorReference.ForMember(approverMemberId, approverDisplay),
        };
        Remember(separationOfDutiesWaiver);
        RaiseEvent(approved);
        return null;
    }

    public CommandFailure? ProposeSuccessor(Uuid expectedApprovedVersionId,
        Uuid draftVersionId, BoundaryContent content, Uuid authorMemberId,
        string authorDisplay, DateTimeOffset changedAt, Uuid commandId = default)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The boundary was not found.");
        if (_latestApprovedVersionId == Uuid.Empty)
            return CommandFailure.StateConflict("The boundary has no approved version.");
        if (expectedApprovedVersionId != _latestApprovedVersionId)
            return CommandFailure.ForVersion(
                VersionedRecordRules.StaleApprovedVersion("boundary",
                    _latestApprovedVersionId.ToGuid()));
        if (_draftVersionId != Uuid.Empty)
            return CommandFailure.StateConflict("The boundary already has an open successor draft.");
        var validation = Validate(content);
        if (validation is not null)
            return CommandFailure.InvalidContent(validation);
        if (commandId != Uuid.Empty)
            _successorCommands[draftVersionId] = commandId;
        RaiseEvent(new BoundarySuccessorProposedContent(_tenantId, Id, draftVersionId,
            _latestApprovedVersionId, content, authorMemberId, authorDisplay, changedAt));
        return null;
    }

    CommandFailure? CheckDraft(Uuid draftVersionId, long expectedRevision)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The boundary was not found.");
        if (_draftVersionId == Uuid.Empty)
            return CommandFailure.StateConflict(
                "The approved boundary is immutable. Propose a successor draft.");
        return DraftVersionConflict(draftVersionId, expectedRevision) is { } conflict
            ? CommandFailure.ForVersion(conflict)
            : null;
    }

    internal static string? Validate(BoundaryContent? content)
    {
        if (content is null || string.IsNullOrWhiteSpace(content.Statement))
            return "A boundary requires a statement.";
        if (content.EngagementStage is not ("readiness" or "type_i" or "type_ii"))
            return "The engagement stage must be readiness, type_i, or type_ii.";
        if (content.TrustServicesCategories is null || content.TrustServicesCategories.Count == 0 ||
            content.TrustServicesCategories.Any(category =>
                category is not ("security" or "availability" or "processing_integrity" or
                    "confidentiality" or "privacy")) ||
            content.TrustServicesCategories.Distinct(StringComparer.Ordinal).Count() !=
            content.TrustServicesCategories.Count)
            return "The boundary requires distinct, recognized Trust Services categories.";
        if (!content.TrustServicesCategories.Contains("security", StringComparer.Ordinal))
            return "The boundary requires the security category; optional categories add to it.";
        if (content.Entries is null || content.Entries.Any(entry =>
                entry is null || entry.EntryId == Uuid.Empty ||
                entry.Kind is not ("inclusion" or "exclusion" or "assumption" or "question") ||
                entry.SubjectType is not ("service" or "person" or "application" or
                    "system_instance" or
                    "component" or "information" or "data_flow" or "process" or
                    "location" or "provider" or "commitment") ||
                string.IsNullOrWhiteSpace(entry.Subject) ||
                string.IsNullOrWhiteSpace(entry.OwnerReference) ||
                string.IsNullOrWhiteSpace(entry.Rationale) ||
                entry.GovernedRecordId == Uuid.Empty ||
                (entry.Unresolved && entry.GovernedRecordId is not null) ||
                (!entry.Unresolved && entry.GovernedRecordId is null)) ||
            content.Entries.Select(static entry => entry.EntryId).Distinct().Count() !=
            content.Entries.Count)
            return "Every scope entry requires a unique ID, typed subject, owner, rationale, and a consistent governed reference state.";
        return null;
    }

    static bool Equivalent(BoundaryContent? left, BoundaryContent? right) =>
        left is not null && right is not null &&
        left.Statement == right.Statement && left.EngagementStage == right.EngagementStage &&
        left.TrustServicesCategories.SequenceEqual(right.TrustServicesCategories) &&
        left.Entries.SequenceEqual(right.Entries);
    readonly Dictionary<Uuid, Uuid> _successorCommands = [];
    internal Uuid SuccessorCommand(Uuid version) => _successorCommands.TryGetValue(version, out var command) ? command
        : throw new InvalidOperationException("A native successor seed requires its original command identity.");
    readonly Dictionary<Uuid, BoundaryVersionView> _readVersions = [];
    readonly List<BoundaryDecisionView> _readDecisions = [];
    BoundaryDecisionView? _latestDecision;
    BoundaryContent? _currentContent;
    string _draftAuthorDisplay = string.Empty;
    public BoundaryContent? CurrentContent => _currentContent;
    public BoundaryVersionView? Draft => _draftContent is null ? null : new BoundaryVersionView(_tenantId, Id, _programId,
        _draftVersionId, _draftRevision, _draftContent, "draft", null, _draftAuthorMemberId, _draftAuthorDisplay, _draftChangedAt ?? default);
    public BoundaryVersionView? LatestApprovedVersion => _readVersions.GetValueOrDefault(_latestApprovedVersionId);
    public BoundaryDecisionView? LatestDecision => _latestDecision;
    public BoundaryView ToView() => new(_tenantId, Id, _programId, Draft, LatestApprovedVersion, LatestDecision, _revision);
    public IReadOnlyList<BoundaryVersionView> ReadVersions() => _readVersions.Values.OrderBy(static value => value.EffectiveFrom).ToArray();
    public IReadOnlyList<BoundaryDecisionView> ReadDecisions() => _readDecisions.ToArray();
    public BoundaryVersionView? FindVersion(Uuid version) => Draft?.VersionId == version ? Draft : _readVersions.GetValueOrDefault(version);
    public BoundaryDecisionView? FindDecision(Uuid decision) => _readDecisions.SingleOrDefault(value => value.DecisionId == decision);
    public BoundaryVersionView? EffectiveVersion(DateOnly at) => _readVersions.Values
        .Where(value => value.EffectiveFrom <= at).OrderByDescending(static value => value.EffectiveFrom).FirstOrDefault();
    internal BoundaryAuthorityHeader Header(ImmutableContentRef content) => new(_tenantId, _programId, Id, _revision,
        _created, IsVisible, _draftVersionId, _draftRevision, _draftAuthorMemberId, _draftEverReviewed,
        _acceptedReviewDecisionId, _latestReviewOutcome, _latestApprovedVersionId, FirstApprovedVersionId,
        _latestApprovedEffectiveFrom, _draftChangedAt, content);
    internal BoundaryAuthorityContent Authority(BoundaryAuthorityHeader header)
    {
        var scope = new ResponsibilityScope("boundary", Id, _draftVersionId, _draftRevision);
        return new(header, _draftContent is not null ? GetResponsibilitySet(scope).ReadAssignments().ToArray() : [],
            _draftContent is not null && _responsibilityDecisions.TryGetValue(scope, out var decisions)
                ? decisions.Select(static value => new BoundaryDecisionConstraint(value.MemberId, value.Type, value.At, value.WaiverId)).ToArray() : []);
    }
    void Remember(SeparationOfDutiesWaiverProof? proof)
    {
        if (proof is not null)
            _waivers[proof.WaiverId] = proof;
    }
    internal IReadOnlyList<SeparationOfDutiesWaiverProof> WaiversFor(IBoundaryMutationContent change)
    {
        var ids = change switch
        {
            BoundaryReviewedContent { SeparationOfDutiesWaiverId: { } id } => [id],
            BoundaryApprovedContent { SeparationOfDutiesWaiverId: { } id } => [id],
            ResponsibilityAssigned assigned => assigned.SeparationOfDutiesWaiverIds,
            _ => [],
        };
        return ids.Select(id => _waivers.TryGetValue(id, out var proof) ? proof
            : throw new InvalidOperationException("The prepared boundary change has no exact immutable waiver proof.")).ToArray();
    }
    void FoldRead(IBoundaryMutationContent change)
    {
        switch (change)
        {
            case BoundaryDraftCreatedContent created:
                _currentContent = created.Content;
                _draftAuthorDisplay = created.AuthorDisplay;
                break;
            case BoundaryDraftRevisedContent revised:
                _currentContent = revised.Content;
                _draftAuthorDisplay = revised.AuthorDisplay;
                _latestDecision = null;
                break;
            case BoundarySuccessorProposedContent proposed:
                _currentContent = proposed.Content;
                _draftAuthorDisplay = proposed.AuthorDisplay;
                _latestDecision = null;
                break;
            case BoundaryReviewedContent reviewed:
                _latestDecision = new BoundaryDecisionView(reviewed.TenantId, reviewed.BoundaryId, reviewed.DecisionId,
                    reviewed.DraftVersionId, reviewed.Revision, reviewed.Outcome, reviewed.ActorMemberId,
                    reviewed.ActorDisplay, reviewed.Rationale, reviewed.DecidedAt,
                    _latestDecision is { } previous && previous.VersionId == reviewed.DraftVersionId && previous.Revision == reviewed.Revision
                        ? previous.DecisionId : null, null, null, reviewed.SeparationOfDutiesWaiverId)
                { Actor = reviewed.Actor };
                _readDecisions.Add(_latestDecision);
                break;
            case BoundaryApprovedContent approved:
                if (Draft is not { } draft)
                    throw new InvalidOperationException("The boundary approval has no exact preceding draft.");
                _readVersions.Add(approved.DraftVersionId, draft with { Status = "approved", EffectiveFrom = approved.EffectiveFrom });
                _latestDecision = new BoundaryDecisionView(approved.TenantId, approved.BoundaryId, approved.ApprovalDecisionId,
                    approved.DraftVersionId, approved.Revision, "approve", approved.ActorMemberId, approved.ActorDisplay,
                    approved.Rationale, approved.DecidedAt, null, approved.AcceptedReviewDecisionId, approved.ImpactDigest,
                    approved.SeparationOfDutiesWaiverId)
                { Actor = approved.Actor };
                _readDecisions.Add(_latestDecision);
                break;
        }
    }
}
