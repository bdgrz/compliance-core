using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations.Models;

/// <summary>Versioned rules, scope, and authorized fields for one audit population.</summary>
public sealed record AuditPopulationDefinitionContent(
    string Name,
    string Purpose,
    string SourceUniverse,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string InclusionRules,
    string ExclusionRules,
    string ExpectedCompletenessEvidence,
    IReadOnlyList<string> AuthorizedFields,
    IReadOnlyList<Uuid> ControlIds);
