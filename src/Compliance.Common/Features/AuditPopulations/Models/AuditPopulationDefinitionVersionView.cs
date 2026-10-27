using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations.Models;

/// <summary>An immutable, attributed version of a population definition.</summary>
public sealed record AuditPopulationDefinitionVersionView(
    int Version,
    AuditPopulationDefinitionContent Content,
    string ContentSha256,
    Uuid RecordedByMemberId,
    string RecordedByDisplay,
    DateTimeOffset RecordedAt,
    string ChangeReason);
