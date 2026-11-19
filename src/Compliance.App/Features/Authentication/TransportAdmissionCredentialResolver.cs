using Bdgrz.Compliance.Features.AccessControl.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Authentication;

/// <summary>Selects the request-scoped HTTP or supported MCP authentication adapter.</summary>
/// <remarks>Unregistered. Other invocation kinds remain unavailable.</remarks>
sealed class TransportAdmissionCredentialResolver(HttpAdmissionCredentialResolver http,
    McpHttpAdmissionCredentialResolver mcp) : ITrustedAdmissionCredentialResolver
{
    ITrustedAdmissionCredentialResolver? _selected;

    public bool IsQualified => _selected?.IsQualified ?? (http.IsQualified || mcp.IsQualified);

    public ValueTask<AccessAdmissionSemanticEvaluator.Credential?> ResolveAsync(IRequestContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _selected = context.Invocation switch
        {
            HttpInvocation => http,
            McpInvocation => mcp,
            _ => null,
        };
        return _selected is null
            ? ValueTask.FromResult<AccessAdmissionSemanticEvaluator.Credential?>(null)
            : _selected.ResolveAsync(context, ct);
    }
}
