using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>Metadata about an evidence artifact linked to a control affecting an application.</summary>
public sealed record ApplicationEvidenceSourceReferenceView(Uuid TenantId, Uuid ProgramId,
    Uuid EvidenceSupportId, Uuid ControlId, string SupportedRecordType,
    long SupportedRecordRevision, Uuid ArtifactId, long ArtifactRevision,
    DateOnly PeriodStart, DateOnly PeriodEnd, string Purpose)
{
    /// <summary>The authoritative current status, enriched after exact artifact discovery is authorized.</summary>
    public EvidenceSupportStatusView? SupportStatus { get; init; }
}
