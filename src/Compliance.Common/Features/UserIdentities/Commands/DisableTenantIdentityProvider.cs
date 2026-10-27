using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Disables one connection without changing any platform-user identity or membership history.</summary>
[Discriminator("bdgrz.user-identities.disable-tenant-identity-provider", 1)]
public sealed record DisableTenantIdentityProvider(Uuid CommandId, Uuid TenantId, Uuid ProviderConnectionId,
    long ExpectedRevision, string Rationale)
    : IRequest, IClientRbacMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
