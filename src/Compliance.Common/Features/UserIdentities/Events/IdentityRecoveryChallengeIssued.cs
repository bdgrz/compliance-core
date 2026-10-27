using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
namespace Bdgrz.Compliance.Features.UserIdentities.Events;

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
