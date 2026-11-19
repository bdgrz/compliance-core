# Criteria MCP HTTP admission ingress review

Status: **source-reviewed, conditional, unregistered; not deployment-qualified**.
This review concerns the current stock stateless HTTP tool ingress. It does not
qualify access policy, identity freshness, source currentness, live revocation,
writer fencing, or arbitrary transports. The MCP-aware credential adapter is
not registered and the criteria authoring MCP registration remains uncalled.

## Source trace

- [Compliance Program.cs](../../../src/Compliance.App/Program.cs) adds
  `AddMcpHttp()` with no custom transport callback, adds an HTTP context
  accessor, and maps `MapPortiaMcp("/mcp")` with `ApiUser` authorization.
  Portia's 0.10.0 source `src/Portia.Mcp.AspNetCore/Hosting/PortiaMcpHttpApplicationExtensions.cs`
  sets stateless mode; `PortiaMcpEndpointRouteBuilderExtensions.cs` maps the
  SDK transport and requires authorization. Compliance's supported schemes are
  session and optionally configured external resource schemes; this adapter
  only accepts the Bdgrz session scheme.
- The installed `ModelContextProtocol.AspNetCore` 2.3.1 NuGet nuspec identifies
  [SDK source commit 61420937](https://github.com/modelcontextprotocol/csharp-sdk/tree/61420937cbef3eab895cafc8a89418811fb71f54).
  In its [StreamableHttpHandler.cs](https://github.com/modelcontextprotocol/csharp-sdk/blob/61420937cbef3eab895cafc8a89418811fb71f54/src/ModelContextProtocol.AspNetCore/StreamableHttpHandler.cs),
  each POST parses its own message, assigns that request's authenticated
  `HttpContext.User` to its non-serialized message context, creates a fresh
  stateless session and uses that same `HttpContext.RequestServices`, with SDK
  per-request re-scoping disabled. The original POST awaits transport handling;
  the stateless session does not outlive that request. The SDK's
  [JsonRpcMessage.Context](https://github.com/modelcontextprotocol/csharp-sdk/blob/61420937cbef3eab895cafc8a89418811fb71f54/src/ModelContextProtocol.Core/Protocol/JsonRpcMessage.cs)
  is `JsonIgnore`, so an untrusted JSON-RPC payload cannot set the principal.
- The SDK's [McpSessionHandler.cs](https://github.com/modelcontextprotocol/csharp-sdk/blob/61420937cbef3eab895cafc8a89418811fb71f54/src/ModelContextProtocol.Core/McpSessionHandler.cs)
  runs the message loop in the context in which that per-request session is
  started and force-yields handlers; it may handle messages concurrently. The
  stateless case has a distinct session and request service scope per POST.
  [MessageContext.cs](https://github.com/modelcontextprotocol/csharp-sdk/blob/61420937cbef3eab895cafc8a89418811fb71f54/src/ModelContextProtocol.Core/Server/MessageContext.cs)
  exposes the message principal and server services. Portia's
  `src/Portia.Mcp/Tools/McpToolRegistration.cs` chooses `call.User`, resolves
  the request bus from `call.Services`, and dispatches the same actor with a
  `McpInvocation`; it does not include the raw token in tool arguments.
  Streaming responses do not change these bindings while the POST remains live.
  A handler continuing after request cancellation, detached polling, stateful
  or custom session transport is not covered by this conclusion.

These are source-derived conditions, not an executed integration test. The
stock SDK and Portia versions, the no-custom-session-hook host configuration,
request-scope lifetime and ASP.NET Core ExecutionContext behavior must be
preserved and tested together. SDK upgrades, custom filters and stateful
configuration invalidate this review until requalified.

## Adapter boundary

[McpHttpAdmissionCredentialResolver.cs](../../../src/Compliance.App/Features/Authentication/McpHttpAdmissionCredentialResolver.cs)
requires an `McpInvocation`, stateless options without session-option overrides,
the same live `/mcp` POST and trace, the identical `HttpContext.User` principal
object, and the resolver instance from that HTTP request's service scope. It
reuses the existing HTTP resolver's Bdgrz session reauthentication, exact
identity and user claims, original `nbf`/`exp`, fallback-key/developer-bypass
refusal and live request rechecks. A locally constructed HTTP invocation is
only an internal bridge into the existing credential parser; the application
request retains its original MCP invocation. Other schemes, HTTP requests,
request lifetime changes, missing scopes and unknown authentication fail
unavailable. This is not a source-authoritative permission grant.

`McpInvocation` is a public in-process descriptor. Code already executing with
host privileges can manufacture a request context or alter DI, filters,
HttpContext, or transport behavior; the wrapper is not a sandbox against that
code. That is distinct from an untrusted HTTP caller: the caller cannot supply
the runtime message principal, request scope, or an authenticated ticket merely
by submitting tool arguments. The deployment must treat host middleware,
transport configuration and DI as trusted and prohibit unreviewed custom
session lifecycle hooks; no claim is made about a hostile in-process component.

## Remaining qualification

The focused tests use a fake authentication service and do not themselves prove
cryptographic JWT validation or the real SDK's transport/context behavior.
Before registration, add real broker-independent ASP.NET Core integration tests
with valid and invalid signed session JWTs, current identity source, concurrent
POSTs from distinct principals, streaming completion/cancellation, actor and
request scope isolation, no credential available after disposal, expired tokens,
stateful/custom transport rejection, and SDK-version/package readback. The
host must register the resolver *scoped*, only for this transport profile, and
preserve an independently qualified policy, operator and identity authority.
No builds or tests were run for this review.
