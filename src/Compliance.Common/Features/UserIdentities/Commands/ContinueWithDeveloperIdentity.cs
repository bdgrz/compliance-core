using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Commands;

/// <summary>Continues with a deterministic development identity derived from an email address.</summary>
[Discriminator("bdgrz.developer-identity.continue", 1)]
public sealed record ContinueWithDeveloperIdentity(string EmailAddress)
    : IRequest<AuthenticatedUserIdentity>, ICallable;
