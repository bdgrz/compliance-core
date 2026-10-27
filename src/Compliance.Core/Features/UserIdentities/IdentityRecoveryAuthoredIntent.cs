using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     The authored fields of a recovery completion that its intent digest covers. The token is a bearer secret and
///     never enters a digest; the replacement is bound by the provider and subject the caller proved.
/// </summary>
public sealed record IdentityRecoveryAuthoredIntent(string EmailAddress, Uuid ChallengeId, Uuid OldIdentityId,
    string ReplacementProvider, string ReplacementSubject);
