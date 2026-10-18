using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>An exact version selection paired with the stable identity of its source record.</summary>
public sealed record AdvisorRecordVersion(Uuid RecordId, Uuid VersionId);
