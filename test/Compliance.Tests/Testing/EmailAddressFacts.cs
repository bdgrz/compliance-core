using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Seeds recovery facts on an email address aggregate with the operation provenance native facts require.</summary>
static class EmailAddressFacts
{
    static readonly ActorReference Actor = new("anonymous", "anonymous", "Anonymous recovery request");

    public static ImmutableContentRef Content { get; } = new(Uuid.CreateVersion4(), new string('1', 64), 2,
        "application/json", "test-content.v1");

    public static OperationProvenance Operation(EmailAddress address, Uuid logicalId, string commandKind) =>
        Operation(address.Stream, logicalId, commandKind);

    public static OperationProvenance Operation(EventStreamAddress owner, Uuid logicalId, string commandKind) =>
        new(logicalId, logicalId, null, commandKind, Actor, owner, logicalId.ToString(), null,
            new string('a', 64), new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero), owner, owner);

    /// <summary>Issues a recovery challenge as the native acceptance of a start with the challenge as logical identity.</summary>
    public static Result IssueRecovery(this EmailAddress address, Uuid userId, Uuid challengeId, string tokenHash,
        DateTimeOffset expiresAt, DateTimeOffset now, string tokenKeyId) =>
        address.IssueRecoveryChallenge(Operation(address, challengeId, "StartIdentityRecovery"), Content, userId,
            challengeId, tokenHash, expiresAt, now, tokenKeyId);

    /// <summary>Claims a recovery challenge as the native acceptance of a completion with the challenge as logical identity.</summary>
    public static Result Claim(this EmailAddress address, Uuid userId, Uuid challengeId, string token,
        Uuid oldIdentityId, Uuid replacementIdentityId, DateTimeOffset now) =>
        address.ClaimIdentityRecovery(Operation(address, challengeId, "CompleteIdentityRecovery"), Content, Content,
            new ActorReference("user", userId.ToString(), replacementIdentityId.ToString()), Content, Content, now, userId,
            challengeId, token, oldIdentityId, replacementIdentityId, now);

    /// <summary>The issued fact of a recovery challenge as a reactor would receive it.</summary>
    public static IdentityRecoveryChallengeIssued IssuedFact(EmailAddress address, Uuid userId, string email,
        Uuid challengeId, string tokenHash, DateTimeOffset expiresAt, string tokenKeyId) =>
        new(userId, email, challengeId, tokenHash, expiresAt, tokenKeyId,
            Operation(address, challengeId, "StartIdentityRecovery"), Content, null);
}
