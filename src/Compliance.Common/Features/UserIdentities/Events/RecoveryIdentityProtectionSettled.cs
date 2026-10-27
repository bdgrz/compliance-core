using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Events;

/// <summary>
///     The protection of one slot for one recovery challenge is settled. A slot that was never protected is settled
///     too, which fences a delayed protection for the same challenge.
/// </summary>
[Discriminator("bdgrz.user-identity.recovery-identity-protection-settled", 1)]
public sealed record RecoveryIdentityProtectionSettled(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, Uuid ChallengeId)
    : DomainEvent, INativeOperationFact;
