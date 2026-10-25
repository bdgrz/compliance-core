using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     A closure operation as its authoritative owner says it is: the hydrated campaign, the accepted closure fact and
///     the frozen intent with its complete participants. A non-null <see cref="Error" /> is the transient or
///     permanent verification failure; nothing else is set then.
/// </summary>
sealed record PolicyCampaignClosure(PolicyCampaignSnapshot? Campaign, PolicyCampaignClosureAccepted? Accepted,
    ClosureIntent? Intent, IReadOnlyList<ClosureParticipant>? Participants, RequestError? Error)
{
    public static PolicyCampaignClosure Failed(RequestError error) => new(null, null, null, null, error);
}
