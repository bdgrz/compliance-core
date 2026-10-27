using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Models;

/// <summary>
///     Non-bearer provenance of one recovery proof: how it was verified and for what, never the token itself.
///     <paramref name="AssertedEmail" /> is the address a provider asserted for the replacement identity, if any.
/// </summary>
public sealed record IdentityRecoveryProofProvenance(string Method, Uuid ChallengeId, string Subject, string? TokenKeyId,
    string? AssertedEmail, DateTimeOffset VerifiedAt)
{
    /// <summary>Schema of the provenance content.</summary>
    public const string Schema = "identity-recovery-proof-provenance.v1";
}
