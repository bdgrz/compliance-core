# Criteria publication authority and bounds review

Status: **locked design, not implemented**, 2026-11-05. Product direction: an
aggregate does not need to own all of its children for the system to be correct.
Canonical rules are [CR-09/10 and their owners](../contexts/criteria.md#decisions).
The recommendation to move all structural definitions into an edition-owned
manifest was rejected; each `Criterion` keeps its structural authority.

The 2026-11-05 direction supersedes the former whole-edition manifest proposal:
no aggregate or content object holds the complete edition. The edition publishes a
small reference to its exact frozen terminal window and source cut. Each criterion
owns its own facts; the retained admission chain and exact participant facts/fences
establish the accepted set. A small coordinator owns only admission and closure,
not the children. It allows one structural change in flight per edition, a
throughput tradeoff that must be qualified rather than concealed. Validation reads
source incrementally or in bounded-memory passes; it does not sort the history or
materialize the catalog in one object. Source-chain length and traversal time can
still grow.

No pending reservation may be forgotten: a no-fact refusal, cancelled command or
unrecorded no-op needs an exact target fence before admission is released,
including when the target stream is empty. A fence and an accepted write contend
on the same target; an earlier acceptance must be reported, never erased. A
terminal-window reference or digest alone does not prove a complete cut.

A window accepts at most 128 changes. It records at most one admission and one
terminal outcome per change. Opening, freeze, publication request, publication
outcome and closure consume at most five more records: **261 total**. Separately
emitted application audit records count against that same budget and reduce the
number of operations; capacity for an in-flight outcome and terminal controls is
reserved. Each serialized application record is capped at 8 KiB. Provider framing,
metadata and any provider-generated records remain to be qualified. A successor is
bound to its predecessor's terminal closure, and current-head discovery verifies
source evidence; a read model is only a hint.

The proposed 4,096-entry and 16 MiB edition-manifest limits are withdrawn: they
were introduced without a business requirement, and no whole-edition content object
is needed. The existing store's 16 MiB **per-object** technical limit does not
establish a catalog size requirement. Preserve the seed's partial-points-of-focus
and unsupported Privacy disclosures as individually scoped source facts; a missing
disclosure means unknown, not complete.

A per-code label claim serializes code reservation and rename without a global
roster. An unknown target outcome stays pending; a claim used by an accepted
create/rename is never released just because its acknowledgement was lost. Exact
refusal or target fence is necessary before releasing a new reservation; an old
claim releases only after the exact rename is accepted and the new claim confirmed.
Normalization v1 and the fixed seed ID are in CR-10. Reused-code claim history,
criterion lifecycle/summary history, program history and the number of draft
windows remain unbounded over time. Only the named per-window bounds
are claimed; no Portia snapshots, generic compaction, bounded total traversal or
safe claim rollover are assumed. The design therefore preserves small ownership
and bounds each window, but does **not** claim every aggregate has a finite
lifetime event-count limit.

## Jev evidence

The first five calls use `jev-1.14.0` and evaluated an earlier candidate that
included a whole-edition manifest. They are historical evidence; their
manifest-specific answers do not qualify the revised no-manifest design:

1. [Initial request](criteria-authority-jev-request.json) and
   [response](criteria-authority-jev-response.json): separate criterion owners
   .90; empty-child fencing .83; bounded conditional window .84; per-code claim
   .86; published source for program .89; partial seed proves completeness .03;
   separate summary authority .91. Snapshot-alone .30 and wait-before-seal .58
   were not decisive.
2. [Race request](criteria-authority-races-jev-request.json) and
   [response](criteria-authority-races-jev-response.json): late child after a
   snapshot .72; delayed creation on empty target .33; wait before seal .80; a
   128 × 2 + 4 conditional bound .19. These conflict with other answers or simple
   arithmetic; the model does not establish the outcome. The final design names
   **five** controls, making its conditional bound 128 × 2 + 5 = 261.
3. [Final request](criteria-authority-final-jev-request.json) and
   [response](criteria-authority-final-jev-response.json): independent criterion
   owners .82; late no-op can become a write .65; lost-ack release can permit
   duplicate claim .17; manifest hash alone proves complete cut .06; a specifically
   enforced 261-record budget is bounded .29; a per-window limit bounds all
   lifetime history .04; irrevocable publication source for program .85.

4. [Label and refusal counterexamples](criteria-authority-counterexamples-jev-request.json)
   and [response](criteria-authority-counterexamples-jev-response.json): accepted
   rename and a second reservation coexist .92; release before outcome known .77;
   delayed write after no-fact refusal .32; claim confirmation before target
   acceptance .08. The .92 answer reverses the earlier .17 on the more explicit
   label example; the .32 refusal answer remains inconclusive.
5. [Stalled writer request](criteria-authority-stalled-writer-jev-request.json)
   and [response](criteria-authority-stalled-writer-jev-response.json): a handler
   that already read admission and then stalls before append can append after
   publication .26; a target fence causes its original expected-version append to
   conflict .53. These do not establish the race behavior. Source semantics say
   that, if the target version stays unchanged and no other guard is rechecked,
   the already-prepared append can win; a committed target fence changes that
   version. Verify those assumptions with the actual event-store provider.

The .17 and .29 answers are recorded as disagreement, not clearance. The .92
label counterexample improves that narrow judgment but is not high confidence in
the overall protocol. The stalled-writer .26/.53 responses remain unresolved as
model judgments; they do not override the explicit expected-version trace. The label
protocol preserves the exact reservation while the target outcome is unknown,
which is the conservative source-authority rule; the cap follows only if the
specified budget is enforced. The .65 no-op signal is not treated as a definitive
model finding: the explicit delayed-retry trace demonstrates why a terminal
no-effect fact or fence is required. Jev is advisory and cannot prove the protocol.

## No-manifest recheck — 2026-11-05

The user rejected large content and god objects. The
[small-record request](criteria-small-records-jev-request.json) and
[response](criteria-small-records-jev-response.json) give .03 that independent
owners require a parent collection; .93 that a small cut reference with verified
source traversal can work without a bulk object; .03 that the cut reference alone
proves completeness; .88 that one in-flight change serializes changes; .11 that
bounded windows imply a bounded total window count; and .26 for the stalled-writer
fence question. Those are advisory; particularly the .26 is not a reason to drop
the target-side fence. The earlier contradictory stalled-writer judgments remain
unresolved pending executable source/provider tests.

## Qualification required during implementation

Run executable checks for edit/publish OCC races; parent removal/point-of-focus
admission; missing, truncated or stale source cuts; target absence and delayed
writes; committed outcome with lost acknowledgement; no-fact refusal/no-op and
fence races; 128-operation rollover and record/payload budgets including audit;
crashes/restarts and stale hints; label claim contention, accepted rename with lost
ack and stale releases; exact small source-cut proof and bounded-memory traversal;
partial seed disclosures;
and program creation/retry/tenant isolation over API, MCP and real stores. Verify
actual Portia/Fitz source-read and event-store guarantees before claiming the physical
bounds or publication completion. Existing native #1188 and assembly #1189 remain
separate qualification blockers.
