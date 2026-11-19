# Retention boundaries: local decisions and authoritative hold ordering

Status: **candidate, not qualified for implementation**, 2026-11-05. This review
replaces the earlier protection-window proposal. It does not change accepted
product policy or assert that the required storage transaction exists.

## Invariants and independent records

M0-D16 requires an artifact's retention to have elapsed and no active covering
hold before disposition; a decision requires an Org Admin and can be batched.
RT-03 adds specific actors and report/package prerequisites to engagement hold
release. Historical RT-06 orders a hold against an authorization; whether that
authorization survives a hold activated before physical deletion is unresolved
against M0-D16. A valid later hold remains recordable. RT-07 says
a retention extension after authorization is shown as `disposed_before_extension`.
RT-05 adds a whole-batch completion guarantee; M0-D16 itself permits batching
but does not prescribe such an event. Neither a stale projection nor a successful
dispatch proves any of these outcomes.

Use independent owners: one legal hold per hold ID; one engagement hold per
examination; one hold-generation/artifact coverage record; one later-only
retention record per artifact; and one disposition per requested artifact.
A batch reference can group user requests for display without becoming a source
roster. A per-item command handles a single artifact and its direct business
facts, with an exact operation identity, intent and outcome. An exact retry
returns its original outcome even if holds have changed in the meantime. Changed
intent under the same operation ID conflicts. A timeout leaves the result
pending until the exact outcome is recovered or a delayed command is fenced.

Per-item outcomes and a projected worklist do **not** prove the prior RT-05
all-items completion guarantee. Replacing that guarantee is a product decision;
until resolved, no new all-items completion should be presented as equivalent.

## Candidate authority for one artifact

The ordering decision needs to establish whether any active organization-wide
hold or active coverage for **this artifact** exists. A possible source-owned
index holds independent current entries keyed by tenant and hold for
organization-wide holds, and by tenant, artifact, hold and generation for
individual coverage. A single indexed existence predicate can yield one blocking
hold or prove the relevant ranges empty. It does not require hydrating a parent
with all artifacts, scanning every child, sorting a dataset, or collecting all
hold IDs in memory. Rows are index entries, not a holding aggregate.

The index cannot be merely a lagging read model. Hold activation, release,
coverage and this disposition's authorization must share a real serial order.
The existence check and decision must be indivisible in that order, including
an insertion into a previously empty relevant range. Two alternatives need
actual qualification:

1. **Serializable decision transaction.** The index update, relevant predicate
   check with phantom protection, and durable decision/outcome are in the same
   serializable authority. Publication of any domain event must be atomic with,
   or durably recoverable from, that same authority. A database transaction
   followed by an unrelated Portia append is a dual write and can split.
2. **Ordered authoritative decision processor.** Independent hold/coverage and
   per-artifact disposition intents share a proven committed ordering scope. A
   durable decision processor applies each intent in order, and atomically
   advances its authoritative index, exact outcome, checkpoint and outbox.
   Callers remain pending until their specific durable result. This is new
   centralized infrastructure, not an ordinary asynchronous projection. It
   must demonstrate safe recovery, no duplicated decisions, correct retained
   history, and operational throughput and availability. It must not reconstruct
   all hold history for every authorization or retain a parent per-child log.

The current Compliance `PersistenceEventStore` and Portia expected-position
append establish a single-stream write, not either transaction above. Local Fitz
specifications describe ordered area/realm commit positions, but actual deployed
versions, same-scope routing, durable visibility, retained records, scope reset
and `DROP`, processing checkpoint/outbox atomicity, and ambiguity recovery are
not proven. Observing a notification or an empty read is not a complete frontier.
If the authority cannot be proved, leave the item pending: do not ship a
projection check, a count, or a cross-stream read as a legal-hold guard.

Example ordered outcomes, assuming a qualified authority and no other blockers:
`hold H placed`, `authorize A`, `release H`, `authorize B` yields H placed,
A refused as held, release accepted, then B eligible for its own decision. An
exact retry of A after release returns the original refusal. Separate overlapping
holds remain independent: releasing H must not erase a different active hold.
This example decides authorization only; it does not decide whether Evidence
may delete bytes after a new covering hold becomes active.

The earlier fixed-size window chain is **withdrawn**. Even if each stream is
small, its sequence is a distributed per-operation ledger; reconstructing
active holds by walking it on every decision grows with history and serializes
unrelated work. Source cuts and pagination do not make that an acceptable
all-child write decision.

## Scope, delivery, and product semantics

For a selected-artifact hold, each membership is independent. If A's coverage
is accepted and B's is still pending, the application must not say the entire
selected scope is active. Separate member admissions can expose a window where
B is not yet protected. A tenant-wide pending barrier during scope staging is
one possible stricter policy, but the activation, release, abandonment, recovery,
late-command fencing and expected operator experience are unresolved. It is
not the same as the entire hold being active.

For an engagement delivery, one possible local rule establishes the artifact's
coverage before exposing that artifact's bytes. It does not establish the prior
whole-delivery atomic ordering; changing that expectation needs an explicit
product decision. Already external disclosures remain recorded as truth.
Protection must not be inferred solely from an eventually delivered reactor.

Authorization and physical deletion are separate decisions. Consider an
authorization accepted at 10:00, a covering legal hold activated at 10:01, and
Evidence asked to remove bytes at 10:02. The historical RT-06 interpretation
would let the earlier authorization stand, while M0-D16 says an active hold
blocks disposition; whether it protects still-present bytes is unresolved.
Pending that product interpretation and implementation proof, the conservative
candidate does not initiate or continue known deletion under an active hold.
This does not assert that an already-in-flight storage delete can be stopped.
If protection extends to physical deletion, the authoritative hold ordering
must include a durable fence at the irreversible Evidence/storage boundary:
checking again immediately before sending an unfenced delete still races a
new hold. A lost delete acknowledgement stays unknown until the actual storage
outcome is reconciled. A hold cannot retroactively restore bytes already
removed, and the deletion and later hold must both remain visible as facts.

RT-07 expressly addresses an extension *after* authorization. M0-D16's
eligibility rule also raises the case where an extension committed **before**
authorization but a lagging advisory read missed it. Its intended meaning and
source-authority requirement are unresolved. Do not extend the exception for a
late extension to earlier committed facts without a decision.

## Jev review and remaining evidence

The [local-authority request](retention-local-authority-jev-request.json) and
[response](retention-local-authority-jev-response.json) use `jev-1.14.0` on
synthetic traces. The model rated the missed phantom race 0.94, overstating
partially applied scope as active 0.96, timeout as unknown 0.92, and a crash
between independent index and event writes 0.95. It rated the claim that the
indexed single-artifact decision must load all children 0.05. These are
conditional judgments, not proof that a provider implements the transactions.
The prior [window request](retention-boundaries-jev-request.json) and
[response](retention-boundaries-jev-response.json) are retained as historical
review evidence; their favorable assumptions do not reinstate that design.

Before implementation, prove the actual authority and index source, complete
range/phantom semantics, hold placement versus disposal and release races,
unknown commits, exact retry and changed intent, lost removal acknowledgement,
overlapping holds, staging/abandonment, actor checks, retention extension timing,
disclosure ordering, physical deletion versus hold activation, retention and
crash recovery. Decide RT-05's batch claim, whether a preexisting authorization
survives a hold before deletion, and staged-scope/whole-delivery semantics
explicitly. No build, test, CI or
runtime qualification is asserted by this design review.
