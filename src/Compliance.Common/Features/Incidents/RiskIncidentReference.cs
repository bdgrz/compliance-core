using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Attributable reference to an earlier externally sourced R1-07 risk incident.</summary>
public sealed record RiskIncidentReference(
    string SourceSystem,
    string SourceIdentifier,
    string SourceUrl,
    DateTimeOffset OccurredAt,
    string Summary);
