using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Incidents.Models;

/// <summary>An exact affected scope, risk, vendor, control, evidence, or system-description record.</summary>
public sealed record ComplianceIncidentAffectedRecordReference(
    string RecordType,
    Uuid RecordId,
    Uuid VersionId,
    long Version);
