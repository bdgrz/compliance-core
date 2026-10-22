using Bdgrz.Compliance.Hosting.Persistence;
using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>
/// Retains one externally supplied auditor conclusion and report reference. This root never
/// calculates opinion, assurance, or product readiness from the examination's other records.
/// </summary>
public sealed class AuditOutcome : Aggregate
{
    const int MaximumTextLength = 4000;
    const int MaximumEventBytes = DomainEventPayload.MaximumBytes;

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    AuditOutcomeView? _current;

    public long Revision => _current?.Revision ?? 0;
    public AuditOutcomeView? Current => _current;

    public AuditOutcome(Uuid tenantId, Uuid programId, Uuid outcomeId)
        : base(outcomeId, new EventStreamAddress(tenantId.ToString(), "audit-outcomes",
            outcomeId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        On<AuditOutcomeRecorded>(Apply);
    }

    public CommandFailure? Record(AuditOutcomeContent content, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset recordedAt)
    {
        if (_current is { } existing)
            return SameInitial(existing, content, actorMemberId)
                ? null
                : CommandFailure.StateConflict("An audit outcome is immutable once recorded.");
        if (_tenantId == Uuid.Empty || _programId == Uuid.Empty || Id == Uuid.Empty ||
            actorMemberId == Uuid.Empty || !Bounded(actorDisplay, 1, 200) || recordedAt == default)
            return Invalid("An audit outcome requires stable identities and attributed recording.");
        if (NormalizeContent(content) is not { } normalized)
            return Invalid("An audit outcome must retain a valid auditor-supplied conclusion and exact report/package provenance.");

        var state = new AuditOutcomeView(_tenantId, _programId, Id, normalized, "recorded",
            1, actorMemberId, actorDisplay.Trim(), recordedAt, recordedAt, Digest(normalized));
        var ev = new AuditOutcomeRecorded(_tenantId, Id, 1, actorMemberId, state, recordedAt);
        if (!Fits(ev))
            return Invalid("The complete audit outcome exceeds the bounded event payload.");
        RaiseEvent(ev);
        return null;
    }

    void Apply(AuditOutcomeRecorded ev)
    {
        if (ev.TenantId != _tenantId || ev.SubjectId != Id || ev.State is null ||
            ev.State.TenantId != _tenantId || ev.State.ProgramId != _programId ||
            ev.State.AuditOutcomeId != Id || ev.Revision != Revision + 1 || ev.Revision != 1 ||
            ev.State.Revision != ev.Revision || ev.State.Status != "recorded" ||
            ev.ActorMemberId is not { } actor || actor == Uuid.Empty ||
            ev.State.RecordedByMemberId != actor || ev.State.RecordedAt != ev.OccurredAt ||
            ev.State.UpdatedAt != ev.OccurredAt || ev.OccurredAt == default ||
            NormalizeContent(ev.State.Content) is not { } normalized ||
            normalized != ev.State.Content || ev.State.ContentSha256 != Digest(normalized))
            throw new InvalidOperationException("The audit outcome event is invalid.");
        _current = ev.State with { Content = normalized };
    }

    static AuditOutcomeContent? NormalizeContent(AuditOutcomeContent? content)
    {
        if (content is null || content.ExaminationId == Uuid.Empty ||
            content.ScopeKind is not ("type_i" or "type_ii") ||
            content.SourceSnapshotId == Uuid.Empty || !Sha256(content.SourceSnapshotSha256) ||
            content.PackageId == Uuid.Empty || content.PackageVersion <= 0 ||
            content.PackageManifestId == Uuid.Empty || !Sha256(content.PackageManifestSha256) ||
            content.SourceKind != "auditor" || !Token(content.OutcomeCode, 100) ||
            !Bounded(content.OutcomeSummary, 1, MaximumTextLength) ||
            !Bounded(content.ReportReference, 1, 500) || content.IssuedOn == default ||
            (content.ReportArtifactId is null) != (content.ReportArtifactSha256 is null) ||
            content.ReportArtifactId == Uuid.Empty ||
            content.ReportArtifactSha256 is not null && !Sha256(content.ReportArtifactSha256))
            return null;
        return content with
        {
            ScopeKind = content.ScopeKind.Trim(),
            SourceSnapshotSha256 = NormalizeHash(content.SourceSnapshotSha256),
            PackageManifestSha256 = NormalizeHash(content.PackageManifestSha256),
            OutcomeCode = content.OutcomeCode.Trim().ToLowerInvariant(),
            OutcomeSummary = content.OutcomeSummary.Trim(),
            ReportReference = content.ReportReference.Trim(),
            ReportArtifactSha256 = content.ReportArtifactSha256 is null
                ? null : NormalizeHash(content.ReportArtifactSha256),
        };
    }

    static bool SameInitial(AuditOutcomeView state, AuditOutcomeContent? content, Uuid actor) =>
        NormalizeContent(content) is { } normalized && Digest(normalized) == state.ContentSha256 &&
        actor == state.RecordedByMemberId;

    static string Digest<T>(T content) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(content))).ToLowerInvariant();
    static bool Fits(DomainEvent ev) => DomainEventPayload.Size(ev) <= MaximumEventBytes;
    static CommandFailure Invalid(string message) => CommandFailure.InvalidContent(message);
    static bool Bounded(string? value, int minimum, int maximum) => value is not null &&
        value.Trim().Length >= minimum && value.Trim().Length <= maximum;
    static bool Token(string? value, int maximum) => Bounded(value, 1, maximum) &&
        value!.Trim().All(static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');
    static bool Sha256(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
    static string NormalizeHash(string value) => value.ToLowerInvariant();
}
