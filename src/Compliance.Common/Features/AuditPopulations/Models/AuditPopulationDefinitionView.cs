using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Authorized definition listing with its current source and period boundary.</summary>
public sealed record AuditPopulationDefinitionView(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid AuditPopulationDefinitionId,
    Uuid AuditPopulationId,
    string Name,
    string SourceUniverse,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int DefinitionVersion,
    string Status,
    long Revision,
    DateTimeOffset UpdatedAt);
