using Bdgrz.Compliance.Features.Incidents.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Explicit verified read composition; not aggregate state or a compatibility owner surface.</summary>
public sealed record ComplianceIncidentReadSnapshot(ComplianceIncident Owner, ComplianceIncidentRules Rules,
    ComplianceIncidentCurrentAuthority? Authority, IReadOnlyList<DomainEvent> Facts)
{
    public ComplianceIncidentView? View => Rules.Current;
}
