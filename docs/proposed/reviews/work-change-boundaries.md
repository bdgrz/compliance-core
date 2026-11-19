# Work queue and commitment boundary review

Date: 2026-11-05. Scope: proposed design only. The exact synthetic request and
response are [here](work-change-boundaries-jev-request.json) and
[here](work-change-boundaries-jev-response.json). No private source data or
credentials were sent. Jev is advisory; its probabilities do not establish
concurrency guarantees or a verified implementation.

## Source decisions and design consequences

- [M0-D15](../decisions/m0-d15-work-queue.md) prohibits assigning someone the
  source would refuse. A successful source read followed by a separate append
  cannot enforce this while eligibility changes; a source-owned admission or
  another proven atomic coordination is still needed.
- The accepted [R2 weekly digest decision](../decisions/r2-weekly-work-digest-delivery.md)
  prohibits automatic resend of ambiguous SMTP outcomes. This overrides the
  older, contradictory model judgment in the proposed context. A dispatch owns
  one member/week and its bounded attempts, not all items in the email. SMTP
  acceptance does not prove delivery. Bounded enumeration, deduplication and
  current visibility during composition/dispatch need a concrete mechanism.
- [M0-D09](../decisions/m0-d09-commitments.md) requires category validation
  against the current approved boundary at creation, revision and approval.
  Recording a boundary version does not prove it remained current through the
  commitment append. The protocol for authoritative cross-owner admission and
  recovery is still open. Link endpoint currentness has the same issue.
- Commitment drafts retain their authoritative revision facts. An author check
  can scan those facts one at a time for one reviewer and rely on a conditional
  append at the observed stream revision; this does not require an author roster
  in aggregate state. Storage paging, completeness and concurrency semantics
  must be verified before implementation is claimed correct. These are revisions
  of the same commitment, not independent children, but Portia hydrates the whole
  commitment stream; paging a separate author check does not bound that replay.
  A separate draft-episode owner is only a candidate until the one-open-draft and
  previous-version transitions are authoritative.
- A single eligible work-item witness is a candidate for the digest's nonempty
  decision. The actual email is assembled through read-side enumeration, not a
  write aggregate's item list. Source closure and permission changes can race
  the witness or send. Ordered, deduplicated output in bounded memory also needs
  an actual keyset/index or external workspace; neither is established merely
  by describing the source as paged.

## Jev review

`jev-1.14.0` returned these Noul probabilities for the concrete scenarios in
the retained request. They are not mathematical proofs and the question on
assignment did not produce a decisive signal.

| Question | Probability of yes | Interpretation |
| --- | ---: | --- |
| Does omitting item IDs from the dispatch event prevent listing items in the email? | 0.17 | Consistent with independently composing the email. |
| Can automatic resend after an ambiguous SMTP failure cause duplicates? | 0.73 | Consistent with the accepted delivery decision; do not auto-resend. |
| In the stated race, does commitment approval occur after the boundary removes the category? | 0.75 | Supports investigating the current-boundary race. |
| Does recording the earlier boundary version alone prevent that race? | 0.17 | Consistent with the explicit counterexample in the request. |
| Can the reviewer check every source revision without retaining all authors in memory? | 0.94 | Consistent with incremental scanning of complete source facts. |
| Does an expected stream revision conflict when a draft revision is appended after scanning? | 0.93 | Conditional on the actual event store honoring the expected revision. |
| Can an assignment commit after eligibility changes? | 0.46 | Inconclusive; the specified interleaving itself demonstrates the race. |

These are scenario judgments, not an exhaustive adversarial review. No general
high-confidence claim is warranted until the open admission, source completeness,
visibility and recovery mechanisms are specified and tested.

The [follow-up request](work-local-invariants-jev-request.json) and
[response](work-local-invariants-jev-response.json) gave 0.03 to an initial
open-and-visible witness proving authorization still exists at send, 0.07 to
constant-memory exact global sorting and deduplication under arbitrary paged
ordering without an index, repeated scans or external storage, and 0.14 to a
separately paged author check preventing full aggregate hydration. These are
advisory judgments for the stated examples, not proofs of deployed behavior.
