using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Models;

/// <summary>The stable identity established by authentication.</summary>
public sealed record AuthenticatedUserIdentity(Uuid UserIdentityId, Uuid UserId, string? EmailAddress);
