# Qualified criteria source: Jev adversarial review

Status: **open; no provider qualified**. This review covers
`criteria-qualified-source-integration.md` and the provisional Fitz and
Portia worktrees. It does not approve their implementation or deployment.
After this review, a separate provisional bounded area/realm enumeration
contract was added to the Portia worktree. Jev did not review that later
contract; it has no granting provider and is not executable qualification.

One bounded System One request to `jev-latest` returned `jev-1.14.0`.
The state comprised the integration proposal, Portia's provisional
observation/session/page contracts, short Fitz excerpts for missing-head
fallback, layout activation, retention, restore and counter transactions, and
an explicit note that no qualified Fitz provider exists. The request contained
no credentials. Values are Jev's probabilities that each statement is true;
they are review signals, not executable evidence.

| Judgment submitted to Jev | Probability |
| --- | ---: |
| A lineage token stored only in a storage image detects every rollback to an older copy of that image. | 0.08 |
| Missing resource metadata and an empty fallback scan prove the resource never existed. | 0.05 |
| A next-offset counter proves all earlier event bodies remain readable. | 0.05 |
| A current-head check alone prevents a later write or restore before a separate append. | 0.11 |
| A contiguous read beginning at a nonzero retained boundary proves no earlier event existed. | 0.05 |
| Exact facts with independently qualified offsets in the same realm incarnation and common committed order can be ordered across areas. | 0.81 |
| An ordinary broker-reported realm offset alone establishes qualified cross-owner ordering. | 0.04 |
| Separate qualified resource sessions with current heads establish one common instant across both resources. | 0.06 |
| An atomic, provider-qualified conditional append bound to the same source incarnation and captured head closes the check-to-append race for that exact resource. | 0.69 |
| An earlier participant fact and a later decision offset alone prove the decision checked that exact participant. | 0.06 |
| A caught-up numeric projection checkpoint proves every publication participant outcome was independently verified. | 0.04 |
| Replaying a snapshot of readable records into an empty resource proves the snapshot contained all earlier source records. | 0.05 |
| Every shared source checkpoint must wait for all child operations to reach terminal outcomes. | 0.53 |

## Source-grounded conclusions and open gates

- **Restore and rollback.** The local family incarnation rotates before Stream
  snapshot replay, including failed partial replay
  (`src/domains/stream/sink/snapshot.rs` in the Fitz worktree). It is not an
  exposed provider capability and does not protect previously committed
  decisions when an operator restores an older full image. An independent
  anti-rollback authority or demonstrably enforced coordinated recovery must
  handle durable observations and decisions; restart-local invalidation alone
  is narrower.
- **Genesis and retention.** Fitz resource metadata falls back to scanning
  fragments (`src/domains/stream/store/sequence_and_filters.rs`); the family
  layout marker says which format is in use, not that a resource never existed
  (`store/config_and_layout.rs`). Snapshot replay can omit expired events and
  reassign offsets (`sink/snapshot.rs` and
  `docs/development/domain-snapshots.md`). Expiry removes event bodies
  (`store/maintenance.rs`). An explicit new-namespace genesis and protected
  creation record would need qualified initial emptiness and storage integrity;
  existing or imported histories remain unknown. A retained boundary must
  account for holes and remain valid through the entire session, not just
  when the head is sampled.
- **Realm order.** Fitz writes realm counters in its conflict-checked promotion
  transaction with event rows (`store/compact_page_writes.rs`). This is a
  candidate ordering mechanism, not a currently qualified contract. Its
  actual serialized-commit behavior, delayed/ambiguous writes, routing,
  retention and incarnation must be established. Comparisons require exact
  facts in the same realm, route-family authority and qualified realm
  incarnation. An observed offset, event UUID or same-named realm by itself
  supplies none of those guarantees. Even qualified ordering does not prove a
  decision consumed the fact: verify the exact binding and participant
  protocol separately.
- **Read session and write race.** The provisional Portia
  `IQualifiedDomainEventReadSession` explicitly binds pages to one observation
  and warns that `CheckCurrentHeadAsync` cannot reserve the resource. Its
  observation carries source incarnation, retained interval and optional
  area/realm cuts; these are provider promises, not guarantees conferred by
  the data types. A later append must atomically test the exact incarnation and
  head at the storage commit boundary, and every cross-owner invariant still
  needs its own immutable participant evidence or fence. Jev's 0.69 answer is
  not decisive; the conditional append is sufficient for the **single target
  race only under that stated atomic provider guarantee**, not for unrelated
  owner changes or multi-source simultaneity.
- **Scope and checkpoint.** Portia's current provisional
  `IQualifiedDomainEventSource` opens an exact resource; optional area/realm
  fields describe that resource's coverage, not an enumerable realm-wide
  projection. A qualified contiguous realm checkpoint and exact publication
  marker need a separate provider contract and independently checked
  participant outcomes. Jev's 0.53 shared-checkpoint judgment is unresolved;
  design for durable per-operation signals so a blocked participant need not
  stall unrelated source intake, while ensuring the checkpoint never passes
  an event whose durable intake is unknown.

No build, test, or production-provider qualification was performed for this
review. Verify all stated guarantees with focused races and real broker-backed
tests after the implementation is complete.
