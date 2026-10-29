using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Policies.Contracts;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Policies.Commands;
using Bdgrz.Compliance.Features.Policies.Events;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Policies.Aggregates;

/// <summary>Deterministic Policy identity and compact native authority. Full content/history belongs to verified immutable readers.</summary>
public sealed class Policy : Aggregate
{
    public const string Area = "policies";
    public const int MaximumBodyLength = PolicyRules.MaximumBodyLength;
    public const int MaximumEventPayloadBytes = DomainEventPayload.MaximumBytes;
    readonly Uuid _tenantId;
    PolicyAuthorityHeader? _header;
    public Policy(Uuid tenantId, Uuid policyId, Uuid selectedCommandId = default)
        : base(policyId, new EventStreamAddress(tenantId.ToString(), Area, policyId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new(selectedCommandId);
        On<PolicyDraftCreated>(ApplyFact);
        On<PolicyDraftRevised>(ApplyFact);
        On<PolicyReviewed>(ApplyFact);
        On<PolicyApproved>(ApplyFact);
        On<PolicyPeriodicReviewConfirmed>(ApplyFact);
        On<PolicyRetirementProposed>(ApplyFact);
        On<PolicyRetired>(ApplyFact);
        On<PolicyDraftDiscarded>(ApplyFact);
    }
    public NativeAcceptance Acceptance { get; }
    public long NativeSequence { get; private set; }
    public ImmutableContentRef? CurrentAuthority { get; private set; }
    public PolicyAuthorityHeader? Header => _header;
    public Uuid TenantId => _tenantId;
    public Uuid ProgramId => _header?.ProgramId ?? Uuid.Empty;
    public string? Identifier => _header?.Identifier;
    public long Revision => _header?.Revision ?? 0;
    public bool IsCreated => _header?.IsCreated ?? false;
    public bool IsVisible => _header?.IsVisible ?? false;
    public bool IsRetired => _header?.IsRetired ?? false;
    public static string NormalizeIdentifier(string? value) => PolicyRules.NormalizeIdentifier(value);
    public static Uuid IdFor(Uuid tenant, Uuid program, string identifier) => PolicyRules.IdFor(tenant, program, identifier);
    public static string? ValidateContent(PolicyContent? content) => PolicyRules.ValidateContent(content);
    public static string Hash(PolicyContent content) => PolicyRules.Hash(content);
    public static string Key(PolicyApplicabilityReference reference) => PolicyRules.Key(reference);

    public Result Prepare(Uuid programId, bool creating)
    {
        if (creating)
            return IsCreated ? Fail(RequestErrorKind.Conflict, "The policy identifier already exists.") : Result.Success;
        return IsVisible && ProgramId == programId ? Result.Success : Fail(RequestErrorKind.NotFound, "The policy was not found.");
    }

    public Result Append(DomainEvent value, long preparedSequence, ulong preparedPhysicalPosition,
        PolicyMutationContent mutation, PolicyAuthorityContent? before, PolicyAuthorityContent after)
    {
        if (preparedSequence != NativeSequence || preparedPhysicalPosition != CommittedStreamPosition)
            return Fail(RequestErrorKind.Conflict, "The policy changed while its exact content and authority were prepared.", true);
        if (value is not IPolicyFact fact || mutation is null || after is null)
            return Fail(RequestErrorKind.Validation, "A policy mutation requires its exact prepared native authority.");
        try
        {
            Validate(value, fact);
            Require(after.Header == fact.Header && Matches(fact.Authority, after, PolicyFactContent.AuthoritySchema) &&
                Matches(fact.Change, mutation, PolicyFactContent.ChangeSchema) && PolicyFactContent.InScope(mutation.Change, fact.Header),
                "The native Policy immutable references differ from their prepared scope and content.");
            Require(_header is null ? before is null : before is not null && before.Header == _header &&
                CurrentAuthority is not null && Matches(CurrentAuthority, before, PolicyFactContent.AuthoritySchema),
                "The native Policy preparation has another original authority cut.");
            var receipt = PolicyFactFactory.Create(fact.Operation.CommandKind, fact.Header, fact.NativeSequence,
                fact.Operation, fact.Change, fact.Authority, fact.FirstResponse!, fact.OperationResult!, mutation);
            Require(ContentDigest.Sha256(receipt, receipt.GetType()) == ContentDigest.Sha256(value, value.GetType()),
                "The native Policy receipt differs from the original immutable mutation.");
            var body = mutation.Change switch
            {
                PolicyDraftCreatedContent created => created.Content,
                PolicyDraftRevisedContent revised => revised.Content,
                _ => null,
            };
            if (body is not null)
                Require(ValidateContent(body) is null && Matches(fact.Header.Content, body, PolicyFactContent.ContentSchema),
                    "The native Policy body violates its exact authored content contract.");
            Require(Matches(fact.Authority, ExpectedAuthority(mutation, fact.Header, before, body), PolicyFactContent.AuthoritySchema),
                "The native Policy after authority differs from its local business mutation.");
            ValidateDecision(mutation, before, fact.Operation.AcceptedAt);
            if (value is PolicyDraftDiscarded)
                Require(before!.CurrentApplicabilityCount == 0, "Discard cannot remove retained applicability.");
            if (value is PolicyApproved)
                Require(before!.HasAccountableOwner, "Activation requires an accountable policy owner.");
            Require(DomainEventPayload.Size(value) <= DomainEventPayload.MaximumBytes,
                "The native Policy scalar envelope exceeds the bounded event payload size.");
        }
        catch (InvalidOperationException)
        {
            return Fail(RequestErrorKind.Validation, "The native Policy mutation has an invalid local authority binding.");
        }
        RaiseEvent(value);
        return Result.Success;
    }

    void ApplyFact(DomainEvent value)
    {
        var fact = (IPolicyFact)value;
        Validate(value, fact);
        _header = fact.Header;
        CurrentAuthority = fact.Authority;
        NativeSequence = fact.NativeSequence;
        Acceptance.Observe(value);
    }

    void Validate(DomainEvent value, IPolicyFact fact)
    {
        Require(fact.Operation is not null && fact.Operation.Actor is not null && fact.Header is not null,
            "A native Policy fact requires its original provenance and compact header.");
        OwnerProvenance.RequireOwn(this, fact.Operation.LogicalId, fact.Operation);
        var header = fact.Header;
        var operation = fact.Operation;
        Require(fact.NativeSequence == NativeSequence + 1 && fact.TenantId == TenantId && header.TenantId == TenantId &&
            header.PolicyId == Id && header.ProgramId != Uuid.Empty && Id == IdFor(TenantId, header.ProgramId, header.Identifier) &&
            header.Identifier == NormalizeIdentifier(header.Identifier) && PolicyRules.ValidateIdentifier(header.Identifier) is null && header.IsCreated && header.Revision >= 1 &&
            operation.RootOperationId == operation.LogicalId && operation.CausationEventId is null && operation.Episode == Id.ToString() &&
            operation.AffectedOwner == Stream && operation.ExpectedRevisionOwner == Stream &&
            (operation.FirstOwner is null || operation.FirstOwner == Stream) && operation.Actor.Kind == "member" &&
            Uuid.TryParse(operation.Actor.Id, out var member) && member != Uuid.Empty && Hex(operation.AuthoredIntentSha256) &&
            Reference(header.Content, PolicyFactContent.ContentSchema) && Reference(fact.Change, PolicyFactContent.ChangeSchema) &&
            Reference(fact.Authority, PolicyFactContent.AuthoritySchema) && Reference(fact.FirstResponse, FirstResponses.Schema) &&
            Reference(fact.OperationResult, "mutation-result-content.v1"), "A Policy fact has another native owner or operation binding.");
        var actor = Uuid.Parse(operation.Actor.Id, CultureInfo.InvariantCulture);
        var businessActor = value switch
        {
            PolicyDraftCreated item => item.ActorMemberId,
            PolicyDraftRevised item => item.ActorMemberId,
            PolicyReviewed item => item.ActorMemberId,
            PolicyApproved item => item.ActorMemberId,
            PolicyPeriodicReviewConfirmed item => item.ActorMemberId,
            PolicyRetirementProposed item => item.ActorMemberId,
            PolicyRetired item => item.ActorMemberId,
            PolicyDraftDiscarded item when item.Actor is { Kind: "member" } discardActor && Uuid.TryParse(discardActor.Id, out var id) => id,
            _ => Uuid.Empty,
        };
        Require(actor == businessActor, "The original Policy actor differs from its native admission actor.");
        var snapshot = value switch
        {
            PolicyDraftCreated item => item.Actor,
            PolicyDraftRevised item => item.Actor,
            PolicyReviewed item => item.Actor,
            PolicyApproved item => item.Actor,
            PolicyPeriodicReviewConfirmed item => item.Actor,
            PolicyRetirementProposed item => item.Actor,
            PolicyRetired item => item.Actor,
            PolicyDraftDiscarded item => item.Actor,
            _ => null,
        };
        Require(snapshot is not null && (snapshot.Kind == "member" ? Uuid.TryParse(snapshot.Id, out var snapshotId) && snapshotId == actor :
            snapshot.Kind == "system_process" && (value is PolicyDraftCreated or PolicyDraftRevised or PolicyDraftDiscarded)),
            "The original Policy actor snapshot has another personal identity.");
        var recordedAt = value switch
        {
            PolicyDraftCreated item => item.ChangedAt,
            PolicyDraftRevised item => item.ChangedAt,
            PolicyReviewed item => item.DecidedAt,
            PolicyApproved item => item.DecidedAt,
            PolicyPeriodicReviewConfirmed item => item.DecidedAt,
            PolicyRetirementProposed item => item.ProposedAt,
            PolicyRetired item => item.DecidedAt,
            PolicyDraftDiscarded item => item.DiscardedAt,
            _ => DateTimeOffset.MinValue,
        };
        Require(recordedAt == operation.AcceptedAt, "The Policy receipt must retain its original native acceptance instant.");
        if (_header is null)
        {
            Require(value is PolicyDraftCreated created && operation.CommandKind == nameof(CreatePolicyDraft) &&
                operation.LogicalId == Id && operation.OriginalExpectedRevision is null && created.ProgramId == header.ProgramId &&
                created.PolicyId == Id && created.Identifier == header.Identifier && created.Content == header.Content && created.ContentSha256 == header.Content.Sha256 &&
                header == new PolicyAuthorityHeader(TenantId, header.ProgramId, Id, header.Identifier, 1, true, true, false, true,
                    null, null, null, null, null, null, null, null, null, false, header.Content),
                "Policy creation requires its deterministic identity and exact first draft authority.");
            return;
        }
        var before = _header;
        Require(before.IsVisible && !before.IsRetired && header.ProgramId == before.ProgramId && header.Identifier == before.Identifier,
            "Terminal Policy authority and identity cannot be rewritten.");
        var expectedGate = operation.CommandKind is nameof(ConfirmPolicyReview) or nameof(ProposePolicySuccessor) or nameof(ProposePolicyRetirement)
            ? before.CurrentVersion : before.Revision;
        Require(expectedGate is not null && operation.OriginalExpectedRevision == unchecked((ulong)expectedGate.Value),
            "The native Policy operation lost its original revision or current-version gate.");
        var expected = before;
        switch (value)
        {
            case PolicyDraftRevised revised:
                var successor = operation.CommandKind == nameof(ProposePolicySuccessor);
                Require((successor ? before.CurrentVersion is not null && !before.HasOpenDraft && before.PendingRetirementVersion is null &&
                    revised.PredecessorVersion == before.CurrentVersion : operation.CommandKind == nameof(RevisePolicyDraft) && before.HasOpenDraft &&
                    revised.PredecessorVersion is null) && revised.Revision == before.Revision + 1 && revised.Content == header.Content && revised.ContentSha256 == header.Content.Sha256,
                    "Policy revision requires the exact open line or approved predecessor.");
                expected = before with
                {
                    Revision = revised.Revision,
                    HasOpenDraft = true,
                    Content = header.Content,
                    DraftPredecessorVersion = successor ? revised.PredecessorVersion : before.DraftPredecessorVersion,
                    AcceptedReviewDecisionId = null,
                    AcceptedReviewerMemberId = null,
                    LatestReviewOutcome = null
                };
                break;
            case PolicyReviewed review:
                Require(operation.CommandKind == nameof(ReviewPolicyDraft) && operation.LogicalId == review.DecisionId &&
                    review.Revision == before.Revision && (before.HasOpenDraft || before.PendingRetirementVersion is not null) &&
                    review.Outcome is "accept" or "request_changes" && Rationale(review.Rationale),
                    "Policy review must answer the exact pending revision.");
                expected = before with
                {
                    EverReviewed = true,
                    LatestReviewOutcome = review.Outcome,
                    AcceptedReviewDecisionId = review.Outcome == "accept" ? review.DecisionId : null,
                    AcceptedReviewerMemberId = review.Outcome == "accept" ? actor : null,
                    PendingRetirementVersion = review.Outcome != "accept" && !before.HasOpenDraft ? null : before.PendingRetirementVersion,
                    PendingRetirementEffectiveUntil = review.Outcome != "accept" && !before.HasOpenDraft ? null : before.PendingRetirementEffectiveUntil
                };
                break;
            case PolicyApproved approved:
                Require(operation.CommandKind == nameof(ApprovePolicy) && operation.LogicalId == approved.DecisionId &&
                    before.HasOpenDraft && approved.Revision == before.Revision && approved.Version == (before.CurrentVersion ?? 0) + 1 &&
                    before.AcceptedReviewDecisionId is not null && approved.AcceptedReviewDecisionId == before.AcceptedReviewDecisionId &&
                    approved.PredecessorVersion == before.DraftPredecessorVersion && approved.ContentSha256 == before.Content.Sha256 &&
                    approved.EffectiveFrom != default && Rationale(approved.Rationale) &&
                    (before.CurrentVersion is null ? approved.Major : !string.IsNullOrWhiteSpace(approved.ImpactDigest) &&
                        approved.EffectiveFrom > before.CurrentVersionEffectiveFrom), "Policy approval must retain the exact reviewed body and predecessor.");
                expected = before with
                {
                    HasOpenDraft = false,
                    DraftPredecessorVersion = null,
                    AcceptedReviewDecisionId = null,
                    AcceptedReviewerMemberId = null,
                    LatestReviewOutcome = null,
                    CurrentVersion = approved.Version,
                    CurrentVersionEffectiveFrom = approved.EffectiveFrom,
                    LastReviewedOn = DateOnly.FromDateTime(approved.DecidedAt.UtcDateTime)
                };
                break;
            case PolicyPeriodicReviewConfirmed confirmed:
                Require(operation.CommandKind == nameof(ConfirmPolicyReview) && operation.LogicalId == confirmed.DecisionId &&
                    confirmed.Version == before.CurrentVersion && Rationale(confirmed.Rationale) &&
                    confirmed.ReviewedOn == DateOnly.FromDateTime(confirmed.DecidedAt.UtcDateTime),
                    "Periodic review must confirm the exact approved version and original date.");
                expected = before with { LastReviewedOn = confirmed.ReviewedOn };
                break;
            case PolicyRetirementProposed retirement:
                Require(operation.CommandKind == nameof(ProposePolicyRetirement) && before.CurrentVersion is not null &&
                    !before.HasOpenDraft && before.PendingRetirementVersion is null && retirement.Version == before.CurrentVersion &&
                    retirement.Revision == before.Revision + 1 && retirement.EffectiveUntil > before.CurrentVersionEffectiveFrom && Rationale(retirement.Rationale),
                    "Policy retirement must target the exact current version and later exclusive end.");
                expected = before with
                {
                    Revision = retirement.Revision,
                    PendingRetirementVersion = retirement.Version,
                    PendingRetirementEffectiveUntil = retirement.EffectiveUntil,
                    AcceptedReviewDecisionId = null,
                    AcceptedReviewerMemberId = null,
                    LatestReviewOutcome = null
                };
                break;
            case PolicyRetired retired:
                Require(operation.CommandKind == nameof(ApprovePolicyRetirement) && operation.LogicalId == retired.DecisionId &&
                    retired.Revision == before.Revision && before.PendingRetirementVersion == retired.Version &&
                    before.PendingRetirementEffectiveUntil == retired.EffectiveUntil && before.AcceptedReviewDecisionId is not null &&
                    retired.AcceptedReviewDecisionId == before.AcceptedReviewDecisionId && Rationale(retired.Rationale),
                    "Policy retirement approval must answer the exact latest reviewed proposal.");
                expected = before with
                {
                    IsRetired = true,
                    PendingRetirementVersion = null,
                    PendingRetirementEffectiveUntil = null,
                    AcceptedReviewDecisionId = null,
                    AcceptedReviewerMemberId = null
                };
                break;
            case PolicyDraftDiscarded discarded:
                Require(operation.CommandKind == nameof(DiscardPolicyDraft) && before.HasOpenDraft && before.CurrentVersion is null &&
                    !before.EverReviewed && discarded.Revision == before.Revision && Rationale(discarded.Rationale),
                    "Discard requires an unused, unreviewed initial Policy draft.");
                expected = before with { IsVisible = false, HasOpenDraft = false };
                break;
            default:
                throw new InvalidOperationException("The Policy mutation has another selected business kind.");
        }
        Require(header == expected, "The compact Policy header differs from its exact business transition.");
    }

    PolicyAuthorityContent ExpectedAuthority(PolicyMutationContent mutation, PolicyAuthorityHeader header,
        PolicyAuthorityContent? before, PolicyContent? body)
    {
        var result = before is null ? new PolicyAuthorityContent(header, [], 0, false) : before with { Header = header };
        var authors = result.PendingAuthorMemberIds;
        authors = mutation.Change switch
        {
            PolicyDraftCreatedContent created => [created.ActorMemberId],
            PolicyDraftRevisedContent revised => _header!.HasOpenDraft ? authors.Append(revised.ActorMemberId).Distinct().ToArray() : [revised.ActorMemberId],
            PolicyRetirementProposedContent proposed => [proposed.ActorMemberId],
            PolicyApprovedContent or PolicyRetiredContent => [],
            _ => authors,
        };
        result = result with { PendingAuthorMemberIds = authors.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray() };
        return body is null ? result : result with
        {
            CurrentApplicabilityCount = body.Applicability?.Count ?? 0,
            HasAccountableOwner = !string.IsNullOrWhiteSpace(body.OwnerReference)
        };
    }

    void ValidateDecision(PolicyMutationContent mutation, PolicyAuthorityContent? before, DateTimeOffset at)
    {
        (Uuid Actor, string Action, Uuid? Waiver, bool Approval) decision = mutation.Change switch
        {
            PolicyReviewedContent review => (review.ActorMemberId, SeparationOfDutiesActions.Review,
                review.SeparationOfDutiesWaiverId, false),
            PolicyApprovedContent approval => (approval.ActorMemberId, SeparationOfDutiesActions.Approve, approval.SeparationOfDutiesWaiverId, true),
            PolicyRetiredContent retired => (retired.ActorMemberId, SeparationOfDutiesActions.Approve, retired.SeparationOfDutiesWaiverId, true),
            _ => (Uuid.Empty, string.Empty, (Uuid?)null, false),
        };
        if (decision.Actor == Uuid.Empty)
        {
            Require(mutation.Waiver is null, "A Policy waiver can authorize only a selected personal decision.");
            return;
        }
        var conflict = before!.PendingAuthorMemberIds.Contains(decision.Actor) || decision.Approval &&
            before.Header.AcceptedReviewerMemberId == decision.Actor;
        Require(conflict ? mutation.Waiver is { } proof && proof.WaiverId == decision.Waiver &&
            PolicyWaiverProof.Allows(proof, TenantId, new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.Policy,
                Id, Id, before.Header.Revision, decision.Action), decision.Actor, at) : mutation.Waiver is null && decision.Waiver is null,
            "Policy independence requires the exact immutable waiver proof only for an actual current conflict.");
    }

    static bool Reference(ImmutableContentRef? reference, string schema) => reference is not null && reference.ContentId != Uuid.Empty &&
        reference.Schema == schema && reference.MediaType == ImmutableContentStoreExtensions.JsonMediaType && reference.Bytes >= 0 && Hex(reference.Sha256);
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
