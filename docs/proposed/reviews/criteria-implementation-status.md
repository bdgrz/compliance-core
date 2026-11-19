# Criteria publication and program creation: implementation checkpoint

Status: **in progress; not ready for use or review as a complete feature**.
The implementation worktree contains provisional domain owners, source-proof
interfaces, read models, program changes, transport contracts and focused tests.
No build or test run has been performed for this chunk.

The user rejected the proposed separate Fitz authority namespace, independent
recovery record, synchronous-only profile and coordinated rollback requirement.
The user also clarified that there is no existing application or deployed data
to migrate, and backwards compatibility and a legacy cutover are not required.
The new per-user platform operator owners and bounded primary seats can therefore
be initialized as the authority from the start; older roster references below are
historical implementation notes, not a migration requirement.
The implementation is being revised to use the existing Portia/Fitz committed
read, expected-position append and area/realm ordering contracts. Publication
still requires exact participant outcomes and fencing of real cross-owner races;
an uncertain append remains pending until the same operation is reconciled.
Read models are eventual and must not authorize writes.

The detailed checkpoint below records the earlier, more restrictive assessment.
It is historical, not a list of accepted prerequisites. In particular, neither a
new provider protocol nor the rejected recovery profile is required for normal
operation. The concrete stale cached resource-head case after an ambiguous Fitz
commit remains under investigation; its provisional fix has not been tested.
Authoring routes and worker definitions have been added to composition, but
their dependencies are still being integrated and the feature is not complete.

## Authority and provider dependency

The pinned Fitz 0.4.0 stream actor can retain a stale resource head after a
commit reports an error even when that commit was applied. A subsequent
resource read can therefore stop before an already committed event. Portia's
ordinary event-reader interface does not establish a fresh-to-head guarantee.
Consequently, a quiet page cannot prove a revocation, pre-publication entry or
other source fact is absent. A qualified provider capability and a real
authorization-path evaluator are required; the current source-cut coverage
implementation intentionally reports unavailable.

The current source-cut interface passes a resource offset and area cut but does
not bind the completed read to a lineage observation. Its signature must change
with the provider contract; comparing offsets alone cannot detect a restore or
source replacement between reading and checking coverage.

Label claims and edition events are in different areas. Their area offsets are
not comparable, and exact event references alone do not establish their commit
order. The participant verifiers require a separately qualified cross-area
causal-order proof; no production provider currently supplies one.
The required cross-layer evidence and integration steps are in
[criteria-qualified-source-integration.md](criteria-qualified-source-integration.md).

The source-backed proof interfaces and two-pass admission machinery do not
themselves provide those guarantees. Do not register an unqualified read as
authority or treat pending as an accepted outcome. The source qualifier must
also account for retention, truncation, recovery and stream continuity.

Fitz 0.4.0 stores the resource head with its records, but its public API does
not expose a qualified, constant-work durable-head read. Its internal fallback
can scan all resource pages when the head key is absent. A candidate provider
fix is to expose the durable head and stream lineage/retention guarantees,
without that fallback, and recover or invalidate cached heads after ambiguous
commits. The SDK and Portia must pass the guarantee through. This remains a
provider change to implement and verify, not an available capability today.
An isolated Fitz v0.4.0 worktree now contains provisional changes for both
ambiguous-commit head recovery and live-actor snapshot restore. These changes
have not been compiled or tested and do not provide lineage or retention proof.

For publication coverage, the tagged Fitz area store appends the area counter
and rows together and reads them in bounded fragments. A contiguous,
unfiltered projection that actually observes the exact publication marker is a
candidate prefix witness under verified append-only, no-expiry, stable-lineage
and authoritative-routing conditions. This is an incremental ordered scan, not
a write-model child roster. The current generic reader does not certify those
conditions, and a merely caught-up checkpoint is insufficient.

## Current implementation and remaining work

- Label reservation, fencing, refusal, activation and publication participants
  have provisional source-checked handlers and independently scheduled signals.
  Their workers remain disabled until source heads, release routes and scheduler
  recovery are qualified. Exact terminal receipts for several edition operations
  still remain pending even when their source facts exist.
- Entry and metadata/disclosure changes use one operation owner per client ID,
  separately owned target records, and a decision signal per proposal. Their
  source readers verify exact claims and decision evidence; draft-time decisions
  require a qualified current edition head. Summary no-change and stale-revision
  outcomes now have individual target facts and exact-claim retry handling;
  later execution generations and full durable recovery remain open. The provisional source
  verifiers check intermediate label and witness participants and require a
  qualified cross-area ordering provider, which does not yet exist. These
  authoring handlers and workers are not enabled.
- Program creation still needs qualified edition publication, current actor
  permission, and recovery. An unregistered admission coordinator evaluates a
  fixed primary-admin or organization-wide team path and rechecks its trusted
  route. Selection also has a separate provisional route for an organization
  or exact-program grant. An unregistered HTTP-only credential adapter binds
  the live validated Bdgrz request; MCP and other ingress remain unavailable.
  The complete-route resolvers and source adapter are not wired or qualified,
  so both paths remain unavailable.
  The current Bdgrz session token carries `user_identity_id` but no session ID.
  The provisional evaluator now permits that identity-only scheme only when a
  trusted resolver certifies the exact binding and current policy explicitly
  declares the additional session, account and global-veto owners inapplicable.
  No authoritative policy provider supplies that proof yet.
  A provisional low-churn policy head records one generation and immutable
  content reference; its activation authority, retained policy bytes, source
  qualification and deployment barrier are not implemented.
- The approved platform-primary replacement rule now has provisional per-user
  operator and two-seat primary owners. They are not authoritative until exact
  source-verifying handlers, independent participant recovery, qualified
  bootstrap and a fenced migration from the existing roster are in place. The
  initial primary confirmation does not claim all configured operators were
  seeded. Both owner slices and their focused tests remain uncompiled and unrun.
- Seed entries now carry a separate seed claim and are checked against their
  exact draft request and compiled seed content. An unregistered system-only
  writer can bind the fixed seed edition, submit individual metadata,
  disclosures and entry proposals, and start publication after a qualified
  witness check. It waits for independent entry and publication outcomes;
  durable scheduling and verified import coverage remain pending. An optional
  coverage contract requires incremental, exhaustive source proof but has no
  granting provider. The writer is not registered, and seed metadata is not
  general edition metadata.
- Versioned runtime read routes and bounded legacy edition bridges exist, and
  the catalog has independently indexed metadata/disclosure owners. Responses
  remain pending or redacted until an actual provider qualifies source
  continuity, identity, claims, decisions and the publication cut. The default
  coverage provider deliberately returns unavailable. Per-record size limits
  apply to individual events and response pages, not to an aggregate roster.
- Complete API and MCP write/receipt route registration, handler and projector
  wiring, and recovery only after the required authority is available. Then run
  the focused interleavings and repository gates listed in
  [criteria-implementation-acceptance.md](criteria-implementation-acceptance.md).

The edition owner now retains the selected operation's receipt and a fixed set
of participant facts instead of dictionaries for all historical operations.
Portia still loads the edition's entire lifecycle stream, so this bounds the
retained operation state, not the stream's lifetime or replay cost. Review
edition activity and retention before claiming a fixed replay bound; the
absence of a child roster alone does not settle that question. The selected
replay design and Jev review are in
[criteria-edition-selected-operation-review.md](criteria-edition-selected-operation-review.md).
The label claim similarly retains its current holder and one selected historical
reservation, rather than every reservation. Its full lifecycle stream is still
replayed, and delayed-participant cleanup is not yet fully scheduled.

## Integration boundary

The versioned tenant read routes and program create/selection routes are mapped.
Criteria authoring and operator receipt HTTP and MCP mappings and their handler
registrations are authored as uncalled extensions; they are not exposed. Before
activating them, supply the required admission and source providers. The authoring services have a
non-granting readiness check before appends; activation and shutdown still need
a coordinated boundary because that check does not fence a subsequent append.
Review and qualify the proposed operator route and MCP names as part of that
integration.

The label, publication, entry and value signal intakes, reactors, process
definitions, reconciliation handlers, participant handlers and runtime catalog
projector have an uncalled composition extension. They must be activated as a
single qualified graph with durable process storage and sweep recovery.
Do not enable the older direct label/publication reactors alongside it.
The runtime catalog projector needs a qualified incremental source checkpoint;
registering the projection service alone does not run it. The seed writer needs
a durable driver and import coverage proof.
