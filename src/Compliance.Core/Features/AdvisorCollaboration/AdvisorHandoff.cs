using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>
/// Owns one handoff stream: selected exact record versions, access state, and feedback history.
/// Feedback stays in this stream so revocation and feedback admission share one consistency boundary.
/// </summary>
public sealed class AdvisorHandoff : Aggregate
{
    public const int MaximumSelectionCount = 100;

    static readonly HashSet<string> FeedbackKinds = new(StringComparer.Ordinal)
        { "comment", "change_request", "validated" };

    readonly Uuid _tenantId;
    Uuid _programId;
    readonly List<AdvisorRecordVersion> _selection = [];
    readonly List<AdvisorFeedbackEntry> _feedback = [];
    bool _created;
    string? _purpose;
    DateOnly _expiresOn;
    ActorReference? _createdBy;
    DateTimeOffset? _createdAt;
    DateTimeOffset? _updatedAt;
    Uuid? _comparedToHandoffId;
    string _status = "missing";
    long _revision;
    bool _wasReadyForReview;

    public bool IsCreated => _created;
    public Uuid ProgramId => _programId;
    public string Status => _status;
    public long Revision => _revision;
    public string? Purpose => _purpose;
    public DateOnly ExpiresOn => _expiresOn;
    public ActorReference? CreatedBy => _createdBy;
    public DateTimeOffset? CreatedAt => _createdAt;
    public DateTimeOffset? UpdatedAt => _updatedAt;
    public Uuid? ComparedToHandoffId => _comparedToHandoffId;
    public bool WasReadyForReview => _wasReadyForReview;
    public IReadOnlyList<AdvisorRecordVersion> Selection => _selection.ToArray();

    public AdvisorHandoff(Uuid tenantId, Uuid programId, Uuid handoffId)
        : base(handoffId, new EventStreamAddress(tenantId.ToString(), "advisor-handoffs",
            handoffId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        On<AdvisorHandoffCreated>(ev =>
        {
            if (ev.ComparedToHandoffId is { } comparedTo &&
                (comparedTo == Uuid.Empty || comparedTo == Id))
                throw new InvalidOperationException(
                    "An advisor handoff cannot compare its selection to itself or an empty handoff.");
            _created = true;
            _programId = ev.ProgramId;
            _revision = 1;
            _purpose = ev.Purpose;
            _expiresOn = ev.ExpiresOn;
            _createdBy = ev.CreatedBy;
            _createdAt = ev.CreatedAt;
            _updatedAt = ev.CreatedAt;
            _comparedToHandoffId = ev.ComparedToHandoffId;
            _selection.AddRange(ev.RecordVersions);
            _status = "draft";
        });
        On<AdvisorHandoffSelectionRevised>(ev =>
        {
            _revision = ev.Revision;
            _updatedAt = ev.RevisedAt;
            _selection.Clear();
            _selection.AddRange(ev.RecordVersions);
            _status = "draft";
        });
        On<AdvisorHandoffReadiedForReview>(ev =>
        {
            _revision = ev.Revision;
            _updatedAt = ev.ReadiedAt;
            _status = "ready_for_review";
            _wasReadyForReview = true;
        });
        On<AdvisorHandoffClosed>(ev =>
        {
            _revision = ev.Revision;
            _updatedAt = ev.ClosedAt;
            _status = "closed";
        });
        On<AdvisorHandoffRevoked>(ev =>
        {
            _revision = ev.Revision;
            _updatedAt = ev.RevokedAt;
            _status = "revoked";
        });
        On<AdvisorFeedbackRecorded>(ApplyFeedbackRecorded);
        On<AdvisorFeedbackCorrected>(ev =>
        {
            _revision++;
            _updatedAt = ev.CorrectedAt;
            var index = _feedback.FindIndex(item => item.FeedbackId == ev.FeedbackId);
            if (index >= 0)
                _feedback[index] = _feedback[index] with
                {
                    Corrections = [.. _feedback[index].Corrections ?? [],
                        new AdvisorFeedbackCorrection(ev.CorrectionId, ev.CorrectedBody,
                            ev.CorrectionReason, ev.CorrectedBy, ev.CorrectedAt)],
                };
        });
        On<AdvisorFeedbackResponded>(ev =>
        {
            _revision++;
            _updatedAt = ev.RespondedAt;
            var index = _feedback.FindIndex(item => item.FeedbackId == ev.FeedbackId);
            if (index >= 0)
                _feedback[index] = _feedback[index] with
                {
                    Responses = [.. _feedback[index].Responses ?? [],
                        new AdvisorFeedbackResponse(ev.Response, ev.RespondedBy, ev.RespondedAt)],
                };
        });
        On<AdvisorFeedbackResolved>(ev =>
        {
            RequireFeedbackScope(ev.TenantId, ev.ProgramId, ev.HandoffId);
            var index = _feedback.FindIndex(item => item.FeedbackId == ev.FeedbackId);
            if (ev.FeedbackId == Uuid.Empty || !ValidActor(ev.ResolvedBy) ||
                ev.ResolvedAt == default || index < 0 || _feedback[index].ResolvedAt is not null)
                throw new InvalidOperationException("The advisor feedback resolution event is invalid.");
            _revision++;
            _updatedAt = ev.ResolvedAt;
            _feedback[index] = _feedback[index] with
            {
                ResolvedAt = ev.ResolvedAt,
                ResolvedBy = ev.ResolvedBy,
            };
        });
    }

    public Result<bool> Create(string purpose, IReadOnlyList<AdvisorRecordVersion> recordVersions,
        DateOnly expiresOn, ActorReference createdBy, DateTimeOffset createdAt,
        Uuid? comparedToHandoffId = null)
    {
        if (_tenantId == Uuid.Empty || _programId == Uuid.Empty || Id == Uuid.Empty ||
            string.IsNullOrWhiteSpace(purpose) || !ValidSelection(recordVersions) ||
            !ValidActor(createdBy) || expiresOn < DateOnly.FromDateTime(createdAt.UtcDateTime) ||
            comparedToHandoffId is { } compareId &&
            (compareId == Uuid.Empty || compareId == Id))
            return Failure(RequestErrorKind.Validation, "A handoff requires purpose, exact selected versions, an actor, and a valid expiry.");
        if (_created)
            return _purpose == purpose.Trim() && _expiresOn == expiresOn &&
                   _selection.SequenceEqual(recordVersions) &&
                   _comparedToHandoffId == comparedToHandoffId
                ? Result<bool>.Success(true)
                : Failure(RequestErrorKind.Conflict, "The handoff already exists with different content.");

        RaiseEvent(new AdvisorHandoffCreated(_tenantId, _programId, Id, recordVersions.ToArray(),
            purpose.Trim(), expiresOn, createdBy, createdAt, comparedToHandoffId));
        return Result<bool>.Success(true);
    }

    public Result<bool> ReviseSelection(long expectedRevision,
        IReadOnlyList<AdvisorRecordVersion> recordVersions, string changeReason,
        ActorReference revisedBy, DateTimeOffset revisedAt)
    {
        if (!IsOpen())
            return Failure(RequestErrorKind.Conflict, "Only an open handoff can be revised.");
        if (expectedRevision != _revision)
            return Failure(RequestErrorKind.Conflict, "The handoff revision is stale.");
        if (!ValidSelection(recordVersions) || string.IsNullOrWhiteSpace(changeReason) ||
            !ValidActor(revisedBy))
            return Failure(RequestErrorKind.Validation, "A selection revision requires exact versions, a reason, and an actor.");
        if (_selection.SequenceEqual(recordVersions))
            return Failure(RequestErrorKind.Validation, "The revised selection must change the handoff selection.");

        RaiseEvent(new AdvisorHandoffSelectionRevised(_tenantId, _programId, Id,
            _revision + 1, recordVersions.ToArray(), changeReason.Trim(), revisedBy, revisedAt));
        return Result<bool>.Success(true);
    }

    public Result<bool> MarkReadyForReview(long expectedRevision, ActorReference readiedBy,
        DateTimeOffset readiedAt)
    {
        if (!IsOpen() || _status != "draft")
            return Failure(RequestErrorKind.Conflict, "Only a draft handoff can be marked ready for review.");
        if (expectedRevision != _revision)
            return Failure(RequestErrorKind.Conflict, "The handoff revision is stale.");
        if (!ValidActor(readiedBy) || _selection.Count == 0 ||
            _expiresOn < DateOnly.FromDateTime(readiedAt.UtcDateTime))
            return Failure(RequestErrorKind.Validation, "A handoff requires an active exact selection and valid actor before it can be shared.");

        RaiseEvent(new AdvisorHandoffReadiedForReview(_tenantId, _programId, Id,
            _revision + 1, readiedBy, readiedAt));
        return Result<bool>.Success(true);
    }

    public Result<bool> RecordFeedback(Uuid feedbackId, Uuid recordId, Uuid recordVersionId,
        string kind, string body, ActorReference author, DateTimeOffset createdAt)
        => RecordFeedback(feedbackId, new AdvisorRecordVersion(recordId, recordVersionId),
            kind, body, author, createdAt);

    public Result<bool> RecordFeedback(Uuid feedbackId, AdvisorRecordVersion sourceVersion,
        string kind, string body, ActorReference author, DateTimeOffset createdAt)
    {
        var existing = _feedback.Find(item => item.FeedbackId == feedbackId);
        var recordId = sourceVersion?.RecordId ?? Uuid.Empty;
        var recordVersionId = sourceVersion?.VersionId ?? Uuid.Empty;
        if (existing is not null)
            return (existing.SourceVersion ?? new AdvisorRecordVersion(existing.RecordId,
                       existing.RecordVersionId)) == sourceVersion &&
                   existing.Kind == kind && existing.Body == body && existing.Author == author
                ? Result<bool>.Success(true)
                : Failure(RequestErrorKind.Conflict, "The feedback identifier already has different content.");
        if (_status != "ready_for_review" || !IsOpen())
            return Failure(RequestErrorKind.Conflict, "Feedback is accepted only while the handoff is ready for review.");
        if (feedbackId == Uuid.Empty || sourceVersion is null || !sourceVersion.HasValidIdentity ||
            !_selection.Contains(sourceVersion) ||
            !FeedbackKinds.Contains(kind) || string.IsNullOrWhiteSpace(body) || !ValidActor(author))
            return Failure(RequestErrorKind.Validation, "Feedback must target a selected exact version and include a supported kind, body, and author.");

        RaiseEvent(new AdvisorFeedbackRecorded(_tenantId, _programId, Id, feedbackId, recordId,
            recordVersionId, kind, body.Trim(), author, createdAt)
        {
            SelectedSource = sourceVersion.IsSupported ? sourceVersion : null,
        });
        return Result<bool>.Success(true);
    }

    public Result<bool> CorrectFeedback(Uuid feedbackId, Uuid correctionId,
        string correctedBody, string correctionReason, ActorReference correctedBy,
        DateTimeOffset correctedAt)
    {
        var feedback = _feedback.Find(item => item.FeedbackId == feedbackId);
        if (feedback is null)
            return Failure(RequestErrorKind.Validation, "The feedback to correct was not found.");
        var existing = feedback.Corrections?.FirstOrDefault(item => item.CorrectionId == correctionId);
        if (existing is not null)
            return existing.Body == correctedBody && existing.Reason == correctionReason
                ? Result<bool>.Success(true)
                : Failure(RequestErrorKind.Conflict, "The correction identifier already has different content.");
        if (correctionId == Uuid.Empty || string.IsNullOrWhiteSpace(correctedBody) ||
            string.IsNullOrWhiteSpace(correctionReason) || !ValidActor(correctedBy))
            return Failure(RequestErrorKind.Validation, "A feedback correction requires content, a reason, and an actor.");

        RaiseEvent(new AdvisorFeedbackCorrected(_tenantId, _programId, Id, feedbackId,
            correctionId, correctedBody.Trim(), correctionReason.Trim(), correctedBy, correctedAt));
        return Result<bool>.Success(true);
    }

    public Result<bool> RespondToFeedback(Uuid feedbackId, string response,
        ActorReference respondedBy, DateTimeOffset respondedAt)
    {
        if (!_feedback.Exists(item => item.FeedbackId == feedbackId))
            return Failure(RequestErrorKind.Validation, "The feedback to respond to was not found.");
        if (string.IsNullOrWhiteSpace(response) || !ValidActor(respondedBy))
            return Failure(RequestErrorKind.Validation, "A feedback response requires content and an actor.");

        RaiseEvent(new AdvisorFeedbackResponded(_tenantId, _programId, Id, feedbackId,
            response.Trim(), respondedBy, respondedAt));
        return Result<bool>.Success(true);
    }

    public Result<bool> ResolveFeedback(Uuid feedbackId, ActorReference resolvedBy,
        DateTimeOffset resolvedAt)
    {
        if (feedbackId == Uuid.Empty || !ValidActor(resolvedBy) || resolvedAt == default)
            return Failure(RequestErrorKind.Validation,
                "Resolving advisor feedback requires a feedback identity, actor, and time.");
        var feedback = _feedback.Find(item => item.FeedbackId == feedbackId);
        if (feedback is null)
            return Failure(RequestErrorKind.NotFound, "The advisor feedback to resolve was not found.");
        if (feedback.ResolvedAt is not null)
            return Result<bool>.Success(true);

        RaiseEvent(new AdvisorFeedbackResolved(_tenantId, _programId, Id, feedbackId,
            resolvedBy, resolvedAt));
        return Result<bool>.Success(true);
    }

    public Result<bool> Close(string rationale, ActorReference closedBy, DateTimeOffset closedAt) =>
        End(rationale, closedBy, closedAt, revoke: false);

    public Result<bool> Revoke(string rationale, ActorReference revokedBy, DateTimeOffset revokedAt) =>
        End(rationale, revokedBy, revokedAt, revoke: true);

    public bool IsValidationCurrent(Uuid recordId) =>
        _feedback.Any(feedback => feedback.RecordId == recordId && feedback.Kind == "validated" &&
            _selection.Contains(feedback.SourceVersion ??
                new AdvisorRecordVersion(feedback.RecordId, feedback.RecordVersionId)));

    public IReadOnlyList<AdvisorFeedbackEntry> ReadFeedback() => _feedback.Select(feedback =>
        feedback with
        {
            Corrections = feedback.Corrections?.ToArray(),
            Responses = feedback.Responses?.ToArray(),
            Disposition = _selection.Contains(feedback.SourceVersion ??
                new AdvisorRecordVersion(feedback.RecordId, feedback.RecordVersionId))
                ? AdvisorFeedbackDisposition.Current :
                AdvisorFeedbackDisposition.Historical,
        }).ToArray();

    Result<bool> End(string rationale, ActorReference actor, DateTimeOffset occurredAt, bool revoke)
    {
        if (!IsOpen())
            return Failure(RequestErrorKind.Conflict, "Only an open handoff can be closed or revoked.");
        if (string.IsNullOrWhiteSpace(rationale) || !ValidActor(actor))
            return Failure(RequestErrorKind.Validation, "Ending a handoff requires a rationale and actor.");
        if (revoke)
            RaiseEvent(new AdvisorHandoffRevoked(_tenantId, _programId, Id, _revision + 1,
                rationale.Trim(), actor, occurredAt));
        else
            RaiseEvent(new AdvisorHandoffClosed(_tenantId, _programId, Id, _revision + 1,
                rationale.Trim(), actor, occurredAt));
        return Result<bool>.Success(true);
    }

    bool IsOpen() => _created && _status is "draft" or "ready_for_review";

    void ApplyFeedbackRecorded(AdvisorFeedbackRecorded ev)
    {
        var sourceVersion = ev.SelectedSource ?? new AdvisorRecordVersion(ev.RecordId,
            ev.RecordVersionId);
        if (ev.TenantId != _tenantId || ev.ProgramId != _programId || ev.HandoffId != Id ||
            ev.FeedbackId == Uuid.Empty || ev.RecordId == Uuid.Empty ||
            sourceVersion.RecordId != ev.RecordId || sourceVersion.VersionId != ev.RecordVersionId ||
            !sourceVersion.HasValidIdentity || !FeedbackKinds.Contains(ev.Kind) ||
            string.IsNullOrWhiteSpace(ev.Body) || ev.Body.Trim() != ev.Body ||
            !ValidActor(ev.Author) || ev.CreatedAt == default ||
            !_selection.Contains(sourceVersion) || _feedback.Any(item => item.FeedbackId == ev.FeedbackId))
            throw new InvalidOperationException("The advisor feedback event is invalid.");
        _revision++;
        _updatedAt = ev.CreatedAt;
        _feedback.Add(new AdvisorFeedbackEntry(ev.FeedbackId, ev.RecordId,
            ev.RecordVersionId, ev.Kind, ev.Body, ev.Author, ev.CreatedAt,
            SourceVersion: ev.SelectedSource));
    }

    static bool ValidSelection(IReadOnlyList<AdvisorRecordVersion>? selection) =>
        selection is { Count: > 0 and <= MaximumSelectionCount } &&
        selection.All(item => item is not null &&
            item.HasValidIdentity) &&
        selection.Select(item => (item.SourceFamily, item.RecordId, item.EntryId))
            .Distinct().Count() == selection.Count;

    static bool ValidActor(ActorReference? actor) => actor is not null &&
        !string.IsNullOrWhiteSpace(actor.Kind) && !string.IsNullOrWhiteSpace(actor.Id) &&
        !string.IsNullOrWhiteSpace(actor.Display);

    void RequireFeedbackScope(Uuid tenantId, Uuid programId, Uuid handoffId)
    {
        if (tenantId != _tenantId || programId != _programId || handoffId != Id)
            throw new InvalidOperationException(
                "Advisor feedback event scope does not match its handoff stream.");
    }

    static Result<bool> Failure(RequestErrorKind kind, string message) =>
        Result<bool>.Failure(new RequestError(kind, message));
}

public enum AdvisorFeedbackDisposition { Current, Historical }

public sealed record AdvisorFeedbackEntry(Uuid FeedbackId, Uuid RecordId, Uuid RecordVersionId,
    string Kind, string Body, ActorReference Author, DateTimeOffset CreatedAt,
    IReadOnlyList<AdvisorFeedbackCorrection>? Corrections = null,
    IReadOnlyList<AdvisorFeedbackResponse>? Responses = null,
    DateTimeOffset? ResolvedAt = null,
    AdvisorFeedbackDisposition Disposition = AdvisorFeedbackDisposition.Current,
    AdvisorRecordVersion? SourceVersion = null)
{
    public ActorReference? ResolvedBy { get; init; }
}

public sealed record AdvisorFeedbackCorrection(Uuid CorrectionId, string Body, string Reason,
    ActorReference Actor, DateTimeOffset CorrectedAt);

public sealed record AdvisorFeedbackResponse(string Body, ActorReference Actor,
    DateTimeOffset RespondedAt);
