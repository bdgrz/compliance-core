using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Commands;

/// <summary>Links the authenticated OIDC identity to the current browser user.</summary>
[Discriminator("bdgrz.oidc-provider.link", 1)]
public sealed record LinkOidcProviderIdentity : IRequest<AuthenticatedUserIdentity>, ICallable;
