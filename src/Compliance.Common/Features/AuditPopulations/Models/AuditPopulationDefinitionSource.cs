using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AuditPopulations.Models;

/// <summary>Exact frozen period-close fact used to define a population.</summary>
public sealed record AuditPopulationDefinitionSource(SourceFactLocator CloseFact, string CloseMetadataSha256)
{
    public const string Schema = "audit-population-definition-source.v1";
}
