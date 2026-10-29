using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
namespace Bdgrz.Compliance.Features.UserIdentities.Events;

/// <summary>
///     The native acceptance of <see cref="RotateTenantIdentityProviderCredentials" />: a new credential epoch whose
///     secret-store pointer lives in protected content, and a forced fresh connection test.
/// </summary>
[Discriminator("bdgrz.useridentities.tenant-identity-provider-credentials-rotated", 2)]
public sealed record TenantIdentityProviderCredentialsRotated(
    Uuid TenantId,
    Uuid ProviderConnectionId,
    ImmutableContentRef CredentialReference,
    string RotationReference,
    long CredentialRevision,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset RotatedAt,
    OperationProvenance Operation,
    ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
