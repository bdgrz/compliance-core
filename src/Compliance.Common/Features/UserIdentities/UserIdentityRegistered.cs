using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Records the provider-neutral identity linked to a platform user. A registration caused by an identity
///     recovery names the recovery challenge and carries the receiver's operation provenance and frozen intent.
/// </summary>
[Discriminator("bdgrz.user-identity.registered", 1)]
public sealed record UserIdentityRegistered(
    Uuid UserId,
    string Provider,
    string Identifier,
    string? EmailAddress,
    OperationProvenance? Operation = null,
    Uuid? RecoveryChallengeId = null,
    ImmutableContentRef? Intent = null,
    ImmutableContentRef? FirstResponse = null,
    ImmutableContentRef? OperationResult = null) : DomainEvent;
