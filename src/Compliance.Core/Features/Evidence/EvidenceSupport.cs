using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Owns one immutable, period-aware assertion linking an artifact to an exact source revision.</summary>
public sealed class EvidenceSupport : Aggregate
{
    const int MaximumTextLength = 2000;
    static readonly HashSet<string> SupportedTypes = new(StringComparer.Ordinal)
    {
        "control", "control_version", "control_occurrence", "control_evaluation", "period"
    };

    readonly Uuid _tenantId;
    readonly Uuid _programId;

    public EvidenceSupportLinked? Current { get; private set; }
    public long Revision => Current?.Revision ?? 0;

    public EvidenceSupport(Uuid tenantId, Uuid programId, Uuid supportId)
        : base(supportId, new EventStreamAddress(tenantId.ToString(), "evidence-support",
            supportId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        On<EvidenceSupportLinked>(Apply);
    }

    /// <summary>Derives a stable relationship identity from the exact artifact, target and covered period.</summary>
    public static Uuid IdFor(LinkEvidenceSupport request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var recordType = NormalizeType(request.SupportedRecordType);
        var identity = string.Join('\n', "evidence-support-v1", request.ProgramId,
            request.ArtifactId, recordType, request.SupportedRecordId,
            request.SupportedRecordRevision.ToString(CultureInfo.InvariantCulture),
            request.PeriodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            request.PeriodEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        return Uuid.CreateVersion5(request.TenantId, identity);
    }

    public CommandFailure? Link(LinkEvidenceSupport request, long artifactRevision,
        string artifactContentSha256, ActorReference actor, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);
        var recordType = NormalizeType(request.SupportedRecordType);
        var purpose = request.Purpose?.Trim() ?? string.Empty;
        var rationale = request.SufficiencyRationale?.Trim() ?? string.Empty;
        if (_tenantId == Uuid.Empty || _programId == Uuid.Empty || Id == Uuid.Empty ||
            request.TenantId != _tenantId || request.ProgramId != _programId ||
            Id != IdFor(request) || request.ArtifactId == Uuid.Empty ||
            artifactRevision <= 0 || !Sha256(artifactContentSha256) ||
            !SupportedTypes.Contains(recordType) || request.SupportedRecordId == Uuid.Empty ||
            request.SupportedRecordRevision <= 0 || request.PeriodStart == default ||
            request.PeriodEnd == default || request.PeriodStart > request.PeriodEnd ||
            !BoundedText(purpose) || !BoundedText(rationale) ||
            actor is not { Kind: "member", Id.Length: > 0, Display.Length: > 0 and <= 200 } ||
            !Uuid.TryParse(actor.Id, out var actorMemberId) || actorMemberId == Uuid.Empty ||
            occurredAt == default)
            return CommandFailure.InvalidContent(
                "Evidence support requires an exact source revision, valid covered period, and attributed rationale.");

        if (Current is { } existing)
            return SameRelationship(existing, request, artifactRevision, artifactContentSha256,
                    recordType, purpose, rationale, actorMemberId, actor.Display.Trim())
                ? null
                : CommandFailure.StateConflict(
                    "The evidence support identity is already linked with different facts.");

        RaiseEvent(new EvidenceSupportLinked(_tenantId, Id, 1, actorMemberId, occurredAt)
        {
            ProgramId = _programId,
            ArtifactId = request.ArtifactId,
            ArtifactRevision = artifactRevision,
            ArtifactContentSha256 = artifactContentSha256,
            SupportedRecordType = recordType,
            SupportedRecordId = request.SupportedRecordId,
            SupportedRecordRevision = request.SupportedRecordRevision,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            Purpose = purpose,
            SufficiencyRationale = rationale,
            ActorDisplay = actor.Display.Trim(),
        });
        return null;
    }

    void Apply(EvidenceSupportLinked ev)
    {
        var recordType = NormalizeType(ev.SupportedRecordType);
        if (Current is not null || ev.TenantId != _tenantId || ev.SubjectId != Id ||
            ev.ProgramId != _programId || ev.Revision != 1 || ev.ArtifactId == Uuid.Empty ||
            ev.ArtifactRevision <= 0 || !Sha256(ev.ArtifactContentSha256) ||
            !SupportedTypes.Contains(recordType) || ev.SupportedRecordType != recordType ||
            ev.SupportedRecordId == Uuid.Empty || ev.SupportedRecordRevision <= 0 ||
            ev.PeriodStart == default || ev.PeriodEnd == default || ev.PeriodStart > ev.PeriodEnd ||
            !BoundedText(ev.Purpose) || ev.Purpose!.Trim() != ev.Purpose ||
            !BoundedText(ev.SufficiencyRationale) || ev.SufficiencyRationale!.Trim() != ev.SufficiencyRationale ||
            ev.ActorMemberId is not { } actorMemberId || actorMemberId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(ev.ActorDisplay) || ev.ActorDisplay.Trim() != ev.ActorDisplay ||
            ev.ActorDisplay.Length > 200 || ev.OccurredAt == default)
            throw new InvalidOperationException("The evidence support link event is invalid.");
        Current = ev;
    }

    static bool SameRelationship(EvidenceSupportLinked existing, LinkEvidenceSupport request,
        long artifactRevision, string artifactContentSha256, string recordType, string purpose,
        string rationale, Uuid actorMemberId, string actorDisplay) =>
        existing.TenantId == request.TenantId && existing.ProgramId == request.ProgramId &&
        existing.ArtifactId == request.ArtifactId && existing.ArtifactRevision == artifactRevision &&
        existing.ArtifactContentSha256 == artifactContentSha256 &&
        existing.SupportedRecordType == recordType &&
        existing.SupportedRecordId == request.SupportedRecordId &&
        existing.SupportedRecordRevision == request.SupportedRecordRevision &&
        existing.PeriodStart == request.PeriodStart && existing.PeriodEnd == request.PeriodEnd &&
        existing.Purpose == purpose && existing.SufficiencyRationale == rationale &&
        existing.ActorMemberId == actorMemberId && existing.ActorDisplay == actorDisplay;

    static string NormalizeType(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    static bool BoundedText(string? value) => !string.IsNullOrWhiteSpace(value) &&
        value.Trim().Length <= MaximumTextLength;

    static bool Sha256(string? value) => value is { Length: 64 } &&
        value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
