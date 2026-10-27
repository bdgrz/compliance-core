using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Commitments.Commands;
using Bdgrz.Compliance.Features.Commitments.Events;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Responsibilities.Commands;
using Bdgrz.Compliance.Features.Responsibilities.Events;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments.Aggregates;

/// <summary>One deterministic commitment identity and compact authority; immutable bodies and historical scope sets are read separately.</summary>
public sealed class CommitmentDraft : Aggregate
{
    public const string Area = "commitment-drafts";
    public const int MaximumDraftEventPayloadBytes = DomainEventPayload.MaximumBytes;
    readonly Uuid _tenantId;
    CommitmentAuthorityHeader? _header;
    public CommitmentDraft(Uuid tenantId, Uuid draftId, Uuid selectedCommandId = default)
        : base(draftId, new EventStreamAddress(tenantId.ToString(), Area, draftId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new(selectedCommandId);
        On<CommitmentDraftCreated>(ApplyFact);
        On<CommitmentDraftRevised>(ApplyFact);
        On<CommitmentReviewed>(ApplyFact);
        On<CommitmentApproved>(ApplyFact);
        On<CommitmentRetired>(ApplyFact);
        On<ResponsibilityAssigned>(ApplyFact);
        On<ResponsibilityRevoked>(ApplyFact);
    }
    public NativeAcceptance Acceptance { get; }
    public CommitmentAuthorityHeader? Header => _header;
    public ImmutableContentRef? CurrentAuthority { get; private set; }
    public long NativeSequence { get; private set; }
    public Uuid TenantId => _tenantId;
    public Uuid ProgramId => _header?.ProgramId ?? Uuid.Empty;
    public Uuid ServiceId => _header?.ServiceId ?? Uuid.Empty;
    public Uuid? ProviderId => _header?.ProviderId;
    public string? Kind => _header?.Kind;
    public string? Identifier => _header?.Identifier;
    public long Revision => _header?.Revision ?? 0;
    public bool IsCreated => _header?.IsCreated ?? false;
    public bool IsRetired => _header?.IsRetired ?? false;
    public string? SourceReference => _header?.SourceReference;
    public long EffectiveVersionCount => _header?.EffectiveVersionCount ?? 0;
    public long? LatestEffectiveRevision => _header?.LatestEffectiveRevision;
    public Uuid? AcceptedReviewDecisionId => _header?.AcceptedReviewDecisionId;
    public Uuid? AcceptedReviewerMemberId => _header?.AcceptedReviewerMemberId;
    public DateOnly? RetiredEffectiveOn => _header?.RetiredEffectiveOn;
    public static string NormalizeIdentifier(string? value) => CommitmentRules.NormalizeIdentifier(value);
    public static string NormalizeKind(string? value) => CommitmentRules.NormalizeKind(value);
    public static Uuid IdFor(Uuid tenant, Uuid program, string kind, string identifier) => CommitmentRules.IdFor(tenant, program, kind, identifier);
    public static string PerformedBy(string kind) => CommitmentRules.PerformedBy(kind);
    public static bool IsInternallyPerformed(string kind) => CommitmentRules.IsInternallyPerformed(kind);
    public bool IsCurrentResponsibilityScope(ResponsibilityScope scope) => scope is not null && IsCreated && !IsRetired &&
        scope.RecordType == SeparationOfDutiesRecordTypes.Commitment && scope.RecordId == Id && scope.VersionId == Id &&
        scope.Revision == Revision && LatestEffectiveRevision != Revision;

    public Result Prepare(Uuid program, bool creating)
    {
        if (creating)
            return IsCreated ? Fail(RequestErrorKind.Conflict, "The draft identifier already exists.") : Result.Success;
        if (!IsCreated || ProgramId != program)
            return Fail(RequestErrorKind.NotFound, "The draft was not found.");
        return IsRetired ? Fail(RequestErrorKind.Conflict, "A retired commitment cannot be changed.") : Result.Success;
    }

    public Result Append(DomainEvent value, long preparedSequence, ulong preparedPhysicalPosition,
        CommitmentMutationContent mutation, CommitmentAuthorityContent? before, CommitmentAuthorityContent after)
    {
        if (NativeSequence != preparedSequence || CommittedStreamPosition != preparedPhysicalPosition)
            return Fail(RequestErrorKind.Conflict, "The commitment changed while exact content and authority were prepared.", true);
        if (value is not ICommitmentFact fact || fact.Header is null || mutation is null || after is null)
            return Fail(RequestErrorKind.Validation, "A commitment mutation requires its exact native preparation.");
        try
        {
            Validate(value, fact);
            Require(mutation.Changes is not null && mutation.Changes.Count == fact.ChangeCount && (mutation.Changes.Count == 0) == (mutation.Receipt is not null) &&
                mutation.Changes.All(change => CommitmentFactContent.InScope(change, fact.Header)) &&
                (mutation.Receipt is null || CommitmentFactContent.InScope(mutation.Receipt, fact.Header)) &&
                Matches(fact.Change, mutation, CommitmentFactContent.ChangeSchema) && after.Header == fact.Header &&
                Matches(fact.Authority, after, CommitmentFactContent.AuthoritySchema), "The commitment mutation has another prepared body or scope.");
            Require(_header is null ? before is null : before is not null && before.Header == _header && CurrentAuthority is not null &&
                Matches(CurrentAuthority, before, CommitmentFactContent.AuthoritySchema), "The commitment before authority has another physical source cut.");
            var expectedFact = CommitmentFactFactory.Create(fact.Operation.CommandKind, fact.Header, fact.NativeSequence, fact.Operation,
                fact.Change, fact.Authority, fact.FirstResponse!, fact.OperationResult!, mutation);
            Require(ContentDigest.Sha256(expectedFact, expectedFact.GetType()) == ContentDigest.Sha256(value, value.GetType()),
                "The commitment native metadata differs from its immutable original mutation.");
            var body = mutation.Changes.Select(change => change switch
            {
                CommitmentDraftCreatedContent created => new CommitmentDraftContent(created.Statement, created.Context, created.SourceReference),
                CommitmentDraftRevisedContent revised => new CommitmentDraftContent(revised.Statement, revised.Context, revised.SourceReference),
                _ => null,
            }).OfType<CommitmentDraftContent>().SingleOrDefault();
            if (body is not null)
                Require(CommitmentRules.Validate(fact.Header.Kind, fact.Header.Identifier, body.Statement, body.Context,
                    body.SourceReference, fact.Header.ProviderId) is null && Matches(fact.Header.Content, body, CommitmentFactContent.ContentSchema),
                    "The commitment authored body violates its exact content contract.");
            Require(Matches(fact.Authority, ExpectedAuthority(value, fact.Header, before), CommitmentFactContent.AuthoritySchema),
                "The commitment after authority differs from its local business change.");
            ValidateLocal(value, mutation, before);
            Require(DomainEventPayload.Size(value) <= DomainEventPayload.MaximumBytes,
                "The native commitment scalar envelope exceeds the bounded event payload size.");
        }
        catch (InvalidOperationException)
        {
            return Fail(RequestErrorKind.Validation, "The native commitment mutation has an invalid local authority binding.");
        }
        RaiseEvent(value);
        return Result.Success;
    }

    void ApplyFact(DomainEvent value)
    {
        var fact = (ICommitmentFact)value;
        Validate(value, fact);
        _header = fact.Header;
        CurrentAuthority = fact.Authority;
        NativeSequence = fact.NativeSequence;
        Acceptance.Observe(value);
    }

    void Validate(DomainEvent value, ICommitmentFact fact)
    {
        Require(fact.Operation is not null && fact.Operation.Actor is not null && fact.Header is not null,
            "A commitment native fact requires its original operation and compact header.");
        var operation = fact.Operation;
        var header = fact.Header;
        OwnerProvenance.RequireOwn(this, operation.LogicalId, operation);
        Require(TenantId != Uuid.Empty && Id != Uuid.Empty && fact.TenantId == TenantId && header.TenantId == TenantId && header.DraftId == Id &&
            header.ProgramId != Uuid.Empty && header.ServiceId != Uuid.Empty && header.Kind == NormalizeKind(header.Kind) &&
            header.Identifier == NormalizeIdentifier(header.Identifier) && Id == IdFor(TenantId, header.ProgramId, header.Kind, header.Identifier) &&
            fact.NativeSequence == NativeSequence + 1 && header.IsCreated && header.Revision > 0 &&
            operation.RootOperationId == operation.LogicalId && operation.CausationEventId is null && operation.Episode == Id.ToString() &&
            operation.AffectedOwner == Stream && operation.ExpectedRevisionOwner == Stream && (operation.FirstOwner is null || operation.FirstOwner == Stream) &&
            operation.Actor.Kind == "member" && Uuid.TryParse(operation.Actor.Id, out var actor) && actor != Uuid.Empty && Hex(operation.AuthoredIntentSha256) &&
            Reference(header.Content, CommitmentFactContent.ContentSchema) && Reference(fact.Change, CommitmentFactContent.ChangeSchema) &&
            Reference(fact.Authority, CommitmentFactContent.AuthoritySchema) && Reference(fact.FirstResponse, FirstResponses.Schema) &&
            Reference(fact.OperationResult, "mutation-result-content.v1"), "A commitment native fact has another owner or operation binding.");
        var member = Uuid.Parse(operation.Actor.Id, CultureInfo.InvariantCulture);
        var businessActor = value switch
        {
            CommitmentDraftCreated item => item.ActorMemberId,
            CommitmentDraftRevised item => item.ActorMemberId,
            CommitmentReviewed item => item.ActorMemberId,
            CommitmentApproved item => item.ActorMemberId,
            CommitmentRetired item => item.ActorMemberId ?? Uuid.Empty,
            ResponsibilityAssigned item => item.AssignedByMemberId,
            ResponsibilityRevoked item => item.RevokedByMemberId,
            _ => Uuid.Empty,
        };
        var at = value switch
        {
            CommitmentDraftCreated item => item.ChangedAt,
            CommitmentDraftRevised item => item.ChangedAt,
            CommitmentReviewed item => item.DecidedAt,
            CommitmentApproved item => item.DecidedAt,
            CommitmentRetired item => item.OccurredAt,
            ResponsibilityAssigned item => item.AssignedAt,
            ResponsibilityRevoked item => item.RevokedAt,
            _ => DateTimeOffset.MinValue,
        };
        var display = value switch
        {
            CommitmentDraftCreated item => item.ActorDisplay,
            CommitmentDraftRevised item => item.ActorDisplay,
            CommitmentReviewed item => item.ActorDisplay,
            CommitmentApproved item => item.ActorDisplay,
            CommitmentRetired item => item.ActorDisplay,
            ResponsibilityAssigned item => item.AssignedByDisplay,
            ResponsibilityRevoked item => item.RevokedByDisplay,
            _ => string.Empty,
        };
        Require(member == businessActor && at == operation.AcceptedAt && operation.Actor.Display == display,
            "The commitment receipt lost its original member, display snapshot or native acceptance instant.");
        if (_header is null)
        {
            Require(value is CommitmentDraftCreated created && fact.ChangeCount == 1 && operation.CommandKind == nameof(CreateCommitmentDraft) &&
                operation.LogicalId == Id && operation.OriginalExpectedRevision is null && created.Content == header.Content &&
                header == new CommitmentAuthorityHeader(TenantId, created.ProgramId, Id, created.ServiceId, created.ProviderId,
                    created.Kind, created.Identifier, 1, true, false, created.SourceReference, 0, null, null, null, null, null, header.Content),
                "A commitment begins with its deterministic exact first draft.");
            return;
        }
        var before = _header;
        Require(value is not CommitmentDraftCreated && before.ProgramId == header.ProgramId && before.ServiceId == header.ServiceId &&
            before.ProviderId == header.ProviderId && before.Kind == header.Kind && before.Identifier == header.Identifier && !before.IsRetired &&
            operation.OriginalExpectedRevision == unchecked((ulong)before.Revision), "The commitment identity, terminal state or original revision gate changed.");
        if (fact.ChangeCount == 0)
        {
            Require(value is ResponsibilityAssigned assignment
                ? operation.CommandKind == nameof(AssignResponsibility) && operation.LogicalId == assignment.AssignmentId
                : value is ResponsibilityRevoked && operation.CommandKind == nameof(RevokeResponsibility),
                "A no-op acceptance must retain its exact responsibility operation identity.");
            Require((value is ResponsibilityAssigned or ResponsibilityRevoked) && IsCurrentResponsibilityScope(value is ResponsibilityAssigned assigned ? assigned.Scope : ((ResponsibilityRevoked)value).Scope) &&
                header == before, "Only an unchanged current-scope responsibility operation may accept without a business change.");
            return;
        }
        Require(fact.ChangeCount == 1, "A commitment native operation publishes one business mutation.");
        var expected = before;
        switch (value)
        {
            case CommitmentDraftRevised revised:
                Require(operation.CommandKind == nameof(ReviseCommitmentDraft) && revised.Revision == before.Revision + 1 &&
                    revised.Content == header.Content, "Revision must retain the exact next content revision.");
                expected = before with
                {
                    Revision = revised.Revision,
                    Content = header.Content,
                    SourceReference = revised.SourceReference,
                    AcceptedReviewDecisionId = null,
                    AcceptedReviewerMemberId = null
                };
                break;
            case CommitmentReviewed reviewed:
                Require(operation.CommandKind == nameof(ReviewCommitmentDraft) && operation.LogicalId == reviewed.DecisionId && reviewed.Revision == before.Revision &&
                    reviewed.Outcome is "accept" or "request_changes" && (reviewed.Outcome != "accept" || before.LatestEffectiveRevision != before.Revision),
                    "Review must answer the exact pending commitment revision.");
                var legacy = reviewed.Version is not null && reviewed.EffectiveFrom is not null;
                expected = before with
                {
                    AcceptedReviewDecisionId = !legacy && reviewed.Outcome == "accept" ? reviewed.DecisionId : null,
                    AcceptedReviewerMemberId = !legacy && reviewed.Outcome == "accept" ? member : null,
                    EffectiveVersionCount = legacy ? before.EffectiveVersionCount + 1 : before.EffectiveVersionCount,
                    LatestEffectiveRevision = legacy ? reviewed.Revision : before.LatestEffectiveRevision,
                    LatestEffectiveFrom = legacy ? reviewed.EffectiveFrom : before.LatestEffectiveFrom
                };
                break;
            case CommitmentApproved approved:
                Require(operation.CommandKind == nameof(ApproveCommitmentDraft) && operation.LogicalId == approved.DecisionId && approved.Revision == before.Revision &&
                    before.AcceptedReviewDecisionId is not null && approved.AcceptedReviewDecisionId == before.AcceptedReviewDecisionId &&
                    approved.Version == before.EffectiveVersionCount + 1 && approved.EffectiveFrom != default &&
                    (before.LatestEffectiveFrom is null || approved.EffectiveFrom > before.LatestEffectiveFrom),
                    "Approval must retain the latest exact accepted review and later effective version.");
                expected = before with
                {
                    EffectiveVersionCount = approved.Version,
                    LatestEffectiveRevision = approved.Revision,
                    LatestEffectiveFrom = approved.EffectiveFrom,
                    AcceptedReviewDecisionId = null,
                    AcceptedReviewerMemberId = null
                };
                break;
            case CommitmentRetired retired:
                Require(operation.CommandKind == nameof(RetireCommitment) && retired.Revision == before.Revision && before.EffectiveVersionCount > 0 &&
                    retired.EffectiveOn > before.LatestEffectiveFrom, "Retirement requires a later effective end on the exact commitment revision.");
                expected = before with { IsRetired = true, RetiredEffectiveOn = retired.EffectiveOn };
                break;
            case ResponsibilityAssigned assigned:
                Require(operation.CommandKind == nameof(AssignResponsibility) && operation.LogicalId == assigned.AssignmentId &&
                    IsCurrentResponsibilityScope(assigned.Scope), "Assignment requires the exact current pending commitment revision.");
                break;
            case ResponsibilityRevoked revoked:
                Require(operation.CommandKind == nameof(RevokeResponsibility) && IsCurrentResponsibilityScope(revoked.Scope),
                    "Revocation requires the exact current pending commitment revision.");
                break;
            default:
                throw new InvalidOperationException("The commitment has another native mutation kind.");
        }
        Require(header == expected, "The commitment compact header differs from its exact local transition.");
    }

    CommitmentAuthorityContent ExpectedAuthority(DomainEvent value, CommitmentAuthorityHeader header, CommitmentAuthorityContent? before)
    {
        var after = before is null ? new CommitmentAuthorityContent(header, [], [], []) : before with { Header = header };
        if (((ICommitmentFact)value).ChangeCount == 0)
            return after;
        switch (value)
        {
            case CommitmentDraftCreated created:
                return after with { PendingAuthorMemberIds = [created.ActorMemberId] };
            case CommitmentDraftRevised revised:
                var authors = _header!.LatestEffectiveRevision == _header.Revision ? new[] { revised.ActorMemberId }
                    : after.PendingAuthorMemberIds.Append(revised.ActorMemberId).Distinct().ToArray();
                return after with { PendingAuthorMemberIds = authors.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray(), Assignments = [], DecisionConstraints = [] };
            case CommitmentReviewed reviewed:
                return after with
                {
                    DecisionConstraints = after.DecisionConstraints.Append(new CommitmentDecisionConstraint(reviewed.ActorMemberId,
                    ResponsibilityType.AssignedReviewer, reviewed.DecidedAt, reviewed.SeparationOfDutiesWaiverId)).ToArray()
                };
            case CommitmentApproved approved:
                return after with
                {
                    DecisionConstraints = after.DecisionConstraints.Append(new CommitmentDecisionConstraint(approved.ActorMemberId,
                    ResponsibilityType.PolicyApprover, approved.DecidedAt, approved.SeparationOfDutiesWaiverId)).ToArray()
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

    void ValidateLocal(DomainEvent value, CommitmentMutationContent mutation, CommitmentAuthorityContent? before)
    {
        var proofs = mutation.Waivers ?? [];
        Require(proofs.All(proof => proof is not null && LocatorValid(proof)) && proofs.Select(proof => proof.WaiverId).Distinct().Count() == proofs.Count,
            "A commitment mutation requires exact distinct immutable waiver proofs.");
        if (value is CommitmentReviewed reviewed)
        {
            var content = (CommitmentReviewedContent)mutation.Changes.Single();
            Require(content.Version is null && content.EffectiveFrom is null && content.ImpactDigest is null && Rationale(content.Rationale) &&
                content.InterpretationNote?.Length is not > 4000 && (content.Outcome != "accept" || !string.IsNullOrWhiteSpace(content.OwnerReference) &&
                    content.OwnerReference.Length <= 200 && content.Applicability is "applicable" or "not_applicable" &&
                    content.Interpretation is "supported" or "unsupported" && content.SourceVerification == "verified" &&
                    content.SourceVerifiedReference == _header!.SourceReference && !string.IsNullOrWhiteSpace(content.SourceEvidence) && content.SourceEvidence.Length <= 2000),
                "Acceptance requires the exact current verified source and a separate approval.");
            ValidateDecision(before!, reviewed.ActorMemberId, ResponsibilityType.AssignedReviewer, SeparationOfDutiesActions.Review,
                reviewed.DecidedAt, reviewed.SeparationOfDutiesWaiverId, proofs, false);
            return;
        }
        if (value is CommitmentApproved approved)
        {
            var content = (CommitmentApprovedContent)mutation.Changes.Single();
            Require(Rationale(content.Rationale) && !string.IsNullOrWhiteSpace(content.ImpactDigest), "Approval requires a bounded rationale and acknowledged impact.");
            ValidateDecision(before!, approved.ActorMemberId, ResponsibilityType.PolicyApprover, SeparationOfDutiesActions.Approve,
                approved.DecidedAt, approved.SeparationOfDutiesWaiverId, proofs, before!.Header.AcceptedReviewerMemberId == approved.ActorMemberId);
            return;
        }
        if (value is CommitmentRetired)
        {
            var content = (CommitmentRetiredContent)mutation.Changes.Single();
            Require(Rationale(content.Rationale) && !string.IsNullOrWhiteSpace(content.ActorDisplay) && content.ActorDisplay.Length <= 200 &&
                content.OccurredAt != default, "Retirement requires a named actor and bounded reason.");
        }
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
            if (((ICommitmentFact)value).ChangeCount == 0)
                Require(Matches(CurrentAuthority!, before! with { Assignments = set.ReadAssignments() }, CommitmentFactContent.AuthoritySchema), "No-op assignment changed authority.");
            return;
        }
        Require(proofs.Count == 0, "This commitment mutation cannot consume a decision waiver.");
        if (value is ResponsibilityRevoked revoked)
        {
            var set = Set(before!);
            Require(set.Revoke(revoked.AssignmentId, revoked.RevokedByMemberId, revoked.RevokedByDisplay, revoked.RevokedAt, revoked.Reason) is null,
                "Revocation requires the retained exact assignment.");
            if (((ICommitmentFact)value).ChangeCount == 0)
                Require(Matches(CurrentAuthority!, before! with { Assignments = set.ReadAssignments() }, CommitmentFactContent.AuthoritySchema), "No-op revocation changed authority.");
        }
    }

    void ValidateDecision(CommitmentAuthorityContent before, Uuid member, ResponsibilityType role, string action, DateTimeOffset at,
        Uuid? waiverId, IReadOnlyList<SeparationOfDutiesWaiverProof> proofs, bool isReviewer)
    {
        var set = Set(before);
        var assigned = before.Assignments.Where(item => item.Type == role && item.RevokedAt is null && item.EffectiveFrom <= at &&
            (item.EffectiveUntil is null || item.EffectiveUntil > at)).ToArray();
        Require(assigned.Length == 0 || assigned.Any(item => item.MemberId == member), "Only assigned participants of this exact revision may decide.");
        var proof = waiverId is { } id ? proofs.SingleOrDefault(item => item.WaiverId == id) : null;
        Require(proofs.Count == (proof is null ? 0 : 1) && (waiverId is null || proof is not null), "A decision must bind its exact immutable waiver.");
        var author = before.PendingAuthorMemberIds.Contains(member);
        Require(ResponsibilityDecisionGuard.ValidateAuthorized(set, Scope(), member, role, at, author || isReviewer, proof) is null &&
            (proof is not null ? CommitmentWaiverProof.Allows(proof, TenantId, new SeparationOfDutiesWaiverScope(
                SeparationOfDutiesRecordTypes.Commitment, Id, Id, Revision, action), member, at) : !author && !isReviewer),
            "The commitment decision violates its exact author, reviewer or responsibility independence.");
    }
    ResponsibilityScope Scope() => new(SeparationOfDutiesRecordTypes.Commitment, Id, Id, Revision);
    ResponsibilitySet Set(CommitmentAuthorityContent authority)
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
    static bool LocatorValid(SeparationOfDutiesWaiverProof proof) => proof.ApprovalFact.EventId != Uuid.Empty &&
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
    static bool Rationale(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 4000;
    static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
    static Result Fail(RequestErrorKind kind, string message, bool transient = false) => Result.Failure(new RequestError(kind, message, transient));
}
