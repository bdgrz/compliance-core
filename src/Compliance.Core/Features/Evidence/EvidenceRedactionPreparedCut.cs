using Bdgrz.Compliance.Features.Evidence.Aggregates;
namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Exact native, physical, business and payload/metadata prefix prepared before append.</summary>
sealed record EvidenceRedactionPreparedCut(ulong Version, ulong PhysicalPosition, long Revision, string Digest, string MetadataDigest)
{
    public static EvidenceRedactionPreparedCut Of(EvidenceRedaction owner) =>
        new(owner.Version, owner.CommittedStreamPosition, owner.Revision, owner.ReplayCut.Digest, owner.ReplayCut.MetadataDigest);
}
