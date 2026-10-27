using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Commands;

/// <summary>Disables one connection without changing any platform-user identity or membership history.</summary>
[Discriminator("bdgrz.user-identities.disable-tenant-identity-provider", 1)]
public sealed record DisableTenantIdentityProvider(Uuid CommandId, Uuid TenantId, Uuid ProviderConnectionId,
    long ExpectedRevision, string Rationale)
    : IRequest, IClientRbacMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
