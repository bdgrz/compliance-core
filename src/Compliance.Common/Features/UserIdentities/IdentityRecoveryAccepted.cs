using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>The recovery owner accepted the swap after both identity slots were protected. An accepted swap completes forward and can no longer be rejected.</summary>
[Discriminator("bdgrz.identity-recovery.identity-recovery-accepted", 1)]
public sealed record IdentityRecoveryAccepted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation, Uuid ChallengeId,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, IIdentityRecoveryFact;
