# Admission transport binding: proposed contract

Status: the HTTP-only resolver and request-context contract are drafted but
unregistered and untested. Admission remains unavailable without the independent
identity, policy, veto and participant source authorities and ingress validation.

## Finding

`ITrustedAdmissionCredentialResolver.ResolveAsync` currently receives only a
`ClaimsPrincipal` and cancellation token. The three `IAccessAdmissionAuthority`
methods also receive only the actor. They cannot establish that the actor being
admitted belongs to the validated transport request which is currently executing.
Portia snapshots the actor into `RequestDispatchContext` and returns copies from
`Actor`; object identity with `HttpContext.User` is intentionally not preserved.

Portia's `HttpInvocation` is a public record containing method, path, route and
request identifier, and `McpInvocation` is a public record containing only a
tool name. The host does not expose an opaque transport-binding capability.
These records and `RequestDispatchContext` are constructible by in-process code;
that is not by itself an external forgery, since application code already runs
inside the trusted host boundary. No untrusted ingress accepting a caller-made
invocation was found in the inspected paths. The execution ID, request ID,
ambient context or equal claims alone must nevertheless not be used as a
credential.

HTTP binding creates a dispatch context from `HttpContext.User` in
`PortiaHttpBinding.CreateDispatchContext`. MCP resolves its call principal and
creates a separate dispatch context in `McpToolRegistration.DispatchAsync`;
HTTP MCP returns anonymous if its transport supplied no principal, while the
stdio fallback can obtain an `IMcpActorProvider`. The app configures HTTP
authentication before mapping its authorized MCP endpoint and registers
`IHttpContextAccessor`. Reactors execute as system; process sweeps construct
system direct contexts. No inspected application path lets a client select the
invocation of these admission calls. A trusted handler can deliberately create
a new dispatch: this is a host-code trust issue, not evidence of a remote
forgery. The exact MCP SDK relationship between a streamed/concurrent tool call,
`call.User` and its live HTTP request has not yet been established.

## Proposed smallest boundary

1. Replace the principal parameter of all three admission methods with
   `IRequestContext`, and pass that same context to the credential resolver:

   ```csharp
   ValueTask<Credential?> ResolveAsync(IRequestContext execution, CancellationToken ct);
   ```

   `IRequestContext`, not merely `IExecutionContext`, supplies `Invocation`.
   Extract the actor from this context inside the authority. Reject system actors
   and direct, queue, notice, schedule, RPC and unknown invocations. This type
   check is necessary but is not itself proof of ingress.
2. For ordinary HTTP under the current trusted-host threat model, use a scoped
   resolver and the live `IHttpContextAccessor.HttpContext`. Require a
   `HttpInvocation` whose request identifier, method and path match that exact
   active HTTP request, then authenticate the **specific** Bdgrz session scheme
   on that request and match its validated principal to the dispatch actor. The
   server constructs the invocation and owns the request scope. Reject missing,
   completed, aborted, mismatched and non-HTTP contexts; recheck the binding
   before admitting. Treat trusted in-process code which fabricates an invocation
   or deliberately forwards one to a different request as outside this boundary.
   If that code becomes untrusted, this proof is insufficient and a framework
   ingress-issued opaque binding is required.
3. For MCP, do not infer an HTTP binding from `McpInvocation` or a matching
   actor. First verify the MCP SDK's actual per-call and per-request principal,
   scope and `HttpContext` behavior under concurrent calls and streamed sessions.
   If those checks establish the same trusted-request binding, use the scoped
   approach. Otherwise add an ingress-owned per-call capability; until one of
   these is proven, return unavailable. Do not fall back to an
   `IMcpActorProvider`, request claims or an external bearer identity. The
   configured developer-authentication mode is supported only in the Development
   environment; only that explicit mode may use the known fallback signing key.
4. From that authenticated result, verify exactly one well-formed signed
   `sub`, `user_identity_id`, issuer, original `nbf` and original `exp` for the
   Bdgrz session and match them to the bound dispatch actor. Reject ambiguous,
   missing, expired or changed values as unavailable. Preserve the original
   half-open validity interval; do not synthesize an issuance time, extend the
   JWT lifetime using clock skew, or accept a competing external identity.
   Revalidate the binding and credential lifetime through completion of the
   admission capture. Cancellation must propagate.
5. Keep this authentication proof separate from current authorization. The
   session handler's ordinary `UserIdentity` hydration is not qualified current
   source coverage. The existing evaluator still needs its independently
   qualified identity, policy and complete applicable veto/grant sources; no
   identity, session, account or veto status follows merely from the token.
   Missing qualification means unavailable, never denial or admission evidence.

This proposes the smallest scoped HTTP proof under the existing trusted-host
boundary; real ingress integration and lifetime tests must establish its
assumptions before registration. An opaque capability is conditional on the MCP
investigation or a stronger threat model, not a demonstrated HTTP prerequisite.

The HTTP/MCP adapters permit the known fallback signing key only while the
actual startup developer-authentication flag is enabled in a Development
environment. A developer session can survive a restart using the same key, so
the code policy reads the identity owner and rejects a development provider
when that mode is disabled. The distinct development policy records its real
operator bypass rather than inventing an operator source event. The alternate
source-policy evaluator has an explicit `AllowDevelopmentIdentities` flag which
defaults to false. A future source-policy adapter must bind that flag to the
supported policy content digest. The session does not establish current
identity, veto or access status.

## Migration inventory

Production call sites passing `context.Actor` today:

- `CriteriaEditionOperations`, `CriteriaEditionValueOperations` and
  `CriteriaEntryOperations` pass their existing typed request contexts.
- `CriteriaOperatorReadAdmission` changes its own actor parameter to
  `IRequestContext`; `GetCriteriaEditionDraftHandler`,
  `GetCriteriaOperationHandler` and `GetCriteriaEditionValueOperationHandler`
  pass their contexts through it.
- `ProgramOperations` passes its existing typed request context for creation
  and edition selection.
- `AccessAdmissionAuthority` forwards the same context to the credential
  resolver on all three paths and retains system rejection, exact principal
  matching, expiry checks and independent source qualification.

Test implementations, direct calls or registration assertions appear in
`AccessAdmissionAuthorityTests`, `ProgramCreationAdmissionAuthorityTests`,
`AttestProgramManagementWriteWallTests`, `CriteriaAuthoringRegistrationTests`,
`CriteriaEditionOperationsTests`, `CriteriaEditionValueOperationsTests`,
`CriteriaEditionValueReceiptHandlerTests`, `CriteriaEntryOperationsTests`,
`CriteriaOperatorReadAdmissionTests`, `ComplianceProgramTests`,
`ProgramCreateEditionTests`, and `Testing/ProgramSeeds.cs`. Fixture authorities
must accept context rather than claims; fixtures must not masquerade as host
proof in production.

Focused verification should cover real validated HTTP and MCP ingress, same
principal in a direct or background dispatch, fabricated invocation records,
cross-request and cross-MCP-call binding reuse, concurrent sessions, expired
or malformed original times, duplicate identity claims, external and fallback
development credentials, cancellation, binding expiry during source capture,
and unavailable identity/veto sources. No test or build has been run for this
design.
