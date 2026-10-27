using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AuditPopulations.Aggregates;
namespace Bdgrz.Compliance.Features.AuditPopulations.Models;

/// <summary>The exact hydrated native, physical, logical and saved metadata preparation cut.</summary>
sealed record AuditPopulationPreparedCut(ulong Version, ulong PhysicalPosition, long Revision, string PayloadSha256, string MetadataSha256)
{
    public static AuditPopulationPreparedCut Of(AuditPopulation owner) => new(owner.Version, owner.CommittedStreamPosition,
        owner.Revision, owner.ReplayCut.Digest, owner.ReplayCut.MetadataDigest);
}
