using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Hosting;
using Cntryl.Portia;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;

namespace Bdgrz.Compliance.Features.Authentication;

/// <summary>Resolves a Bdgrz credential only for the same live stateless MCP HTTP POST.</summary>
/// <remarks>
/// Unregistered. The deployment must retain Portia's default stateless HTTP transport and
/// SDK request/session dispatch and register this resolver once per HTTP request scope.
/// The scope, current request and actor are checked again throughout admission. This is an
/// authentication adapter only; it does not qualify identity, policy or authorization source reads.
/// </remarks>
sealed class McpHttpAdmissionCredentialResolver(
    IHttpContextAccessor accessor,
    BdgrzSessionTokens tokens,
    DeveloperUserRegistration developer,
    TimeProvider clock,
    IOptions<HttpServerTransportOptions> transport,
    IHostEnvironment environment) : ITrustedAdmissionCredentialResolver
{
    readonly HttpAdmissionCredentialResolver _http = new(accessor, tokens, developer, clock, environment);
    HttpContext? _bound;
    ClaimsPrincipal? _actor;
    string? _traceIdentifier;
    bool _resolved;

    public bool IsQualified => _http.IsQualified && SupportedTransport() &&
        (!_resolved || _bound is not null && _actor is not null && IsSameRequest(_bound, _actor, _traceIdentifier));

    public async ValueTask<AccessAdmissionSemanticEvaluator.Credential?> ResolveAsync(IRequestContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _resolved = false;
        _bound = null;
        _actor = null;
        _traceIdentifier = null;
        var http = accessor.HttpContext;
        var traceIdentifier = http?.TraceIdentifier;
        var httpActor = http?.User;
        if (!SupportedTransport() || http is null || context.Invocation is not McpInvocation ||
            httpActor is null || !IsSameRequest(http, httpActor, traceIdentifier))
            return null;

        // This descriptor is only an internal adapter to reuse the existing JWT verification;
        // the externally visible request remains MCP, with its original actor and intent.
        var invocation = new HttpInvocation(http.Request.Method,
            http.Request.PathBase.Add(http.Request.Path).Value ?? "/", null, traceIdentifier!);
        var credential = await _http.ResolveAsync(new RequestDispatchContext(context.Actor, invocation), ct)
            .ConfigureAwait(false);
        if (credential is null || !SupportedTransport() || !IsSameRequest(http, httpActor, traceIdentifier))
            return null;
        _bound = http;
        _actor = httpActor;
        _traceIdentifier = traceIdentifier;
        _resolved = true;
        return credential;
    }

    bool SupportedTransport() => transport.Value.SessionMode == HttpServerSessionMode.Stateless &&
        transport.Value.ConfigureSessionOptions is null;

    bool IsSameRequest(HttpContext http, ClaimsPrincipal actor, string? traceIdentifier) =>
        ReferenceEquals(accessor.HttpContext, http) && ReferenceEquals(http.User, actor) &&
        !http.RequestAborted.IsCancellationRequested && http.TraceIdentifier == traceIdentifier &&
        HttpMethods.IsPost(http.Request.Method) && http.Request.Path == AuthorizationDenialLog.McpPath &&
        ReferenceEquals(http.RequestServices.GetService(typeof(McpHttpAdmissionCredentialResolver)), this);
}
