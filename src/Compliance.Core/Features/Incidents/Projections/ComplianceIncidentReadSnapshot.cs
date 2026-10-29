using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Incidents.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Models;
namespace Bdgrz.Compliance.Features.Incidents.Projections;

/// <summary>Explicit verified read composition; not aggregate state or a compatibility owner surface.</summary>
public sealed record ComplianceIncidentReadSnapshot(ComplianceIncident Owner, ComplianceIncidentRules Rules,
    ComplianceIncidentCurrentAuthority? Authority, IReadOnlyList<DomainEvent> Facts)
{
    public ComplianceIncidentView? View => Rules.Current;
}
