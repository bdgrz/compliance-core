using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>One unpublished preparation is valid only at the exact source prefix it verified.</summary>
sealed record AuditPackagePreparedCut(EventStreamAddress Owner, ulong Version, ulong Position, string Digest)
{
    public static AuditPackagePreparedCut Capture(AuditPackageManifest owner) =>
        new(owner.Stream, owner.Version, owner.CommittedStreamPosition, owner.ReplayCut.Digest);
    public bool Matches(AuditPackageManifest owner) => owner.Stream == Owner && owner.Version == Version &&
        owner.CommittedStreamPosition == Position && owner.ReplayCut.Digest == Digest;
}
