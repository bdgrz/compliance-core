using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;

/// <summary>Compact authority for one proposal, its independent review and its immutable platform acceptance.</summary>
public sealed class MethodologyTemplateContribution : Aggregate
{
    readonly Uuid _tenantId;
    Uuid _proposerUserId;
    Uuid? _reviewerUserId;
    string? _status;
    long _revision;

    public MethodologyTemplateContribution(Uuid tenantId, Uuid contributionId, Uuid selectedLogicalId = default)
        : base(contributionId, new EventStreamAddress(tenantId.ToString(), "methodology-template-contributions",
            contributionId.ToString()))
    {
        if (tenantId == Uuid.Empty || contributionId == Uuid.Empty)
            throw new ArgumentException("A contribution needs its tenant and immutable identity.");
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<MethodologyTemplateContributionProposed>(Apply);
        On<MethodologyTemplateContributionReviewed>(Apply);
        On<MethodologyTemplateContributionAccepted>(Apply);
    }

    public NativeAcceptance Acceptance { get; }
    internal Uuid ProposerUserId => _proposerUserId;
    internal Uuid? ReviewerUserId => _reviewerUserId;
    internal Uuid LastActingUserId { get; private set; }
    public long Revision => _revision;
    public string? Status => _status;
    public ImmutableContentRef? Content { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public Result PreparePropose() => _status is null ? Result.Success
        : Conflict("The contribution identity already has a proposal.");

    public Result PrepareReview(Uuid userId) => _status is null
        ? Missing() : _status != "proposed"
        ? Conflict("Only a proposed contribution can receive its immutable review.")
        : userId == _proposerUserId ? Forbidden("A contribution author cannot review their own de-identification.")
        : Result.Success;

    public Result PrepareAccept(Uuid userId) => _status is null
        ? Missing() : _status != "reviewed"
        ? Conflict("Only an independently approved contribution can be accepted.")
        : userId == _proposerUserId || userId == _reviewerUserId
            ? Forbidden("The contributor and reviewer cannot accept the contribution.") : Result.Success;

    public Result Propose(OperationProvenance operation, Uuid memberId, Uuid userId, ImmutableContentRef content,
        ImmutableContentRef response, ImmutableContentRef result)
    {
        var guard = PreparePropose();
        if (!guard.IsSuccess)
            return guard;
        RaiseEvent(new MethodologyTemplateContributionProposed(_tenantId, Id, 1, memberId, userId, content,
            operation.AcceptedAt, operation, response, result));
        return Result.Success;
    }

    public Result Review(OperationProvenance operation, Uuid memberId, Uuid userId, string outcome,
        ImmutableContentRef content, ImmutableContentRef response, ImmutableContentRef result)
    {
        var guard = PrepareReview(userId);
        if (!guard.IsSuccess)
            return guard;
        if (outcome is not ("approved" or "declined"))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A contribution review needs an explicit outcome."));
        RaiseEvent(new MethodologyTemplateContributionReviewed(_tenantId, Id, _revision + 1, memberId, userId,
            content, operation.AcceptedAt, operation, response, result, outcome));
        return Result.Success;
    }

    public Result Accept(OperationProvenance operation, Uuid memberId, Uuid userId, ImmutableContentRef content,
        ImmutableContentRef response, ImmutableContentRef result)
    {
        var guard = PrepareAccept(userId);
        if (!guard.IsSuccess)
            return guard;
        RaiseEvent(new MethodologyTemplateContributionAccepted(_tenantId, Id, _revision + 1, memberId, userId,
            content, operation.AcceptedAt, operation, response, result));
        return Result.Success;
    }

    void Apply(MethodologyTemplateContributionProposed fact)
    {
        Require(fact, fact.TenantId, fact.SubjectId, fact.Revision, fact.ActorMemberId, fact.ActingUserId, fact.Content, fact.OccurredAt, nameof(ProposeDeidentifiedTemplateContribution), false);
        if (_status is not null || fact.Operation.LogicalId != Id || fact.Revision != 1)
            throw new InvalidOperationException("A contribution proposal is accepted exactly once at its immutable identity.");
        _proposerUserId = fact.ActingUserId;
        Set(fact, "proposed", fact.Revision, fact.Content, fact.OccurredAt, fact.ActingUserId);
    }

    void Apply(MethodologyTemplateContributionReviewed fact)
    {
        Require(fact, fact.TenantId, fact.SubjectId, fact.Revision, fact.ActorMemberId, fact.ActingUserId, fact.Content, fact.OccurredAt, nameof(ReviewTemplateContribution), true);
        if (!PrepareReview(fact.ActingUserId).IsSuccess || fact.ReviewOutcome is not ("approved" or "declined"))
            throw new InvalidOperationException("A contribution review must be independent and answer the pending proposal.");
        _reviewerUserId = fact.ActingUserId;
        Set(fact, fact.ReviewOutcome == "approved" ? "reviewed" : "declined", fact.Revision, fact.Content, fact.OccurredAt, fact.ActingUserId);
    }

    void Apply(MethodologyTemplateContributionAccepted fact)
    {
        Require(fact, fact.TenantId, fact.SubjectId, fact.Revision, fact.ActorMemberId, fact.ActingUserId, fact.Content, fact.OccurredAt, nameof(AcceptTemplateContribution), true);
        if (!PrepareAccept(fact.ActingUserId).IsSuccess)
            throw new InvalidOperationException("Contribution acceptance requires the independently approved proposal.");
        Set(fact, "accepted", fact.Revision, fact.Content, fact.OccurredAt, fact.ActingUserId);
    }

    void Require<T>(T fact, Uuid tenant, Uuid id, long revision, Uuid memberId, Uuid userId, ImmutableContentRef content,
        DateTimeOffset at, string kind, bool platform) where T : DomainEvent, INativeOperationFact
    {
        OwnerProvenance.RequireOwn(this, fact.Operation.LogicalId, fact.Operation);
        if (tenant != _tenantId || id != Id || revision != _revision + 1 || userId == Uuid.Empty ||
            memberId != RbacIds.Member(platform ? Uuid.Empty : _tenantId, userId) || content is null ||
            at == default || at < UpdatedAt || at != fact.Operation.AcceptedAt || fact.FirstResponse is null ||
            fact.Operation.RootOperationId != fact.Operation.LogicalId || fact.Operation.CausationEventId is not null ||
            fact.Operation.CommandKind != kind || fact.Operation.Episode != Id.ToString() ||
            fact.Operation.AffectedOwner != Stream || fact.Operation.ExpectedRevisionOwner != Stream ||
            fact.Operation.OriginalExpectedRevision is not null ||
            fact.Operation.Actor.Kind != (platform ? "platform_operator" : "member") ||
            fact.Operation.Actor.Id != (platform ? userId : RbacIds.Member(_tenantId, userId)).ToString())
            throw new InvalidOperationException("A contribution fact must advance its exact owner and native root operation.");
    }

    void Set<T>(T fact, string status, long revision, ImmutableContentRef content, DateTimeOffset at, Uuid userId)
        where T : DomainEvent, INativeOperationFact
    {
        Acceptance.Observe(fact);
        LastActingUserId = userId;
        (_status, _revision, Content, UpdatedAt) = (status, revision, content, at);
    }

    static Result Missing() => Result.Failure(new RequestError(RequestErrorKind.NotFound, "The template contribution was not found."));
    static Result Conflict(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
    static Result Forbidden(string message) => Result.Failure(new RequestError(RequestErrorKind.Forbidden, message));
}
