using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
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
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Rules;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Events;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Examinations.Aggregates;

/// <summary>
/// Owns one received auditor observation or exception. History remains in its stream; only the
/// current source, latest response/link, and selected command's receipt are retained in memory.
/// Source existence and recording authority must be established by a consuming handler.
/// </summary>
public sealed class AuditObservation : Aggregate
{
    const int MaximumTextLength = 4000;
    const int MaximumAffectedRecords = 64;
    const int MaximumEventBytes = 32 * 1024;
    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid? _commandId;
    AuditObservationStateView? _current;
    DomainEvent? _receipt;

    public long Revision => _current?.Revision ?? 0;
    public AuditObservationStateView? Current => _current;

    public AuditObservation(Uuid tenantId, Uuid programId, Uuid observationId, Uuid? commandId = null)
        : base(observationId, new EventStreamAddress(tenantId.ToString(), "audit-observations", observationId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        _commandId = commandId;
        On<AuditObservationRecorded>(Apply);
        On<AuditObservationCorrected>(Apply);
        On<AuditObservationManagementResponseRecorded>(Apply);
        On<AuditObservationFindingLinked>(Apply);
    }

    public CommandFailure? Record(Uuid requestId, AuditObservationContent content,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset recordedAt)
    {
        if (CheckCommand(requestId) is { } commandFailure)
            return commandFailure;
        if (Normalize(content) is not { } normalized)
            return Invalid("An observation requires received auditor wording and exact source references.");
        if (_receipt is { } receipt)
            return receipt is AuditObservationRecorded recorded &&
                   recorded.ActorMemberId == actorMemberId && SameContent(recorded.Received.Content, normalized)
                ? null : ChangedRequest();
        if (_current is not null)
            return CommandFailure.StateConflict("The received observation already exists; record an auditor correction.");
        if (!ValidActor(actorMemberId, actorDisplay, recordedAt) || normalized.ReceivedAt > recordedAt)
            return Invalid("An observation requires valid recording attribution after its receipt time.");
        var received = new AuditObservationReceivedVersion(1, requestId, normalized, Digest(normalized),
            actorMemberId, actorDisplay.Trim(), recordedAt);
        return Emit(new AuditObservationRecorded(_tenantId, Id, 1, actorMemberId, _programId, received, recordedAt));
    }

    public CommandFailure? Correct(Uuid requestId, long expectedRevision, int previousSourceVersion,
        AuditObservationContent content, string reason, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset recordedAt)
    {
        if (CheckCommand(requestId) is { } commandFailure)
            return commandFailure;
        if (Normalize(content) is not { } normalized || !Text(reason, MaximumTextLength))
            return Invalid("An auditor correction requires received source content and a reason.");
        var cleanReason = reason.Trim();
        if (_receipt is { } receipt)
            return receipt is AuditObservationCorrected corrected && corrected.Revision - 1 == expectedRevision &&
                   corrected.PreviousSourceVersion == previousSourceVersion && corrected.Reason == cleanReason &&
                   corrected.ActorMemberId == actorMemberId && SameContent(corrected.Received.Content, normalized)
                ? null : ChangedRequest();
        if (CheckCurrent(expectedRevision) is { } currentFailure)
            return currentFailure;
        var current = _current!;
        if (previousSourceVersion != current.Received.Version)
            return CommandFailure.StateConflict("An auditor correction must name the current received version.");
        if (!ValidActor(actorMemberId, actorDisplay, recordedAt) || recordedAt < current.UpdatedAt ||
            normalized.ReceivedAt > recordedAt || normalized.ReceivedAt < current.Received.Content.ReceivedAt ||
            !SameExamination(current.Received.Content, normalized) ||
            SameContent(current.Received.Content, normalized) || previousSourceVersion == int.MaxValue)
            return Invalid("A correction must preserve the examination and snapshot, identify changed received facts, and follow the current source.");
        var received = new AuditObservationReceivedVersion(previousSourceVersion + 1, requestId,
            normalized, Digest(normalized), actorMemberId, actorDisplay.Trim(), recordedAt);
        return Emit(new AuditObservationCorrected(_tenantId, Id, Revision + 1, actorMemberId,
            _programId, previousSourceVersion, received, cleanReason, recordedAt));
    }

    public CommandFailure? Respond(Uuid requestId, long expectedRevision, int sourceVersion,
        string text, Uuid actorMemberId, string actorDisplay, DateTimeOffset recordedAt)
    {
        if (CheckCommand(requestId) is { } commandFailure)
            return commandFailure;
        if (!Text(text, MaximumTextLength))
            return Invalid("A management response requires bounded text.");
        var cleanText = text.Trim();
        if (_receipt is { } receipt)
            return receipt is AuditObservationManagementResponseRecorded responded &&
                   responded.Revision - 1 == expectedRevision && responded.Response.SourceVersion == sourceVersion &&
                   responded.Response.Text == cleanText && responded.ActorMemberId == actorMemberId
                ? null : ChangedRequest();
        if (CheckCurrent(expectedRevision) is { } currentFailure)
            return currentFailure;
        if (sourceVersion != _current!.Received.Version ||
            !ValidActor(actorMemberId, actorDisplay, recordedAt) || recordedAt < _current.UpdatedAt)
            return Invalid("A management response must identify the current received version and attributed recording time.");
        var response = new AuditObservationManagementResponseView(requestId, sourceVersion, cleanText,
            actorMemberId, actorDisplay.Trim(), recordedAt);
        return Emit(new AuditObservationManagementResponseRecorded(_tenantId, Id, Revision + 1,
            actorMemberId, _programId, response, recordedAt));
    }

    public CommandFailure? LinkFinding(Uuid requestId, long expectedRevision,
        AuditObservationFindingReference finding, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset recordedAt)
    {
        if (CheckCommand(requestId) is { } commandFailure)
            return commandFailure;
        if (_receipt is { } receipt)
            return receipt is AuditObservationFindingLinked linked && linked.Revision - 1 == expectedRevision &&
                   linked.ActorMemberId == actorMemberId && linked.Link.Finding == finding
                ? null : ChangedRequest();
        if (CheckCurrent(expectedRevision) is { } currentFailure)
            return currentFailure;
        if (!ValidFinding(finding, _current!.Received) ||
            !ValidActor(actorMemberId, actorDisplay, recordedAt) || recordedAt < _current.UpdatedAt)
            return Invalid("A finding link must identify this exact received source version in the same tenant and program.");
        var link = new AuditObservationFindingLinkView(requestId, _current.Received.Version, finding,
            actorMemberId, actorDisplay.Trim(), recordedAt);
        return Emit(new AuditObservationFindingLinked(_tenantId, Id, Revision + 1,
            actorMemberId, _programId, link, recordedAt));
    }

    void Apply(AuditObservationRecorded ev)
    {
        if (_current is not null || ev.Revision != 1 ||
            !ValidEnvelope(ev.TenantId, ev.SubjectId, ev.ProgramId, ev.Revision, ev.ActorMemberId,
                ev.OccurredAt, ev, ComplianceCoreJsonContext.Default.AuditObservationRecorded) ||
            !ValidReceived(ev.Received, 1, ev.ActorMemberId, ev.OccurredAt))
            throw InvalidEvent();
        Remember(ev.Received.RequestId, ev with { Received = Freeze(ev.Received) });
        _current = new(_tenantId, _programId, Id, 1, Freeze(ev.Received), null, null, ev.OccurredAt);
    }

    void Apply(AuditObservationCorrected ev)
    {
        if (_current is not { } current ||
            !ValidEnvelope(ev.TenantId, ev.SubjectId, ev.ProgramId, ev.Revision, ev.ActorMemberId,
                ev.OccurredAt, ev, ComplianceCoreJsonContext.Default.AuditObservationCorrected) ||
            current.Received.Version == int.MaxValue || ev.PreviousSourceVersion != current.Received.Version ||
            !ValidReceived(ev.Received, current.Received.Version + 1, ev.ActorMemberId, ev.OccurredAt) ||
            !Text(ev.Reason, MaximumTextLength) || ev.Reason != ev.Reason.Trim() ||
            ev.Received.Content.ReceivedAt < current.Received.Content.ReceivedAt ||
            !SameExamination(current.Received.Content, ev.Received.Content) ||
            SameContent(current.Received.Content, ev.Received.Content))
            throw InvalidEvent();
        Remember(ev.Received.RequestId, ev with { Received = Freeze(ev.Received) });
        _current = current with { Revision = ev.Revision, Received = Freeze(ev.Received), UpdatedAt = ev.OccurredAt };
    }

    void Apply(AuditObservationManagementResponseRecorded ev)
    {
        if (_current is not { } current ||
            !ValidEnvelope(ev.TenantId, ev.SubjectId, ev.ProgramId, ev.Revision, ev.ActorMemberId,
                ev.OccurredAt, ev, ComplianceCoreJsonContext.Default.AuditObservationManagementResponseRecorded) ||
            ev.Response is not { } response || response.RequestId == Uuid.Empty ||
            response.SourceVersion != current.Received.Version || !Text(response.Text, MaximumTextLength) ||
            response.Text != response.Text.Trim() || response.RecordedAt != ev.OccurredAt ||
            response.RecordedByMemberId != ev.ActorMemberId ||
            !ValidActor(response.RecordedByMemberId, response.RecordedByDisplay, response.RecordedAt) ||
            response.RecordedByDisplay != response.RecordedByDisplay.Trim())
            throw InvalidEvent();
        Remember(response.RequestId, ev);
        _current = current with { Revision = ev.Revision, LatestManagementResponse = response, UpdatedAt = ev.OccurredAt };
    }

    void Apply(AuditObservationFindingLinked ev)
    {
        if (_current is not { } current ||
            !ValidEnvelope(ev.TenantId, ev.SubjectId, ev.ProgramId, ev.Revision, ev.ActorMemberId,
                ev.OccurredAt, ev, ComplianceCoreJsonContext.Default.AuditObservationFindingLinked) ||
            ev.Link is not { } link || link.RequestId == Uuid.Empty ||
            link.SourceVersion != current.Received.Version || !ValidFinding(link.Finding, current.Received) ||
            link.RecordedAt != ev.OccurredAt || link.RecordedByMemberId != ev.ActorMemberId ||
            !ValidActor(link.RecordedByMemberId, link.RecordedByDisplay, link.RecordedAt) ||
            link.RecordedByDisplay != link.RecordedByDisplay.Trim())
            throw InvalidEvent();
        Remember(link.RequestId, ev);
        _current = current with { Revision = ev.Revision, LatestFindingLink = link, UpdatedAt = ev.OccurredAt };
    }

    bool ValidEnvelope<T>(Uuid tenantId, Uuid subjectId, Uuid programId, long revision,
        Uuid? actor, DateTimeOffset at, T ev, JsonTypeInfo<T> jsonType) =>
        tenantId == _tenantId && subjectId == Id && programId == _programId &&
        _tenantId != Uuid.Empty && _programId != Uuid.Empty && Id != Uuid.Empty &&
        actor is { } member && member != Uuid.Empty && revision == Revision + 1 &&
        at != default && (_current is null || at >= _current.UpdatedAt) &&
        JsonSerializer.SerializeToUtf8Bytes(ev, jsonType).Length <= MaximumEventBytes;

    static bool ValidReceived(AuditObservationReceivedVersion? received, int version,
        Uuid? actor, DateTimeOffset at) => received is not null && received.RequestId != Uuid.Empty &&
        received.Version == version && received.RecordedByMemberId == actor && received.RecordedAt == at &&
        ValidActor(received.RecordedByMemberId, received.RecordedByDisplay, at) &&
        received.RecordedByDisplay == received.RecordedByDisplay.Trim() &&
        Normalize(received.Content) is { } normalized && received.Content.ReceivedAt <= at &&
        received.ContentSha256 == Digest(normalized) && received.ContentSha256 == Digest(received.Content);

    bool ValidFinding(AuditObservationFindingReference? finding, AuditObservationReceivedVersion received) =>
        finding is { FindingId: var findingId, Revision: > 0, Source: { } source } && findingId != Uuid.Empty &&
        finding.TenantId == _tenantId && finding.ProgramId == _programId &&
        source.Kind == "examination_item" && source.RecordId == Id &&
        source.Version == received.Version.ToString(CultureInfo.InvariantCulture) &&
        source.SourceText == received.Content.Wording;

    CommandFailure? CheckCommand(Uuid requestId) => requestId == Uuid.Empty || _commandId != requestId ||
        _tenantId == Uuid.Empty || _programId == Uuid.Empty || Id == Uuid.Empty
        ? Invalid("A mutation requires hydration with its exact nonempty command identity.") : null;

    CommandFailure? CheckCurrent(long expectedRevision) => _current is null
        ? CommandFailure.MissingRecord("The received observation was not found.")
        : expectedRevision != Revision
            ? CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("audit observation", Revision)) : null;

    void Remember(Uuid requestId, DomainEvent ev)
    {
        if (_commandId != requestId)
            return;
        if (_receipt is not null)
            throw InvalidEvent();
        _receipt = ev;
    }

    CommandFailure? Emit(AuditObservationRecorded ev) => Emit(ev, ComplianceCoreJsonContext.Default.AuditObservationRecorded);
    CommandFailure? Emit(AuditObservationCorrected ev) => Emit(ev, ComplianceCoreJsonContext.Default.AuditObservationCorrected);
    CommandFailure? Emit(AuditObservationManagementResponseRecorded ev) => Emit(ev, ComplianceCoreJsonContext.Default.AuditObservationManagementResponseRecorded);
    CommandFailure? Emit(AuditObservationFindingLinked ev) => Emit(ev, ComplianceCoreJsonContext.Default.AuditObservationFindingLinked);

    CommandFailure? Emit<T>(T ev, JsonTypeInfo<T> jsonType) where T : DomainEvent
    {
        if (JsonSerializer.SerializeToUtf8Bytes(ev, jsonType).Length > MaximumEventBytes)
            return Invalid("The received observation event exceeds its bounded serialized payload.");
        RaiseEvent(ev);
        return null;
    }

    static AuditObservationContent? Normalize(AuditObservationContent? content)
    {
        if (content is null || content.ExaminationEngagementId == Uuid.Empty ||
            content.Kind is not ("observation" or "exception") || content.SourceKind != "auditor" ||
            !Text(content.SourceReference, 1000) || !Text(content.AuditorAuthor, 200) ||
            content.ReceivedAt == default || !Text(content.Wording, MaximumTextLength) ||
            content.Wording.Length > MaximumTextLength || !Text(content.Severity, 100) ||
            !ValidReference(content.SourceSnapshot) || content.SourceSnapshot.SourceType is not ("type_i_baseline" or "type_ii_close") ||
            content.AffectedRecords is null || content.AffectedRecords.Count > MaximumAffectedRecords ||
            content.AffectedRecords.Any(static reference => !ValidReference(reference)))
            return null;
        var references = content.AffectedRecords.OrderBy(static reference => reference.SourceType, StringComparer.Ordinal)
            .ThenBy(static reference => reference.RecordId.ToString(), StringComparer.Ordinal)
            .ThenBy(static reference => reference.VersionId.ToString(), StringComparer.Ordinal)
            .ThenBy(static reference => reference.Version).ToArray();
        if (references.Select(static reference => (reference.SourceType, reference.RecordId, reference.VersionId, reference.Version))
            .Distinct().Count() != references.Length)
            return null;
        return content with
        {
            SourceReference = content.SourceReference.Trim(),
            AuditorAuthor = content.AuditorAuthor.Trim(),
            Severity = content.Severity.Trim(),
            AffectedRecords = Array.AsReadOnly(references),
        };
    }

    static bool ValidReference(ExaminationContentReference? reference) => reference is not null &&
        Text(reference.SourceType, 100) && reference.SourceType == reference.SourceType.Trim() &&
        reference.RecordId != Uuid.Empty && reference.VersionId != Uuid.Empty && reference.Version > 0 &&
        reference.ContentSha256 is { Length: 64 } hash && hash.All(static character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static AuditObservationReceivedVersion Freeze(AuditObservationReceivedVersion received) =>
        received with { Content = Normalize(received.Content)! };

    static bool SameExamination(AuditObservationContent left, AuditObservationContent right) =>
        left.ExaminationEngagementId == right.ExaminationEngagementId && left.SourceSnapshot == right.SourceSnapshot;

    static bool SameContent(AuditObservationContent left, AuditObservationContent right) => Digest(left) == Digest(right);

    static string Digest(AuditObservationContent content) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(content, ComplianceCoreJsonContext.Default.AuditObservationContent)))
        .ToLowerInvariant();

    static bool ValidActor(Uuid memberId, string? display, DateTimeOffset at) =>
        memberId != Uuid.Empty && Text(display, 200) && at != default;

    static bool Text(string? value, int maximum) => value is not null && value.Trim().Length is > 0 && value.Trim().Length <= maximum;
    static CommandFailure Invalid(string message) => CommandFailure.InvalidContent(message);
    static CommandFailure ChangedRequest() => CommandFailure.StateConflict("The command identity already records different observation facts.");
    static InvalidOperationException InvalidEvent() => new("The received auditor observation event is invalid.");
}
