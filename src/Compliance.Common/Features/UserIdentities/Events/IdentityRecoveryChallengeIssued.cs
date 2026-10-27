using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Starts recovery using a separately verified email channel; the native acceptance of StartIdentityRecovery.</summary>
[Discriminator("bdgrz.identity-recovery.challenge-issued", 1)]
public sealed record IdentityRecoveryChallengeIssued(
    Uuid UserId,
    string EmailAddress,
    Uuid ChallengeId,
    string TokenHash,
    DateTimeOffset ExpiresAt,
    string TokenKeyId,
    OperationProvenance Operation,
    ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
