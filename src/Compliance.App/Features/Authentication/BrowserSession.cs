using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Events;
namespace Bdgrz.Compliance.Features.Authentication;

sealed record BrowserSession(string Id, string? EmailAddress, bool EmailAddressVerified);
