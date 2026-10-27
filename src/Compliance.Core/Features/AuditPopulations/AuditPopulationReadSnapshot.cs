using Bdgrz.Compliance.Features.AuditPopulations.Aggregates;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Complete transient reconciliation and history at an exact compact owner cut.</summary>
public sealed record AuditPopulationReadSnapshot(AuditPopulation Owner, AuditPopulationRules Rules, IReadOnlyList<DomainEvent> Facts)
{
    public AuditPopulationView? View => Rules.Current;
}
