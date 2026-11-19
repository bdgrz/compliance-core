# Criteria bounded owner redesign

Status: **design direction; source qualification and implementation remain open**. The earlier criteria edition implementation retained dictionaries of operation, participant, fence, and abort histories. The current implementation retains only the selected operation and a fixed set of participant facts; see [its review](criteria-edition-selected-operation-review.md). Portia still loads the edition's full stream, so selection does not establish a fixed replay bound. This document is a proposed rollover design, not an assertion that its extra machinery is necessary for a low-churn owner or that its storage assumptions are qualified.

## Required properties

- An edition decides only its current label and publication lifecycle. It has no list of criteria, operations, callbacks, or past generations.
- Any one loaded edition-generation owner and operation-attempt owner has a fixed upper bound on events and state. There is no assumption that total activity, retained history, operations, children, or attempts are bounded.
- The committed edition publication fact is the authoritative structural cut. It references the exact pinned witness and confirmed label. Entry and edition sources remain in one source ordering scope; no child scan is required to publish.
- A retry with the same scoped operation ID and intent finds the same result; changed intent is refused. An unknown append result stays unknown until authoritative source or an effective target fence resolves it.
- No projection, cache, client-supplied proof, lease timeout, or missing event is authority for these decisions.

## Bounded owners and source records

Use a deterministic operation identity `(editionId, operationId)`, also scoped by identifier for an entry operation. The operation has an immutable, create-once intent record containing its normalized intent digest, authenticated actor, target and initial authorization provenance. Exact create retry resolves to this record; a different digest or actor is a conflict. Human admission is produced by the server and includes the target and current execution epoch in its digest.

Each execution attempt has a distinct deterministic address derived from the operation identity and attempt generation. Its owner contains only the admission for that execution, the exact target source identity and expected position, and one terminal result or fence. An attempt has a fixed finite transition set. Retrying an already recorded step reads its exact fact; it does not append another copy. A new attempt is possible only after the prior target execution has been conclusively fenced. Repeated attempts create more independently addressed, bounded streams, not a list in either owner.

A bounded **operation head**, addressed by the stable operation identity, identifies the immutable intent, current attempt generation, and terminal receipt if any. It requires an authoritative conditional-update primitive whose stored state is replaced, rather than a Portia stream that grows on every update. Its compare-and-swap token protects the transition from an old attempt to a new one. Terminal receipt stores exact source event address and identity, not merely a claimed result. A durable operation head is also the point lookup for receipts. `pending` means an intent or attempt exists without a terminal outcome; absent is distinct from refused and remains uncertain where an append result is unknown.

An edition-generation owner holds just the current confirmed label and claim generation, draft generation, at most one active lifecycle transition and its operation/attempt identity, publication ordinal, execution epoch, and terminal publication or abandonment facts. It retains no previous operation receipts or participant results. The operation head/attempt and exact participant streams answer historical questions. For an independently identified entry, a corresponding owner keeps its current incarnation, revision and at most one active mutation or witness; the same bounded-history and rollover requirements apply to its own stream.

## Edition generation rollover

The edition requires a bounded, authoritative **edition head** at a deterministic address. It stores the current monotonically increasing generation, that generation's exact stream address, and a conditional-update token. This head must also be a replaceable conditional-update record, not an indefinitely appended Portia aggregate. Generation addresses derive deterministically from edition ID and generation number; the head discovers the current one, while an explicit historical generation directly addresses an earlier source. A retained, independently addressable seal and successor fact links generations for audit and recovery; the head does not carry a growing list.

Each generation has an enforced event-count and serialized-byte budget, with space reserved for a seal. The actual limits and maximum allowed individual fact size require storage qualification and product decisions; they are resource limits, not lists of children or a manifest. When the next ordinary event would exceed either budget, writers help or wait for rollover instead of writing it. All external source references, pending transitions, and the current publication/witness state must fit the bounded carried state.

1. A writer loads the edition head and its generation, then conditionally appends a `GenerationSealed` fact at the observed edition source position. The seal freezes that owner and records a bounded, exact state handoff and deterministic successor identity. No ordinary event is accepted after the seal.
2. A writer creates the successor's initial fact with the handoff and exact seal source reference. The creation is idempotent and create-once. The successor is not externally writable yet.
3. A conditional head update from the sealed generation to that successor makes it current. A losing writer reads the winning head and source facts. Either participant can resume from the seal or successor after interruption; neither invents a new successor or skips the handoff.
4. Every ordinary write targets the head's generation, validates the matching epoch, and uses the target stream's expected position. A stale append racing the seal either commits first and forces seal retry from the new state, or loses to the seal and cannot be rebased. Reauthorization is required before any newly fenced execution is attempted in the successor.

The conditional head is a discovery and admission fence; it is not enough on its own to stop an append already in flight to the old stream. The old stream's seal and expected-position check provide that target-side fence. Likewise, creating the successor before the head moves must not allow a writer to use it early. Published and abandoned editions must preserve the same terminal semantics across rollover and remain directly verifiable from their committed terminal facts.

## Coordination, callbacks, and recovery

Operation intent creation and an edition decision are not one transaction. An operation head can exist while its target decision is pending. The internal coordinator reads the exact current edition head, obtains admission for that epoch and intended target, and attempts the edition append at the expected position. A committed edition fact references the exact operation and attempt. The coordinator records the corresponding terminal receipt only after verifying the committed edition and participant facts. A failed or timed-out receipt update is repaired from those facts; it does not repeat the business decision.

If a target append has an unknown result, inspect the exact target source. If the result cannot be resolved, append an explicit fence at the target's expected position. If the decision wins, the fence loses and reconciliation reads the decision. If the fence wins, a late append at the old expected position loses. A fresh attempt uses a strictly later execution epoch and fresh authorization; no handler may silently rebase a previously admitted operation. A rollover seal can provide the equivalent old-generation target fence only if its source position and epoch prove that the unresolved attempt could not have committed. Exact proof conditions need qualification.

Callbacks identify their operation attempt and the exact committed source event. A receiver reads and checks source address, event ID, type, edition, generation, attempt, and payload before affecting another owner. The active edition transition must still match. A delayed callback for an earlier generation cannot mutate the current edition merely because its operation ID matches; it instead finds its recorded terminal outcome or performs the explicit participant cleanup required by the protocol.

For labels, a pending operation retains only its exact destination claim identity and generation, while the current edition state retains the old confirmed claim until the new one is confirmed. A stale reservation after cancellation must be released using committed cancellation and reservation evidence. A delayed confirmation cannot revive an abandoned or replaced transition. For publication, abort first commits to the edition; the draft stays blocked until the exact witness owner commits the matching pin fence. A delayed first pin must fail that fence. Publication succeeds only for the live attempt with source-verified witness pin and confirmed label, and is irreversible.

Source receipts and cleanup survive rollover: references use exact stream generation and event identity, not only the current edition head. Participants may be retried indefinitely, but retries return their exact existing result without appending repeated facts. Recovery scans or timers can discover unfinished work through rebuildable indexes; those indexes only trigger exact authoritative reads and never decide outcomes. Loss of an index must not make pending work irrecoverable: provide a source-derived rebuild or separately qualified durable scheduling mechanism.

## Race examples

| Race | Required outcome |
| --- | --- |
| Draft append and initial recovery fence | One expected-position append wins. The winner's exact committed fact determines whether to continue the draft or authorize a later attempt; an unknown result is never reported as refusal. |
| Rename decision and generation seal | If rename wins first, the handoff includes its pending transition. If the seal wins, the stale rename cannot append to the sealed generation and must obtain fresh admission for the successor. |
| Publication pin and abort | The edition records either publication or abort first. After abort, target-side witness fencing prevents a delayed first pin; publication remains unavailable until cleanup is proven. |
| Cancellation and delayed label reservation | The committed cancellation prevents activation. The reservation is located from source and released using its exact operation/generation and cancellation proof. |
| Receipt update fails after a committed decision | The operation remains pending until reconciliation verifies the decision and updates its head; retry does not append another decision or claim the decision failed. |
| Crash after seal, before head update | Readers and helpers discover the seal, create or verify its deterministic successor, and conditionally advance the head. No writer skips the sealed source or independently chooses a different successor. |

## Retention and operational requirements

Keep immutable intent, attempt, generation, seal, participant, and terminal source facts for as long as exact retries, audit, callback reconciliation, and published-edition verification are supported. An operation head or exact identity tombstone must survive that same retry contract. Do not delete a referenced generation or participant fact while a later generation, receipt, or delayed callback may need it. If finite retention is required, define an explicit expiry contract and a safe archival/lookup path first; deletion must not turn an old ID into a reusable new intent. Total retained history can grow even though each loaded owner is bounded.

Operational diagnostics should expose the active generation, current transition, operation and attempt IDs, source positions, recovery/fence status, and age of pending cleanup. Discovery and repair need bounded page or streaming access; they must not materialize all editions, entries, or historical operations to make a write decision.

## Decisions requiring source qualification

1. **Conditional mutable records.** Verify whether the deployed Fitz/Portia stack offers durable, linearizable compare-and-swap and create-once records with the necessary failure, recovery, and retention semantics. A conventional Portia stream for either head reintroduces unbounded loading. If unavailable, select and qualify a different authoritative primitive or explicitly narrow the product guarantee before implementing.
2. **Stream sealing and expected position.** Verify exact concurrency behavior, position types, replay validation, cancellation after unknown commit, and whether a stale writer can physically append after a seal. Invalid post-seal appends must not silently corrupt hydration or permit a decision.
3. **Ordering across generations.** Prove that edition generations, entries, witness fences, and publication facts remain in one retained ordered source scope and that a published source cut has the intended ordering. Qualify source-reader pagination and continuation semantics; do not substitute timestamps or projections.
4. **Atomic discovery and recovery.** Prove conditional head advancement, deterministic create-once successor behavior, crash recovery at every handoff point, and no write access to an unactivated successor. Determine how unfinished operations are durably rediscovered without relying on a lossy projection.
5. **Bounds.** Choose and enforce count, byte, and fact-size limits, reserve rollover capacity, and qualify individual serialized events including admission evidence. Identify owners that can still grow, including operation attempts, label claims, entry histories, and any new control source.
6. **Cross-owner authority.** Specify and test exact committed fact verification for every label, witness, operation and edition transition, including durable receipt repair and all unknown-outcome races. A UUID alone is not proof.
7. **Security and retention.** Verify trusted internal seed admission, epoch-bound human admission, delayed callback authority, long-term retry guarantees, archival, and the consequences of any policy-driven deletion.

Jev may help challenge these assumptions, but a model answer is not evidence that any storage primitive, ordering guarantee, or race resolution exists. Require source inspection and executable race/replay tests after the complete implementation is assembled.

## Adversarial Jev review and source check

The exact state, fourteen questions, and raw results are in [the request](criteria-bounded-owner-jev-request.json) and [response](criteria-bounded-owner-jev-response.json). Jev returned model `jev-1.14.0`; the numbers below are its probability for **yes**, not measured correctness or implementation evidence.

| Exact question | Yes |
| --- | ---: |
| Does state.lookup_alone bound the memory needed to load an edition? | 0.06 |
| Does moving the head in state.head_only itself prevent the delayed old-stream append from committing? | 0.11 |
| In state.seal_race, does a committed seal prevent the competing old append from also committing at that same expected position? | 0.84 |
| Does the missing response in state.unknown_append establish that the append failed? | 0.03 |
| In state.receipt_race, is the business decision still committed despite the failed receipt update? | 0.96 |
| In state.receipt_race, is it safe to repeat the business decision without first checking its exact source fact? | 0.03 |
| In state.pin_race, does the edition abort alone prove the witness has been fenced? | 0.05 |
| Does state.cancellation_race describe an explicit cleanup for the delayed reservation? | 0.91 |
| Does state.design require the edition owner to load a roster of its criteria before deciding publication? | 0.05 |
| Does state.design require all retained historical data to remain finite as the number of operations grows without limit? | 0.05 |
| Does state.unproven establish that the necessary storage primitive actually exists? | 0.03 |
| Does the rollover in state.design rely on an authoritative conditional mutable head rather than an indefinitely appended head stream? | 0.95 |
| Could deleting operation identity records allow an old operation ID to be mistaken for a new intent? | 0.86 |
| Does the conditional design in state.design by itself prove that its concurrent storage races are correctly implemented? | 0.03 |

The local Portia source's `AggregateRepository.HydrateAsync` accumulates each event from the current committed position in a `List<DomainEvent>` before loading the aggregate; a newly constructed owner starts at the beginning. Its `SaveCoreAsync` sends `CommittedStreamPosition` as the event store's expected append position. This repository's `PersistenceEventStore.AppendAsync` passes that expected position through to its inner store. These observations substantiate the full-load concern and show the intended target-fence mechanism; they do **not** establish the deployed backend's physical durability, seal enforcement, exact error behavior, or correctness under races. The inspected sibling Portia checkout is not itself proof of the precise deployed package; this repository pins Portia 0.10.0.

The sibling Portia `FitzKvCheckpoints` source shows that Fitz KV read-write transactions take a resource-level lock and that checkpoint values are read through a transaction. This suggests a candidate for a bounded mutable head, but does not establish that this deployment provides a durable, linearizable business-authority CAS with the necessary restart, unknown-commit, retention, or atomic interaction with event streams. In particular, a Fitz lease token is explicitly tied to in-memory, TTL-based ownership and cannot substitute for a durable edition head. Qualifying the actual client, broker and storage configuration remains a blocker.

The seal result is conditional on the exact expected-position premise given to Jev. Even if that premise is verified, a buggy writer physically appending after a valid seal could corrupt future hydration; the implementation must enforce and test the post-seal rule. The operation/head and generation design remains unimplemented and is not ready to claim as a proven bounded solution.
