using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
namespace Bdgrz.Compliance.Features.UserIdentities.Commands;

/// <summary>
///     Configures one tenant OIDC connection. <paramref name="CommandId" /> is the logical identity of the operation and
///     <paramref name="ExpectedRevision" /> the revision of the connection the caller observed (zero for a new one).
///     <paramref name="EncryptedCredentialReference" /> is a secret-store pointer; it is frozen as protected content and
///     never enters a fact or an intent digest.
/// </summary>
[Discriminator("bdgrz.user-identities.configure-tenant-identity-provider", 1)]
public sealed record ConfigureTenantIdentityProvider(Uuid CommandId, Uuid TenantId, Uuid ProviderConnectionId,
    string Issuer, string ClientId, string EncryptedCredentialReference, IReadOnlyList<string> AllowedAudiences,
    long ExpectedRevision) : IRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
