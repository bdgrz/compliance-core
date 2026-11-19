# Methodology destination entry execution

Tracking: #1176. Source preparation only: authored tests precede implementation;
**no .NET compilation, observed RED/GREEN or full gate has run for this packet**.

`MethodologyTemplateEntryApplicationReactor` sends one event-derived stable command
per new or changed entry from the exact canonical committed application.
`ApplyMethodologyTemplateEntryHandler` verifies that source fact, tenant/program/
template/version and immutable entry snapshot. It accepts no asserted outcome or
actor. An unopened outcome owner is transient and preserves the reactor checkpoint.

Typed entry payloads map to owning Control/Policy/Risk create commands. Control and
Policy successors use the original approved base selected and retained atomically
on the existing version application. That business base remains unchanged across
destination retries. Generic legacy template summaries lack required destination
business terms and produce a reasoned refusal; they are not expanded into invented
control or policy content.

The source stores the originally admitted user ID. The handler derives a delegated
principal only after verifying the committed application, dispatches through the
real request bus and current destination authorization, and retains causal request
metadata. Existing destination operations require a personal user. No external
principal or member ID is accepted from the reaction command.

Success requires the destination's actual selected native acceptance and exact
binding: tenant/owner, logical command, command kind, actor identity, episode,
expected revision and authored request digest. Its committed fact reference is
retained on the outcome. A crash after destination commit can recover that exact
fact before attempting another authorization/mutation. A conflicting original
creation records `identifier_in_use`; a successful bus result without its exact
accepted fact stays retryable. Permanent destination refusals become terminal
reasoned outcomes; transient refusals retain the checkpoint. The legacy asserted
outcome handler explicitly refuses canonical V4 applications.

The authored tests exercise stable fanout and checkpoint retention, personal caller
refusal before source access, committed-source incomplete-payload refusal/retry and
the canonical asserted-outcome bypass. Typed mapping tests cover exact payloads,
owner-derived identities, missing terms, base binding and owned collection copies.
Real native destination success/proposal and permission-loss recovery need executed
acceptance in the coordinator's assembled lane; they are not proved by source.

Remaining source qualification: Risk revision proposals require a governed receiver
that preserves existing client content. The current factory returns an explicit
refusal for that unsupported path. Policy/Risk creation must include typed template
provenance in the authored native digest so unrelated templates cannot exact-replay
the same identifier and identical generic content. Coordinated provenance edits are
being prepared separately. This packet is not full methodology completion.

Shared integration: register the new command and nested payload metadata; register
`ApplyMethodologyTemplateEntryHandler`, `MethodologyTemplateEntryApplicationAuthorizer`
and tenant-scoped `MethodologyTemplateEntryApplicationReactor`. Preserve canonical
opening/completion reactors. Root owns JSON contexts, DI, catalog generation and
public HTTP/MCP apply/creation/retirement/read cutovers. The new outcome fact's
optional destination reference preserves historical decoding.
