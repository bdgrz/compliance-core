using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>Metadata about an evidence artifact linked to a control affecting an application.</summary>
public sealed record ApplicationEvidenceSourceReferenceView(Uuid TenantId, Uuid ProgramId,
    Uuid EvidenceSupportId, Uuid ControlId, string SupportedRecordType,
    long SupportedRecordRevision, Uuid ArtifactId, long ArtifactRevision,
    DateOnly PeriodStart, DateOnly PeriodEnd, string Purpose);
