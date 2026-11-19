using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;

/// <summary>Owns one methodology draft's revisions, independent review and terminal approval or discard.</summary>
public sealed class MethodologyTemplateDraft : Aggregate
{
    public const string Area = "methodology-template-drafts";
    public const string EntriesSchema = "methodology-template-draft-entries.v1";
    readonly Uuid _templateId;
    readonly Uuid _draftId;
    readonly HashSet<Uuid> _authors = [];
    Uuid _startEventId;
    long _basedOnVersion;
    string[] _openingEntryKeys = [];
    ImmutableContentRef? _openingContent;
    Uuid _lastAuthor;
    Uuid _approvedBy;
    Uuid _discardedBy;
    string? _discardReason;
    (long Revision, Uuid Reviewer, bool Accept, string Comments)? _lastReview;

    public MethodologyTemplateDraft(Uuid templateId, Uuid draftId)
        : base(Identity(templateId, draftId),
            new EventStreamAddress(MethodologyTemplate.Realm, Area, Identity(templateId, draftId).ToString()))
    {
        (_templateId, _draftId) = (templateId, draftId);
        On<MethodologyTemplateDraftOpened>(Apply);
        On<MethodologyTemplateDraftRevisedV4>(Apply);
        On<MethodologyTemplateDraftAccepted>(Apply);
        On<MethodologyTemplateDraftReturned>(Apply);
        On<MethodologyTemplateDraftApproved>(Apply);
        On<MethodologyTemplateDraftDiscarded>(Apply);
    }

    public long Revision { get; private set; }
    public string Status { get; private set; } = "unopened";
    public ImmutableContentRef? EntriesContent { get; private set; }
    public Uuid? AcceptedReviewer { get; private set; }
    public IReadOnlyCollection<Uuid> RevisionAuthors => Array.AsReadOnly(_authors.ToArray());
    bool IsTerminal => Status is "approved" or "discarded";

    public static Uuid Identity(Uuid templateId, Uuid draftId)
    {
        if (templateId == Uuid.Empty || draftId == Uuid.Empty)
            throw new ArgumentException("A methodology draft requires its exact template and draft identities.");
        return Uuid.CreateVersion5(templateId, $"methodology-template-draft:{draftId}");
    }

    public Result Open(Uuid startEventId, long basedOnVersion, IReadOnlyList<MethodologyTemplateRecordContent> entries,
        ImmutableContentRef content)
    {
        if (startEventId == Uuid.Empty || basedOnVersion < 0 || !ValidManifest(entries, content) ||
            basedOnVersion == 0 && entries.Count != 0)
            return Invalid("Opening requires the line's committed start and its exact unique entry snapshot.");
        if (_startEventId != Uuid.Empty)
            return _startEventId == startEventId && _basedOnVersion == basedOnVersion &&
                   _openingContent == content && _openingEntryKeys.SequenceEqual(entries.Select(entry => entry.EntryKey))
                ? Result.Success : Conflict("This draft is bound to another opening source or snapshot.");
        RaiseEvent(new MethodologyTemplateDraftOpened(_templateId, _draftId, startEventId, basedOnVersion,
            content, entries.Select(entry => entry.EntryKey).ToArray()));
        return Result.Success;
    }

    public Result Revise(long expectedRevision, IReadOnlyList<MethodologyTemplateRecordContent> entries,
        ImmutableContentRef content, Uuid author)
    {
        if (Status == "unopened")
            return Missing();
        if (IsTerminal)
            return Conflict("A terminal methodology draft accepts no revisions.");
        if (author == Uuid.Empty || !ValidManifest(entries, content))
            return Invalid("A revision requires a personal author and unique original methodology entries.");
        if (expectedRevision == Revision - 1 && _lastAuthor == author && EntriesContent == content)
            return Result.Success;
        if (expectedRevision != Revision || Revision == long.MaxValue)
            return Conflict("The draft revision changed before this revision was prepared.");
        RaiseEvent(new MethodologyTemplateDraftRevisedV4(_templateId, _draftId, Revision + 1, content,
            entries.Select(entry => entry.EntryKey).ToArray(), author));
        return Result.Success;
    }

    public Result Review(long revision, Uuid reviewer, bool accept, string comments)
    {
        if (Status == "unopened")
            return Missing();
        if (IsTerminal)
            return Conflict("A terminal methodology draft accepts no review.");
        if (reviewer == Uuid.Empty || !Text(comments, 2000))
            return Invalid("A draft review requires a personal reviewer and comments.");
        if (_authors.Contains(reviewer))
            return Forbidden("A reviewer must have authored no revision of this draft.");
        comments = comments.Trim();
        if (_lastReview is { } prior && prior.Revision == revision && prior.Reviewer == reviewer &&
            prior.Accept == accept && prior.Comments == comments)
            return Result.Success;
        if (revision != Revision || Status != "drafting")
            return Conflict("Review must answer the current drafting revision.");
        if (accept)
            RaiseEvent(new MethodologyTemplateDraftAccepted(_templateId, _draftId, revision, reviewer, comments));
        else
            RaiseEvent(new MethodologyTemplateDraftReturned(_templateId, _draftId, revision, reviewer, comments));
        return Result.Success;
    }

    public Result Approve(long revision, Uuid approver)
    {
        if (Status == "approved" && revision == Revision && approver == _approvedBy)
            return Result.Success;
        if (Status == "unopened")
            return Missing();
        if (Status != "reviewed" || revision != Revision || AcceptedReviewer is null)
            return Conflict("Approval requires independent acceptance of the current draft revision.");
        if (approver == Uuid.Empty)
            return Invalid("Approval requires a personal approver.");
        if (_authors.Contains(approver) || approver == AcceptedReviewer)
            return Forbidden("The approver must be neither a revision author nor the accepting reviewer.");
        RaiseEvent(new MethodologyTemplateDraftApproved(_templateId, _draftId, revision, EntriesContent!, approver));
        return Result.Success;
    }

    public Result Discard(Uuid discarder, string reason)
    {
        if (discarder == Uuid.Empty || !Text(reason, 2000))
            return Invalid("Discarding requires a personal actor and reason.");
        reason = reason.Trim();
        if (Status == "discarded" && _discardedBy == discarder && _discardReason == reason)
            return Result.Success;
        if (Status == "unopened")
            return Missing();
        if (IsTerminal)
            return Conflict("A terminal methodology draft accepts no further discard decision.");
        RaiseEvent(new MethodologyTemplateDraftDiscarded(_templateId, _draftId, Revision, discarder, reason));
        return Result.Success;
    }

    void Apply(MethodologyTemplateDraftOpened fact)
    {
        Scope(fact.TemplateId, fact.DraftId);
        if (Status != "unopened" || fact.StartEventId == Uuid.Empty || fact.BasedOnVersion < 0 ||
            !ValidReference(fact.EntriesContent) || !ValidKeys(fact.EntryKeys) ||
            fact.BasedOnVersion == 0 && fact.EntryKeys.Count != 0)
            throw new InvalidOperationException("A draft opens once from an exact line start and immutable entries.");
        _startEventId = fact.StartEventId;
        _basedOnVersion = fact.BasedOnVersion;
        _openingEntryKeys = fact.EntryKeys.ToArray();
        _openingContent = fact.EntriesContent;
        EntriesContent = fact.EntriesContent;
        Status = "drafting";
    }

    void Apply(MethodologyTemplateDraftRevisedV4 fact)
    {
        Scope(fact.TemplateId, fact.DraftId);
        if (Status is not ("drafting" or "reviewed") || fact.Revision != Revision + 1 ||
            fact.RevisedBy == Uuid.Empty || !ValidReference(fact.EntriesContent) || !ValidKeys(fact.EntryKeys))
            throw new InvalidOperationException("A revision must advance this active draft and identify its author.");
        Revision = fact.Revision;
        EntriesContent = fact.EntriesContent;
        _lastAuthor = fact.RevisedBy;
        _authors.Add(fact.RevisedBy);
        AcceptedReviewer = null;
        _lastReview = null;
        Status = "drafting";
    }

    void Apply(MethodologyTemplateDraftAccepted fact) => ApplyReview(fact.TemplateId, fact.DraftId,
        fact.Revision, fact.ReviewedBy, true, fact.Comments);

    void Apply(MethodologyTemplateDraftReturned fact) => ApplyReview(fact.TemplateId, fact.DraftId,
        fact.Revision, fact.ReviewedBy, false, fact.Comments);

    void ApplyReview(Uuid template, Uuid draft, long revision, Uuid reviewer, bool accept, string comments)
    {
        Scope(template, draft);
        if (Status != "drafting" || revision != Revision || reviewer == Uuid.Empty ||
            _authors.Contains(reviewer) || !Text(comments, 2000))
            throw new InvalidOperationException("Review requires the exact current revision and a reviewer who authored none.");
        _lastReview = (revision, reviewer, accept, comments);
        AcceptedReviewer = accept ? reviewer : null;
        Status = accept ? "reviewed" : "drafting";
    }

    void Apply(MethodologyTemplateDraftApproved fact)
    {
        Scope(fact.TemplateId, fact.DraftId);
        if (Status != "reviewed" || fact.Revision != Revision || fact.ApprovedBy == Uuid.Empty ||
            fact.ApprovedBy == AcceptedReviewer || _authors.Contains(fact.ApprovedBy) ||
            fact.EntriesContent != EntriesContent)
            throw new InvalidOperationException("Approval must preserve the accepted revision and independent third approver.");
        _approvedBy = fact.ApprovedBy;
        Status = "approved";
    }

    void Apply(MethodologyTemplateDraftDiscarded fact)
    {
        Scope(fact.TemplateId, fact.DraftId);
        if (Status is not ("drafting" or "reviewed") || fact.Revision != Revision ||
            fact.DiscardedBy == Uuid.Empty || !Text(fact.Reason, 2000))
            throw new InvalidOperationException("Only an active draft can be discarded with an actor and reason.");
        _discardedBy = fact.DiscardedBy;
        _discardReason = fact.Reason;
        Status = "discarded";
    }

    void Scope(Uuid template, Uuid draft)
    {
        if (template != _templateId || draft != _draftId)
            throw new InvalidOperationException("The draft fact belongs to another template or draft identity.");
    }

    internal static bool ValidManifest(IReadOnlyList<MethodologyTemplateRecordContent>? entries, ImmutableContentRef? content)
    {
        if (!ValidEntries(entries) || content is null || !ValidReference(content))
            return false;
        var info = ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(IReadOnlyList<MethodologyTemplateRecordContent>))
            ?? throw new InvalidOperationException("Methodology entry serialization metadata is missing.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(entries, info);
        return content.Bytes == bytes.LongLength && content.Sha256 == ContentDigest.Sha256(bytes);
    }

    internal static bool ValidEntries(IReadOnlyList<MethodologyTemplateRecordContent>? entries) =>
        entries is { Count: <= 100 } && entries.All(entry => entry is not null && Key(entry.EntryKey) &&
            (entry.RecordKind is "control" or "policy" or "risk") && Text(entry.Title, 200) &&
            Text(entry.Summary, 2000) && entry.ContributionApprovalId is null &&
            MethodologyTemplateDestinationCommands.ValidatePayload(entry, requirePayload: false) is null &&
            entry.CriteriaReferences is { Count: <= 50 } references && references.All(reference =>
                reference is not null && reference.EditionId != Uuid.Empty && Key(reference.Identifier))) &&
        entries.Select(entry => entry.EntryKey).Distinct(StringComparer.Ordinal).Count() == entries.Count;

    internal static bool ValidReference(ImmutableContentRef? content) => content is not null && content.ContentId != Uuid.Empty &&
        content.Schema == EntriesSchema && content.MediaType == "application/json" && content.Bytes > 0 &&
        content.Sha256 is { Length: 64 } hash && hash.All(Uri.IsHexDigit);
    static bool ValidKeys(IReadOnlyList<string>? keys) => keys is { Count: <= 100 } &&
        keys.All(Key) && keys.Distinct(StringComparer.Ordinal).Count() == keys.Count;
    static bool Key(string? value) => value is { Length: > 0 and <= 100 } && value == value.Trim() &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
    static bool Text(string? value, int maximum) => value is not null && value.Length <= maximum && !string.IsNullOrWhiteSpace(value);
    Result Missing() => Result.Failure(new RequestError(RequestErrorKind.NotFound, $"Methodology draft {Id} is not open yet.", true));
    static Result Invalid(string message) => Result.Failure(new RequestError(RequestErrorKind.Validation, message));
    static Result Conflict(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
    static Result Forbidden(string message) => Result.Failure(new RequestError(RequestErrorKind.Forbidden, message));
}
