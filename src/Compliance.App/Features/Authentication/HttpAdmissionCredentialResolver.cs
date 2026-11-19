using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Cntryl.Portia;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Tokens;

namespace Bdgrz.Compliance.Features.Authentication;

/// <summary>Resolves only the current HTTP request's validated Bdgrz session credential.</summary>
/// <remarks>Unregistered. It authenticates a token, not current identity, access, policy or veto state.
/// The independent qualified identity participant must reject developer identities unless policy
/// explicitly permits them. MCP, background and development-bypass ingress are unavailable.</remarks>
sealed class HttpAdmissionCredentialResolver(IHttpContextAccessor accessor, BdgrzSessionTokens tokens,
    DeveloperUserRegistration developer, TimeProvider clock, IHostEnvironment environment) : ITrustedAdmissionCredentialResolver
{
    const string Fallback = "bdgrz-development-authentication-key-v1-only";
    HttpContext? _bound;
    HttpInvocation? _invocation;
    DateTimeOffset _notBefore;
    DateTimeOffset _expires;
    bool _resolved;

    public bool IsQualified => Enabled() && (!_resolved || _bound is not null &&
        ReferenceEquals(accessor.HttpContext, _bound) && _invocation is not null && MatchesRequest(_invocation, _bound) &&
        !_bound.RequestAborted.IsCancellationRequested &&
        _notBefore <= clock.GetUtcNow() && clock.GetUtcNow() < _expires);

    public async ValueTask<AccessAdmissionSemanticEvaluator.Credential?> ResolveAsync(IRequestContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _resolved = false;
        _bound = null;
        _invocation = null;
        var http = accessor.HttpContext;
        if (!Enabled() || http is null || http.RequestAborted.IsCancellationRequested ||
            context.Invocation is not HttpInvocation invocation || !MatchesRequest(invocation, http) ||
            RequestActor.IsSystem(context.Actor))
            return null;
        AuthenticateResult result;
        try
        { result = await http.AuthenticateAsync(ComplianceAuthenticationSchemes.Session).ConfigureAwait(false); }
        catch (InvalidOperationException) { return null; }
        ct.ThrowIfCancellationRequested();
        if (!result.Succeeded || result.Ticket?.AuthenticationScheme != ComplianceAuthenticationSchemes.Session ||
            result.Principal is null || !TryCredential(result.Principal, out var credential) ||
            !TryCredential(context.Actor, out var actor) || actor != credential ||
            !ReferenceEquals(accessor.HttpContext, http) || http.RequestAborted.IsCancellationRequested ||
            !MatchesRequest(invocation, http))
            return null;
        var now = clock.GetUtcNow();
        if (credential.NotBefore > now || now >= credential.ExpiresAt)
            return null;
        _bound = http;
        _invocation = invocation;
        _notBefore = credential.NotBefore;
        _expires = credential.ExpiresAt;
        _resolved = true;
        return credential;
    }

    bool Enabled() => (!developer.Enabled || environment.IsDevelopment()) && tokens.SigningKey is SymmetricSecurityKey key &&
        (developer.Enabled || !CryptographicOperations.FixedTimeEquals(key.Key, Encoding.UTF8.GetBytes(Fallback))) &&
        accessor.HttpContext is not null;

    static bool MatchesRequest(HttpInvocation invocation, HttpContext http) =>
        invocation.RequestIdentifier == http.TraceIdentifier && invocation.Method == http.Request.Method &&
        invocation.Path == (http.Request.PathBase.Add(http.Request.Path).Value ?? "/");

    static bool TryCredential(ClaimsPrincipal principal, out AccessAdmissionSemanticEvaluator.Credential credential)
    {
        credential = null!;
        var identities = principal.Identities.Where(static identity => identity.IsAuthenticated).Take(2).ToArray();
        if (identities.Length != 1)
            return false;
        var identity = identities[0];
        if (!One(identity, "iss", out var issuer) || issuer != BdgrzSessionTokens.Issuer ||
            !One(identity, "sub", out var subject) || !Uuid.TryParse(subject, CultureInfo.InvariantCulture, out var user) || user == Uuid.Empty ||
            !One(identity, "user_identity_id", out var identityText) ||
            !Uuid.TryParse(identityText, CultureInfo.InvariantCulture, out var identityId) || identityId == Uuid.Empty ||
            !One(identity, "nbf", out var nbf) || !long.TryParse(nbf, NumberStyles.Integer, CultureInfo.InvariantCulture, out var start) ||
            !One(identity, "exp", out var exp) || !long.TryParse(exp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var end))
            return false;
        try
        {
            var from = DateTimeOffset.FromUnixTimeSeconds(start);
            var until = DateTimeOffset.FromUnixTimeSeconds(end);
            if (from >= until)
                return false;
            credential = new(user, identityId, Uuid.Empty, subject, issuer, from, until,
                AccessAdmissionSemanticEvaluator.CredentialScheme.BdgrzIdentityOnly, true);
            return true;
        }
        catch (ArgumentOutOfRangeException) { return false; }
    }

    static bool One(ClaimsIdentity identity, string type, out string value)
    {
        value = string.Empty;
        var claims = identity.FindAll(type).Take(2).ToArray();
        if (claims.Length != 1 || string.IsNullOrWhiteSpace(claims[0].Value))
            return false;
        value = claims[0].Value;
        return true;
    }
}
