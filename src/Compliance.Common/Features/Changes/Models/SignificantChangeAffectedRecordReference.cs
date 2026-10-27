using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>An exact affected boundary, commitment, control, inventory, risk, vendor, or description record.</summary>
public sealed record SignificantChangeAffectedRecordReference(
    string RecordType,
    Uuid RecordId,
    Uuid VersionId,
    long Version);
