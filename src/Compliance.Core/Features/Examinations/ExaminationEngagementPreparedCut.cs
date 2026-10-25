using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>One unpublished preparation is valid only at the exact source prefix it verified.</summary>
sealed record ExaminationEngagementPreparedCut(EventStreamAddress Owner, ulong Version, ulong Position, string Digest)
{
    public static ExaminationEngagementPreparedCut Capture(ExaminationEngagement owner) =>
        new(owner.Stream, owner.Version, owner.CommittedStreamPosition, owner.ReplayCut.Digest);
    public bool Matches(ExaminationEngagement owner) => owner.Stream == Owner && owner.Version == Version &&
        owner.CommittedStreamPosition == Position && owner.ReplayCut.Digest == Digest;
}
