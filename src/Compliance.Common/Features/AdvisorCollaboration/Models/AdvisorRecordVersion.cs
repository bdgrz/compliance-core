using System.Text.Json.Serialization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Models;

/// <summary>
///     An exact selected source version. Legacy UUID pairs remain readable for previously
///     recorded handoffs; new selections preserve each source family's native version identity.
/// </summary>
public sealed record AdvisorRecordVersion(Uuid RecordId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] Uuid VersionId)
{
    public const string Legacy = "legacy";
    public const string ControlDraftKind = "control_draft";
    public const string ControlVersionKind = "control_version";
    public const string PolicyDraftKind = "policy_draft";
    public const string PolicyVersionKind = "policy_version";
    public const string EvidenceArtifactKind = "evidence_artifact";
    public const string ReadinessGapKind = "readiness_gap";
    public const string BoundaryQuestionKind = "boundary_question";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceKind { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Revision { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? VersionNumber { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContentSha256 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ulong? SourcePosition { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uuid? AssessmentId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InputFingerprint { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uuid? EntryId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uuid? EvidenceSupportId { get; init; }

    [JsonIgnore]
    public string SourceFamily => (SourceKind ?? Legacy) switch
    {
        ControlDraftKind or ControlVersionKind => "control",
        PolicyDraftKind or PolicyVersionKind => "policy",
        _ => SourceKind ?? Legacy,
    };

    [JsonIgnore]
    public bool IsSupported => HasValidIdentity && SourceKind is not null && SourceKind != Legacy;

    [JsonIgnore]
    public bool HasValidIdentity
    {
        get
        {
            if (RecordId == Uuid.Empty)
                return false;
            return (SourceKind ?? Legacy) switch
            {
                Legacy => VersionId != Uuid.Empty && Revision is null && VersionNumber is null &&
                          ContentSha256 is null && SourcePosition is null && AssessmentId is null &&
                          InputFingerprint is null && EntryId is null && EvidenceSupportId is null,
                ControlDraftKind or ControlVersionKind => VersionId != Uuid.Empty && Revision is > 0 &&
                    VersionNumber is null && ContentSha256 is null && SourcePosition is null &&
                    AssessmentId is null && InputFingerprint is null && EntryId is null &&
                    EvidenceSupportId is null,
                PolicyDraftKind => VersionId == Uuid.Empty && Revision is > 0 && VersionNumber is null &&
                    ContentSha256 is null && SourcePosition is null && AssessmentId is null &&
                    InputFingerprint is null && EntryId is null && EvidenceSupportId is null,
                PolicyVersionKind => VersionId == Uuid.Empty && Revision is > 0 && VersionNumber is > 0 &&
                    ContentSha256 is null && SourcePosition is null && AssessmentId is null &&
                    InputFingerprint is null && EntryId is null && EvidenceSupportId is null,
                EvidenceArtifactKind => VersionId == Uuid.Empty && Revision is > 0 &&
                    VersionNumber is null && IsSha256(ContentSha256) && SourcePosition is > 0 &&
                    AssessmentId is null && InputFingerprint is null && EntryId is null &&
                    EvidenceSupportId is { } supportId && supportId != Uuid.Empty,
                ReadinessGapKind => VersionId == Uuid.Empty && Revision is null && VersionNumber is null &&
                    ContentSha256 is null && SourcePosition is null && AssessmentId is { } assessmentId &&
                    assessmentId != Uuid.Empty && IsSha256(InputFingerprint) && EntryId is null &&
                    EvidenceSupportId is null,
                BoundaryQuestionKind => VersionId != Uuid.Empty && Revision is > 0 &&
                    VersionNumber is null && ContentSha256 is null && SourcePosition is null &&
                    AssessmentId is null && InputFingerprint is null && EntryId is { } entryId &&
                    entryId != Uuid.Empty && EvidenceSupportId is null,
                _ => false,
            };
        }
    }

    public static AdvisorRecordVersion ControlDraft(Uuid controlId, Uuid draftVersionId,
        long revision) => new(controlId, draftVersionId)
        {
            SourceKind = ControlDraftKind,
            Revision = revision,
        };

    public static AdvisorRecordVersion ControlVersion(Uuid controlId, Uuid versionId,
        long revision) => new(controlId, versionId)
        {
            SourceKind = ControlVersionKind,
            Revision = revision,
        };

    public static AdvisorRecordVersion PolicyDraft(Uuid policyId, long revision) =>
        new(policyId, Uuid.Empty)
        {
            SourceKind = PolicyDraftKind,
            Revision = revision,
        };

    public static AdvisorRecordVersion PolicyVersion(Uuid policyId, long version,
        long revision) => new(policyId, Uuid.Empty)
        {
            SourceKind = PolicyVersionKind,
            VersionNumber = version,
            Revision = revision,
        };

    public static AdvisorRecordVersion EvidenceArtifact(Uuid artifactId, long revision,
        ulong sourcePosition, string contentSha256, Uuid evidenceSupportId) =>
        new(artifactId, Uuid.Empty)
        {
            SourceKind = EvidenceArtifactKind,
            Revision = revision,
            SourcePosition = sourcePosition,
            ContentSha256 = contentSha256,
            EvidenceSupportId = evidenceSupportId,
        };

    public static AdvisorRecordVersion ReadinessGap(Uuid gapId, Uuid assessmentId,
        string inputFingerprint) => new(gapId, Uuid.Empty)
        {
            SourceKind = ReadinessGapKind,
            AssessmentId = assessmentId,
            InputFingerprint = inputFingerprint,
        };

    public static AdvisorRecordVersion BoundaryQuestion(Uuid boundaryId, Uuid entryId,
        Uuid versionId, long revision) => new(boundaryId, versionId)
        {
            SourceKind = BoundaryQuestionKind,
            EntryId = entryId,
            Revision = revision,
        };

    static bool IsSha256(string? value) => value is { Length: 64 } &&
        value.All(char.IsAsciiHexDigit);
}
