using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
namespace Bdgrz.Compliance.Features.UserIdentities.Events;

/// <summary>The recovery owner accepted the swap after both identity slots were protected. An accepted swap completes forward and can no longer be rejected.</summary>
[Discriminator("bdgrz.identity-recovery.identity-recovery-accepted", 1)]
public sealed record IdentityRecoveryAccepted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation, Uuid ChallengeId,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, IIdentityRecoveryFact;
