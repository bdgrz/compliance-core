using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Kernel;
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

    /// <summary>The authored command identity is the immutable support assertion identity.</summary>
    public static Uuid IdFor(LinkEvidenceSupport request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.CommandId;
    }

    /// <summary>Replays the accepted assertion before selecting mutable artifact or target facts.</summary>
    public Result<EvidenceSupportView>? Replay(LinkEvidenceSupport request, ActorReference actor)
    {
        if (Current is not { } original)
            return null;
        return request.CommandId == Id && request.TenantId == _tenantId && request.ProgramId == _programId &&
               original.OriginalExpectedRevision == request.ExpectedRevision &&
               original.AuthoredIntentSha256 == AuthoredIntentDigest.Compute(request) &&
               actor.Kind == "member" && actor.Id == original.ActorMemberId!.Value.ToString()
            ? Result<EvidenceSupportView>.Success(View()!)
            : Result<EvidenceSupportView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The support identity already accepted another actor or authored intent."));
    }

    /// <summary>The frozen original assertion view, including its original source, actor display and time.</summary>
    public EvidenceSupportView? View() => Current is not { } value ? null : new(value.TenantId, value.ProgramId,
        value.SubjectId, value.ArtifactId, value.ArtifactRevision, value.ArtifactContentSha256!,
        value.SupportedRecordType!, value.SupportedRecordId, value.SupportedRecordRevision,
        value.PeriodStart, value.PeriodEnd, value.Purpose!, value.SufficiencyRationale!,
        value.ActorMemberId!.Value, value.ActorDisplay!, value.OccurredAt);

    public CommandFailure? Link(LinkEvidenceSupport request, long artifactRevision,
        string artifactContentSha256, ActorReference actor, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);
        if (Replay(request, actor) is { } original)
            return original.IsSuccess ? null : CommandFailure.StateConflict(original.Error!.Message!);
        if (request.ExpectedRevision != Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("evidence support", Revision));
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

        RaiseEvent(new EvidenceSupportLinked(_tenantId, Id, 1, actorMemberId, occurredAt)
        {
            OriginalExpectedRevision = request.ExpectedRevision,
            AuthoredIntentSha256 = AuthoredIntentDigest.Compute(request),
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

    static string NormalizeType(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    static bool BoundedText(string? value) => !string.IsNullOrWhiteSpace(value) &&
        value.Trim().Length <= MaximumTextLength;

    static bool Sha256(string? value) => value is { Length: 64 } &&
        value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
