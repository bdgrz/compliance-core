using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     A provider identity slot is protected for one recovery challenge: until the protection is settled, no other
///     account can associate the slot. <paramref name="Role" /> is the side of the swap the slot is.
/// </summary>
[Discriminator("bdgrz.user-identity.recovery-identity-protected", 1)]
public sealed record RecoveryIdentityProtected(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, Uuid ChallengeId, Uuid AccountId,
    RecoveryIdentityRole Role) : DomainEvent, INativeOperationFact;
