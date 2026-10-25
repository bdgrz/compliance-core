using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>One deterministic control identity and compact current authority; full bodies/history never enter command state.</summary>
public sealed class ControlDraft : Aggregate, ISourceUseOwner
{
    public const string Area = "controls";
    public const string OrganizationAuthored = "organization_authored";
    public const int MaximumDraftEventPayloadBytes = 48 * 1024;
    readonly Uuid _tenantId;
    ControlAuthorityHeader? _header;

    public ControlDraft(Uuid tenantId, Uuid controlId, Uuid selectedCommandId = default)
        : base(controlId, new EventStreamAddress(tenantId.ToString(), Area, controlId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedCommandId);
        Uses = new SourceUseReservations(Stream);
        On<ControlDraftCreated>(ApplyFact);
        On<ControlDraftRevised>(ApplyFact);
        On<ControlDraftDiscarded>(ApplyFact);
        On<ControlReviewed>(ApplyFact);
        On<ControlApproved>(ApplyFact);
        On<ControlSuccessorProposed>(ApplyFact);
        On<ControlRetirementProposed>(ApplyFact);
        On<ControlRetired>(ApplyFact);
        On<ControlProposalWithdrawn>(ApplyFact);
        On<ControlOwnerPersonDesignated>(ApplyFact);
        On<ResponsibilityAssigned>(ApplyFact);
        On<ResponsibilityRevoked>(ApplyFact);
    }

    public NativeAcceptance Acceptance { get; }
    public SourceUseReservations Uses { get; }
    public long NativeSequence { get; private set; }
    public ImmutableContentRef? CurrentAuthority { get; private set; }
    public ControlAuthorityHeader? Header => _header;
    public Uuid TenantId => _tenantId;
    public Uuid ProgramId => _header?.ProgramId ?? Uuid.Empty;
    public long Revision => _header?.Revision ?? 0;
    public bool IsCreated => _header?.IsCreated ?? false;
    public bool IsVisible => _header?.IsVisible ?? false;
    public bool IsApproved => _header?.IsApproved ?? false;
    public bool IsRetired => _header?.IsRetired ?? false;
    public bool HasOpenDraft => _header?.HasOpenDraft ?? false;
    public Uuid DraftVersionId => _header?.DraftVersionId ?? ControlVersionIds.Initial(Id);
    public Uuid? PendingTargetId => _header?.PendingTargetId;
    public Uuid? PendingRetirementId => _header?.PendingRetirementId;
    public DateOnly? PendingRetirementEffectiveUntil => _header?.PendingRetirementEffectiveUntil;
    public Uuid? PendingAuthorMemberId => _header?.PendingAuthorMemberId;
    public Uuid? AcceptedReviewDecisionId => _header?.AcceptedReviewDecisionId;
    public Uuid? AcceptedReviewerMemberId => _header?.AcceptedReviewerMemberId;
    public Uuid? LatestReviewDecisionId => _header?.LatestReviewDecisionId;
    public DateTimeOffset? PendingDecisionChangedAt => _header?.PendingDecisionChangedAt;

    public static Uuid IdFor(Uuid tenantId, Uuid programId, string identifier) => ControlIds.For(tenantId, programId, identifier);
    public static string NormalizeIdentifier(string? identifier) => ControlIds.NormalizeIdentifier(identifier);
    internal static RequestError? ValidateContent(ControlDraftContent? content) => ControlRules.ValidateContent(content);
    internal static RequestError? ValidateCreateInput(string? identifier, ControlDraftContent? content) =>
        ControlRules.ValidateCreateInput(identifier, content);

    public bool IsCurrentResponsibilityScope(ResponsibilityScope scope) => scope.RecordType == SeparationOfDutiesRecordTypes.Control &&
        scope.RecordId == Id && PendingTargetId == scope.VersionId && Revision == scope.Revision;

    public Result Prepare(Uuid programId, long? expectedRevision, bool creating = false)
    {
        if (Uses.HasActiveUse)
            return Fail(RequestErrorKind.Conflict, "The control is held by an accepted source use.", true);
        if (creating)
            return IsCreated ? Fail(RequestErrorKind.Conflict, "The control identifier already exists.") : Result.Success;
        if (!IsVisible || ProgramId != programId)
            return Fail(RequestErrorKind.NotFound, "The control was not found.");
        return expectedRevision is { } expected && expected != Revision
            ? Result.Failure(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("control draft", Revision)).ToRequestError())
            : Result.Success;
    }

    /// <summary>The exact preparation cut is checked inside the owning OCC append, including same-content-revision decisions.</summary>
    public Result Append(DomainEvent value, long preparedSequence, ulong preparedPhysicalPosition,
        ControlMutationContent? preparedMutation = null, ControlAuthorityContent? beforeAuthority = null,
        ControlAuthorityContent? afterAuthority = null)
    {
        if (NativeSequence != preparedSequence || CommittedStreamPosition != preparedPhysicalPosition)
            return Fail(RequestErrorKind.Conflict, "The control changed while its exact content and authority were prepared.", true);
        if (Uses.HasActiveUse)
            return Fail(RequestErrorKind.Conflict, "The control is held by an accepted source use.", true);
        if (value is not IControlFact fact || fact.Header is null)
            return Fail(RequestErrorKind.Validation, "A control mutation requires a compact native fact.");
        try
        {
            Validate(value, fact);
            if (!PreparedAuthority(value, fact, preparedMutation, beforeAuthority, afterAuthority))
                return Fail(RequestErrorKind.Validation, "The control mutation does not carry its exact prepared local authority.");
            if (DomainEventPayload.Size(value) > DomainEventPayload.MaximumBytes)
                return Fail(RequestErrorKind.Validation, "The native control fact exceeds the bounded event payload size.");
        }
        catch (InvalidOperationException)
        {
            return Fail(RequestErrorKind.Validation, "The native control mutation has an invalid local authority binding.");
        }
        RaiseEvent(value);
        return Result.Success;
    }

    void ApplyFact(DomainEvent value)
    {
        var fact = (IControlFact)value;
        Validate(value, fact);
        _header = fact.Header;
        CurrentAuthority = fact.Authority;
        NativeSequence = fact.NativeSequence;
        Acceptance.Observe(value);
    }

    void Validate(DomainEvent value, IControlFact fact)
    {
        Require(fact.Operation is not null && fact.Operation.Actor is not null, "A native control mutation requires its operation and actor.");
        OwnerProvenance.RequireOwn(this, fact.Operation.LogicalId, fact.Operation);
        var after = fact.Header;
        if (after is null || fact.NativeSequence != NativeSequence + 1 || fact.TenantId != _tenantId ||
            after.TenantId != _tenantId || after.ControlId != Id || after.ProgramId == Uuid.Empty ||
            Id != IdFor(_tenantId, after.ProgramId, after.Identifier) || after.Revision < 1 || !after.IsCreated ||
            !Reference(fact.Change, ControlFactContent.ChangeSchema) || !Reference(fact.Authority, ControlFactContent.AuthoritySchema) ||
            !Reference(fact.FirstResponse, FirstResponses.Schema) || !Reference(fact.OperationResult, "mutation-result-content.v1") ||
            !Reference(after.Content, ControlFactContent.DraftSchema) ||
            fact.ChangeCount < 0 || !Hex(fact.Operation.AuthoredIntentSha256) || fact.Operation.RootOperationId != fact.Operation.LogicalId ||
            fact.Operation.CausationEventId is not null || fact.Operation.Episode != Id.ToString() ||
            fact.Operation.AffectedOwner != Stream || fact.Operation.ExpectedRevisionOwner != Stream ||
            fact.Operation.FirstOwner is { } first && first != Stream ||
            fact.Operation.Actor.Kind != "member" || !Uuid.TryParse(fact.Operation.Actor.Id, out var actor) || actor == Uuid.Empty ||
            after.IsApproved != (after.ApprovedVersionCount > 0) ||
            after.IsRetired && (after.HasOpenDraft || after.PendingTargetId is not null) ||
            after.HasOpenDraft && after.PendingTargetId != after.DraftVersionId)
            throw new InvalidOperationException("The control fact has an invalid exact native owner or authority binding.");
        if (_header is null)
        {
            if (value is not ControlDraftCreated || after.Revision != 1 || !after.IsVisible || after.IsApproved ||
                after.DraftVersionId != ControlVersionIds.Initial(Id) || fact.Operation.LogicalId != Id ||
                fact.Operation.CommandKind != nameof(CreateControlDraft) || fact.Operation.OriginalExpectedRevision is not null)
                throw new InvalidOperationException("A control begins with its deterministic own creation and first draft.");
            ValidateTransition(value, fact, actor);
            return;
        }
        if (value is ControlDraftCreated || after.ProgramId != _header.ProgramId || after.Identifier != _header.Identifier ||
            after.Revision < Revision || after.Revision > Revision + 1 ||
            after.ApprovedVersionCount < _header.ApprovedVersionCount || after.ApprovedVersionCount > _header.ApprovedVersionCount + 1 ||
            !_header.IsVisible && fact.ChangeCount > 0 || _header.IsRetired && fact.ChangeCount > 0)
            throw new InvalidOperationException("A control mutation cannot change its identity or rewrite terminal authority.");
        var kind = value switch
        {
            ControlDraftRevised => nameof(ReviseControlDraft),
            ControlDraftDiscarded => nameof(DiscardControlDraft),
            ControlReviewed => nameof(ReviewControl),
            ControlApproved => nameof(ApproveControl),
            ControlSuccessorProposed => nameof(ProposeControlSuccessor),
            ControlRetirementProposed => nameof(ProposeControlRetirement),
            ControlRetired => nameof(RetireControl),
            ControlProposalWithdrawn => nameof(WithdrawControlProposal),
            ControlOwnerPersonDesignated => nameof(DesignateControlOwnerPerson),
            ResponsibilityAssigned => nameof(AssignResponsibility),
            ResponsibilityRevoked => nameof(RevokeResponsibility),
            _ => throw new InvalidOperationException("The control native mutation has another business kind."),
        };
        if (fact.Operation.CommandKind != kind)
            throw new InvalidOperationException("The control's native operation kind differs from its accepted business fact.");
        if (value is ControlApproved approved &&
            (PendingTargetId != approved.VersionId || AcceptedReviewDecisionId != approved.AcceptedReviewDecisionId ||
             after.ApprovedVersionId != approved.VersionId || after.HasOpenDraft ||
             after.ApprovedVersionCount != _header.ApprovedVersionCount + 1))
            throw new InvalidOperationException("Control approval must answer the exact current draft and accepted review.");
        if (value is ControlSuccessorProposed successor &&
            (successor.PredecessorVersionId != _header.ApprovedVersionId ||
             successor.VersionId != ControlVersionIds.Sequence(Id, _header.ApprovedVersionCount + 1)))
            throw new InvalidOperationException("A successor must preserve its deterministic line and exact approved predecessor.");
        ValidateTransition(value, fact, actor);
    }

    void ValidateTransition(DomainEvent value, IControlFact fact, Uuid actor)
    {
        var after = fact.Header;
        var member = value switch
        {
            ControlDraftCreated item => item.ActorMemberId,
            ControlDraftRevised item => item.ActorMemberId,
            ControlDraftDiscarded item => item.ActorMemberId,
            ControlReviewed item => item.ActorMemberId,
            ControlApproved item => item.ActorMemberId,
            ControlSuccessorProposed item => item.ActorMemberId,
            ControlRetirementProposed item => item.ActorMemberId,
            ControlRetired item => item.ActorMemberId,
            ControlProposalWithdrawn item => item.ActorMemberId,
            ControlOwnerPersonDesignated item => item.ActorMemberId,
            ResponsibilityAssigned item => item.AssignedByMemberId,
            ResponsibilityRevoked item => item.RevokedByMemberId,
            _ => Uuid.Empty,
        };
        Require(member == actor, "The original business actor differs from the native operation actor.");
        var logical = value switch
        {
            ControlReviewed item => item.DecisionId,
            ControlApproved item => item.ApprovalDecisionId,
            ControlRetired item => item.DecisionId,
            ControlProposalWithdrawn item => item.DecisionId,
            ControlOwnerPersonDesignated item => item.DesignationId,
            ResponsibilityAssigned item => item.AssignmentId,
            _ => fact.Operation.LogicalId,
        };
        Require(logical != Uuid.Empty && logical == fact.Operation.LogicalId,
            "The original business decision differs from the native logical identity.");
        if (value is ControlDraftCreated created)
        {
            var initial = new ControlAuthorityHeader(TenantId, created.ProgramId, Id, created.Identifier,
                1, true, true, false, false, true, ControlVersionIds.Initial(Id), ControlVersionIds.Initial(Id),
                null, null, member, null, null, null, created.ChangedAt, 0, null, null, false, false, after.Content);
            Require(fact.ChangeCount == 1 && after == initial && created.Identifier == NormalizeIdentifier(created.Identifier),
                "Creation requires the exact first local authority.");
            return;
        }
        var before = _header!;
        var expectedRevision = value is ControlSuccessorProposed or ControlRetirementProposed
            ? (ulong?)null : unchecked((ulong)before.Revision);
        Require(fact.Operation.OriginalExpectedRevision == expectedRevision,
            "The native operation must retain the original logical revision gate.");
        if (fact.ChangeCount == 0)
        {
            var valid = value switch
            {
                ControlDraftRevised revised => before.HasOpenDraft && revised.Revision == before.Revision,
                ControlOwnerPersonDesignated designated => before.HasOpenDraft && designated.Revision == before.Revision &&
                    designated.VersionId == before.DraftVersionId && designated.PersonId != Uuid.Empty && Rationale(designated.Rationale),
                ResponsibilityAssigned assigned => IsCurrentResponsibilityScope(assigned.Scope),
                ResponsibilityRevoked revoked => IsCurrentResponsibilityScope(revoked.Scope),
                _ => false,
            };
            Require(valid && before.IsVisible && !before.IsRetired && after == before,
                "Only a valid unchanged owner-local operation may accept without a business change.");
            return;
        }
        Require(before.IsVisible && !before.IsRetired, "Terminal control authority cannot change.");
        ControlAuthorityHeader expected;
        switch (value)
        {
            case ControlDraftRevised revised:
                Require(before.HasOpenDraft && revised.Revision == before.Revision + 1,
                    "Revision requires the exact open draft and next content revision.");
                expected = before with
                {
                    Revision = revised.Revision,
                    Content = after.Content,
                    PendingAuthorMemberId = actor,
                    AcceptedReviewDecisionId = null,
                    AcceptedReviewerMemberId = null,
                    LatestReviewDecisionId = null,
                    PendingDecisionChangedAt = revised.ChangedAt
                };
                break;
            case ControlReviewed review:
                Require(before.PendingTargetId == review.VersionId && review.Revision == before.Revision &&
                    review.Outcome is "accept" or "request_changes" && Rationale(review.Rationale) &&
                    review.SupersedesDecisionId == before.LatestReviewDecisionId,
                    "Review must answer the current exact pending revision and latest decision.");
                expected = before with
                {
                    EverReviewed = true,
                    LatestReviewDecisionId = review.DecisionId,
                    AcceptedReviewDecisionId = review.Outcome == "accept" ? review.DecisionId : null,
                    AcceptedReviewerMemberId = review.Outcome == "accept" ? actor : null,
                    PendingDecisionChangedAt = review.DecidedAt
                };
                break;
            case ControlApproved approved:
                Require(before.HasOpenDraft && before.PendingTargetId == approved.VersionId &&
                    approved.Revision == before.Revision && before.AcceptedReviewDecisionId is not null &&
                    approved.AcceptedReviewDecisionId == before.AcceptedReviewDecisionId &&
                    approved.OwnerAssignmentId != Uuid.Empty && approved.EffectiveFrom != default && Rationale(approved.Rationale) &&
                    approved.PredecessorVersionId == before.ApprovedVersionId &&
                    (before.ApprovedVersionId is null || !string.IsNullOrWhiteSpace(approved.ImpactDigest) &&
                        approved.EffectiveFrom > before.ApprovedEffectiveFrom),
                    "Approval must preserve the exact reviewed draft, owner, predecessor and effective interval.");
                expected = before with
                {
                    HasOpenDraft = false,
                    IsApproved = true,
                    PendingTargetId = null,
                    PendingAuthorMemberId = null,
                    AcceptedReviewDecisionId = null,
                    AcceptedReviewerMemberId = null,
                    PendingDecisionChangedAt = null,
                    ApprovedVersionCount = before.ApprovedVersionCount + 1,
                    ApprovedVersionId = approved.VersionId,
                    ApprovedEffectiveFrom = approved.EffectiveFrom
                };
                break;
            case ControlSuccessorProposed successor:
                Require(before.IsApproved && !before.HasOpenDraft && successor.Revision == before.Revision + 1 &&
                    successor.PredecessorVersionId == before.ApprovedVersionId &&
                    successor.VersionId == ControlVersionIds.Sequence(Id, before.ApprovedVersionCount + 1),
                    "A successor requires its exact approved predecessor and deterministic next version.");
                expected = before with
                {
                    Revision = successor.Revision,
                    Content = after.Content,
                    HasOpenDraft = true,
                    DraftVersionId = successor.VersionId,
                    PendingTargetId = successor.VersionId,
                    PendingRetirementId = null,
                    PendingRetirementEffectiveUntil = null,
                    PendingAuthorMemberId = actor,
                    AcceptedReviewDecisionId = null,
                    AcceptedReviewerMemberId = null,
                    LatestReviewDecisionId = null,
                    PendingDecisionChangedAt = successor.ChangedAt
                };
                break;
            case ControlRetirementProposed retirement:
                Require(before.IsApproved && !before.HasOpenDraft && before.PendingRetirementId is null &&
                    retirement.VersionId == before.ApprovedVersionId && retirement.Revision == before.Revision &&
                    retirement.RetirementId == ControlVersionIds.Retirement(Id, retirement.VersionId) &&
                    retirement.EffectiveUntil > before.ApprovedEffectiveFrom && Rationale(retirement.Rationale),
                    "Retirement requires the exact current version and a later exclusive effective end.");
                expected = before with
                {
                    PendingTargetId = retirement.RetirementId,
                    PendingRetirementId = retirement.RetirementId,
                    PendingRetirementEffectiveUntil = retirement.EffectiveUntil,
                    PendingAuthorMemberId = actor,
                    AcceptedReviewDecisionId = null,
                    AcceptedReviewerMemberId = null,
                    LatestReviewDecisionId = null,
                    PendingDecisionChangedAt = retirement.ChangedAt
                };
                break;
            case ControlRetired retired:
                Require(before.PendingRetirementId == retired.RetirementId && before.PendingTargetId == retired.RetirementId &&
                    retired.VersionId == before.ApprovedVersionId && retired.Revision == before.Revision &&
                    before.AcceptedReviewDecisionId is not null && retired.AcceptedReviewDecisionId == before.AcceptedReviewDecisionId &&
                    retired.EffectiveUntil == before.PendingRetirementEffectiveUntil && Rationale(retired.Rationale) &&
                    !string.IsNullOrWhiteSpace(retired.ImpactDigest),
                    "Retirement approval must answer the exact reviewed proposal and frozen effective end.");
                expected = before with
                {
                    IsRetired = true,
                    PendingTargetId = null,
                    PendingRetirementId = null,
                    PendingRetirementEffectiveUntil = null,
                    PendingAuthorMemberId = null,
                    AcceptedReviewDecisionId = null,
                    AcceptedReviewerMemberId = null,
                    PendingDecisionChangedAt = null
                };
                break;
            case ControlProposalWithdrawn withdrawn:
                Require(before.IsApproved && before.PendingTargetId == withdrawn.TargetId &&
                    withdrawn.CurrentVersionId == before.ApprovedVersionId && withdrawn.Revision == before.Revision &&
                    withdrawn.Kind == (before.HasOpenDraft ? "successor" : "retirement") && Rationale(withdrawn.Rationale),
                    "Withdrawal must name the exact pending proposal and retained approved version.");
                expected = before with
                {
                    Revision = before.Revision + 1,
                    Content = after.Content,
                    HasOpenDraft = false,
                    DraftVersionId = before.ApprovedVersionId!.Value,
                    PendingTargetId = null,
                    PendingRetirementId = null,
                    PendingRetirementEffectiveUntil = null,
                    PendingAuthorMemberId = null,
                    AcceptedReviewDecisionId = null,
                    AcceptedReviewerMemberId = null,
                    LatestReviewDecisionId = null,
                    PendingDecisionChangedAt = null
                };
                break;
            case ControlOwnerPersonDesignated designated:
                Require(before.HasOpenDraft && designated.VersionId == before.DraftVersionId && designated.Revision == before.Revision &&
                    designated.PersonId != Uuid.Empty && (designated.PersonId is not null || designated.CorrelatedMemberId is null) &&
                    Rationale(designated.Rationale), "Person designation requires the exact open draft revision and original intent.");
                expected = before;
                break;
            case ResponsibilityAssigned assigned:
                Require(IsCurrentResponsibilityScope(assigned.Scope), "Assignment requires the exact current pending scope.");
                expected = before with { ResponsibilityEverAssigned = true };
                break;
            case ResponsibilityRevoked revoked:
                Require(IsCurrentResponsibilityScope(revoked.Scope), "Revocation requires the exact current pending scope.");
                expected = before;
                break;
            case ControlDraftDiscarded discarded:
                Require(before.HasOpenDraft && !before.IsApproved && !before.EverReviewed && !before.ResponsibilityEverAssigned &&
                    discarded.Revision == before.Revision && !string.IsNullOrWhiteSpace(discarded.Rationale),
                    "Discard requires an unused, unreviewed, unapproved initial draft.");
                expected = before with
                {
                    IsVisible = false,
                    HasOpenDraft = false,
                    PendingTargetId = null,
                    PendingAuthorMemberId = null,
                    PendingDecisionChangedAt = null
                };
                break;
            default:
                throw new InvalidOperationException("The native control transition is not selected.");
        }
        Require(after == expected, "The native control header differs from its exact local business transition.");
    }

    static bool Rationale(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 4000;

    bool PreparedAuthority(DomainEvent value, IControlFact fact, ControlMutationContent? mutation,
        ControlAuthorityContent? before, ControlAuthorityContent? after)
    {
        if (after is null || after.Header != fact.Header || !Matches(fact.Authority, after, ControlFactContent.AuthoritySchema))
            return false;
        if (mutation is null || mutation.Changes is null || mutation.Changes.Count != fact.ChangeCount ||
            !Matches(fact.Change, mutation, ControlFactContent.ChangeSchema) ||
            mutation.Changes.Any(change => !ControlFactContent.InScope(change, fact.Header)) ||
            mutation.Receipt is { } receipt && !ControlFactContent.InScope(receipt, fact.Header))
            return false;
        if (_header is not null && (before is null || before.Header != _header || CurrentAuthority is null ||
                !Matches(CurrentAuthority, before, ControlFactContent.AuthoritySchema)))
            return false;
        if (_header is null && before is not null)
            return false;
        var expected = ControlFactFactory.Create(fact.Operation.CommandKind, fact.Header, fact.NativeSequence,
            fact.Operation, fact.Change, fact.Authority, fact.FirstResponse!, fact.OperationResult!, mutation);
        if (ContentDigest.Sha256(expected, expected.GetType()) != ContentDigest.Sha256(value, value.GetType()))
            return false;
        if (fact.ChangeCount == 0)
        {
            if (fact.Header != _header || fact.Authority != CurrentAuthority)
                return false;
        }
        else if (mutation.Changes.Count != (value is ControlSuccessorProposed or ControlProposalWithdrawn ? 2 : 1))
            return false;
        if ((value is ControlSuccessorProposed or ControlProposalWithdrawn) &&
            (mutation.Changes[1] is not ControlDraftRevisedContent companion || companion.Revision != fact.Header.Revision ||
             companion.ActorMemberId != Uuid.Parse(fact.Operation.Actor.Id, System.Globalization.CultureInfo.InvariantCulture)))
            return false;
        var contentChange = mutation.Changes.OfType<ControlDraftCreatedContent>().Select(change => change.Content)
            .Concat(mutation.Changes.OfType<ControlDraftRevisedContent>().Select(change => change.Content))
            .Concat(mutation.Changes.OfType<ControlApprovedContent>().Select(change => change.Content))
            .Concat(mutation.Receipt is ControlDraftRevisedContent unchanged ? [unchanged.Content] : []).LastOrDefault();
        if (value is ControlDraftCreated created && (contentChange is null ||
            ControlRules.ValidateCreateInput(created.Identifier, contentChange) is not null))
            return false;
        if (contentChange is not null && (ControlRules.ValidateContent(contentChange) is not null ||
            !Matches(fact.Header.Content, contentChange, ControlFactContent.DraftSchema)))
            return false;
        if (!Matches(fact.Authority, ExpectedAuthority(value, fact.Header, before, contentChange), ControlFactContent.AuthoritySchema))
            return false;
        if (value is ControlProposalWithdrawn &&
            (before?.ApprovedContentSha256 is not { } approved || fact.Header.Content.Sha256 != approved))
            return false;
        if (value is ControlDraftDiscarded && before?.CurrentApplicabilityCount != 0)
            return false;
        var supplied = mutation.Waivers ?? [];
        if (supplied.Any(proof => proof is null) || supplied.Select(proof => proof.WaiverId).Distinct().Count() != supplied.Count)
            return false;
        if (value is ControlReviewed review)
            return DecisionAllowed(before!, review.ActorMemberId, ResponsibilityType.AssignedReviewer,
                SeparationOfDutiesActions.Review, review.DecidedAt, review.SeparationOfDutiesWaiverId, supplied);
        if (value is ControlApproved approval)
        {
            if (!DecisionAllowed(before!, approval.ActorMemberId, ResponsibilityType.PolicyApprover,
                    SeparationOfDutiesActions.Approve, approval.DecidedAt, approval.SeparationOfDutiesWaiverId, supplied))
                return false;
            if (approval.OwnerPersonId is { } person)
                return before!.PersonOwnerBinding is { } binding && binding.PersonId == person &&
                    binding.DesignationId == approval.OwnerAssignmentId &&
                    (approval.SeparationOfDutiesWaiverId is not null || approval.OwnerMemberId != approval.ActorMemberId);
            return before!.Assignments.Any(assignment => assignment.AssignmentId == approval.OwnerAssignmentId &&
                assignment.MemberId == approval.OwnerMemberId && assignment.Type == ResponsibilityType.ControlOwner &&
                Active(assignment, approval.DecidedAt));
        }
        if (value is ControlRetired retired)
            return DecisionAllowed(before!, retired.ActorMemberId, ResponsibilityType.PolicyApprover,
                SeparationOfDutiesActions.Approve, retired.DecidedAt, retired.SeparationOfDutiesWaiverId, supplied);
        if (value is ResponsibilityAssigned assigned)
        {
            var set = Set(before!);
            var proposed = new ResponsibilityAssignmentView(TenantId, assigned.AssignmentId, assigned.MemberId,
                assigned.Type, assigned.Scope, assigned.AssignedAt, assigned.AssignedByMemberId,
                assigned.EffectiveFrom, assigned.EffectiveUntil, null, Uuid.Empty, []);
            foreach (var constraint in before!.DecisionConstraints.Where(constraint => constraint.WaiverId is null))
            {
                var decision = new ResponsibilityAssignmentView(TenantId, Uuid.Empty, constraint.MemberId,
                    constraint.Type, assigned.Scope, constraint.At, Uuid.Empty, constraint.At,
                    constraint.At.AddTicks(1), null, Uuid.Empty, []);
                if (ResponsibilityConflictPolicy.FindConflicts(before.Assignments.Append(proposed), decision).Count > 0)
                    return false;
            }
            return supplied.Select(proof => proof.WaiverId).OrderBy(id => id.ToString(), StringComparer.Ordinal)
                    .SequenceEqual(assigned.SeparationOfDutiesWaiverIds.OrderBy(id => id.ToString(), StringComparer.Ordinal)) &&
                set.AssignAuthorized(assigned.AssignmentId, assigned.MemberId, assigned.Type,
                assigned.AssignedByMemberId, assigned.AssignedByDisplay, assigned.AssignedAt,
                assigned.EffectiveFrom, assigned.EffectiveUntil, supplied) is null &&
                supplied.All(proof => assigned.SeparationOfDutiesWaiverIds.Contains(proof.WaiverId) && LocatorValid(proof)) &&
                (fact.ChangeCount != 0 || Matches(CurrentAuthority!, before! with { Assignments = set.ReadAssignments() },
                    ControlFactContent.AuthoritySchema));
        }
        if (value is ResponsibilityRevoked revoked)
        {
            var set = Set(before!);
            return supplied.Count == 0 && set.Revoke(revoked.AssignmentId, revoked.RevokedByMemberId,
                revoked.RevokedByDisplay, revoked.RevokedAt, revoked.Reason) is null &&
                (fact.ChangeCount != 0 || Matches(CurrentAuthority!, before! with { Assignments = set.ReadAssignments() },
                    ControlFactContent.AuthoritySchema));
        }
        if (fact.ChangeCount == 0 && value is ControlOwnerPersonDesignated designated)
            return supplied.Count == 0 && before!.PersonOwnerBinding?.PersonId == designated.PersonId &&
                before.PersonOwnerBinding?.CorrelatedMemberId == designated.CorrelatedMemberId;
        return supplied.Count == 0;
    }

    ControlAuthorityContent ExpectedAuthority(DomainEvent value, ControlAuthorityHeader header,
        ControlAuthorityContent? before, ControlDraftContent? content)
    {
        var expected = before is null
            ? new ControlAuthorityContent(header, [], [], null, CurrentApplicabilityCount: content?.Applicability?.Count ?? 0)
            : before with { Header = header };
        if (((IControlFact)value).ChangeCount == 0)
            return expected;
        if (value is ControlDraftRevised or ControlSuccessorProposed or ControlRetirementProposed or
            ControlProposalWithdrawn or ControlDraftDiscarded or ControlApproved or ControlRetired)
            expected = expected with { Assignments = [], DecisionConstraints = [], PersonOwner = null, PersonOwnerBinding = null };
        if (content is not null)
            expected = expected with { CurrentApplicabilityCount = content.Applicability?.Count ?? 0 };
        if (value is ControlApproved)
            expected = expected with { ApprovedContentSha256 = header.Content.Sha256 };
        if (value is ControlReviewed review)
            expected = expected with
            {
                DecisionConstraints = expected.DecisionConstraints.Append(new ControlDecisionConstraint(
                review.ActorMemberId, ResponsibilityType.AssignedReviewer, review.DecidedAt, review.SeparationOfDutiesWaiverId)).ToArray()
            };
        if (value is ControlOwnerPersonDesignated designated)
            expected = expected with
            {
                PersonOwner = designated.PersonId is { } person
                    ? new ControlPersonOwnerDesignationView(person, designated.DesignationId, designated.Rationale,
                        designated.Actor, designated.DesignatedAt) : null,
                PersonOwnerBinding = designated.PersonId is { } binding
                    ? new ControlPersonOwner(binding, designated.CorrelatedMemberId, designated.DesignationId) : null
            };
        if (value is ResponsibilityAssigned assigned)
        {
            var set = Set(before!);
            set.Apply(assigned);
            expected = expected with { Assignments = set.ReadAssignments() };
        }
        if (value is ResponsibilityRevoked revoked)
        {
            var set = Set(before!);
            set.Apply(revoked);
            expected = expected with { Assignments = set.ReadAssignments() };
        }
        return expected;
    }

    bool DecisionAllowed(ControlAuthorityContent before, Uuid actor, ResponsibilityType role, string action,
        DateTimeOffset at, Uuid? waiverId, IReadOnlyList<SeparationOfDutiesWaiverProof> proofs)
    {
        var waiver = waiverId is { } id ? proofs.SingleOrDefault(proof => proof.WaiverId == id) : null;
        if (waiverId is not null && waiver is null || proofs.Count != (waiver is null ? 0 : 1))
            return false;
        var scope = new ResponsibilityScope(SeparationOfDutiesRecordTypes.Control, Id, PendingTargetId!.Value, Revision);
        var author = PendingAuthorMemberId == actor;
        if (waiver is not null && (!LocatorValid(waiver) || !ControlWaiverProof.Allows(waiver, TenantId,
            new SeparationOfDutiesWaiverScope(scope.RecordType, Id, scope.VersionId, Revision, action), actor, at)))
            return false;
        if (ResponsibilityDecisionGuard.ValidateAuthorized(Set(before), scope, actor, role, at, author, waiver) is not null)
            return false;
        return waiver is not null || !author && before.PersonOwnerBinding?.CorrelatedMemberId != actor;
    }

    ResponsibilitySet Set(ControlAuthorityContent authority)
    {
        var scope = new ResponsibilityScope(SeparationOfDutiesRecordTypes.Control, Id, PendingTargetId!.Value, Revision);
        var set = new ResponsibilitySet(TenantId, scope);
        foreach (var assignment in authority.Assignments)
        {
            Require(assignment.TenantId == TenantId && assignment.Scope == scope,
                "Current responsibility authority has another scope.");
            set.Apply(new ResponsibilityAssigned(TenantId, assignment.AssignmentId, assignment.MemberId,
                assignment.Type, scope, assignment.AssignedAt, assignment.AssignedByMemberId,
                assignment.EffectiveFrom, assignment.EffectiveUntil, assignment.SeparationOfDutiesWaiverIds,
                assignment.AssignedByDisplay));
            if (assignment.RevokedAt is { } revoked)
                set.Apply(new ResponsibilityRevoked(TenantId, assignment.AssignmentId, scope, assignment.RevokedByMemberId,
                    revoked, assignment.RevocationReason ?? string.Empty,
                    assignment.RevokedByDisplay ?? string.Empty));
        }
        return set;
    }

    static bool Active(ResponsibilityAssignmentView assignment, DateTimeOffset at) =>
        assignment.EffectiveFrom <= at && (assignment.EffectiveUntil is null || at < assignment.EffectiveUntil) &&
        assignment.RevokedAt is null;

    static bool LocatorValid(SeparationOfDutiesWaiverProof proof) => proof.ApprovalFact.EventId != Uuid.Empty &&
        proof.ApprovalFact.ResourcePosition == 1 && proof.ApprovalFact.AggregateVersion == 2 &&
        Hex(proof.ApprovalFact.PayloadSha256) && proof.ValidFrom < proof.ValidUntil;

    static bool Reference(ImmutableContentRef? reference, string schema) => reference is not null &&
        reference.ContentId != Uuid.Empty && reference.Schema == schema && reference.Bytes >= 0 &&
        reference.MediaType == ImmutableContentStoreExtensions.JsonMediaType && Hex(reference.Sha256);

    static bool Matches<T>(ImmutableContentRef reference, T value, string schema) where T : notnull
    {
        if (!Reference(reference, schema))
            return false;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))!);
        return reference.Bytes == bytes.LongLength && reference.Sha256 == ContentDigest.Sha256(bytes);
    }

    static bool Hex(string? value) => value is { Length: 64 } && value.All(character =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    static Result Fail(RequestErrorKind kind, string message, bool transient = false) =>
        Result.Failure(new RequestError(kind, message, transient));
    protected override void Apply(DomainEvent ev)
    {
        base.Apply(ev);
        Uses.ObserveOwnerFact(ev);
    }
}
