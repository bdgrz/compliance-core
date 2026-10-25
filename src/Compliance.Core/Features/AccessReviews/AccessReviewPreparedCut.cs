using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>The exact native Access owner authority used to prepare one immutable result.</summary>
sealed record AccessReviewPreparedCut(EventStreamAddress Stream, ulong Version, ulong Position, string Digest)
{
    public static AccessReviewPreparedCut Capture(Aggregate owner) => owner switch
    {
        AccessExpectation expectation => new(expectation.Stream, expectation.Version, expectation.CommittedStreamPosition,
            expectation.ReplayCut.Digest),
        AccessReviewCampaign campaign => new(campaign.Stream, campaign.Version, campaign.CommittedStreamPosition,
            campaign.ReplayCut.Digest),
        AccessReviewItem item => new(item.Stream, item.Version, item.CommittedStreamPosition, item.ReplayCut.Digest),
        _ => throw new ArgumentException("The native cut is not an Access owner.", nameof(owner)),
    };
    public bool Matches(Aggregate owner) => this == Capture(owner);
    public static Result Changed() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The access review authority changed while immutable content was prepared.", true));
}
