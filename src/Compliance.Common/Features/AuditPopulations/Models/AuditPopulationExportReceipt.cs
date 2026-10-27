namespace Bdgrz.Compliance.Features.AuditPopulations.Models;

/// <summary>Export receipt that identifies governed evidence without issuing an unscoped download location.</summary>
public sealed record AuditPopulationExportReceipt(
    AuditPopulationExportView Export,
    string ArtifactState,
    string? ArtifactStateReason);
