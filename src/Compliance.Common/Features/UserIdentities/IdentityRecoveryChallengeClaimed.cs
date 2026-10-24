using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Consumes an email proof challenge for one old and one replacement identity and freezes the exact candidate.
///     It is the native acceptance of CompleteIdentityRecovery; the proofs are retained as non-bearer provenance.
/// </summary>
[Discriminator("bdgrz.identity-recovery.challenge-claimed", 1)]
public sealed record IdentityRecoveryChallengeClaimed(
    Uuid UserId,
    string EmailAddress,
    Uuid ChallengeId,
    Uuid OldIdentityId,
    Uuid ReplacementIdentityId,
    OperationProvenance Operation,
    ActorReference OriginalActor,
    ImmutableContentRef VerifiedEmailProof,
    ImmutableContentRef VerifiedReplacementProof,
    DateTimeOffset AcceptedAt,
    ImmutableContentRef Intent,
    ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
