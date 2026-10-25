using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>The precise local native authority against which immutable feasibility was prepared.</summary>
sealed record PolicyCampaignPreparedCut(EventStreamAddress Stream, ulong Version, ulong Position, string Digest)
{
    public static PolicyCampaignPreparedCut? Capture(Aggregate owner) => owner switch
    {
        PolicyDistributionCampaign value => new(value.Stream, value.Version, value.CommittedStreamPosition, value.ReplayCut.Digest),
        PolicyCampaignParticipant value => new(value.Stream, value.Version, value.CommittedStreamPosition, value.ReplayCut.Digest),
        _ => null,
    };

    public bool Matches(Aggregate owner) => this == Capture(owner);

    public static Result Changed() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The campaign native authority changed while immutable content was prepared.", true));
}
