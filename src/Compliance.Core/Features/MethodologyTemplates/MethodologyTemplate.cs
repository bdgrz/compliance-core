using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>The compact current version/publication authority of a platform template; history is not command state.</summary>
public sealed class MethodologyTemplate : Aggregate
{
    public const string Realm = "bdgrz";
    public const string Area = "methodology-templates";
    long _revision;
    bool _retired;
    bool _hasPublished;

    public MethodologyTemplate(Uuid templateId, Uuid selectedLogicalId = default)
        : base(templateId, new EventStreamAddress(Realm, Area, templateId.ToString()))
    {
        if (templateId == Uuid.Empty)
            throw new ArgumentException("A template requires its immutable identity.");
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<MethodologyTemplateCreated>(ApplyFact);
        On<MethodologyTemplateDraftRevised>(ApplyFact);
        On<MethodologyTemplateDraftReviewed>(ApplyFact);
        On<MethodologyTemplateVersionPublished>(ApplyFact);
        On<MethodologyTemplateRetired>(ApplyFact);
    }

    public NativeAcceptance Acceptance { get; }
    public long Revision => _revision;
    public long VersionNumber { get; private set; }
    public Uuid VersionId { get; private set; }
    public string? VersionStatus { get; private set; }
    public Uuid AuthorMemberId { get; private set; }
    public Uuid? ReviewerMemberId { get; private set; }
    public string? ContentSha256 { get; private set; }
    public ImmutableContentRef? CurrentContent { get; private set; }
    public ImmutableContentRef? CurrentVersionContent { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public bool IsRetired => _retired;
    public long PublishedVersionNumber { get; private set; }
    public Uuid PublishedVersionId { get; private set; }

    public Result PrepareCreate() => _revision == 0 ? Result.Success : Conflict("The template identity already exists.");
    public Result PrepareRevise(long expectedRevision) => Target(expectedRevision) is { IsSuccess: false } failure
        ? failure : _retired || VersionStatus == "reviewed"
            ? Conflict("Only an active draft, returned draft or published template can be revised.") : Result.Success;
    public Result PrepareReview(long version, Uuid memberId) => _revision == 0 ? Missing()
        : _retired || version != VersionNumber || VersionStatus != "draft"
            ? Conflict("Review must answer the current active draft.")
            : memberId == AuthorMemberId ? Forbidden("A template author cannot review their own draft.") : Result.Success;
    public Result PreparePublish(long version, Uuid memberId) => _revision == 0 ? Missing()
        : _retired || version != VersionNumber || VersionStatus != "reviewed"
            ? Conflict("Publication requires the independently reviewed current draft.")
            : memberId == AuthorMemberId || memberId == ReviewerMemberId
                ? Forbidden("The author and reviewer cannot approve this template version.") : Result.Success;
    public Result PrepareRetire(long expectedRevision) => Target(expectedRevision) is { IsSuccess: false } failure
        ? failure : _retired || !_hasPublished ? Conflict("Retirement requires an active published template.") : Result.Success;

    public Result Append(string kind, long preparedRevision, OperationProvenance operation, Uuid memberId,
        MethodologyTemplateVersionView version, ImmutableContentRef change, ImmutableContentRef first,
        ImmutableContentRef result)
    {
        if (_revision != preparedRevision)
            return Conflict("The template changed while its immutable content was prepared.");
        var guard = kind switch
        {
            nameof(CreateMethodologyTemplate) => PrepareCreate(),
            nameof(ReviseMethodologyTemplateDraft) => PrepareRevise(preparedRevision),
            nameof(ReviewMethodologyTemplate) => PrepareReview(version.VersionNumber, memberId),
            nameof(ApproveMethodologyTemplateVersion) => PreparePublish(version.VersionNumber, memberId),
            nameof(RetireMethodologyTemplate) => PrepareRetire(preparedRevision),
            _ => Conflict("The template command kind is unsupported."),
        };
        if (!guard.IsSuccess)
            return guard;
        DomainEvent fact = kind switch
        {
            nameof(CreateMethodologyTemplate) => new MethodologyTemplateCreated(Uuid.Empty, Id, _revision + 1,
                memberId, version.VersionNumber, version.VersionId, version.Status, version.AuthoredByMemberId,
                version.Review?.ReviewedByMemberId, version.ContentSha256, change, operation.AcceptedAt, operation, first, result),
            nameof(ReviseMethodologyTemplateDraft) => new MethodologyTemplateDraftRevised(Uuid.Empty, Id, _revision + 1,
                memberId, version.VersionNumber, version.VersionId, version.Status, version.AuthoredByMemberId,
                version.Review?.ReviewedByMemberId, version.ContentSha256, change, operation.AcceptedAt, operation, first, result),
            nameof(ReviewMethodologyTemplate) => new MethodologyTemplateDraftReviewed(Uuid.Empty, Id, _revision + 1,
                memberId, version.VersionNumber, version.VersionId, version.Status, version.AuthoredByMemberId,
                version.Review?.ReviewedByMemberId, version.ContentSha256, change, operation.AcceptedAt, operation, first, result),
            nameof(ApproveMethodologyTemplateVersion) => new MethodologyTemplateVersionPublished(Uuid.Empty, Id, _revision + 1,
                memberId, version.VersionNumber, version.VersionId, version.Status, version.AuthoredByMemberId,
                version.Review?.ReviewedByMemberId, version.ContentSha256, change, operation.AcceptedAt, operation, first, result),
            _ => new MethodologyTemplateRetired(Uuid.Empty, Id, _revision + 1,
                memberId, version.VersionNumber, version.VersionId, version.Status, version.AuthoredByMemberId,
                version.Review?.ReviewedByMemberId, version.ContentSha256, change, operation.AcceptedAt, operation, first, result),
        };
        RaiseEvent(fact);
        return Result.Success;
    }

    void ApplyFact(DomainEvent value)
    {
        var fact = (IMethodologyTemplateFact)value;
        OwnerProvenance.RequireOwn(this, fact.Operation.LogicalId, fact.Operation);
        if (fact.SubjectId != Id || fact.TenantId != Uuid.Empty || fact.Revision != _revision + 1 ||
            fact.VersionId == Uuid.Empty || fact.Change is null || fact.OccurredAt == default || fact.OccurredAt < UpdatedAt ||
            fact.Operation.AcceptedAt != fact.OccurredAt || fact.Operation.Actor.Kind != "platform_operator" ||
            fact.FirstResponse is null || fact.OperationResult is null ||
            !Uuid.TryParse(fact.Operation.Actor.Id, out var userId) || userId == Uuid.Empty ||
            fact.ActorMemberId != RbacIds.Member(Uuid.Empty, userId) ||
            fact.Operation.Episode != Id.ToString() || fact.Operation.AffectedOwner != Stream ||
            fact.Operation.ExpectedRevisionOwner != Stream || fact.Operation.CausationEventId is not null ||
            fact.Operation.RootOperationId != fact.Operation.LogicalId)
            throw new InvalidOperationException("The template fact has an invalid exact owner or native binding.");
        Result guard;
        switch (value)
        {
            case MethodologyTemplateCreated created:
                guard = PrepareCreate();
                if (fact.VersionNumber != 1 || fact.VersionStatus != "draft" || created.ActorMemberId != fact.AuthorMemberId ||
                    fact.Operation.LogicalId != Id || fact.Operation.OriginalExpectedRevision is not null ||
                    fact.Operation.CommandKind != nameof(CreateMethodologyTemplate))
                    throw new InvalidOperationException("A template starts with its exact first authored version.");
                break;
            case MethodologyTemplateDraftRevised revised:
                guard = PrepareRevise(_revision);
                if (fact.VersionNumber != VersionNumber + 1 || fact.VersionStatus != "draft" ||
                    revised.ActorMemberId != fact.AuthorMemberId || fact.ReviewerMemberId is not null ||
                    fact.Operation.OriginalExpectedRevision != (ulong)_revision ||
                    fact.Operation.CommandKind != nameof(ReviseMethodologyTemplateDraft))
                    throw new InvalidOperationException("A revision creates one fresh version on the exact current authority.");
                break;
            case MethodologyTemplateDraftReviewed reviewed:
                guard = PrepareReview(fact.VersionNumber, reviewed.ActorMemberId);
                if (fact.VersionStatus is not ("reviewed" or "changes_requested") ||
                    fact.ReviewerMemberId != reviewed.ActorMemberId || fact.Operation.OriginalExpectedRevision is not null ||
                    fact.Operation.CommandKind != nameof(ReviewMethodologyTemplate))
                    throw new InvalidOperationException("A template review is an independent immutable current-version decision.");
                break;
            case MethodologyTemplateVersionPublished published:
                guard = PreparePublish(fact.VersionNumber, published.ActorMemberId);
                if (fact.VersionStatus != "published" || fact.Operation.OriginalExpectedRevision is not null ||
                    fact.Operation.CommandKind != nameof(ApproveMethodologyTemplateVersion))
                    throw new InvalidOperationException("A publication must answer the independently reviewed version.");
                _hasPublished = true;
                PublishedVersionNumber = fact.VersionNumber;
                PublishedVersionId = fact.VersionId;
                break;
            default:
                guard = PrepareRetire(_revision);
                if (fact.VersionNumber != VersionNumber || fact.Operation.OriginalExpectedRevision != (ulong)_revision ||
                    fact.Operation.CommandKind != nameof(RetireMethodologyTemplate))
                    throw new InvalidOperationException("Retirement must answer the current template revision.");
                _retired = true;
                break;
        }
        if (!guard.IsSuccess || value is not (MethodologyTemplateCreated or MethodologyTemplateDraftRevised) &&
            (fact.VersionId != VersionId || fact.AuthorMemberId != AuthorMemberId || fact.ContentSha256 != ContentSha256))
            throw new InvalidOperationException("The template fact violates its current authoritative version.");
        CurrentContent = fact.Change;
        if (value is not MethodologyTemplateRetired)
            CurrentVersionContent = fact.Change;
        Acceptance.Observe(value);
        (_revision, VersionNumber, VersionId, VersionStatus, AuthorMemberId, ReviewerMemberId, ContentSha256, UpdatedAt) =
            (fact.Revision, fact.VersionNumber, fact.VersionId, fact.VersionStatus, fact.AuthorMemberId,
                fact.ReviewerMemberId, fact.ContentSha256, fact.OccurredAt);
    }

    Result Target(long expectedRevision) => _revision == 0 ? Missing()
        : expectedRevision != _revision ? Conflict("The template revision is stale.") : Result.Success;
    static Result Missing() => Result.Failure(new RequestError(RequestErrorKind.NotFound, "The methodology template was not found."));
    static Result Conflict(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
    static Result Forbidden(string message) => Result.Failure(new RequestError(RequestErrorKind.Forbidden, message));
}
