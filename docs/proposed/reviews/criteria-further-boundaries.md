# Criteria publication: smaller coordination boundary

Status: **coordination unresolved**, 2026-11-05. This
revisits CR-09's 128-operation windows; it does not establish a framework
guarantee or change CR-10's label-claim rules.

**Superseded product alternatives:** The product owner subsequently selected
editable draft incarnations and allowed program creation against a valid published
edition while unrelated entries remain pending. The append-only and operation-chain
alternatives below are historical review evidence, not the selected design. See
the current [criteria context](../contexts/criteria.md),
[draft lifecycle](criteria-draft-lifecycle-design.md) and
[program creation](program-creation-criteria-design.md). Provider and recovery
qualification remain required.

## Append-only draft candidate

Under the latest product direction, no write decision may require reading all
children. A candidate makes each accepted criterion identity, kind, edition and
category immutable for the draft, and each accepted point of focus retains its
exact immutable parent. The point-of-focus decision reads the parent's exact
prior acceptance, verifies criterion kind and matching edition/category, and
checks the same live draft generation. With no deletion or structural change,
that one reference check preserves parent validity inductively. Publication
checks a single exact, previously accepted criterion witness for nonemptiness;
it does not enumerate the edition. Every creation, including seed import, must
obey the rule. The witness alone does not establish the parent invariant.

**Unapproved product change:** earlier CR-09 allowed draft entry removal and
re-addition. In this candidate an incorrect identifier, kind, category or
parent requires abandoning the draft and building another edition identity.
Operators need a clear way to rebuild a draft, understand partial progress and
preserve provenance; copying existing entries may proceed individually. An
ordered abandonment must reject late writes and publication, and label reuse
must wait for safe release of the old claim. M0-D02 requires published-edition
immutability but does not specify draft deletion; this is still a real UX
restriction requiring explicit product acceptance. Consider authoring and
validating outside the authoritative draft before import, or an editable
per-entry draft lifecycle, but the latter needs its own no-all-child proof.

An ordered-source candidate puts independent structural proposals and the
edition's terminal publication/abandonment event in one durable commit order.
The published set is defined by the position of each independently valid proposal
relative to the winning marker; it does not depend on how far an evaluator has
progressed. If a valid proposal commits before publication but its participant
outcome is delayed, it is a member and the later outcome records that prior
decision. If a proposal commits after the marker, it is not a member, even if it
read `draft` earlier. The marker validates one exact, previously accepted,
nonremovable criterion witness. It does not wait for every other proposal,
outcome or source-reader checkpoint. This conclusion assumes the proposal's
per-identity winning status and every validity check are fixed deterministically
and authoritatively; a pre-marker proposal alone does not prove acceptance.
A lagging list is explicitly incomplete.
The marker's scope position must be discovered from the committed source; the
event cannot include its own not-yet-assigned position. Every participant
outcome and receipt must be reconciled with its own exact intent and source
position. Results that are lost or unknown stay pending; exact retries replay
the same durable outcome, changed intent under the same token is rejected, and
timed-out publication/abandonment cannot reopen admission while delayed
acceptance remains possible.

This is a product-semantic candidate: if the existing contract promises that
all member outcomes are recorded before publication, allowing later settlement
changes that promise. Such a change is not yet approved. It also depends on
validity being locally determinable for each proposal; cross-entry uniqueness,
mutable parent references or post-hoc external approval would invalidate the
argument unless separately governed. A candidate is to allow exactly one
structural proposal for a given `(edition, identifier)` stream as an append at
expected empty-stream position. A competing append reloads the winner and may
not use the new position to append a second candidate. Its exact retry or
changed-intent refusal must be durably attributable without adding an event for
every failed attempt to the entry stream. Invalid, ambiguous and timed-out
first attempts need a correct target-side refusal/fencing protocol: leaving an
empty stream is not itself a durable fence. This is an unqualified candidate,
not a demonstrated capability.

If conflicting proposals A and B for the same identity both commit before the
marker, A before B, later owner OCC on `CriterionAdded` does not make A the
winner. If B's worker writes first, worker scheduling can change the frozen
contents, and independent replay may select A. A safe alternative must either
prevent both proposals from committing, as above, or prove deterministic
per-identity source-order selection and durable outcomes. An ordinary read of
the current edition or actor permission is not an authoritative admission
check. All time-sensitive validity inputs must be captured or decided at the
business-required instant under a proven ordering/fence; later checks that can
change with worker timing cannot define historical membership. Until these
conditions, source replay and unknown-outcome recovery are proven, the purported
frozen membership is unqualified.

An available Fitz notice stream and ordered markers are a possible substrate,
not proof of this protocol. Qualify common routing/order, evaluator durability,
epoch scoping, parent-before-child acceptance, exact witness lookup, replay,
retention, marker loss, conflict and ambiguous commits on the actual broker and
packages. Portia exact-resource reads expose resource offsets rather than
directly comparable shared-area positions; a directly read confirmed
acceptance may provide causal evidence only if its ordering, durability and
epoch contract is proved. Alternatively qualify a real atomic cross-stream
primitive, if one exists. Never use a stale edition read or projection as the
fence. The source consumer may stream proposals for recovery and for an explicit
complete read-side view, but the publication write does not scan or wait for all
child records. Buffering or a single evaluator that serializes all child writes
would reintroduce an operational bottleneck even without a parent aggregate.

### Concrete order and recovery checks

| Sequence or failure | Required result |
| --- | --- |
| Valid proposal commits at 10, marker commits at 11, outcome is recorded later | Member at publication; later outcome materializes the already determined decision. |
| Proposals A and B for the same identity commit before marker; B's outcome worker runs first | Reject the protocol unless the initial append prevents the pair or a durable source-order rule selects A; generic later CAS cannot prove frozen membership. |
| Actor permission is revoked around proposal admission | Classify from an authoritative decision bound to the required instant, not a stale read or whichever state a recovery worker later observes; unresolved protocol means pending. |
| Draft was read before marker, proposal commits at 12 after marker 11 | Refused/nonmember, even if the participant's resource was empty. |
| Publisher's proposed witness first commits after marker 11 | Publication cannot use that witness; an earlier nonremovable accepted criterion is required. |
| Outcome or marker commit is unknown | Pending; recover exact identity, intent and committed source order, never assume refusal or release from timeout. |
| Published marker exists but a child outcome has not settled | A child-dependent command waits for that participant's durable acceptance and exact parent evidence; reads and readiness show incomplete/unknown, not absent or accepted. Whether Program creation itself can proceed remains a product choice. |
| Publication and abandonment race | One expected-position edition append wins. An ambiguous loser reconciles that actual event before reporting a terminal result. |
| Reader sees offsets 9 then 11 | It cannot claim complete membership through 11; recover 10 or visibly fail qualification. |
| Draft label is renamed while publication binds the previous version | Edition expected-position concurrency and confirmed label-claim protocol must reject the stale publication; no stale pre-read is authority. |

The focused [Jev request](criteria-marker-order-jev-request.json) and
[response](criteria-marker-order-jev-response.json) assumed a complete durable
shared sequence. Jev gave 0.96 for the valid pre-marker proposal remaining a
member, 0.03 for a post-marker proposal being a member, 0.03 that publication
must wait for that proposal's individual outcome, 0.05 that a read with a missing
offset is complete, and 0.02 that the assumptions establish deployed behavior.
These scores assess the supplied examples only; `valid proposal` already assumes
deterministic identity selection and all time-sensitive validity checks. They do
not establish either assumption, the provider, or the settlement protocol.

Jev's separate [request](criteria-inductive-jev-request.json) and
[response](criteria-inductive-jev-response.json) gave 0.95 for the specified
nonempty witness, 0.04 that it alone proved all parent relationships, 0.08 that
a stale draft read fenced the late append, 0.76 that the concrete late write
committed after publication, and 0.11 that a changed-intent retry was exact.
The no-child-scan question scored 0.34, an inconclusive result; it is not proof
of either design. The explicit traces and implementation qualification settle
correctness, not these model scores.

## Parent-incarnation alternative

Instead of forbidding draft removal, an independently owned point of focus can
reference one exact immutable parent incarnation and count as effective only
while that incarnation is included. Removing the parent makes its points of
focus ineffective by definition, without writing or enumerating them. Re-adding
the same parent identifier creates a new incarnation, so an old reference stays
ineffective. Restoring it requires an explicit new point-of-focus incarnation
with a reference to the new parent. The operator can correct the draft under the
same edition identity, but a correction can leave many points of focus needing
independent repair. The UI must expose the consequence and repair progress.

This changes the earlier CR-09 rule that refused parent removal while it was
referenced. Neither this alternative nor the append-only draft is product
approved. The effective-membership rule needs exact authoritative per-record
inclusion and incarnation evidence, and a coherent source boundary for parent
removal, child acceptance and publication. A publisher cannot use a parent it
read earlier as a nonempty witness if a concurrent removal wins at the
publication boundary. Prevent late writes, retries that revive old incarnations,
and post-publication membership changes. Unknown parent status fails closed.
An ordering mechanism and its race, replay, read-cost and retention guarantees
remain to be established; do not assume a projection or unqualified index is
authoritative. No write decides by loading every point of focus.

The separate [Jev request](criteria-parent-incarnation-jev-request.json) and
[response](criteria-parent-incarnation-jev-response.json) gave 0.05 that an old
reference reactivates after parent re-creation, 0.03 that checking one reference
reads all other points of focus, and 0.04 that a stale witness proves inclusion
at publication. It also returned 0.05 that the described correction stays in
the same edition even though that is explicitly stated in the supplied trace;
this conflict is retained and is not evidence against the trace or proof of the
design. Source and executable concurrency qualification remain necessary.

## Earlier coordination alternatives

These earlier alternatives are retained as review history. Any variant that
requires an all-child write decision conflicts with the latest product direction
and is not an acceptable final design.

The business rule is that publication freezes one complete edition, with at
least one criterion and no point of focus referring to an absent or wrong-edition
parent. Each criterion is independently owned. Consider first whether separate
participant edits can use a real source-owned admission fence, with publication
waiting for or fencing all preexisting admitted writers and validating a
complete indexed or otherwise source-backed cut. This needs an authority that
can enforce admission and prevent late writes; a read of edition status is not
such a fence. Parent removal versus point-of-focus creation also needs an
authoritative coordination rule. The actual substrate, precise concurrency
protocol, cut enumeration, failure behavior and proof have not been established.
Identifying all admitted writers must itself be explained without simply moving
an unbounded parent roster or per-child change ledger into another component.

An alternative previously explored is a chain of small draft operations. This
is an **unresolved fallback**, not the preferred fix. Although each stream is
small, the chain is an authoritative distributed ledger of every structural
child change. It serializes unrelated edits and requires whole-history
traversal. It can therefore preserve the user's design smell despite bounded
individual streams. Each operation owns one attempted structural change or
one publication attempt, never both, and retains at most one participant's
outcome. Closing it durably identifies the sole successor; only that successor
may open. A published terminal operation has no successor. A failed validation
or durably refused and fenced publication closes with a successor.

The predecessor closure and first-operation anchor must be verified from exact
committed source facts; a projection can only locate a candidate. Each new
operation has a deterministic identity, binds the exact predecessor and intent,
and wins its own expected-position append. Concurrent attempts for the same
successor cannot both own it. Cancellation, refusal, no-op and an ambiguous
result require the same target-side, exact-token fencing or accepted-outcome
proof as CR-09, including on an empty target stream. Unknown means pending; a
timeout or dispatch is not an outcome. Every structural writer, including
imports and retries, must participate. No new operation opens while the preceding
participant can still append. Publication itself must similarly be accepted or
durably refused/fenced before an unsuccessful attempt allows another operation.

The publication operation freezes a settled chain, verifies every operation and
cited child fact in order, and checks the entire cut. Its small committed request
identifies the exact source boundary and edition revision; the edition accepts
that exact request only. Parent existence and absence of live child references
are validated against the complete source cut while the operation has exclusive
admission. Bounded-memory relationship validation requires a complete qualified
index, external workspace, or repeated source scans; the last may incur
substantial traversal costs. None is automatically efficient, and the index's
coverage must be proved for the exact cut. A partial index or a bare
terminal identifier does not prove completeness. No aggregate holds a list of
criteria; no record or content object holds the full edition. Correcting an
entry's nonstructural summary remains independent after publication.

Each coordinator has constant business state and a constant number of operation
facts, assuming retries do not emit duplicate facts. This is a local logical
bound, not a bound on cumulative chain length, traversal latency, source storage,
per-criterion history, or provider read buffers. An edition serializes draft
structural changes and publication, and one stuck outcome blocks later changes.
Those throughput and recovery costs need operational qualification. A provider
or deployment can impose measured small record and read limits; the previous 128
operations, 261 records and 8 KiB are not established business rules. Portia's
event reader may fetch up to 1,024 records in a page; bounded domain state alone
does not establish a byte bound on provider buffers. The normal Portia aggregate
repository collects the full stream before hydration, so bounded current state
alone does not bound hydration memory. This proposal bounds the operation stream,
not the histories or hydration of an edition, criterion, or reusable label claim.
Portia normally writes aggregate audits to separate session streams; any
application facts written into an operation stream must remain within its own
constant event bound.

Portia's `IDomainEventWriter` requires an expected physical stream position, and
`FitzEventStore` appends to a single stream under that condition. Its empty batch
does not append a fence. A delayed handler which checked admission before
publication can still append to an unchanged child unless a real target fact
advances that child stream. A conflict must reload and reject the already-fenced
token. The declared reader contract is ordered and contiguous; source-level
contracts do not establish deployed read freshness, retention, crash recovery,
or ambiguous-commit handling. Qualify these with executable adversarial tests.

The separate Jev request/response in this directory records a concrete delayed
writer trace. Jev rated a delayed append after publication 0.91, projection proof
of no parent references 0.09, one-operation coordinators containing a criterion
collection 0.05, and bounded-memory validation with growing history 0.94. It
rated the simple expected-version question only 0.35 and the question whether
the proposal bounds total streams 0.43. These conflicting/indeterminate scores
are retained; source reasoning and executable tests, not the probabilities,
must settle safety.

Before considering this fallback, demonstrate exact predecessor/successor
uniqueness, all writer routes, target fence behavior on empty and existing
streams, changed intent and exact retries, late participants, competing appends,
publication/refusal fencing, source-cut completeness under crash and retry,
parent removal versus point-of-focus creation, and replay/retention behavior
against the actual configured provider. Confirm that linear history traversal,
one-at-a-time throughput, and pending-operation recovery are acceptable for
edition sizes and operational use. Evaluate the independent-edit alternative
against equivalent late-writer, parent-reference, exact-cut and recovery races.
Until an alternative meets these requirements CR-09 remains unresolved.
