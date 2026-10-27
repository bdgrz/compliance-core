namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>The complete selected authored command; exactly one typed alternative is present.</summary>
public sealed record AuditPopulationCommandContent(ReviseAuditPopulationDefinition? ReviseDefinition,
    RecordAuditPopulationFacts? RecordFacts, ClassifyAuditPopulationRow? ClassifyRow, ReconcileAuditPopulation? Reconcile,
    FreezeAuditPopulation? Freeze, AmendAuditPopulation? Amend)
{
    public const string Schema = "audit-population-command.v1";
}
