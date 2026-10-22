using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Received auditor wording and provenance, separate from management's response and remediation.</summary>
public sealed record AuditObservationContent
{
    readonly IReadOnlyList<ExaminationContentReference> _affectedRecords = [];

    public Uuid ExaminationEngagementId { get; init; }
    public string Kind { get; init; }
    public string SourceKind { get; init; }
    public string SourceReference { get; init; }
    public string AuditorAuthor { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
    public string Wording { get; init; }
    public string Severity { get; init; }
    public ExaminationContentReference SourceSnapshot { get; init; }
    public IReadOnlyList<ExaminationContentReference> AffectedRecords
    {
        get => _affectedRecords;
        init => _affectedRecords = value is null ? null! : Array.AsReadOnly(value.ToArray());
    }

    public AuditObservationContent(Uuid examinationEngagementId, string kind, string sourceKind,
        string sourceReference, string auditorAuthor, DateTimeOffset receivedAt, string wording,
        string severity, ExaminationContentReference sourceSnapshot,
        IReadOnlyList<ExaminationContentReference> affectedRecords)
    {
        ExaminationEngagementId = examinationEngagementId;
        Kind = kind;
        SourceKind = sourceKind;
        SourceReference = sourceReference;
        AuditorAuthor = auditorAuthor;
        ReceivedAt = receivedAt;
        Wording = wording;
        Severity = severity;
        SourceSnapshot = sourceSnapshot;
        AffectedRecords = affectedRecords;
    }
}
