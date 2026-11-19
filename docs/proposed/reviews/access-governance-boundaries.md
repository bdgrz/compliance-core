# Access governance aggregate-boundary review

Status: **unresolved design review**, 2026-11-05. Existing product policy remains
in force. These are domain alternatives, not a validated implementation design
or authorization to weaken the rules.

## Boundary and policy conflict

One delegation can own one instance–member relationship; a review item can own
one decision, remediation and final outcome. Principal and relationship facts
can be individually addressed, and paths and hops individually represented. A
paged projection can report decisions, pending work and refusals. None requires
a parent to contain an unbounded list or a large content object.

The prior review proposed short-lived admission windows, exact sealed cuts and a
process that traversed every item before a parent completion write. **That is
not an acceptable resolution:** the parent write still depends on all children,
and windows chained per operation constitute a distributed per-item ledger.
Bounded memory or a small aggregate does not change this dependency. Do not
implement that proposal as a substitute for the user's boundary constraint.

Several existing product rules remain in tension with this constraint:

- A population must be complete and immutable, all assignments must reference
  a principal, and group nesting must be acyclic. Independently accepted facts
  alone do not establish those whole-population guarantees. Checking a new
  graph edge for cycles can require an unbounded path traversal.
- A campaign freezes an exact selected scope at launch and may be called
  completed only after every required item closes. Independently closed items
  and a projected report do not supply a durable all-items completion assertion.
- A bulk action identifies an exact selected set and its results. Independent
  commands grouped by an action identifier do not prove the user submitted all
  intended items or yield an authoritative whole-batch completion outcome.

Potential changes for explicit product decision: treat population acceptance as
an owner's attestation while reporting validation gaps; show campaign progress
from individual outcomes instead of writing authoritative campaign completion;
and group independent item decisions under an action ID instead of sealing an
exact batch. Each weakens or changes the existing assertion. In particular,
control occurrences and auditors must not treat a potentially lagging projection
as proof of campaign completion. Existing semantics stay in force until the
product decides. These alternatives are not implementation-ready.

## Local checks and observable progress — 2026-11-05

**Candidate graph invariant.** Give each principal in a population generation
one immutable integer rank. An independently owned nested-group edge names two
exact accepted principal identities and accepts only a strict increase from
source rank to target rank. Every directed cycle would require a strict increase
back to its starting value, so no cycle can consist entirely of such accepted
edges. This local property applies even when two edges are submitted
concurrently, provided both use immutable ranks from the same generation and the
edge owner establishes those exact endpoint facts. It does not require a parent
roster, a global gate, or a traversal when validating one edge.

The restriction is material. An acyclic observed graph can fail a particular
rank assignment; rejecting the validation must not erase the observation or
pretend the account has no access. Although every finite acyclic graph has some
topological ordering, obtaining or changing it may require global analysis and
external working storage; a write decision that itself requires every edge would
violate the stated boundary. Independently supplied ranks are merely a
candidate. Generation rollover, exact endpoints, late writes, arbitrary provider
graphs, role-assumption cycles and the accepted M0-D07 semantics remain open.
Validated edges do not prove that every source edge was submitted. This candidate
does not approve population acceptance or change the current product decision.

**Candidate reporting contract.** If the product adopts independent item
progress instead of an authoritative whole-campaign completion, expose these
separately:

| Observation | What can be said | What cannot be inferred |
| --- | --- | --- |
| Exact item stream says closed | That item's specified decision and evidence are final. | Other required items exist, were opened, or are closed. |
| A delivered operation has an accepted or refused outcome | That operation reached its named terminal result; an exact retry returns it. | Every item in a user selection was delivered or has a result. |
| Delivery or commit outcome is unknown | The operation remains unknown and needs reconciliation or a durable target fence. | A timeout means acceptance, refusal or safe retry of changed intent. |
| A report shows zero pending and a source checkpoint | Those are the observed rows and the report's stated source position and freshness. | The required item universe was complete, all openings were admitted, or the campaign has an authoritative `completed` fact. |
| An action ID groups received item commands | The received commands share that identifier and can be paged in a report. | The server received every item the user intended to select, or a whole batch completed. |

This contract improves visibility while preserving the distinction between an
individual authoritative decision and an advisory collective view. It remains a
product alternative: EA-07 and EA-11 still require their existing exact-set and
completion behavior until explicitly changed. Membership or delegation reads on
other streams still do not serialize a decision against a revocation.

## Jev review

Model: `jev-1.14.0`, invoked through System One, 2026-11-05. Exact request and
response JSON are retained for the
[initial](access-governance-boundaries-jev-request.json) /
[response](access-governance-boundaries-jev-response.json),
[follow-up](access-governance-boundaries-followup-jev-request.json) /
[response](access-governance-boundaries-followup-jev-response.json), and
[independent-decision](access-governance-boundaries-independent-jev-request.json) /
[response](access-governance-boundaries-independent-jev-response.json) reviews.
Values below are probability of **yes**, not proof or a confidence interval.

| Scenario | Jev yes probability | Interpretation |
| --- | ---: | --- |
| A campaign completion write that scans all required item outcomes still depends on all children | 0.91 | Supports rejecting the old scan-based remedy under the stated constraint. |
| An admission record per item traversed before sealing is a distributed per-item ledger | 0.53 | Inconclusive model judgment; the proposed chain visibly records each operation and sealing depends on it, so it remains rejected. |
| A projection of independent item decisions proves authoritative all-items completion | 0.03 | Does not justify changing the completion guarantee silently. |
| Replacing durable completion with projected item statuses weakens the original guarantee | 0.94 | Supports treating the change as a product decision. |
| Independent item decisions can be grouped by a user action identifier for a non-authoritative report | 0.89 | Supports the candidate reporting behavior, not exact batch semantics. |
| Cycle-checking a new edge can require examining unbounded existing edges | 0.91 | Highlights a real tension in population acceptance and graph validation. |
| Revoking one independent delegation necessarily revokes another | 0.04 | Supports per-relationship ownership. |
| An eventually consistent projection that may omit an item proves campaign completion | 0.04 | Does not support projection authority. |
| A complete sealed source plus every irrevocably closed item permits completion without parent outcomes | 0.74; follow-up 0.66 | Inconclusive; in any case such a scan violates the clarified constraint. |
| An unknown close append may be declared closed | 0.03 | Unknown remains unknown. |
| Same-item versioned stream can decide a direct/batch race without a parent owning items | 0.19; follow-up 0.22 for needing another owner | Apparent directional conflict; don't resolve via a model vote. Expected-version semantics and retry behavior need provider evidence and tests. |
| A second append at an already-used expected version can succeed | 0.28 | Inconclusive; verify the actual store contract. |
| On rehydration, a batch must refuse an item already directly decided under the stated rule | 0.86 | Supports the conditional retry rule. |
| A read projection seals a selection while an earlier selection can append | 0.08 | Projection does not supply a stable boundary. |
| An incomplete population scan proves complete source and valid references | 0.05 | Does not support acceptance on partial scans. |
| A digest of enumerated rows proves no omitted row | 0.10 | Digest is integrity evidence, not completeness proof. |
| An incomplete projection proves there is no applicable breach | 0.05 | Absence needs a complete authoritative source. |
| One account-entitlement pair can have excessive path content | 0.85 | Supports separate path/hop evidence. |
| An earlier membership read proves active membership at another stream's later commit | 0.04 | Cross-owner authorization remains unresolved. |

The additional [local-invariant request](access-governance-local-invariants-jev-request.json)
and [response](access-governance-local-invariants-jev-response.json) use
synthetic graph and review examples. Jev rated an accepted rank-increasing cycle
0.02, valid acyclic edges rejected by unsuitable ranks 0.93, incomplete edge
checks proving complete observation 0.03, and a checkpoint proving the required
item universe exists 0.02. It rated concurrent rank-increasing edges creating a
cycle 0.12 and a stale cross-stream authorization read preventing a later
decision 0.05. These answers are advisory and conditional on the supplied
premises; they do not establish endpoint authority, source completeness, actual
provider behavior, or product acceptance. The mathematical cycle argument is
independent of the model's score.

## Evidence and decisions still needed

Product must decide whether the whole-population, exact campaign scope,
authoritative all-item completion, and exact batch guarantees are required or
can be reformulated. If they stay, find an authority mechanism that satisfies
those guarantees without an all-child write or distributed ledger; none is
currently shown. If they change, specify what remains authoritative, how reports
expose incompleteness, and how controls and audit consumers treat the change.

Independently, specify authorization ordering across membership, owner, scope,
population, reviewer and remediation-owner changes. Establish replay,
expected-version, exact retry and unknown-append behavior with actual provider
contracts and executable concurrency regressions. Preserve M0-D07's single
account-entitlement review result and every traceable path without an unbounded
path array; reconcile its current row representation explicitly.

The boundary is not locked or ready for implementation. Jev narrows the review;
it cannot prove provider guarantees or make a product decision.
