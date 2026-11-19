# Control responsibility and evaluation boundaries review

Status: **partial design**, 2026-11-05. This review is specific to the proposed
control-environment context; it does not qualify runtime behavior or other
contexts. Sources: [control environment](../contexts/control-environment.md),
[M0-D13](../decisions/m0-d13-control-evaluation.md), and [M0-D19](../decisions/m0-d19-periodic-reviews.md).

## Decisions and concrete counterexamples

- Independent responsibility streams fail the no-same-holder rule: two commands
  can read both roles empty and each commit the same identity to its own stream.
  A single per-control owner with two fixed role slots serializes the identity
  check. This is a fixed-size cross-role invariant, not ownership of children.
  It does not prove non-overlap between two teams, authorize person actions, or
  resolve future effective-date intervals.
- Individual plan steps, inspected items, evaluation results, deviations and
  retest links have separate identities and owners. M0-D13 requires a frozen plan
  and authoritative results over every step. The earlier window-and-source-cut
  candidate has been withdrawn: even bounded windows form a child ledger and a
  full source fold makes the write decision inspect all children. Both conflict
  with the clarified boundary. The new ordered-marker candidate below avoids
  that write decision, but is not yet qualified.
- Checking "unfrozen" on one stream and writing another is a race. Missing target
  facts or a timeout cannot prove no delayed append. An index or equal counts do
  not prove exact coverage. These counterexamples remain valid even though the
  earlier source-cut proposal is rejected.
- A returned review must preserve unchanged prior results and a retest link must
  order against closure on the original deviation owner; observing "open" is
  insufficient. Their authority protocols remain to be specified.
- Occurrence support and requested actions are proposed as individual identities;
  an exact artifact version stays exact after source changes, but it does not prove
  that a later support link belonged to the sign-off's frozen set. M0-D19's set
  rule cannot be silently replaced by individual links, projection counts, a
  source scan or an admission ledger. Requested-action authority, minimum action
  semantics and M0-D19's management outcome vocabulary also need resolution.

## Jev review

The exact requests and responses are retained here:

- [Boundary request](control-boundaries-jev-request.json) and
  [response](control-boundaries-jev-response.json).
- [Concrete race request](control-boundaries-jevraces-request.json) and
  [response](control-boundaries-jevraces-response.json).

Both used `jev-1.14.0`; the following numbers are the returned Noul probabilities
for the named statements, not measured correctness rates or proofs.

| Statement | Probability |
| --- | ---: |
| Independent role streams allow both same-holder assignments to commit | 0.87 |
| One two-slot stream prevents that same-identity concurrent commit | 0.83 |
| Different team IDs prove no person can participate on both sides | 0.03 |
| Separate freeze checks permit a race (first, abstract formulation) | 0.65 |
| A concrete step edit can commit after evaluation start | 0.85 |
| A settled fenced window permits an old command to newly commit after freeze | 0.17 |
| Releasing an admission after timeout permits the delayed command to commit | 0.83 |
| Observing an empty target prevents a delayed first commit | 0.16 |
| Equal asynchronously projected counts prove exact current step coverage | 0.03 |
| A complete source fold requires keeping the complete list in memory | 0.05 |
| Reading only the new work episode omits an unchanged prior result | 0.91 |
| A retest link can commit after the deviation closes | 0.78 |
| A reactor can derive material findings from a retained verified cut | 0.70 |

The first abstract freeze question is not decisive; the second describes the
actual interleaving. The 0.70 finding-fanout answer is also a limited signal.
The source-cut and bounded-memory fold answers are retained as historical
counterexample probes, not an endorsement of that rejected design. Jev was not
asked to decide or invent the missing product rules.

## Required qualification

1. Decide effective dating, future/backdated corrections and waiver coverage over
   intervals, plus the policy for concurrent team deactivation/control retirement.
   Do not assume that current holder slots prove arbitrary historical or future
   separation.
2. Decide M0-D13 `not_tested` precedence and empty-assertion outcomes; specify
   step/deviation creation and replacement, retest link acceptance against
   deviation closure, and recovery of every participant operation outcome.
3. Resolve plan freeze and every-step submission authority without a write
   deciding over all children or logging a child ledger. Resolve M0-D19's frozen
   support set under that same boundary and do not claim per-item immutability
   alone freezes membership.
4. Define per-action authority, accepted review binding, materialization and
   terminal outcomes; reconcile approved/action_requested/deferred management
   sign-off with the generic review outcomes. Specify individual content limits.
5. Once authority protocols exist, verify concurrency, empty-stream fences,
   unknown outcomes, replay, review return, and retest/closure races with
   source-backed executable tests. No hard lifetime stream bound is claimed.

This is a docs-only review; compilation and tests were intentionally deferred.

## Local-write and ordered-read candidate, 2026-11-05

The [control context](../contexts/control-environment.md#candidate-individual-facts-and-source-ordered-review-boundaries)
now specifies a candidate for plan freeze, evaluation result, and management
sign-off without an all-child write:

- An exact plan epoch's individual accepted step revisions and the evaluation
  start marker share one qualified ordered source scope. Frozen membership is
  defined by that marker. Each step decides its own change; the start operation
  writes no roster or step outcomes.
- The submission records an evaluator's claim, not a proven `effective` result.
  A source-backed read derives M0-D13 outcomes from the frozen plan and exact
  result prefix; it stays pending if its complete frontier or participant
  decisions are unknown. A correction belongs to a later submission.
- An attestation can hold the fixed readiness and agenda references. Individual
  authorized support facts and a specifically reviewed cut in the same ordered
  scope define a candidate immutable evidence set. Facts beyond the cut cannot
  enter it. The read side enumerates the set through a proven frontier; the
  attestation write never loads all measures or records per-measure outcomes.

The marker by itself does **not** prove the signer saw the set: even the same
actor can submit an item that is delayed while they view a stale list, then
have that item commit before sign-off. The final prefix would include an unseen
item. The revised candidate binds a specific immutable cut C, uses a complete
consistent as-of-C view for the content offered to the signer, and excludes
facts beyond C even if they commit before the eventual sign-off marker. It does
not claim that the signer inspected every item. Capturing and retaining C,
stable pagination, attribution and whether conditional sign-off is allowed
remain open authority and product questions, not assumptions about Portia.

The current local Fitz specification describes commit-ordered area/realm
offsets, and the local Portia pattern reader checks consecutive scope offsets.
Those observations do **not** establish deployed version equivalence, same-scope
routing, durable retained history, behavior on `DROP` or ambiguous commit,
participant acceptance, exact signatory consent, or atomic projection/checkpoint
recovery. The known Cassie native startup failure also blocks runtime
qualification. A physical proposal is not automatically a valid accepted fact.
An ordered interpreter must not become a per-child business coordinator or a
source of parent event history. Projection coverage can support display of a
verified derived view; it must not be promoted to an authority for write guards.

These alternatives may change an API or user workflow that previously promised
immediate verified submission or synchronous rejection of incomplete work.
Product decisions remain needed on whether pending submissions can be reviewed,
how signers consent to support proposed by others, required measure evidence,
and `not_tested`/empty-assertion semantics. Preserve M0-D13 and M0-D19 unless an
explicit decision amends them. The candidate avoids assuming any answer.

An independent live `jev-1.14.0` probe used the exact
[request](control-boundaries-local-jev-request.json) and
[response](control-boundaries-local-jev-response.json). In the specified
synthetic cases, Jev rated late support membership 0.05, source order alone
proving consent 0.05, a lagging projection proving completeness 0.10, and a
submission write needing to enumerate all steps for a separate complete
projection 0.13. It rated the missing-result false-effective scenario 0.92 and a
later correction changing an earlier submission 0.09. A request to create a
finding was rated 0.03 as proof of acceptance. These questions assume the ordered
scope in their state; they do not prove the provider or application has it.

## Occurrence-specific Jev probe

`jev-1.14.0` returned 0.03 that a later link proves original membership,
0.97 that an immutable link still points to V1, 0.11 that duplicate delivery
creates two findings when identity uniqueness is enforced, 0.03 that the
projection proves the reviewed set, and 0.93 that an independently writable
link can arrive after the purported freeze. These are case-specific signals, not
proof that an alternative meets M0-D19 or that finding creation is implemented.

Exact request and response (no credentials):

```json
{
  "model": "jev-latest",
  "state": {
    "late": "An attestation A signs a reference to occurrence O but contains no list or commitment to supporting items. Later, a new independent support record S is created claiming it was part of A. S cites A and O. No admission, timestamp authority, or signature from the attester on S exists.",
    "immutable": "Support item S records the exact version V1 of an artifact. The artifact is later updated to V2. S is immutable and still points to V1.",
    "action": "A review requests two actions. Separate immutable action requests A and B each bind the exact review and have unique IDs. A reactor receives A twice and derives the same finding ID from A each time. Finding creation enforces unique IDs.",
    "count": "A reviewer sees a projection showing three supporting items and clicks approve. The approval event records only the occurrence and decision; the source supports can change concurrently and no set or revision is bound to the approval.",
    "membership": "An occurrence states that all supporting links are frozen at attestation. Links are independently writable on separate streams. The attestation checks only the occurrence stream. There is no shared transaction, participant fencing, or trusted ordering boundary."
  },
  "questions": {
    "late_link_proves_original_membership": {
      "type": "noul",
      "instructions": "In `late`, does S citing A prove the attester included S at the time of A?"
    },
    "individual_version_stays_exact": {
      "type": "noul",
      "instructions": "In `immutable`, does S still identify V1 after V2 is created?"
    },
    "same_action_finding_duplicate": {
      "type": "noul",
      "instructions": "In `action`, can replaying A create two findings for A?"
    },
    "projection_preserves_reviewed_set": {
      "type": "noul",
      "instructions": "In `count`, does the approval event prove exactly which three items the reviewer approved?"
    },
    "independent_links_violate_freeze": {
      "type": "noul",
      "instructions": "In `membership`, can a link be added after attestation even though the business rule says links are frozen?"
    }
  }
}
```

```json
{
  "model": "jev-1.14.0",
  "answers": {
    "late_link_proves_original_membership": {
      "type": "noul",
      "noul": 0.03
    },
    "individual_version_stays_exact": {
      "type": "noul",
      "noul": 0.97
    },
    "same_action_finding_duplicate": {
      "type": "noul",
      "noul": 0.11
    },
    "projection_preserves_reviewed_set": {
      "type": "noul",
      "noul": 0.03
    },
    "independent_links_violate_freeze": {
      "type": "noul",
      "noul": 0.93
    }
  },
  "usage": {
    "input_tokens": 644,
    "output_tokens": 115
  }
}
```
