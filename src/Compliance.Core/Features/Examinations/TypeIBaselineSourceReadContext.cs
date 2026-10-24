using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Request authority and earlier source references available to one family reader.</summary>
public sealed record TypeIBaselineSourceReadContext(Uuid RequesterUserId,
    IReadOnlyList<TypeIBaselineSourceReference> PreviouslyCapturedSources)
{
    /// <summary>Optional server-frozen receipt cutoff for a close; the engagement date remains the effective cutoff.</summary>
    public DateTimeOffset? RecordedThrough { get; init; }
}
