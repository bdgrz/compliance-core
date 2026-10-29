using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Commands;

/// <summary>Continues with the authenticated OpenID Connect provider identity.</summary>
[Discriminator("bdgrz.oidc-provider.continue", 1)]
public sealed record ContinueWithOidcProvider : IRequest<AuthenticatedUserIdentity>, ICallable;
