using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>A row's exact reference to an independently owned domain record.</summary>
public sealed record AuditPopulationRecordReference(
    string RecordType,
    Uuid RecordId,
    int Version,
    string ContentSha256,
    string Relationship);
