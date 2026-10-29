using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.Authentication;

/// <summary>Issues the Bdgrz browser session cookie for a successful identity continuation.</summary>
static class UserSessionCookie
{
    public static IResult? Issue(
        HttpContext http,
        Result<AuthenticatedUserIdentity> result,
        BdgrzSessionTokens sessionTokens)
    {
        if (result.IsSuccess)
        {
            http.Response.Cookies.Append(
                BdgrzSessionTokens.CookieName,
                sessionTokens.Issue(result.Value.UserIdentityId, result.Value.UserId,
                    result.Value.EmailAddress, DateTimeOffset.UtcNow),
                new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    MaxAge = TimeSpan.FromHours(12),
                    SameSite = SameSiteMode.Lax,
                    Secure = http.Request.IsHttps,
                });
        }

        return null;
    }

    /// <summary>Clears the Bdgrz browser session cookie, ending the session immediately.</summary>
    public static void Clear(HttpContext http) =>
        http.Response.Cookies.Delete(
            BdgrzSessionTokens.CookieName,
            new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = http.Request.IsHttps,
            });
}
