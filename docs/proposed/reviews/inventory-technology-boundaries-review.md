# Data-flow boundary review

Reviewed 2026-11-05 against M0-D08, TI-02, the assurance-work waiver and the
Jev request and response alongside this file. The model was `jev-1.14.0`.
These are advisory signals, not proof of a protocol or store capability.

## Local decision reassessment

The previously proposed authoritative source-cut and per-link completion pass
would still make one write decision depend on every carried asset. It is
withdrawn as a mechanism. M0-D08's versioned flow, 1..* assets and encryption
requirements remain in force pending a product decision.

Two candidates avoid that global decision: a separately versioned one-asset
flow, with related versions grouped for reading; or an anchored flow with an
independent, effective-dated decision for each additional carried asset. The
former changes flow identity and removes atomic multi-asset changes; the latter
changes immutable whole-set publication to incremental membership. Neither can
label an eventually consistent group a complete immutable historical version.
Both require a real per-asset authority for the classification, endpoint and
waiver facts at the accepted instant. An accepted link at time T does not prove a
waiver remains approved at later publication U. TI-02 still records later
classification changes and displays a gap.

The [local-invariant request](inventory-local-invariants-jev-request.json) and
[response](inventory-local-invariants-jev-response.json) rated an anchor proving
another intended asset 0.03, a waiver still valid after explicit revocation 0.05,
independent versions publishing together atomically 0.03, and an all-outcomes
approval depending on every asset 0.94. These scenarios support the distinctions;
they do not qualify source ordering, participant fencing, or either product change.

| Atomic question | Probability | Interpretation |
| --- | ---: | --- |
| Independent flow-to-asset owners avoid the parent asset list | 0.84 | Supports the split, not the complete protocol. |
| A designated asset ID in each version establishes at least one named asset | 0.87 | This is only cardinality of the reference; acceptance, activity and validity need proof. |
| A lagging projection proves at least one committed edge | 0.14 | Reject it as authority. |
| A classification read remains authoritative after a concurrent change | 0.03 | Reject the check-then-commit race. |
| An approved-waiver read remains authoritative after concurrent revocation | 0.03 | Reject the check-then-commit race. |
| A lagging projection proves all intended edges accepted | 0.03 | Reject a projection-based completion test. |
| Later reclassification requires a visible encryption gap | 0.78 | The existing TI-02 decision establishes this; the model answer alone is not decisive. |
| Independent asset owners alone prove atomic publication | 0.03 | The publication protocol remains open. |

Concrete counterexamples: (1) the flow reads a public asset, then the asset is
classified confidential, then an unencrypted flow commits; (2) the flow reads an
approved waiver, then the waiver is revoked, then the flow commits; (3) an edge
is delayed while the projection reports no pending edges, and the version is
published without a requested asset. Earlier successful reads or projection
counts do not make these commits correct.

The candidate anchor and per-version asset records are deliberately not a
complete solution. Before implementation, settle product semantics and establish
authoritative per-asset admission and outcome, including crash recovery,
refusal, cancellation, late deliveries and exact historical visibility, without
an all-child write decision or hidden cross-stream atomicity. Account for waiver
time and expiry, asset/endpoint lifecycle, and concurrent reclassification. If
that cannot be demonstrated with the event store's real capabilities, retain the
blocker; do not downgrade M0-D08 to an advisory rule. TI-02 continues to permit
reclassification after acceptance and requires a current gap when protection is
inadequate. Source decisions and candidates are in [the context](../contexts/technology-and-information-inventory.md).
