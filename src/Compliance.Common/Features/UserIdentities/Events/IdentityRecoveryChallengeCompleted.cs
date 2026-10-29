using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Events;

/// <summary>Completes the original email challenge of a finished recovery; a newer challenge slot is never cleared.</summary>
[Discriminator("bdgrz.identity-recovery.challenge-completed", 1)]
public sealed record IdentityRecoveryChallengeCompleted(
    Uuid UserId,
    string EmailAddress,
    Uuid ChallengeId,
    Uuid OldIdentityId,
    Uuid ReplacementIdentityId,
    DateTimeOffset CompletedAt,
    OperationProvenance Operation,
    ImmutableContentRef Intent,
    ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
