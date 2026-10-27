using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Commands;

/// <summary>
///     Rotates the credential of a tenant OIDC connection and returns it to pending test. <paramref name="CommandId" />
///     is the logical identity of the operation; the new <paramref name="EncryptedCredentialReference" /> is a
///     secret-store pointer that is frozen as protected content and never enters a fact or an intent digest.
/// </summary>
[Discriminator("bdgrz.user-identities.rotate-tenant-identity-provider-credentials", 1)]
public sealed record RotateTenantIdentityProviderCredentials(Uuid CommandId, Uuid TenantId, Uuid ProviderConnectionId,
    string EncryptedCredentialReference, string RotationReference, long ExpectedRevision)
    : IRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
