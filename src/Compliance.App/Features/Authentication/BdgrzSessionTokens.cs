using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Cntryl.Portia;
using Microsoft.IdentityModel.Tokens;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Authentication;

/// <summary>Issues and validates the Bdgrz browser session JWT.</summary>
public sealed class BdgrzSessionTokens
{
    internal const string CookieName = "bdgrz_session";
    internal const string Issuer = "bdgrz";
    internal const string Audience = "bdgrz-browser";
    const string DevelopmentSigningSecret = "bdgrz-development-authentication-key-v1-only";

    readonly SymmetricSecurityKey _signingKey;

    BdgrzSessionTokens(string signingSecret)
    {
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingSecret));
    }

    internal SecurityKey SigningKey => _signingKey;

    internal static BdgrzSessionTokens Resolve(IConfiguration configuration, bool isDevelopment)
    {
        var signingSecret = configuration["BDGRZ_SESSION_SIGNING_KEY"];
        if (string.IsNullOrWhiteSpace(signingSecret))
        {
            if (!isDevelopment)
            {
                throw new InvalidOperationException(
                    "BDGRZ_SESSION_SIGNING_KEY is required outside the Development environment.");
            }

            signingSecret = DevelopmentSigningSecret;
        }

        if (Encoding.UTF8.GetByteCount(signingSecret) < 32)
        {
            throw new InvalidOperationException("BDGRZ_SESSION_SIGNING_KEY must contain at least 32 bytes.");
        }

        return new BdgrzSessionTokens(signingSecret);
    }

    public string Issue(Uuid userIdentityId, Uuid userId, string? emailAddress, DateTimeOffset now)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("user_identity_id", userIdentityId.ToString()),
            new("email_verified", "false", ClaimValueTypes.Boolean),
        };
        if (emailAddress is not null)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, emailAddress));
        }

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            Issuer,
            Audience,
            claims,
            now.UtcDateTime,
            now.AddHours(12).UtcDateTime,
            credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
