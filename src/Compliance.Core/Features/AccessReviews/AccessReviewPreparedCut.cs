using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>The exact native expectation-line authority used to prepare one immutable result.</summary>
sealed record AccessReviewPreparedCut(EventStreamAddress Stream, ulong Version, ulong Position, string Digest)
{
    public static AccessReviewPreparedCut Capture(AccessExpectation owner) =>
        new(owner.Stream, owner.Version, owner.CommittedStreamPosition, owner.ReplayCut.Digest);
    public bool Matches(AccessExpectation owner) => this == Capture(owner);
    public static Result Changed() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The access expectation authority changed while immutable content was prepared.", true));
}
