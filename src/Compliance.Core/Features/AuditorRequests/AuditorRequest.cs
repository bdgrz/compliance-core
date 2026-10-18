using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
/// One auditor request stream owns request terms, discussion, response versions,
/// intentional deliveries, external dispositions, and immutable sample selections.
/// Cross-stream evidence and population validity is confirmed by the command handler.
/// </summary>
public sealed class AuditorRequest : Aggregate
{
    const int MaximumEventBytes = 48 * 1024;
    const int MaximumComments = 256;
    const int MaximumResponses = 64;
    const int MaximumResponseVersions = 16;
    const int MaximumEvidenceArtifacts = 64;
    const int MaximumSampleSelections = 64;
    const int MaximumSampleRows = 500;
    const int MaximumActorDisplayLength = 200;
    const int MaximumReferenceLength = 500;
    const int MaximumBodyLength = 8_000;
    const int MaximumSummaryLength = 12_000;
    const int MaximumRationaleLength = 2_000;
    const int MaximumHistoryEntries = 512;
    static readonly HashSet<string> Sources = new(StringComparer.Ordinal)
    {
        "external_email", "external_portal", "in_product_attest"
    };
    static readonly HashSet<string> Priorities = new(StringComparer.Ordinal)
    {
        "low", "normal", "high", "urgent"
    };
    static readonly HashSet<string> Compartments = new(StringComparer.Ordinal)
    {
        "internal", "attest_shared"
    };
    static readonly HashSet<string> SampleMethods = new(StringComparer.Ordinal)
    {
        "judgmental", "random", "stratified", "targeted"
    };
    static readonly HashSet<string> Dispositions = new(StringComparer.Ordinal)
    {
        "accepted", "rejected", "changes_requested"
    };

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    AuditorRequestView? _current;

    public long Revision => _current?.Revision ?? 0;

    public AuditorRequestView? Current => _current;

    public AuditorRequest(Uuid tenantId, Uuid programId, Uuid requestId)
        : base(requestId, new EventStreamAddress(tenantId.ToString(), "auditor-requests",
            requestId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        On<AuditorRequestRecorded>(Apply);
        On<AuditorRequestRevised>(Apply);
        On<AuditorRequestAssigned>(Apply);
        On<AuditorRequestCommentRecorded>(Apply);
        On<AuditResponseDraftCreated>(Apply);
        On<AuditResponseDraftRevised>(Apply);
        On<AuditorResponseSubmitted>(Apply);
        On<AuditorResponseDisclosureReviewed>(Apply);
        On<AuditorResponseDelivered>(Apply);
        On<AuditorResponseDispositionRecorded>(Apply);
        On<AuditorRequestResolved>(Apply);
        On<AuditorRequestReopened>(Apply);
        On<SampleSelectionRecorded>(Apply);
    }

    public CommandFailure? Create(AuditorRequestContent content, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset occurredAt)
    {
        if (_current is not null)
            return SameCreate(_current, content, actorMemberId, actorDisplay, occurredAt)
                ? null
                : CommandFailure.StateConflict(
                    "The auditor request already exists with different terms or attribution.");
        if (_tenantId == Uuid.Empty || _programId == Uuid.Empty || Id == Uuid.Empty)
            return Invalid("An auditor request requires tenant, program, and request identities.");
        if (ValidateActor(actorMemberId, actorDisplay, occurredAt) is { } actorFailure)
            return actorFailure;
        if (Normalize(content) is not { } normalized)
            return Invalid("The auditor request terms are incomplete or unsupported.");

        var display = actorDisplay.Trim();
        var terms = Terms(1, normalized, string.Empty, actorMemberId, display, occurredAt);
        var initial = new AuditorRequestView(_tenantId, _programId, Id,
            normalized.ExaminationId, normalized.Source, normalized.ExternalReference,
            normalized.Wording, normalized.Scope, normalized.Priority, normalized.OwnerMemberId,
            normalized.DueOn, normalized.RequiresInternalReview, "open", 1, occurredAt,
            actorMemberId, display, [terms], [], [], [],
            [new AuditorRequestHistoryEntry(1, "recorded", "Request recorded", actorMemberId,
                display, occurredAt)]);
        return Emit(initial, occurredAt, static (tenant, request, revision, actor, state, at) =>
            new AuditorRequestRecorded(tenant, request, revision, actor, state, at));
    }

    public CommandFailure? Revise(long expectedRevision, string wording, string scope,
        string priority, DateOnly dueOn, string changeReason, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset occurredAt)
    {
        if (CurrentOrMissing() is not { } current)
            return CommandFailure.MissingRecord("The auditor request was not found.");
        if (CheckRevision(expectedRevision) is { } revisionFailure)
            return revisionFailure;
        if (current.Status != "open")
            return CommandFailure.StateConflict("A resolved auditor request must be reopened before revision.");
        if (ValidateActor(actorMemberId, actorDisplay, occurredAt) is { } actorFailure)
            return actorFailure;
        wording = CleanText(wording);
        scope = CleanText(scope);
        priority = CleanToken(priority);
        changeReason = CleanText(changeReason);
        if (!Bounded(wording, 1, MaximumBodyLength) || !Bounded(scope, 1, MaximumBodyLength) ||
            !Priorities.Contains(priority) || dueOn == default ||
            !Bounded(changeReason, 1, MaximumRationaleLength))
            return Invalid("A request revision needs bounded wording, scope, priority, due date, and reason.");
        if (current.Wording == wording && current.Scope == scope && current.Priority == priority &&
            current.DueOn == dueOn)
            return null;

        var actorDisplayNormalized = actorDisplay.Trim();
        var nextTerms = new AuditorRequestTermsVersion(current.TermsHistory.Count + 1, wording,
            scope, priority, current.OwnerMemberId, dueOn, changeReason, actorMemberId,
            actorDisplayNormalized, occurredAt);
        var next = Next(current, "revised", changeReason, actorMemberId,
            actorDisplayNormalized, occurredAt) with
        {
            Wording = wording,
            Scope = scope,
            Priority = priority,
            DueOn = dueOn,
            TermsHistory = Append(current.TermsHistory, nextTerms),
        };
        return Emit(next, occurredAt, static (tenant, request, revision, actor, state, at) =>
            new AuditorRequestRevised(tenant, request, revision, actor, state, at));
    }

    public CommandFailure? Assign(long expectedRevision, Uuid ownerMemberId, DateOnly dueOn,
        string assignmentReason, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset occurredAt)
    {
        if (CurrentOrMissing() is not { } current)
            return CommandFailure.MissingRecord("The auditor request was not found.");
        if (CheckRevision(expectedRevision) is { } revisionFailure)
            return revisionFailure;
        if (current.Status != "open")
            return CommandFailure.StateConflict("A resolved auditor request must be reopened before reassignment.");
        if (ValidateActor(actorMemberId, actorDisplay, occurredAt) is { } actorFailure)
            return actorFailure;
        assignmentReason = CleanText(assignmentReason);
        if (ownerMemberId == Uuid.Empty || dueOn == default ||
            !Bounded(assignmentReason, 1, MaximumRationaleLength))
            return Invalid("Assignment requires an owner, due date, and bounded rationale.");
        if (current.OwnerMemberId == ownerMemberId && current.DueOn == dueOn)
            return null;

        var display = actorDisplay.Trim();
        var terms = new AuditorRequestTermsVersion(current.TermsHistory.Count + 1,
            current.Wording, current.Scope, current.Priority, ownerMemberId, dueOn,
            assignmentReason, actorMemberId, display, occurredAt);
        var next = Next(current, "assigned", assignmentReason, actorMemberId, display, occurredAt)
            with
        {
            OwnerMemberId = ownerMemberId,
            DueOn = dueOn,
            TermsHistory = Append(current.TermsHistory, terms),
        };
        return Emit(next, occurredAt, static (tenant, request, revision, actor, state, at) =>
            new AuditorRequestAssigned(tenant, request, revision, actor, state, at));
    }

    public CommandFailure? RecordComment(long expectedRevision, string body, string compartment,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset occurredAt)
    {
        if (CurrentOrMissing() is not { } current)
            return CommandFailure.MissingRecord("The auditor request was not found.");
        if (CheckRevision(expectedRevision) is { } revisionFailure)
            return revisionFailure;
        if (current.Status != "open")
            return CommandFailure.StateConflict("Discussion can only be added to an open request.");
        if (ValidateActor(actorMemberId, actorDisplay, occurredAt) is { } actorFailure)
            return actorFailure;
        body = CleanText(body);
        compartment = CleanToken(compartment);
        if (!Bounded(body, 1, MaximumBodyLength) || !Compartments.Contains(compartment))
            return Invalid("A request comment needs bounded text and a supported compartment.");
        if (current.Comments.Count >= MaximumComments)
            return CommandFailure.StateConflict("The request reached its retained discussion limit.");

        var display = actorDisplay.Trim();
        var comment = new AuditorRequestCommentView(Uuid.CreateVersion4(), body, compartment,
            actorMemberId, display, occurredAt);
        var next = Next(current, "commented", compartment, actorMemberId, display, occurredAt)
            with
        { Comments = Append(current.Comments, comment) };
        return Emit(next, occurredAt, static (tenant, request, revision, actor, state, at) =>
            new AuditorRequestCommentRecorded(tenant, request, revision, actor, state, at));
    }

    public CommandFailure? CreateResponseDraft(long expectedRevision, Uuid responseId,
        string summary, IReadOnlyList<Uuid> evidenceArtifactIds, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset occurredAt)
    {
        if (_current is { } existing && existing.Responses.FirstOrDefault(item =>
                item.ResponseId == responseId) is { } prior)
            return SameDraft(prior, summary, evidenceArtifactIds, actorMemberId, actorDisplay,
                occurredAt)
                ? null
                : CommandFailure.StateConflict(
                    "An auditor response identity cannot be reused with changed draft content.");
        if (CurrentOrMissing() is not { } current)
            return CommandFailure.MissingRecord("The auditor request was not found.");
        if (CheckRevision(expectedRevision) is { } revisionFailure)
            return revisionFailure;
        if (current.Status != "open")
            return CommandFailure.StateConflict("A response can only be drafted for an open request.");
        if (ValidateActor(actorMemberId, actorDisplay, occurredAt) is { } actorFailure)
            return actorFailure;
        if (responseId == Uuid.Empty || current.Responses.Count >= MaximumResponses ||
            NormalizeResponse(summary, evidenceArtifactIds) is not { } normalized)
            return Invalid("A response draft requires a stable identity and bounded content.");

        var display = actorDisplay.Trim();
        var version = new AuditorResponseVersionView(1, normalized.Summary,
            normalized.EvidenceArtifactIds, "draft", actorMemberId, display, occurredAt,
            null, null, null);
        var response = new AuditorRequestResponseHistoryView(responseId, 1, null, "draft",
            [version], null, null);
        var next = Next(current, "response_draft_created", responseId.ToString(), actorMemberId,
            display, occurredAt) with
        { Responses = Append(current.Responses, response) };
        return Emit(next, occurredAt, static (tenant, request, revision, actor, state, at) =>
            new AuditResponseDraftCreated(tenant, request, revision, actor, state, at));
    }

    public CommandFailure? ReviseResponseDraft(long expectedRevision, Uuid responseId,
        int expectedResponseVersion, string summary, IReadOnlyList<Uuid> evidenceArtifactIds,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset occurredAt)
    {
        if (CurrentOrMissing() is not { } current)
            return CommandFailure.MissingRecord("The auditor request was not found.");
        if (CheckRevision(expectedRevision) is { } revisionFailure)
            return revisionFailure;
        if (current.Status != "open")
            return CommandFailure.StateConflict("A resolved request must be reopened before response revision.");
        if (ValidateActor(actorMemberId, actorDisplay, occurredAt) is { } actorFailure)
            return actorFailure;
        if (FindResponse(current, responseId) is not { } response)
            return CommandFailure.MissingRecord("The auditor response was not found.");
        if (response.CurrentVersion != expectedResponseVersion)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "auditor response", response.CurrentVersion));
        var previous = CurrentVersion(response);
        if (response.DeliveredVersion is not null || previous.Status is not ("draft" or "rejected"))
            return CommandFailure.StateConflict(
                "Only a draft or rejected response version can be revised; delivered versions are immutable.");
        if (response.Versions.Count >= MaximumResponseVersions ||
            NormalizeResponse(summary, evidenceArtifactIds) is not { } normalized)
            return Invalid("A response revision needs bounded content and a supported version count.");

        var display = actorDisplay.Trim();
        var version = new AuditorResponseVersionView(response.CurrentVersion + 1,
            normalized.Summary, normalized.EvidenceArtifactIds, "draft", actorMemberId, display,
            occurredAt, null, null, null);
        var revised = response with
        {
            CurrentVersion = version.Version,
            Status = "draft",
            Versions = Append(response.Versions, version),
        };
        var next = ReplaceResponse(Next(current, "response_draft_revised", responseId.ToString(),
            actorMemberId, display, occurredAt), revised);
        return Emit(next, occurredAt, static (tenant, request, revision, actor, state, at) =>
            new AuditResponseDraftRevised(tenant, request, revision, actor, state, at));
    }

    public CommandFailure? SubmitResponse(long expectedRevision, Uuid responseId, int versionNumber,
        string submissionNote, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset occurredAt)
    {
        if (CurrentOrMissing() is not { } current)
            return CommandFailure.MissingRecord("The auditor request was not found.");
        if (CheckRevision(expectedRevision) is { } revisionFailure)
            return revisionFailure;
        if (current.Status != "open")
            return CommandFailure.StateConflict("A response can only be submitted for an open request.");
        if (ValidateActor(actorMemberId, actorDisplay, occurredAt) is { } actorFailure)
            return actorFailure;
        if (FindResponse(current, responseId) is not { } response)
            return CommandFailure.MissingRecord("The auditor response was not found.");
        if (response.CurrentVersion != versionNumber)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "auditor response", response.CurrentVersion));
        var version = CurrentVersion(response);
        submissionNote = CleanText(submissionNote);
        if (version.Status == "submitted" && version.SubmissionNote == submissionNote &&
            version.AuthorMemberId == actorMemberId && version.SubmittedAt == occurredAt)
            return null;
        if (version.Status != "draft" || !Bounded(submissionNote, 1, MaximumRationaleLength))
            return CommandFailure.StateConflict(
                "Only a draft with a bounded submission note can be submitted.");

        var submitted = version with
        {
            Status = "submitted",
            SubmittedAt = occurredAt,
            SubmissionNote = submissionNote,
        };
        var updated = response with { Status = "submitted", Versions = ReplaceVersion(response, submitted) };
        var display = actorDisplay.Trim();
        var next = ReplaceResponse(Next(current, "response_submitted", responseId.ToString(),
            actorMemberId, display, occurredAt), updated);
        return Emit(next, occurredAt, static (tenant, request, revision, actor, state, at) =>
            new AuditorResponseSubmitted(tenant, request, revision, actor, state, at));
    }

    public CommandFailure? ReviewResponse(long expectedRevision, Uuid responseId,
        int versionNumber, string outcome, string rationale, Uuid reviewerMemberId,
        string reviewerDisplay, DateTimeOffset reviewedAt)
    {
        if (CurrentOrMissing() is not { } current)
            return CommandFailure.MissingRecord("The auditor request was not found.");
        if (CheckRevision(expectedRevision) is { } revisionFailure)
            return revisionFailure;
        if (current.Status != "open")
            return CommandFailure.StateConflict("A resolved request cannot receive a response review.");
        if (ValidateActor(reviewerMemberId, reviewerDisplay, reviewedAt) is { } actorFailure)
            return actorFailure;
        if (FindResponse(current, responseId) is not { } response)
            return CommandFailure.MissingRecord("The auditor response was not found.");
        if (response.CurrentVersion != versionNumber)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "auditor response", response.CurrentVersion));
        var version = CurrentVersion(response);
        outcome = CleanToken(outcome);
        rationale = CleanText(rationale);
        if (version.AuthorMemberId == reviewerMemberId)
            return CommandFailure.StateConflict("A response author cannot review their own response.");
        if (version.Status != "submitted" || version.Review is not null)
            return CommandFailure.StateConflict("Only an unreviewed submitted version can be reviewed.");
        if (outcome is not ("approved" or "rejected") ||
            !Bounded(rationale, 1, MaximumRationaleLength))
            return Invalid("A response review needs an approved or rejected outcome and rationale.");

        var display = reviewerDisplay.Trim();
        var review = new AuditorResponseReviewView(outcome, rationale, reviewerMemberId,
            display, reviewedAt);
        var reviewed = version with { Status = outcome, Review = review };
        var updated = response with { Status = outcome, Versions = ReplaceVersion(response, reviewed) };
        var next = ReplaceResponse(Next(current, "response_reviewed", outcome, reviewerMemberId,
            display, reviewedAt), updated);
        return Emit(next, reviewedAt, static (tenant, request, revision, actor, state, at) =>
            new AuditorResponseDisclosureReviewed(tenant, request, revision, actor, state, at));
    }

    public CommandFailure? DeliverResponse(long expectedRevision, Uuid responseId,
        int versionNumber, string recipient, string deliveryReference, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset deliveredAt)
    {
        if (CurrentOrMissing() is not { } current)
            return CommandFailure.MissingRecord("The auditor request was not found.");
        if (CheckRevision(expectedRevision) is { } revisionFailure)
            return revisionFailure;
        if (current.Status != "open")
            return CommandFailure.StateConflict("A response can only be delivered for an open request.");
        if (ValidateActor(actorMemberId, actorDisplay, deliveredAt) is { } actorFailure)
            return actorFailure;
        if (FindResponse(current, responseId) is not { } response)
            return CommandFailure.MissingRecord("The auditor response was not found.");
        if (response.CurrentVersion != versionNumber)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "auditor response", response.CurrentVersion));
        var version = CurrentVersion(response);
        recipient = CleanText(recipient);
        deliveryReference = CleanText(deliveryReference);
        if (response.Delivery is { } existingDelivery)
            return existingDelivery.Version == versionNumber &&
                   existingDelivery.Recipient == recipient &&
                   existingDelivery.DeliveryReference == deliveryReference &&
                   existingDelivery.ActorMemberId == actorMemberId &&
                   existingDelivery.DeliveredAt == deliveredAt
                ? null
                : CommandFailure.StateConflict("A response delivery receipt is immutable.");
        if (version.Status != "submitted" && version.Status != "approved" ||
            current.RequiresInternalReview && version.Status != "approved")
            return CommandFailure.StateConflict(
                "The exact response version must be submitted and approved when internal review is required.");
        if (!Bounded(recipient, 1, MaximumReferenceLength) ||
            !Bounded(deliveryReference, 1, MaximumReferenceLength))
            return Invalid("Intentional response delivery requires a recipient and delivery reference.");

        var display = actorDisplay.Trim();
        var delivery = new AuditorResponseDeliveryView(versionNumber, recipient,
            deliveryReference, actorMemberId, display, deliveredAt);
        var deliveredVersion = version with { Status = "delivered" };
        var delivered = response with
        {
            Status = "delivered",
            DeliveredVersion = versionNumber,
            Versions = ReplaceVersion(response, deliveredVersion),
            Delivery = delivery,
        };
        var next = ReplaceResponse(Next(current, "response_delivered", deliveryReference,
            actorMemberId, display, deliveredAt), delivered);
        return Emit(next, deliveredAt, static (tenant, request, revision, actor, state, at) =>
            new AuditorResponseDelivered(tenant, request, revision, actor, state, at));
    }

    public CommandFailure? RecordResponseDisposition(long expectedRevision, Uuid responseId,
        int versionNumber, string outcome, string? externalReference, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset recordedAt)
    {
        if (CurrentOrMissing() is not { } current)
            return CommandFailure.MissingRecord("The auditor request was not found.");
        if (CheckRevision(expectedRevision) is { } revisionFailure)
            return revisionFailure;
        if (ValidateActor(actorMemberId, actorDisplay, recordedAt) is { } actorFailure)
            return actorFailure;
        if (FindResponse(current, responseId) is not { } response)
            return CommandFailure.MissingRecord("The auditor response was not found.");
        if (response.DeliveredVersion != versionNumber || response.Delivery is null)
            return CommandFailure.StateConflict(
                "A disposition must refer to the exact intentionally delivered response version.");
        outcome = CleanToken(outcome);
        externalReference = NormalizeOptional(externalReference);
        if (!Dispositions.Contains(outcome) || externalReference is { Length: > MaximumReferenceLength })
            return Invalid("The response disposition or external reference is invalid.");
        if (response.Disposition is { } prior)
            return prior.Version == versionNumber && prior.Outcome == outcome &&
                   prior.ExternalReference == externalReference && prior.ActorMemberId == actorMemberId &&
                   prior.RecordedAt == recordedAt
                ? null
                : CommandFailure.StateConflict("A response disposition is immutable.");

        var display = actorDisplay.Trim();
        var disposition = new AuditorResponseDispositionView(versionNumber, outcome,
            externalReference, actorMemberId, display, recordedAt);
        var updated = response with { Disposition = disposition };
        var next = ReplaceResponse(Next(current, "response_disposition_recorded", outcome,
            actorMemberId, display, recordedAt), updated);
        return Emit(next, recordedAt, static (tenant, request, revision, actor, state, at) =>
            new AuditorResponseDispositionRecorded(tenant, request, revision, actor, state, at));
    }

    public CommandFailure? Resolve(long expectedRevision, string resolution,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset resolvedAt)
    {
        if (CurrentOrMissing() is not { } current)
            return CommandFailure.MissingRecord("The auditor request was not found.");
        if (CheckRevision(expectedRevision) is { } revisionFailure)
            return revisionFailure;
        if (current.Status == "resolved")
            return CommandFailure.StateConflict("The auditor request is already resolved.");
        if (ValidateActor(actorMemberId, actorDisplay, resolvedAt) is { } actorFailure)
            return actorFailure;
        resolution = CleanText(resolution);
        if (!Bounded(resolution, 1, MaximumRationaleLength))
            return Invalid("Request resolution needs a bounded explanation.");
        if (current.Responses.Count == 0 || current.Responses.Any(static response =>
                response.Disposition?.Outcome != "accepted"))
            return CommandFailure.StateConflict(
                "A request can be resolved only after every response has an accepted external disposition.");

        var display = actorDisplay.Trim();
        var next = Next(current, "resolved", resolution, actorMemberId, display, resolvedAt)
            with
        { Status = "resolved" };
        return Emit(next, resolvedAt, static (tenant, request, revision, actor, state, at) =>
            new AuditorRequestResolved(tenant, request, revision, actor, state, at));
    }

    public CommandFailure? Reopen(long expectedRevision, string reopenReason,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset reopenedAt)
    {
        if (CurrentOrMissing() is not { } current)
            return CommandFailure.MissingRecord("The auditor request was not found.");
        if (CheckRevision(expectedRevision) is { } revisionFailure)
            return revisionFailure;
        if (current.Status != "resolved")
            return CommandFailure.StateConflict("Only a resolved auditor request can be reopened.");
        if (ValidateActor(actorMemberId, actorDisplay, reopenedAt) is { } actorFailure)
            return actorFailure;
        reopenReason = CleanText(reopenReason);
        if (!Bounded(reopenReason, 1, MaximumRationaleLength))
            return Invalid("Reopening a request needs a bounded explanation.");

        var display = actorDisplay.Trim();
        var next = Next(current, "reopened", reopenReason, actorMemberId, display, reopenedAt)
            with
        { Status = "open" };
        return Emit(next, reopenedAt, static (tenant, request, revision, actor, state, at) =>
            new AuditorRequestReopened(tenant, request, revision, actor, state, at));
    }

    public CommandFailure? RecordSampleSelection(long expectedRevision, Uuid selectionId,
        Uuid populationSnapshotId, string populationContentSha256, string selectionMethod,
        IReadOnlyList<Uuid> selectedRowIds, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset recordedAt)
    {
        if (_current is { } existing && existing.SampleSelections.FirstOrDefault(selection =>
                selection.SampleSelectionId == selectionId) is { } prior)
            return SameSelection(prior, populationSnapshotId, populationContentSha256,
                    selectionMethod, selectedRowIds, actorMemberId, actorDisplay, recordedAt)
                ? null
                : CommandFailure.StateConflict(
                    "A sample selection identity cannot be reused with different population content.");
        if (CurrentOrMissing() is not { } current)
            return CommandFailure.MissingRecord("The auditor request was not found.");
        if (CheckRevision(expectedRevision) is { } revisionFailure)
            return revisionFailure;
        if (current.Status != "open")
            return CommandFailure.StateConflict("A sample selection can only be recorded for an open request.");
        if (ValidateActor(actorMemberId, actorDisplay, recordedAt) is { } actorFailure)
            return actorFailure;
        populationContentSha256 = CleanToken(populationContentSha256);
        selectionMethod = CleanToken(selectionMethod);
        if (selectionId == Uuid.Empty || populationSnapshotId == Uuid.Empty ||
            !ValidDigest(populationContentSha256) || !SampleMethods.Contains(selectionMethod) ||
            selectedRowIds is null || selectedRowIds.Count is 0 or > MaximumSampleRows ||
            selectedRowIds.Any(static id => id == Uuid.Empty) ||
            selectedRowIds.Distinct().Count() != selectedRowIds.Count ||
            current.SampleSelections.Count >= MaximumSampleSelections)
            return Invalid("A sample selection requires an exact frozen population and unique bounded rows.");

        var display = actorDisplay.Trim();
        var rows = FreezeIds(selectedRowIds);
        var selection = new AuditorSampleSelectionView(selectionId, populationSnapshotId,
            populationContentSha256, selectionMethod, rows,
            SelectionDigest(populationSnapshotId, populationContentSha256, selectionMethod, rows),
            actorMemberId, display, recordedAt);
        var next = Next(current, "sample_selection_recorded", selectionId.ToString(), actorMemberId,
            display, recordedAt) with
        { SampleSelections = Append(current.SampleSelections, selection) };
        return Emit(next, recordedAt, static (tenant, request, revision, actor, state, at) =>
            new SampleSelectionRecorded(tenant, request, revision, actor, state, at));
    }

    public string GetWorkStatus(DateOnly asOf)
    {
        if (_current is null)
            return "missing";
        if (_current.Status == "resolved")
            return "resolved";
        if (_current.Responses.Any(static response => response.Disposition?.Outcome == "accepted"))
            return "accepted";
        if (_current.Responses.Any(static response => CurrentVersion(response).Status == "rejected"))
            return "blocked";
        if (_current.Responses.Any(static response =>
                CurrentVersion(response).Status is "submitted" or "approved" or "delivered"))
            return "submitted";
        return asOf > _current.DueOn ? "overdue" : "open";
    }

    void Apply(AuditorRequestRecorded ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, creating: true);

    void Apply(AuditorRequestRevised ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void Apply(AuditorRequestAssigned ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void Apply(AuditorRequestCommentRecorded ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void Apply(AuditResponseDraftCreated ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void Apply(AuditResponseDraftRevised ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void Apply(AuditorResponseSubmitted ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void Apply(AuditorResponseDisclosureReviewed ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void Apply(AuditorResponseDelivered ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void Apply(AuditorResponseDispositionRecorded ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void Apply(AuditorRequestResolved ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void Apply(AuditorRequestReopened ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void Apply(SampleSelectionRecorded ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt);

    void ApplyState(Uuid tenantId, Uuid requestId, long revision, Uuid? actorMemberId,
        AuditorRequestView state, DateTimeOffset occurredAt, bool creating = false)
    {
        if (tenantId != _tenantId || requestId != Id || state is null ||
            state.TenantId != _tenantId || state.ProgramId != _programId ||
            state.AuditorRequestId != Id || state.Revision != revision ||
            state.UpdatedAt != occurredAt || actorMemberId != state.ActorMemberId ||
            state.ActorMemberId == Uuid.Empty || !Bounded(state.ActorDisplay, 1,
                MaximumActorDisplayLength) ||
            creating != (_current is null) || creating && revision != 1 ||
            !creating && (_current is null || revision != Revision + 1) ||
            ValidateState(state) is not null)
            throw new InvalidOperationException("The auditor request event state is invalid.");
        _current = FreezeView(state);
    }

    CommandFailure? Emit(AuditorRequestView state, DateTimeOffset occurredAt,
        Func<Uuid, Uuid, long, Uuid, AuditorRequestView, DateTimeOffset, DomainEvent> createEvent)
    {
        var frozen = FreezeView(state);
        var ev = createEvent(_tenantId, Id, frozen.Revision, frozen.ActorMemberId, frozen,
            occurredAt);
        if (JsonSerializer.SerializeToUtf8Bytes(ev, ev.GetType(),
                ComplianceCoreJsonContext.Default).Length > MaximumEventBytes)
            return Invalid("The auditor request would exceed its bounded event payload.");
        RaiseEvent(ev);
        return null;
    }

    AuditorRequestView Next(AuditorRequestView current, string action, string detail,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset occurredAt)
    {
        var revision = Revision + 1;
        return current with
        {
            Revision = revision,
            UpdatedAt = occurredAt,
            ActorMemberId = actorMemberId,
            ActorDisplay = actorDisplay,
            History = Append(current.History, new AuditorRequestHistoryEntry(revision,
                action, detail, actorMemberId, actorDisplay, occurredAt)),
        };
    }

    CommandFailure? CheckRevision(long expectedRevision) => expectedRevision != Revision
        ? CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("auditor request", Revision))
        : null;

    AuditorRequestView? CurrentOrMissing() => _current;

    static AuditorRequestResponseHistoryView? FindResponse(AuditorRequestView state, Uuid responseId) =>
        state.Responses.FirstOrDefault(response => response.ResponseId == responseId);

    static AuditorResponseVersionView CurrentVersion(AuditorRequestResponseHistoryView response) =>
        response.Versions.Single(version => version.Version == response.CurrentVersion);

    static AuditorRequestView ReplaceResponse(AuditorRequestView state,
        AuditorRequestResponseHistoryView response) => state with
        {
            Responses = Array.AsReadOnly(state.Responses.Select(item =>
                item.ResponseId == response.ResponseId ? response : item).ToArray()),
        };

    static ReadOnlyCollection<AuditorResponseVersionView> ReplaceVersion(
        AuditorRequestResponseHistoryView response, AuditorResponseVersionView version) =>
        Array.AsReadOnly(response.Versions.Select(item => item.Version == version.Version
            ? version : item).ToArray());

    static AuditorRequestContent? Normalize(AuditorRequestContent? content)
    {
        if (content is null)
            return null;
        var source = CleanToken(content.Source);
        var externalReference = NormalizeOptional(content.ExternalReference);
        var wording = CleanText(content.Wording);
        var scope = CleanText(content.Scope);
        var priority = CleanToken(content.Priority);
        if (content.ExaminationId == Uuid.Empty || !Sources.Contains(source) ||
            source != "in_product_attest" && externalReference is null ||
            externalReference is { Length: > MaximumReferenceLength } ||
            !Bounded(wording, 1, MaximumBodyLength) || !Bounded(scope, 1, MaximumBodyLength) ||
            !Priorities.Contains(priority) || content.OwnerMemberId == Uuid.Empty ||
            content.DueOn == default)
            return null;
        return content with
        {
            Source = source,
            ExternalReference = externalReference ?? string.Empty,
            Wording = wording,
            Scope = scope,
            Priority = priority,
        };
    }

    static (string Summary, IReadOnlyList<Uuid> EvidenceArtifactIds)? NormalizeResponse(
        string summary, IReadOnlyList<Uuid> evidenceArtifactIds)
    {
        summary = CleanText(summary);
        if (!Bounded(summary, 1, MaximumSummaryLength) || evidenceArtifactIds is null ||
            evidenceArtifactIds.Count > MaximumEvidenceArtifacts ||
            evidenceArtifactIds.Any(static id => id == Uuid.Empty) ||
            evidenceArtifactIds.Distinct().Count() != evidenceArtifactIds.Count)
            return null;
        return (summary, FreezeIds(evidenceArtifactIds));
    }

    static AuditorRequestTermsVersion Terms(int version, AuditorRequestContent content,
        string reason, Uuid actorMemberId, string actorDisplay, DateTimeOffset at) =>
        new(version, content.Wording, content.Scope, content.Priority, content.OwnerMemberId,
            content.DueOn, reason, actorMemberId, actorDisplay, at);

    static bool SameCreate(AuditorRequestView current, AuditorRequestContent? content,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset occurredAt)
    {
        if (Normalize(content) is not { } normalized || current.Revision != 1 ||
            current.ActorMemberId != actorMemberId || current.ActorDisplay != actorDisplay?.Trim() ||
            current.UpdatedAt != occurredAt || current.Status != "open")
            return false;
        return current.ExaminationId == normalized.ExaminationId && current.Source == normalized.Source &&
            current.ExternalReference == normalized.ExternalReference &&
            current.Wording == normalized.Wording && current.Scope == normalized.Scope &&
            current.Priority == normalized.Priority && current.OwnerMemberId == normalized.OwnerMemberId &&
            current.DueOn == normalized.DueOn &&
            current.RequiresInternalReview == normalized.RequiresInternalReview;
    }

    static bool SameDraft(AuditorRequestResponseHistoryView response, string summary,
        IReadOnlyList<Uuid> evidenceIds, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset occurredAt)
    {
        if (response.CurrentVersion != 1 || response.Versions.Count != 1 ||
            NormalizeResponse(summary, evidenceIds) is not { } normalized)
            return false;
        var current = response.Versions[0];
        return current.Status == "draft" && current.Summary == normalized.Summary &&
            current.EvidenceArtifactIds.SequenceEqual(normalized.EvidenceArtifactIds) &&
            current.AuthorMemberId == actorMemberId && current.AuthorDisplay == actorDisplay?.Trim() &&
            current.CreatedAt == occurredAt;
    }

    static bool SameSelection(AuditorSampleSelectionView selection, Uuid populationId,
        string digest, string method, IReadOnlyList<Uuid> rowIds, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset at)
    {
        if (rowIds is null)
            return false;
        var rows = rowIds.OrderBy(static id => id.ToString(), StringComparer.Ordinal).ToArray();
        return selection.PopulationSnapshotId == populationId &&
            selection.PopulationContentSha256 == CleanToken(digest) &&
            selection.SelectionMethod == CleanToken(method) &&
            selection.SelectedRowIds.SequenceEqual(rows) &&
            selection.ActorMemberId == actorMemberId && selection.ActorDisplay == actorDisplay?.Trim() &&
            selection.RecordedAt == at;
    }

    static CommandFailure? ValidateActor(Uuid memberId, string display, DateTimeOffset at) =>
        memberId == Uuid.Empty || !Bounded(CleanText(display), 1, MaximumActorDisplayLength) ||
        at == default
            ? Invalid("An auditor request decision requires a member, display name, and time.")
            : null;

    static CommandFailure? ValidateState(AuditorRequestView state)
    {
        if (state.ExaminationId == Uuid.Empty || !Sources.Contains(state.Source) ||
            !Priorities.Contains(state.Priority) || state.Status is not ("open" or "resolved") ||
            !Bounded(state.ExternalReference, 0, MaximumReferenceLength) ||
            state.Source != "in_product_attest" && !Bounded(state.ExternalReference, 1, MaximumReferenceLength) ||
            !Bounded(state.Wording, 1, MaximumBodyLength) || !Bounded(state.Scope, 1, MaximumBodyLength) ||
            state.OwnerMemberId == Uuid.Empty || state.DueOn == default ||
            state.TermsHistory is null || state.TermsHistory.Count is 0 or > MaximumHistoryEntries ||
            state.Comments is null || state.Comments.Count > MaximumComments ||
            state.Responses is null || state.Responses.Count > MaximumResponses ||
            state.SampleSelections is null || state.SampleSelections.Count > MaximumSampleSelections ||
            state.History is null || state.History.Count != state.Revision ||
            state.History.Count > MaximumHistoryEntries || state.TermsHistory[^1].Wording != state.Wording ||
            state.TermsHistory[^1].Scope != state.Scope || state.TermsHistory[^1].Priority != state.Priority ||
            state.TermsHistory[^1].OwnerMemberId != state.OwnerMemberId ||
            state.TermsHistory[^1].DueOn != state.DueOn ||
            state.TermsHistory.Select(static item => item.Version).Where((version, index) =>
                version != index + 1).Any() ||
            state.History.Select(static item => item.Revision).Where((revision, index) =>
                revision != index + 1).Any() ||
            state.History[^1].ActorMemberId != state.ActorMemberId ||
            state.History[^1].ActorDisplay != state.ActorDisplay ||
            state.History[^1].OccurredAt != state.UpdatedAt ||
            state.History.Any(static item => item.ActorMemberId == Uuid.Empty ||
                !Bounded(item.Action, 1, 64) || !Bounded(item.Detail, 1, MaximumRationaleLength) ||
                !Bounded(item.ActorDisplay, 1, MaximumActorDisplayLength) || item.OccurredAt == default) ||
            state.TermsHistory.Any(static item => item.OwnerMemberId == Uuid.Empty ||
                !Bounded(item.Wording, 1, MaximumBodyLength) || !Bounded(item.Scope, 1, MaximumBodyLength) ||
                !Priorities.Contains(item.Priority) || item.DueOn == default ||
                !Bounded(item.ActorDisplay, 1, MaximumActorDisplayLength) ||
                item.ActorMemberId == Uuid.Empty || item.UpdatedAt == default ||
                item.Version > 1 && !Bounded(item.ChangeReason, 1, MaximumRationaleLength)) ||
            state.Status == "resolved" && (state.Responses.Count == 0 ||
                state.Responses.Any(static response => response.Disposition?.Outcome != "accepted")))
            return Invalid("The auditor request state is structurally invalid.");
        if (state.Responses.Select(static response => response.ResponseId).Distinct().Count() !=
            state.Responses.Count || state.SampleSelections.Select(static selection =>
                selection.SampleSelectionId).Distinct().Count() != state.SampleSelections.Count ||
            state.Comments.Select(static comment => comment.CommentId).Distinct().Count() !=
                state.Comments.Count)
            return Invalid("The auditor request state contains duplicate identities.");
        foreach (var comment in state.Comments)
            if (comment.CommentId == Uuid.Empty || !Bounded(comment.Body, 1, MaximumBodyLength) ||
                !Compartments.Contains(comment.Compartment) || comment.ActorMemberId == Uuid.Empty ||
                !Bounded(comment.ActorDisplay, 1, MaximumActorDisplayLength) || comment.RecordedAt == default)
                return Invalid("A request discussion entry is structurally invalid.");
        foreach (var response in state.Responses)
        {
            if (response.ResponseId == Uuid.Empty || response.Versions is null ||
                response.Versions.Count is 0 or > MaximumResponseVersions ||
                response.CurrentVersion != response.Versions.Count ||
                response.Status is not ("draft" or "submitted" or "approved" or "rejected" or "delivered") ||
                response.Status != response.Versions[^1].Status ||
                response.Versions.Select(static version => version.Version).Where((version, index) =>
                    version != index + 1).Any() ||
                response.DeliveredVersion is { } delivered &&
                    (delivered != response.CurrentVersion || response.Delivery is null) ||
                response.Delivery is { } delivery && (delivery.Version != response.DeliveredVersion ||
                    !Bounded(delivery.Recipient, 1, MaximumReferenceLength) ||
                    !Bounded(delivery.DeliveryReference, 1, MaximumReferenceLength) ||
                    delivery.ActorMemberId == Uuid.Empty ||
                    !Bounded(delivery.ActorDisplay, 1, MaximumActorDisplayLength) ||
                    delivery.DeliveredAt == default) ||
                response.Disposition is { } disposition &&
                    (response.DeliveredVersion != disposition.Version ||
                     !Dispositions.Contains(disposition.Outcome) ||
                     disposition.ActorMemberId == Uuid.Empty ||
                     !Bounded(disposition.ActorDisplay, 1, MaximumActorDisplayLength) ||
                     disposition.RecordedAt == default ||
                     disposition.ExternalReference is { Length: > MaximumReferenceLength }))
                return Invalid("The auditor response state is structurally invalid.");
            foreach (var version in response.Versions)
                if (NormalizeResponse(version.Summary, version.EvidenceArtifactIds) is null ||
                    version.AuthorMemberId == Uuid.Empty ||
                    !Bounded(version.AuthorDisplay, 1, MaximumActorDisplayLength) ||
                    version.Status is not ("draft" or "submitted" or "approved" or "rejected" or "delivered") ||
                    version.Status is "approved" or "rejected" && version.Review is null ||
                    version.Status == "draft" && (version.SubmittedAt is not null ||
                        version.SubmissionNote is not null || version.Review is not null) ||
                    version.Status is "submitted" or "approved" or "rejected" or "delivered" &&
                        (version.SubmittedAt is null || version.SubmissionNote is null) ||
                    version.Status == "delivered" && response.DeliveredVersion != version.Version ||
                    version.Review is { } review &&
                        (review.ReviewerMemberId == version.AuthorMemberId ||
                         review.Outcome is not ("approved" or "rejected") ||
                         !Bounded(review.Rationale, 1, MaximumRationaleLength) ||
                         !Bounded(review.ReviewerDisplay, 1, MaximumActorDisplayLength) ||
                         review.ReviewedAt == default) ||
                    state.RequiresInternalReview && version.Status == "delivered" &&
                        version.Review?.Outcome != "approved")
                    return Invalid("An auditor response version is structurally invalid.");
        }
        foreach (var selection in state.SampleSelections)
            if (selection.SampleSelectionId == Uuid.Empty || selection.PopulationSnapshotId == Uuid.Empty ||
                !ValidDigest(selection.PopulationContentSha256) || !SampleMethods.Contains(selection.SelectionMethod) ||
                selection.SelectedRowIds is null || selection.SelectedRowIds.Count is 0 or > MaximumSampleRows ||
                selection.SelectedRowIds.Any(static id => id == Uuid.Empty) ||
                selection.SelectedRowIds.Distinct().Count() != selection.SelectedRowIds.Count ||
                selection.ActorMemberId == Uuid.Empty ||
                !Bounded(selection.ActorDisplay, 1, MaximumActorDisplayLength) ||
                selection.RecordedAt == default ||
                selection.SelectionContentSha256 != SelectionDigest(selection.PopulationSnapshotId,
                    selection.PopulationContentSha256, selection.SelectionMethod, selection.SelectedRowIds))
                return Invalid("The sample selection state is structurally invalid.");
        return null;
    }

    static AuditorRequestView FreezeView(AuditorRequestView view) => view with
    {
        TermsHistory = Array.AsReadOnly(view.TermsHistory.ToArray()),
        Comments = Array.AsReadOnly(view.Comments.ToArray()),
        Responses = Array.AsReadOnly(view.Responses.Select(static response => response with
        {
            Versions = Array.AsReadOnly(response.Versions.Select(static version => version with
            {
                EvidenceArtifactIds = FreezeIds(version.EvidenceArtifactIds),
            }).ToArray()),
        }).ToArray()),
        SampleSelections = Array.AsReadOnly(view.SampleSelections.Select(static selection => selection with
        {
            SelectedRowIds = FreezeIds(selection.SelectedRowIds),
        }).ToArray()),
        History = Array.AsReadOnly(view.History.ToArray()),
    };

    static ReadOnlyCollection<T> Append<T>(IReadOnlyList<T> list, T item) =>
        Array.AsReadOnly(list.Append(item).ToArray());

    static ReadOnlyCollection<Uuid> FreezeIds(IEnumerable<Uuid> ids) =>
        Array.AsReadOnly(ids.OrderBy(static id => id.ToString(), StringComparer.Ordinal).ToArray());

    static string SelectionDigest(Uuid populationId, string populationDigest, string method,
        IReadOnlyList<Uuid> rows)
    {
        var canonical = string.Join('\n', populationId, populationDigest, method,
            string.Join('\n', rows.OrderBy(static id => id.ToString(), StringComparer.Ordinal)));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    static string? NormalizeOptional(string? value)
    {
        var normalized = CleanText(value);
        return normalized.Length == 0 ? null : normalized;
    }

    static string CleanText(string? value) => (value ?? string.Empty).Trim()
        .Normalize(NormalizationForm.FormC);

    static string CleanToken(string? value) => CleanText(value).ToLowerInvariant();

    static bool Bounded(string value, int minimum, int maximum) =>
        value is not null && value.Length >= minimum && value.Length <= maximum &&
        (minimum == 0 || !string.IsNullOrWhiteSpace(value));

    static bool ValidDigest(string? value) => value is { Length: 64 } && value.All(static character =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static CommandFailure Invalid(string message) => CommandFailure.InvalidContent(message);
}
