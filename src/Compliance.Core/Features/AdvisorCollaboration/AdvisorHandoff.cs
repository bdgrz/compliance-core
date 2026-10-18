using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>
/// Owns one handoff stream: selected exact record versions, access state, and feedback history.
/// Feedback stays in this stream so revocation and feedback admission share one consistency boundary.
/// </summary>
public sealed class AdvisorHandoff : Aggregate
{
    static readonly HashSet<string> FeedbackKinds = new(StringComparer.Ordinal)
        { "comment", "change_request", "validated" };

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly List<AdvisorRecordVersion> _selection = [];
    readonly List<AdvisorFeedbackEntry> _feedback = [];
    bool _created;
    string? _purpose;
    DateOnly _expiresOn;
    string _status = "missing";
    long _revision;

    public bool IsCreated => _created;
    public string Status => _status;
    public long Revision => _revision;
    public string? Purpose => _purpose;
    public DateOnly ExpiresOn => _expiresOn;
    public IReadOnlyList<AdvisorRecordVersion> Selection => _selection.ToArray();

    public AdvisorHandoff(Uuid tenantId, Uuid programId, Uuid handoffId)
        : base(handoffId, new EventStreamAddress(tenantId.ToString(), "advisor-handoffs",
            handoffId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        On<AdvisorHandoffCreated>(ev =>
        {
            _created = true;
            _revision = 1;
            _purpose = ev.Purpose;
            _expiresOn = ev.ExpiresOn;
            _selection.AddRange(ev.RecordVersions);
            _status = "draft";
        });
        On<AdvisorHandoffSelectionRevised>(ev =>
        {
            _revision = ev.Revision;
            _selection.Clear();
            _selection.AddRange(ev.RecordVersions);
            _status = "draft";
        });
        On<AdvisorHandoffReadiedForReview>(ev =>
        {
            _revision = ev.Revision;
            _status = "ready_for_review";
        });
        On<AdvisorHandoffClosed>(ev =>
        {
            _revision = ev.Revision;
            _status = "closed";
        });
        On<AdvisorHandoffRevoked>(ev =>
        {
            _revision = ev.Revision;
            _status = "revoked";
        });
        On<AdvisorFeedbackRecorded>(ev =>
        {
            _revision++;
            _feedback.Add(new AdvisorFeedbackEntry(ev.FeedbackId, ev.RecordId,
                ev.RecordVersionId, ev.Kind, ev.Body, ev.Author, ev.CreatedAt));
        });
        On<AdvisorFeedbackCorrected>(ev =>
        {
            _revision++;
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
            _revision++;
            var index = _feedback.FindIndex(item => item.FeedbackId == ev.FeedbackId);
            if (index >= 0)
                _feedback[index] = _feedback[index] with { ResolvedAt = ev.ResolvedAt };
        });
    }

    public Result<bool> Create(string purpose, IReadOnlyList<AdvisorRecordVersion> recordVersions,
        DateOnly expiresOn, ActorReference createdBy, DateTimeOffset createdAt)
    {
        if (_tenantId == Uuid.Empty || _programId == Uuid.Empty || Id == Uuid.Empty ||
            string.IsNullOrWhiteSpace(purpose) || !ValidSelection(recordVersions) ||
            !ValidActor(createdBy) || expiresOn < DateOnly.FromDateTime(createdAt.UtcDateTime))
            return Failure(RequestErrorKind.Validation, "A handoff requires purpose, exact selected versions, an actor, and a valid expiry.");
        if (_created)
            return _purpose == purpose.Trim() && _expiresOn == expiresOn &&
                   _selection.SequenceEqual(recordVersions)
                ? Result<bool>.Success(true)
                : Failure(RequestErrorKind.Conflict, "The handoff already exists with different content.");

        RaiseEvent(new AdvisorHandoffCreated(_tenantId, _programId, Id, recordVersions.ToArray(),
            purpose.Trim(), expiresOn, createdBy, createdAt));
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
    {
        var existing = _feedback.Find(item => item.FeedbackId == feedbackId);
        if (existing is not null)
            return existing.RecordId == recordId && existing.RecordVersionId == recordVersionId &&
                   existing.Kind == kind && existing.Body == body && existing.Author == author
                ? Result<bool>.Success(true)
                : Failure(RequestErrorKind.Conflict, "The feedback identifier already has different content.");
        if (_status != "ready_for_review" || !IsOpen())
            return Failure(RequestErrorKind.Conflict, "Feedback is accepted only while the handoff is ready for review.");
        if (feedbackId == Uuid.Empty || recordId == Uuid.Empty || recordVersionId == Uuid.Empty ||
            !_selection.Contains(new AdvisorRecordVersion(recordId, recordVersionId)) ||
            !FeedbackKinds.Contains(kind) || string.IsNullOrWhiteSpace(body) || !ValidActor(author))
            return Failure(RequestErrorKind.Validation, "Feedback must target a selected exact version and include a supported kind, body, and author.");

        RaiseEvent(new AdvisorFeedbackRecorded(_tenantId, _programId, Id, feedbackId, recordId,
            recordVersionId, kind, body.Trim(), author, createdAt));
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

    public Result<bool> Close(string rationale, ActorReference closedBy, DateTimeOffset closedAt) =>
        End(rationale, closedBy, closedAt, revoke: false);

    public Result<bool> Revoke(string rationale, ActorReference revokedBy, DateTimeOffset revokedAt) =>
        End(rationale, revokedBy, revokedAt, revoke: true);

    public bool IsValidationCurrent(Uuid recordId) =>
        _feedback.Any(feedback => feedback.RecordId == recordId && feedback.Kind == "validated" &&
            _selection.Contains(new AdvisorRecordVersion(feedback.RecordId, feedback.RecordVersionId)));

    public IReadOnlyList<AdvisorFeedbackEntry> ReadFeedback() => _feedback.Select(feedback =>
        feedback with
        {
            Corrections = feedback.Corrections?.ToArray(),
            Responses = feedback.Responses?.ToArray(),
            Disposition = _selection.Contains(new AdvisorRecordVersion(feedback.RecordId,
                feedback.RecordVersionId)) ? AdvisorFeedbackDisposition.Current :
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

    static bool ValidSelection(IReadOnlyList<AdvisorRecordVersion>? selection) =>
        selection is { Count: > 0 } && selection.All(item => item is not null &&
            item.RecordId != Uuid.Empty && item.VersionId != Uuid.Empty) &&
        selection.Select(item => item.RecordId).Distinct().Count() == selection.Count &&
        selection.Select(item => item.VersionId).Distinct().Count() == selection.Count;

    static bool ValidActor(ActorReference? actor) => actor is not null &&
        !string.IsNullOrWhiteSpace(actor.Kind) && !string.IsNullOrWhiteSpace(actor.Id) &&
        !string.IsNullOrWhiteSpace(actor.Display);

    static Result<bool> Failure(RequestErrorKind kind, string message) =>
        Result<bool>.Failure(new RequestError(kind, message));
}

public enum AdvisorFeedbackDisposition { Current, Historical }

public sealed record AdvisorFeedbackEntry(Uuid FeedbackId, Uuid RecordId, Uuid RecordVersionId,
    string Kind, string Body, ActorReference Author, DateTimeOffset CreatedAt,
    IReadOnlyList<AdvisorFeedbackCorrection>? Corrections = null,
    IReadOnlyList<AdvisorFeedbackResponse>? Responses = null,
    DateTimeOffset? ResolvedAt = null,
    AdvisorFeedbackDisposition Disposition = AdvisorFeedbackDisposition.Current);

public sealed record AdvisorFeedbackCorrection(Uuid CorrectionId, string Body, string Reason,
    ActorReference Actor, DateTimeOffset CorrectedAt);

public sealed record AdvisorFeedbackResponse(string Body, ActorReference Actor,
    DateTimeOffset RespondedAt);
