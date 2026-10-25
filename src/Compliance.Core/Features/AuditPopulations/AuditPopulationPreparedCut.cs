namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>The exact hydrated native, physical, logical and saved metadata preparation cut.</summary>
sealed record AuditPopulationPreparedCut(ulong Version, ulong PhysicalPosition, long Revision, string PayloadSha256, string MetadataSha256)
{
    public static AuditPopulationPreparedCut Of(AuditPopulation owner) => new(owner.Version, owner.CommittedStreamPosition,
        owner.Revision, owner.ReplayCut.Digest, owner.ReplayCut.MetadataDigest);
}
