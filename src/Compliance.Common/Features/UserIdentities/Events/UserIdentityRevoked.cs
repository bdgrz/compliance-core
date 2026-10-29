using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Events;

/// <summary>
///     Records replacement of a provider identity while preserving its platform user. A revocation caused by an
///     identity recovery names the recovery challenge and carries the receiver's provenance and frozen intent.
/// </summary>
[Discriminator("bdgrz.user-identity.revoked", 1)]
public sealed record UserIdentityRevoked(
    Uuid UserIdentityId,
    Uuid UserId,
    Uuid ReplacementIdentityId,
    DateTimeOffset RevokedAt,
    OperationProvenance? Operation = null,
    Uuid? RecoveryChallengeId = null,
    ImmutableContentRef? Intent = null,
    ImmutableContentRef? FirstResponse = null,
    ImmutableContentRef? OperationResult = null) : DomainEvent;
