using Bdgrz.Compliance.Features.Providers.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>The exact local native gap authority against which source and immutable preparation was performed.</summary>
sealed record ProviderGapPreparedCut(EventStreamAddress Stream, ulong Version, ulong Position, string Digest)
{
    public static ProviderGapPreparedCut Capture(ProviderAssuranceGap owner) =>
        new(owner.Stream, owner.Version, owner.CommittedStreamPosition, owner.ReplayCut.Digest);

    public bool Matches(ProviderAssuranceGap owner) => this == Capture(owner);

    public static Result Changed() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The native provider gap authority changed while immutable content was prepared.", true));
}
