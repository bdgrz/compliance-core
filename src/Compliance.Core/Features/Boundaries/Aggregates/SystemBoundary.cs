using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Boundaries.Services;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Services;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Boundaries.Commands;
using Bdgrz.Compliance.Features.Boundaries.Events;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Responsibilities.Commands;
using Bdgrz.Compliance.Features.Responsibilities.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Boundaries.Aggregates;

/// <summary>Compact current boundary authority. Historical content and responsibility scopes are verified outside the owner.</summary>
public sealed class SystemBoundary : Aggregate
{
    public const string Area = "boundaries";
    readonly Uuid _tenantId;
    BoundaryAuthorityHeader? _header;
    public SystemBoundary(Uuid tenantId, Uuid boundaryId, Uuid selectedCommandId = default)
        : base(boundaryId, new EventStreamAddress(tenantId.ToString(), Area, boundaryId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new(selectedCommandId);
        On<BoundaryDraftCreated>(ApplyFact);
        On<BoundaryDraftRevised>(ApplyFact);
        On<BoundaryReviewed>(ApplyFact);
        On<BoundaryApproved>(ApplyFact);
        On<BoundaryDraftDiscarded>(ApplyFact);
        On<BoundarySuccessorProposed>(ApplyFact);
        On<ResponsibilityAssigned>(ApplyFact);
        On<ResponsibilityRevoked>(ApplyFact);
    }
    public NativeAcceptance Acceptance { get; }
    public BoundaryAuthorityHeader? Header => _header;
    public ImmutableContentRef? CurrentAuthority { get; private set; }
    public long NativeSequence { get; private set; }
    public Uuid TenantId => _tenantId;
    public Uuid ProgramId => _header?.ProgramId ?? Uuid.Empty;
    public long Revision => _header?.Revision ?? 0;
    public bool IsCreated => _header?.IsCreated ?? false;
    public bool IsVisible => _header?.IsVisible ?? false;
    public Uuid DraftVersionId => _header?.DraftVersionId ?? Uuid.Empty;
    public long DraftRevision => _header?.DraftRevision ?? 0;
    public Uuid DraftAuthorMemberId => _header?.DraftAuthorMemberId ?? Uuid.Empty;
    public DateTimeOffset? DraftChangedAt => _header?.DraftChangedAt;
    public string? LatestReviewOutcome => _header?.LatestReviewOutcome;
    public Uuid LatestApprovedVersionId => _header?.LatestApprovedVersionId ?? Uuid.Empty;
    public Uuid FirstApprovedVersionId => _header?.FirstApprovedVersionId ?? Uuid.Empty;
    public bool IsCurrentResponsibilityScope(ResponsibilityScope scope) => scope is not null && IsCreated && DraftVersionId != Uuid.Empty &&
        scope.RecordType == "boundary" && scope.RecordId == Id && scope.VersionId == DraftVersionId && scope.Revision == DraftRevision;
    public Result Prepare(Uuid program, bool creating)
    {
        if (creating)
            return IsCreated ? Fail(RequestErrorKind.Conflict, "The boundary already exists with different content.") : Result.Success;
        return !IsCreated || ProgramId != program ? Fail(RequestErrorKind.NotFound, "The boundary was not found.") : Result.Success;
    }
    public Result Append(DomainEvent value, long preparedSequence, ulong preparedPhysicalPosition,
        BoundaryMutationContent mutation, BoundaryAuthorityContent? before, BoundaryAuthorityContent after)
    {
        if (NativeSequence != preparedSequence || CommittedStreamPosition != preparedPhysicalPosition)
            return Fail(RequestErrorKind.Conflict, "The boundary changed while exact content and authority were prepared.", true);
        if (value is not IBoundaryFact fact || fact.Header is null || mutation is null || after is null)
            return Fail(RequestErrorKind.Validation, "A boundary mutation requires its exact native preparation.");
        try
        {
            Validate(value, fact);
            Require(mutation.Changes is not null && mutation.Changes.Count == fact.ChangeCount && (mutation.Changes.Count == 0) == (mutation.Receipt is not null) &&
                mutation.Changes.All(change => BoundaryFactContent.InScope(change, fact.Header)) &&
                (mutation.Receipt is null || BoundaryFactContent.InScope(mutation.Receipt, fact.Header)) &&
                Matches(fact.Change, mutation, BoundaryFactContent.ChangeSchema) && after.Header == fact.Header &&
                Matches(fact.Authority, after, BoundaryFactContent.AuthoritySchema), "The boundary mutation has another prepared body or scope.");
            Require(_header is null ? before is null : before is not null && before.Header == _header && CurrentAuthority is not null &&
                Matches(CurrentAuthority, before, BoundaryFactContent.AuthoritySchema), "The boundary before authority has another physical source cut.");
            var expectedFact = BoundaryFactFactory.Create(fact.Operation.CommandKind, fact.Header, fact.NativeSequence, fact.Operation,
                fact.Change, fact.Authority, fact.FirstResponse!, fact.OperationResult!, mutation);
            Require(ContentDigest.Sha256(expectedFact, expectedFact.GetType()) == ContentDigest.Sha256(value, value.GetType()),
                "The boundary native metadata differs from its immutable original mutation.");
            var authored = mutation.Changes.Select(change => change switch
            {
                BoundaryDraftCreatedContent created => created.Content,
                BoundaryDraftRevisedContent revised => revised.Content,
                BoundarySuccessorProposedContent proposed => proposed.Content,
                _ => null,
            }).OfType<BoundaryContent>().SingleOrDefault();
            Require(authored is null || BoundaryRules.Validate(authored) is null && ContentDigest.OfJson(authored) == fact.Header.Content.Sha256,
                "The boundary authored content differs from its exact immutable reference.");
            ValidateLocal(value, mutation, before);
            Require(ContentDigest.OfJson(after) == ContentDigest.OfJson(ExpectedAuthority(value, fact.Header, before)),
                "The boundary prepared current authority differs from its exact local transition.");
            Require(DomainEventPayload.Size(value) <= DomainEventPayload.MaximumBytes, "The native boundary scalar envelope exceeds its frame budget.");
        }
        catch (InvalidOperationException)
        {
            return Fail(RequestErrorKind.Validation, "The native boundary mutation has an invalid local authority binding.");
        }
        RaiseEvent(value);
        return Result.Success;
    }
    void ApplyFact(DomainEvent value)
    {
        var fact = (IBoundaryFact)value;
        try
        {
            Validate(value, fact);
        }
        catch (InvalidOperationException)
        {
            throw new ImmutableContentIntegrityException("The stored native boundary envelope has an invalid authority binding.");
        }
        _header = fact.Header;
        CurrentAuthority = fact.Authority;
        NativeSequence = fact.NativeSequence;
        Acceptance.Observe(value);
    }
    void Validate(DomainEvent value, IBoundaryFact fact)
    {
        Require(fact.Operation is not null && fact.Operation.Actor is not null && fact.Header is not null,
            "A boundary native fact requires its original operation and compact header.");
        var operation = fact.Operation;
        var header = fact.Header;
        OwnerProvenance.RequireOwn(this, operation.LogicalId, operation);
        Require(TenantId != Uuid.Empty && Id != Uuid.Empty && fact.TenantId == TenantId && header.TenantId == TenantId && header.BoundaryId == Id &&
            header.ProgramId != Uuid.Empty && fact.NativeSequence == NativeSequence + 1 && header.IsCreated && header.Revision > 0 &&
            operation.RootOperationId == operation.LogicalId && operation.CausationEventId is null && operation.Episode == Id.ToString() &&
            operation.AffectedOwner == Stream && operation.ExpectedRevisionOwner == Stream && (operation.FirstOwner is null || operation.FirstOwner == Stream) &&
            operation.Actor.Kind == "member" && Uuid.TryParse(operation.Actor.Id, out var member) && member != Uuid.Empty && Hex(operation.AuthoredIntentSha256) &&
            Reference(header.Content, BoundaryFactContent.ContentSchema) && Reference(fact.Change, BoundaryFactContent.ChangeSchema) &&
            Reference(fact.Authority, BoundaryFactContent.AuthoritySchema) && Reference(fact.FirstResponse, FirstResponses.Schema) &&
            Reference(fact.OperationResult, "mutation-result-content.v1"), "A boundary native fact has another owner or operation binding.");
        var actor = Uuid.Parse(operation.Actor.Id, CultureInfo.InvariantCulture);
        var metadata = value switch
        {
            BoundaryDraftCreated item => (item.AuthorMemberId, item.AuthorDisplay, item.ChangedAt),
            BoundaryDraftRevised item => (item.AuthorMemberId, item.AuthorDisplay, item.ChangedAt),
            BoundarySuccessorProposed item => (item.AuthorMemberId, item.AuthorDisplay, item.ChangedAt),
            BoundaryReviewed item => (item.ActorMemberId, item.ActorDisplay, item.DecidedAt),
            BoundaryApproved item => (item.ActorMemberId, item.ActorDisplay, item.DecidedAt),
            BoundaryDraftDiscarded item => (item.ActorMemberId, item.ActorDisplay, item.DiscardedAt),
            ResponsibilityAssigned item => (item.AssignedByMemberId, item.AssignedByDisplay, item.AssignedAt),
            ResponsibilityRevoked item => (item.RevokedByMemberId, item.RevokedByDisplay, item.RevokedAt),
            _ => (Uuid.Empty, string.Empty, DateTimeOffset.MinValue),
        };
        Require(actor == metadata.Item1 && operation.Actor.Display == metadata.Item2 && operation.AcceptedAt == metadata.Item3,
            "The boundary receipt lost its original member, display snapshot or acceptance instant.");
        if (_header is null)
        {
            Require(value is BoundaryDraftCreated created && operation.CommandKind == nameof(CreateBoundary) && operation.LogicalId == Id &&
                operation.OriginalExpectedRevision is null && fact.ChangeCount == 1 && created.ProgramId == header.ProgramId &&
                created.DraftVersionId == Uuid.CreateVersion5(Id, "draft-1") && created.Content == header.Content &&
                header == new BoundaryAuthorityHeader(TenantId, created.ProgramId, Id, 1, true, true, created.DraftVersionId, 1,
                    created.AuthorMemberId, false, Uuid.Empty, null, Uuid.Empty, Uuid.Empty, null, created.ChangedAt, header.Content),
                "A boundary begins with its exact authored first draft identity.");
            return;
        }
        var before = _header;
        Require(value is not BoundaryDraftCreated && header.ProgramId == before.ProgramId, "The boundary program identity is immutable.");
        if (fact.ChangeCount == 0)
        {
            Require(value is ResponsibilityAssigned assignment
                ? operation.CommandKind == nameof(AssignResponsibility) && operation.LogicalId == assignment.AssignmentId && IsCurrentResponsibilityScope(assignment.Scope)
                : value is ResponsibilityRevoked revoked && operation.CommandKind == nameof(RevokeResponsibility) && IsCurrentResponsibilityScope(revoked.Scope),
                "A no-op acceptance must retain exact current responsibility operation identity.");
            Require(header == before && operation.OriginalExpectedRevision == unchecked((ulong)before.DraftRevision), "A no-op cannot change current authority.");
            return;
        }
        Require(fact.ChangeCount == 1, "A boundary native operation publishes one business mutation.");
        var expected = before;
        if (value is not BoundarySuccessorProposed)
            Require(operation.OriginalExpectedRevision == unchecked((ulong)before.DraftRevision), "The original exact draft revision gate changed.");
        switch (value)
        {
            case BoundaryDraftRevised revised:
                Require(operation.CommandKind == nameof(ReviseBoundaryDraft) && before.DraftVersionId != Uuid.Empty &&
                    revised.DraftVersionId == before.DraftVersionId && revised.Revision == before.DraftRevision + 1 && revised.Content == header.Content,
                    "Revision must retain the exact next boundary draft revision.");
                expected = before with
                {
                    Revision = before.Revision + 1,
                    DraftRevision = revised.Revision,
                    Content = header.Content,
                    DraftAuthorMemberId = actor,
                    DraftChangedAt = revised.ChangedAt,
                    AcceptedReviewDecisionId = Uuid.Empty,
                    LatestReviewOutcome = null
                };
                break;
            case BoundaryReviewed reviewed:
                Require(operation.CommandKind == nameof(ReviewBoundary) && operation.LogicalId == reviewed.DecisionId &&
                    reviewed.DraftVersionId == before.DraftVersionId && before.DraftVersionId != Uuid.Empty && reviewed.Revision == before.DraftRevision &&
                    reviewed.Outcome is "accept" or "request_changes", "Review must answer the exact pending boundary draft.");
                expected = before with
                {
                    Revision = before.Revision + 1,
                    DraftEverReviewed = true,
                    LatestReviewOutcome = reviewed.Outcome,
                    AcceptedReviewDecisionId = reviewed.Outcome == "accept" ? reviewed.DecisionId : Uuid.Empty
                };
                break;
            case BoundaryApproved approved:
                Require(operation.CommandKind == nameof(ApproveBoundary) && operation.LogicalId == approved.ApprovalDecisionId &&
                    before.DraftVersionId != Uuid.Empty && approved.DraftVersionId == before.DraftVersionId && approved.Revision == before.DraftRevision &&
                    before.AcceptedReviewDecisionId != Uuid.Empty && approved.AcceptedReviewDecisionId == before.AcceptedReviewDecisionId && approved.EffectiveFrom != default &&
                    (before.LatestApprovedEffectiveFrom is null || approved.EffectiveFrom > before.LatestApprovedEffectiveFrom),
                    "Approval must retain the latest exact accepted review and later effective version.");
                expected = before with
                {
                    Revision = before.Revision + 1,
                    DraftVersionId = Uuid.Empty,
                    DraftChangedAt = null,
                    AcceptedReviewDecisionId = Uuid.Empty,
                    LatestReviewOutcome = null,
                    LatestApprovedVersionId = approved.DraftVersionId,
                    LatestApprovedEffectiveFrom = approved.EffectiveFrom,
                    FirstApprovedVersionId = before.FirstApprovedVersionId == Uuid.Empty ? approved.DraftVersionId : before.FirstApprovedVersionId
                };
                break;
            case BoundaryDraftDiscarded discarded:
                Require(operation.CommandKind == nameof(DiscardBoundaryDraft) && before.DraftVersionId != Uuid.Empty && !before.DraftEverReviewed &&
                    discarded.DraftVersionId == before.DraftVersionId && discarded.Revision == before.DraftRevision, "A referenced boundary draft cannot be discarded.");
                expected = before with
                {
                    Revision = before.Revision + 1,
                    DraftVersionId = Uuid.Empty,
                    DraftChangedAt = null,
                    AcceptedReviewDecisionId = Uuid.Empty,
                    LatestReviewOutcome = null,
                    IsVisible = before.LatestApprovedVersionId != Uuid.Empty
                };
                break;
            case BoundarySuccessorProposed proposed:
                Require(operation.CommandKind == nameof(ProposeBoundarySuccessor) && operation.OriginalExpectedRevision == unchecked((ulong)before.Revision) &&
                    before.DraftVersionId == Uuid.Empty && before.LatestApprovedVersionId != Uuid.Empty && proposed.PredecessorVersionId == before.LatestApprovedVersionId &&
                    proposed.DraftVersionId == Uuid.CreateVersion5(operation.LogicalId, "draft") && proposed.Content == header.Content,
                    "A successor must follow the exact approved boundary without another open draft.");
                expected = before with
                {
                    Revision = before.Revision + 1,
                    IsVisible = true,
                    DraftVersionId = proposed.DraftVersionId,
                    DraftRevision = 1,
                    DraftAuthorMemberId = actor,
                    DraftEverReviewed = false,
                    AcceptedReviewDecisionId = Uuid.Empty,
                    LatestReviewOutcome = null,
                    DraftChangedAt = proposed.ChangedAt,
                    Content = header.Content
                };
                break;
            case ResponsibilityAssigned assigned:
                Require(operation.CommandKind == nameof(AssignResponsibility) && operation.LogicalId == assigned.AssignmentId && IsCurrentResponsibilityScope(assigned.Scope),
                    "Assignment requires the exact current boundary draft revision.");
                break;
            case ResponsibilityRevoked revoked:
                Require(operation.CommandKind == nameof(RevokeResponsibility) && IsCurrentResponsibilityScope(revoked.Scope),
                    "Revocation requires the exact current boundary draft revision.");
                break;
            default:
                throw new InvalidOperationException("The boundary has another native mutation kind.");
        }
        Require(header == expected, "The boundary compact header differs from its exact local transition.");
    }
    BoundaryAuthorityContent ExpectedAuthority(DomainEvent value, BoundaryAuthorityHeader header, BoundaryAuthorityContent? before)
    {
        var after = before is null ? new BoundaryAuthorityContent(header, [], []) : before with { Header = header };
        if (((IBoundaryFact)value).ChangeCount == 0)
            return after;
        switch (value)
        {
            case BoundaryDraftCreated or BoundaryDraftRevised or BoundarySuccessorProposed or BoundaryDraftDiscarded or BoundaryApproved:
                return after with { Assignments = [], DecisionConstraints = [] };
            case BoundaryReviewed reviewed:
                return after with
                {
                    DecisionConstraints = after.DecisionConstraints.Append(new BoundaryDecisionConstraint(reviewed.ActorMemberId,
                    ResponsibilityType.AssignedReviewer, reviewed.DecidedAt, reviewed.SeparationOfDutiesWaiverId)).ToArray()
                };
            case ResponsibilityAssigned assigned:
                var assignedSet = Set(before!);
                assignedSet.Apply(assigned);
                return after with { Assignments = assignedSet.ReadAssignments() };
            case ResponsibilityRevoked revoked:
                var revokedSet = Set(before!);
                revokedSet.Apply(revoked);
                return after with { Assignments = revokedSet.ReadAssignments() };
            default:
                return after;
        }
    }
    void ValidateLocal(DomainEvent value, BoundaryMutationContent mutation, BoundaryAuthorityContent? before)
    {
        var proofs = mutation.Waivers ?? [];
        Require(proofs.All(proof => proof is not null && LocatorValid(proof)) && proofs.Select(proof => proof.WaiverId).Distinct().Count() == proofs.Count,
            "A boundary mutation requires exact distinct immutable waiver proofs.");
        if (value is BoundaryReviewed reviewed)
        {
            var data = (BoundaryReviewedContent)mutation.Changes.Single();
            Require(!string.IsNullOrWhiteSpace(data.Rationale), "Review requires its authored rationale.");
            ValidateDecision(before!, reviewed.ActorMemberId, ResponsibilityType.AssignedReviewer, SeparationOfDutiesActions.Review,
                reviewed.DecidedAt, reviewed.SeparationOfDutiesWaiverId, proofs);
            return;
        }
        if (value is BoundaryApproved approved)
        {
            var data = (BoundaryApprovedContent)mutation.Changes.Single();
            Require(!string.IsNullOrWhiteSpace(data.Rationale) && !string.IsNullOrWhiteSpace(data.ImpactDigest), "Approval requires rationale and acknowledged impact.");
            ValidateDecision(before!, approved.ActorMemberId, ResponsibilityType.PolicyApprover, SeparationOfDutiesActions.Approve,
                approved.DecidedAt, approved.SeparationOfDutiesWaiverId, proofs);
            return;
        }
        if (value is BoundaryDraftDiscarded)
            Require(!string.IsNullOrWhiteSpace(((BoundaryDraftDiscardedContent)mutation.Changes.Single()).Rationale), "Discarding a draft requires its rationale.");
        if (value is ResponsibilityAssigned assigned)
        {
            var set = Set(before!);
            var proposed = new ResponsibilityAssignmentView(TenantId, assigned.AssignmentId, assigned.MemberId, assigned.Type, assigned.Scope,
                assigned.AssignedAt, assigned.AssignedByMemberId, assigned.EffectiveFrom, assigned.EffectiveUntil, null, Uuid.Empty, []);
            foreach (var constraint in before!.DecisionConstraints.Where(item => item.WaiverId is null))
            {
                var decision = new ResponsibilityAssignmentView(TenantId, Uuid.Empty, constraint.MemberId, constraint.Type, assigned.Scope,
                    constraint.At, Uuid.Empty, constraint.At, constraint.At.AddTicks(1), null, Uuid.Empty, []);
                Require(ResponsibilityConflictPolicy.FindConflicts(before.Assignments.Append(proposed), decision).Count == 0,
                    "Assignment must preserve unwaived historical decision independence.");
            }
            Require(proofs.Select(proof => proof.WaiverId).OrderBy(id => id.ToString(), StringComparer.Ordinal)
                .SequenceEqual(assigned.SeparationOfDutiesWaiverIds.OrderBy(id => id.ToString(), StringComparer.Ordinal)) &&
                set.AssignAuthorized(assigned.AssignmentId, assigned.MemberId, assigned.Type, assigned.AssignedByMemberId,
                    assigned.AssignedByDisplay, assigned.AssignedAt, assigned.EffectiveFrom, assigned.EffectiveUntil, proofs) is null,
                "Assignment does not satisfy the exact current responsibility and waiver authority.");
            if (((IBoundaryFact)value).ChangeCount == 0)
                Require(Matches(CurrentAuthority!, before! with { Assignments = set.ReadAssignments() }, BoundaryFactContent.AuthoritySchema), "No-op assignment changed authority.");
            return;
        }
        Require(proofs.Count == 0, "This boundary mutation cannot consume a decision waiver.");
        if (value is ResponsibilityRevoked revoked)
        {
            var set = Set(before!);
            Require(set.Revoke(revoked.AssignmentId, revoked.RevokedByMemberId, revoked.RevokedByDisplay, revoked.RevokedAt, revoked.Reason) is null,
                "Revocation requires the retained exact assignment.");
            if (((IBoundaryFact)value).ChangeCount == 0)
                Require(Matches(CurrentAuthority!, before! with { Assignments = set.ReadAssignments() }, BoundaryFactContent.AuthoritySchema), "No-op revocation changed authority.");
        }
    }

    void ValidateDecision(BoundaryAuthorityContent before, Uuid member, ResponsibilityType role, string action, DateTimeOffset at,
        Uuid? waiverId, IReadOnlyList<SeparationOfDutiesWaiverProof> proofs)
    {
        var set = Set(before);
        var proof = waiverId is { } id ? proofs.SingleOrDefault(item => item.WaiverId == id) : null;
        Require(proofs.Count == (proof is null ? 0 : 1) && (waiverId is null || proof is not null), "A decision must bind its exact immutable waiver.");
        var author = before.Header.DraftAuthorMemberId == member;
        Require(ResponsibilityDecisionGuard.ValidateAuthorized(set, Scope(), member, role, at, author, proof) is null &&
            (proof is not null ? BoundaryWaiverProof.Allows(proof, TenantId, new SeparationOfDutiesWaiverScope(
                SeparationOfDutiesRecordTypes.Boundary, Id, DraftVersionId, DraftRevision, action), member, at) : !author),
            "The boundary decision violates its exact author, reviewer or responsibility independence.");
    }
    ResponsibilityScope Scope() => new(SeparationOfDutiesRecordTypes.Boundary, Id, DraftVersionId, DraftRevision);
    ResponsibilitySet Set(BoundaryAuthorityContent authority)
    {
        var scope = Scope();
        var set = new ResponsibilitySet(TenantId, scope);
        foreach (var assignment in authority.Assignments)
        {
            Require(assignment.TenantId == TenantId && assignment.Scope == scope, "The current responsibility authority has another exact scope.");
            set.Apply(new ResponsibilityAssigned(TenantId, assignment.AssignmentId, assignment.MemberId, assignment.Type, scope,
                assignment.AssignedAt, assignment.AssignedByMemberId, assignment.EffectiveFrom, assignment.EffectiveUntil,
                assignment.SeparationOfDutiesWaiverIds, assignment.AssignedByDisplay));
            if (assignment.RevokedAt is { } at)
                set.Apply(new ResponsibilityRevoked(TenantId, assignment.AssignmentId, scope, assignment.RevokedByMemberId, at,
                    assignment.RevocationReason ?? string.Empty, assignment.RevokedByDisplay ?? string.Empty));
        }
        return set;
    }
    static bool LocatorValid(SeparationOfDutiesWaiverProof proof) => proof.ApprovalFact is not null && proof.ApprovalFact.EventId != Uuid.Empty &&
        proof.ApprovalFact.ResourcePosition == 1 && proof.ApprovalFact.AggregateVersion == 2 && Hex(proof.ApprovalFact.PayloadSha256) && proof.ValidFrom < proof.ValidUntil;
    static bool Reference(ImmutableContentRef? value, string schema) => value is not null && value.ContentId != Uuid.Empty && value.Bytes >= 0 &&
        value.MediaType == ImmutableContentStoreExtensions.JsonMediaType && value.Schema == schema && Hex(value.Sha256);
    static bool Matches<T>(ImmutableContentRef reference, T value, string schema) where T : notnull
    {
        if (!Reference(reference, schema))
            return false;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))!);
        return reference.Bytes == bytes.LongLength && reference.Sha256 == ContentDigest.Sha256(bytes);
    }
    static bool Hex(string? value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
    static Result Fail(RequestErrorKind kind, string message, bool transient = false) => Result.Failure(new RequestError(kind, message, transient));
}
