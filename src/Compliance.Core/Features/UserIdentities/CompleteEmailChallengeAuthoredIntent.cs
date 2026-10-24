using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     The authored fields of an email verification that its intent digest covers. The token is a bearer secret and
///     never enters a digest or a fact; it is checked against the challenge's token hash on every call instead.
/// </summary>
public sealed record CompleteEmailChallengeAuthoredIntent(Uuid ChallengeId, Uuid UserId, string EmailAddress);
