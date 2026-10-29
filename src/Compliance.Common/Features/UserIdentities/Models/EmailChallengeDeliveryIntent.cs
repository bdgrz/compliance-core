using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Models;

/// <summary>
///     The immutable effect payload of one challenge delivery, frozen from the accepted challenge fact. It holds no
///     bearer token: the token is re-derived from the protected key material when a message is sent and must match
///     the hash the fact carries.
/// </summary>
public sealed record EmailChallengeDeliveryIntent(DurableIntentHeader Header, Uuid ChallengeId, Uuid UserId,
    string Purpose, DateTimeOffset ExpiresAt, string TokenKeyId)
{
    /// <summary>Schema of the frozen intent content.</summary>
    public const string Schema = "email-challenge-delivery-intent.v1";

    /// <summary>The purpose of a verification challenge.</summary>
    public const string Verification = "verification";

    /// <summary>The purpose of an identity recovery challenge.</summary>
    public const string Recovery = "recovery";
}
