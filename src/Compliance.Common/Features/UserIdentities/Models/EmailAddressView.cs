using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Models;

public sealed record EmailAddressView(Uuid UserId, string EmailAddress, bool Verified);
