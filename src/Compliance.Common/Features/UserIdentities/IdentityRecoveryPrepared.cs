using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>The recovery owner froze the exact both-proof-verified candidate from the claimed challenge. This is a preparation, not an accepted swap.</summary>
[Discriminator("bdgrz.identity-recovery.identity-recovery-prepared", 1)]
public sealed record IdentityRecoveryPrepared(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation, Uuid ChallengeId,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, IIdentityRecoveryFact;
