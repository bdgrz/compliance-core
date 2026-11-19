# Edition-label authority: concrete design candidate

Status: **recommended design**, 2026-11-05. This specifies the lifecycle needed
to implement CR-10 without an edition-label roster. It does not by itself qualify
edition publication, actor revocation, or the deployed event store. The
[criteria proposal](../contexts/criteria.md#edition-label-claim) remains the
product contract until these details are reconciled there.

## Business outcome

A normalized edition label identifies at most one draft or published edition.
A published edition keeps its label. Draft creation, rename and abandonment are
recoverable; a lost response never permits a second edition to take a label
whose first holder may still commit. Each label has one claim owner. An edition
owns its current label and at most one in-flight label transition. Neither owner
stores criteria or other children. These are low-churn owners of their own
business decisions; no arbitrary lifetime event-count limit is introduced.

The client supplies `EditionId` and an operation ID in the normal command/API
contract. Operation identity is scoped to `(EditionId, operation ID)`; the same
operation ID on a different edition is a different identity, not a globally
enforced conflict. A rename or abandonment has a new client-supplied operation
ID for that edition. An exact retry returns its original pending or terminal
result; reusing that scoped identity for different intent, including a different
destination label, is a conflict. Global operation-ID uniqueness would require
another authoritative owner and is not assumed. The
original authenticated actor is part of the accepted intent. Internal commands
derive stable identities from the committed operation, claim identity and step;
they do not replace client identity or expose internal reactor operations.

Labels use the versioned normalization already proposed in CR-10: trim outer
ASCII spaces, lowercase ASCII letters, then validate the documented structured
edition-code format. Every writer computes the same normalized claim identity;
future normalization changes need an explicit migration and collision review.
The existing `2017_tsc_2022_pof` seed keeps its existing edition ID and receives
a deterministic claim through a separately verified seed transition. The
documented length and character restrictions are a product-facing contract and
must be checked against existing data and accepted before exposing them; the
normalization itself does not prove any reservation.

## Participants and source evidence

- `EditionLabelClaim(normalized_label)` owns `available`, or one reservation or
  confirmed holder `(EditionId, operation ID, generation)`. Its own stream's
  expected-position append decides competing reservations. A generation is
  the stable `(EditionId, operation ID)` tuple for that intent; it differs from
  every later use because the edition refuses operation-ID reuse for changed
  intent. Source facts
  retain what is necessary to return the original outcome on a retry; a release
  or confirmation applies only to the exact current holder and generation.
- `CriteriaEdition(EditionId)` owns the edition's creation intent, current label,
  draft/published/abandoned lifecycle and at most one pending rename. Its stream
  serializes begin, change, cancellation, publish and abandon. It must check
  exact intent and expected stream position on every writer path, including
  retries and imports. A published or abandoned edition never accepts a later
  rename. The pending transition concerns one label operation, not child entries.
- A reliable reactor dispatches individual commands from committed facts and
  reconciles uncertain outcomes against exact source events. It has no business
  authority of its own. The target owner verifies the relevant committed source
  fact and exact operation/generation, rather than trusting a projected label or
  an unverified callback. A successful send or timeout is never an outcome.

## Creation

1. An authorized create command appends a small `EditionDraftRequested` fact to
   the edition's empty stream, binding edition ID, normalized label, framework,
   operation ID and actor. This is a **pending creation**, not a usable draft or
   a claim that the label is available. A replay of the same command finds the
   original intent; a different create intent at that ID conflicts.
2. A reactor asks the label owner to reserve that exact edition/operation and
   generation. Only one competing reservation can commit. If another edition
   holds the code, the edition records and exposes a terminal label-unavailable
   refusal for this creation; that fact fences later activation under the same
   intent. A transient no-event response from the label owner alone is not that
   durable terminal outcome. No draft is reported ready. The edition keeps its
   stable ID and enters `needs_label`. The operator can issue a distinct
   `ChooseInitialEditionLabel` operation with a **new** client operation ID,
   exact edition ID and new label. That begins a new claim attempt only after
   the previous attempt is durably refused or fenced. An exact retry of the
   original create still returns its original label-unavailable result; changed
   intent under that operation ID conflicts. No entry can be added and no
   publication can begin before an initial claim is confirmed. If the earlier
   attempt is ambiguous, the edition remains pending until reconciliation or a
   durable cancellation/fence, rather than assuming it was refused.
3. After a successful reservation, the edition records an initial-label
   acceptance referencing that exact reservation, the claim confirms the exact
   committed acceptance, and the edition marks creation complete. Until that
   completion fact, entry creation and publication remain blocked. Until confirmation,
   API/MCP reads and exact retries report pending with the same operation ID.
   A crash at any step resumes from the committed participant facts; it does not
   reserve again under a new generation or report a completed draft early.

Starting with an edition-owned intent avoids needing an empty-stream fence for
the ordinary create path. If an implementation instead reserves first and sends
the first edition write later, cancellation **must** append a real fence at the
empty edition's expected position; a no-op or empty append does not fence it.
Such a fence must permanently prevent the same creation intent from being
accepted, including delayed retries, and must report an already committed create
instead of overriding it.

`ChooseInitialEditionLabel` appends its own edition-owned label-selection intent
and follows the same reserve, activate, confirm and complete sequence. It cannot
supersede an ambiguous previous selection without its durable fence. A delayed
reservation for a previous refused or cancelled selection is reconciled and
released using that selection's exact terminal fact. If it temporarily holds
the same label the new selection wants, expose cleanup as pending and retry;
never treat the stale reservation as the new selection's success. Source history
retains each selection's own outcome for exact retries; the current edition state
holds only its active label or one in-flight label selection.

An operator may cancel pending creation before the edition records completion.
Cancellation appends a terminal fact on the edition stream and prevents any
later activation under that edition ID. A delayed claim confirmation or
completion callback checks the exact creation/selection ID and pending edition
state: an append using a pre-cancellation version conflicts, and after reloading
the terminal edition it is refused. Claim cleanup reconciles the cancellation,
including a confirmation that arrives late; the label stays reserved until the
exact claim generation is released. If creation completion wins the edition
race, cancellation refuses and ordinary draft abandonment is a separate
operation. A lost response is resolved from these committed facts, never by
assuming the cancellation or activation won.

## Rename

1. The edition accepts `BeginRename` only for a draft with no other pending
   rename or unresolved publication attempt and a different valid destination.
   It appends the operation, exact
   source revision, old label/generation, new label and actor. The old confirmed
   claim remains held. A concurrent publish or abandon either commits first and
   rejects the rename, or loses to this intent and cannot complete while it is
   unresolved. This is one edition-stream decision.
2. The new label owner reserves the exact pending operation. If the label is
   unavailable, the edition records a terminal refusal for the operation; the
   old label remains active. If reservation commits, the edition can commit the
   label change only while the exact rename is still pending and the new
   reservation is held. A read of the reservation is not sufficient unless the
   reservation cannot be released before an authoritative terminal outcome or
   target-side fence. It names both the old and new label generations.
3. The new claim confirms the exact committed edition label-change fact. The
   edition records rename completion from that confirmation. While the change is
   committed but confirmation is missing, the edition's source label is the new
   label and API/MCP status is explicitly pending; publication remains blocked.
4. The old claim releases only after verifying the exact committed change away
   from its current holder and generation and the new claim's confirmation.
   Once the new claim is confirmed, a delayed old release merely makes the old
   label temporarily unavailable. An old release cannot clear a newer holder or
   generation. Cleanup is durably retried and visible; it is not inferred from
   a rename request or client disconnect.

If rename X→Y is complete while release of X is still pending, an immediate
rename Y→X can encounter X held by the same edition under its *old* generation.
That is cleanup-pending, not proof another edition owns X and not permission to
reuse the old generation. Reconcile and release X's exact old claim from the
committed X→Y change and confirmed Y claim; then reserve X under the new Y→X
operation generation. Preserve the new rename's pending state and exact retry
throughout. If X is genuinely held by another edition after cleanup, record the
ordinary unavailable outcome.

An operator may request cancellation before the edition label-change fact. The
edition must record an exact-operation cancellation/fence on its stream. If the
label change wins that race, cancellation is too late and recovery completes the
accepted rename; it cannot undo or report a refusal for an already accepted
change. A new reservation is released only after its owner verifies the matching
target cancellation/fence, or a permanent target state that provably excludes
that operation. A returned error, elapsed deadline, empty write, or a read of
absence does not establish a fence.

A reservation can arrive **after** the target's cancellation: a worker may have
read the pending intent before cancellation and commit to the independent claim
afterward. Reactions must run for both committed source facts. On a newly
committed reservation, reconciliation reads the edition's terminal fact for that
exact operation and releases the matching reservation when fenced. Processing
the cancellation once while no reservation exists is not sufficient. If that
reconciliation is unavailable, the label remains visibly reserved; it must not
be silently reused. The same rule applies to a delayed duplicate reservation
after the edition has durably recorded label-unavailable refusal: if the other
holder releases the label in between, a stale worker may still reserve it, but
the refused edition cannot activate. The new reservation event must trigger
reconciliation and release. A successful internal reservation is provisional;
it is not a successful client creation or rename.

## Abandonment, publication and retries

An edition may abandon only after its own pending label transition has a definite
terminal outcome. Its `EditionAbandoned` fact is permanent and names the exact
current label generation. The label claim releases only after verifying that
fact; late create or rename commands reload the edition and refuse. If
publication wins the edition-stream race, abandonment is refused and the
published label stays held. If abandonment wins, publication is refused and
label cleanup is retryable. A publication with unknown commit outcome remains
pending until the exact edition event is found or a definitive refusal/fence is
established. The label protocol does not claim to solve criteria-entry ordering
or publication membership.

The selected CR-09 protocol records `CriteriaPublicationStarted` on the edition
before obtaining the exact witness. That durable attempt binds the current label
and generation and prevents rename and abandonment until its exact outcome is
durably published, aborted or fenced. Starting is not publication success. An
aborted attempt leaves the edition draft and permits a subsequent rename; a
committed `CriteriaEditionPublished` permanently forbids rename. A lost outcome
stays pending until reconciled. No step authorizes scanning the edition's entries.

A direct terminal append with no durable attempt is an **unselected alternative**.
It does not block rename just because a client response is unknown; a safe
variant would need exact revision binding and expected-position conflict
handling. It is not the selected publication protocol.

### Publication does not need a separate label-claim pin

The edition-owned publication attempt is sufficient **if all of the following
claim and edition invariants are implemented together**:

1. The edition begins an attempt only from a completed draft that names its
   exact current label and generation, with a durable matching claim
   confirmation. An initial-label acceptance that is still pending is not a
   completed draft. The attempt appends on that same edition stream at the
   expected version and binds the same label and generation.
2. While that attempt is pending or its outcome is unknown, the edition cannot
   rename or abandon. A published edition cannot later do either. A durable
   attempt refusal leaves the edition draft and re-enables those transitions.
3. The claim owner may release its **current confirmed** generation only on an
   exact committed edition change away from that generation or a committed
   abandonment of that edition. Cancellation of an initial or rename attempt is
   only release evidence for that attempt's reservation; it cannot release an
   already completed, currently active selection. Every release compares the
   current edition and full generation. No timeout, advisory read, administrator
   override, cleanup job or direct store writer bypasses this rule.
4. A generation `(EditionId, operation ID)` is never reused for a later label
   selection. The edition accepts changed intent under a new operation ID only,
   and a stale claim release cannot change a later generation. Seed migration
   establishes the same edition/claim relationship before the new path is open.

For an active edition at `(X, G2)`, a release authorized by a previous change
away from `(X, G1)` cannot clear `G2`. A same-generation `G2` release needs a
committed change away from `G2` or abandonment. Once the publication attempt
commits, the edition owner cannot issue either fact until the attempt is
definitively refused; if it succeeds, it can never issue either. A publication
attempt racing a rename uses the edition's expected position: one wins, the
other reloads and obeys the resulting state. Therefore the claim stays held
through that attempt without an additional per-publication event on the label
claim. This is a participant invariant proof, not a claim that a stale
`ClaimConfirmed` read independently fences a release.

The unselected direct-append alternative would instead let per-edition
expected-version append decide publication versus the first rename or
abandonment. An ambiguous client response would not itself prevent a later
transition. The selected durable-attempt protocol avoids relying on that
different lifecycle.

If an operator can force-release a confirmed label, a cleanup path accepts an old
generation, a writer bypasses these guards, or an unknown publication is
treated as refused, the proof fails. In those cases a source-level repair is
required before relying on it. A declined publication releases its edition-owned
attempt and then an ordinary, fully confirmed rename may release the old
generation; a delayed publication callback must be fenced by the edition's
terminal attempt outcome. No separate label pin or pin-release recovery process
is part of the recommended design.

Portia `v0.10.0`'s `IDomainEventWriter.AppendAsync` documents the expected
physical position of one addressed stream, and that version's
`FitzEventStore.AppendAsync` passes expected offsets to its one-stream append
session and commits it synchronously. These source facts support the proposed
per-owner ordering, not cross-stream atomicity or the yet-unimplemented release
guards. Verify the deployed versions and test every allowed writer, unknown
commit, and event-replay path before claiming the invariant is implemented.

| Scenario | Required result |
| --- | --- |
| Two editions concurrently reserve the same normalized code | One claim-stream append wins. The other receives a durable refusal, or remains pending while the winner/response is unknown. |
| Initial label is unavailable and the operator corrects it | Keep the same edition ID in `needs_label`; a new operation ID selects another label. The original exact create retry still returns its original refusal. |
| Reservation or target append committed but acknowledgement was lost | Reconcile the exact operation and generation. Never release or issue a fresh intent merely because of a timeout. |
| Publish races the start of rename | The edition stream orders the decisions. If publish wins, begin-rename rejects; if rename intent wins, publication waits for its terminal settlement. |
| Rename acceptance races its cancellation | One edition-stream append wins. A delayed worker reloads and honors that outcome; cancellation cannot override a committed rename. |
| Cancellation commits before a delayed destination reservation | The later reservation is reconciled against the exact cancellation and released; until then it is held, not silently available. |
| Rename committed but claim confirmation or old-claim release is lost | Show the operation pending or cleanup pending as appropriate; retry from source. The new claim stays held and no other edition can take either held code. |
| X→Y completed, X release is delayed, and Y→X starts | Keep the new rename pending, finish exact old-generation release, then reserve X with the new operation generation. |
| Initial creation is cancelled before a delayed claim confirmation | Its target terminal event fences activation, including on callback retry; reconcile and release its exact claim. If completed activation won first, cancellation is refused. |
| An old-generation release arrives while publication holds the current generation | The release is refused or a no-op; the current confirmed claim remains held. |
| Publication is refused, then rename wins and an old publication callback arrives | The rename may proceed; the stale publication callback cannot append or block release after the exact refusal fence. |
| A label is released and later reserved under a new generation | An old release, confirmation or retry cannot clear or reacquire the new generation. The old operation returns its original result. |
| Same `(EditionId, operation ID)` is reused with a changed label or original actor | Return conflict; do not reserve the new label or reinterpret a completed operation. The same raw operation ID for a different edition has a different scope. |
| User is revoked between an authorization read and edition decision | An early check alone is not proof of authorization at the later instant. Apply the approved access-authority protocol or keep the operation pending/refused; its exact ordering is a separate qualification. |

## Jev and qualification

The exact synthetic [request](criteria-label-claim-jev-request.json) and
[response](criteria-label-claim-jev-response.json) used `jev-1.14.0`. Jev gave
0.87 to the premature-release duplicate trace, 0.90 to a delayed reservation
remaining leaked without reconciliation, 0.06 to a stale generation releasing
the new holder, and 0.03 to a prior permission read proving authorization at a
later decision. It gave 0.19 to an append at position zero succeeding after a
real position-zero fence, 0.16 to an empty append preventing that write, and
0.04 to changed intent being an exact retry. These are judgments on supplied
traces, not evidence that the store, authorization system or reactors implement
the stated assumptions.

The follow-up [request](criteria-label-correction-jev-request.json) and
[response](criteria-label-correction-jev-response.json) covered correction on
the same edition ID and migration races. Jev gave 0.93 that the corrected label
belongs to the original edition, 0.05 that a later successful attempt changes
the original refusal on exact retry, 0.09 that a stale reservation proves the
new operation's success, and 0.05 that the described legacy collision is already
resolved. These synthetic results support inspecting the cases; the target
stream, claim writer and recovery behavior still need executable qualification.

The adversarial [request](criteria-label-races-jev-request.json) and
[response](criteria-label-races-jev-response.json) also address rename-back,
creation cancellation, publication refusal and scoped identities. Jev gave 0.14
that a rename-back should reuse the old claim token, 0.12 that delayed claim
confirmation activates a cancelled edition, and 0.06 that the same raw token on
two different editions denotes the same scoped operation. It gave 0.54 for a
question about retrying while cleanup is pending; that answer is inconclusive and
does not establish liveness. Exact race traces, source contracts and executable
tests decide these conditions.

The [publication/label request](criteria-label-publication-pin-jev-request.json)
and [response](criteria-label-publication-pin-jev-response.json) exercised the
conditional no-extra-pin argument. Jev gave 0.05 to an old-generation release
clearing the current claim, 0.14 to a valid current-generation release being
created during the locked attempt, 0.95 to release under an explicit bypass, and
0.27 to a separate claim pin being necessary. It gave 0.37 to another edition
reserving the held code, an inconclusive answer for the stated trace. These
numbers do not prove the guards or the release invariant; the proof above and
the required competing-append and recovery tests are the relevant qualification.

The state is `creation_pending → needs_label → creation_pending → draft` when
an initial label is unavailable and then corrected. The current source uses a
fixed platform catalog rather than a `CriteriaEditionDrafted` event; migrate its
actual fixed seed identity and label into the authoritative claim path. If any
other existing edition-event streams are found, map their documented facts to a
completed initial-label selection without changing stable edition IDs; do not
assume such streams exist. Before enforcing the new claim path, establish one
claim for each existing normalized label, including the fixed seed. A
normalization collision or uncertain previous ownership is a migration conflict
requiring explicit resolution; it must not be resolved by silently choosing a
winner. Existing and new writers must share the claim gate during cutover, or
edition creation and rename must be paused while the claims are established.
The exact legacy-event mapping and cutover readiness must be validated against
the actual source data before production use.

Portia 0.10's `IDomainEventWriter` exposes expected **single-stream** physical
positions; `FitzEventStore.AppendAsync` returns without a write for an empty
batch. The local source does not establish a cross-stream transaction. Before
locking the implementation, verify actual configured versions, authoritative
exact-source reads and retention, expected-position conflicts after an ambiguous
commit, token-specific refusal/fence behavior, event-driven plus recovery-driven
reconciliation, writer coverage, activation/query semantics, and actor
authorization ordering. Add meaningful executable races and crash/retry tests
for every scenario above, including the cancellation-before-late-reservation
case and label reuse. Read-model discovery must never be the claim authority.
