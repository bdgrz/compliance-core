# Finding and corrective action boundaries review

2026-11-05. Scope: finding action admission, action outcomes and reviewed
closure. This review reopens AW-09 and records a proposed change to AW-02. The earlier
all-actions-terminal closure rule conflicts with the user's clarified constraint
that no write decision depend on all children; its replacement is a product
decision, not an established behavior.

The previous design recorded each admission and terminal action outcome in the
Finding stream. It made the closure race decidable but required the finding to
manage an unbounded set of children. A read model showing zero open actions
cannot supply authoritative closure. The candidates and remaining questions
are in [assurance work](../contexts/assurance-work.md#finding).

## Current candidate: independently reviewed resolution

A finding closure would mean an authorized independent reviewer accepted the
specific proposal's rationale and evidence that the deficiency was resolved.
Corrective actions retain their own status, owner and due date; they are not
completed or cancelled by closure and may remain open or finish later. The
reviewer can return a proposal if known unfinished work makes its evidence
unconvincing. The decision does not require discovering, counting, folding or
checking every action. Action views remain advisory.

This changes the former business meaning, which guaranteed all accepted actions
were terminal at closure. The UI must make the distinction clear. Keep the
previous prohibition on new action planning while closing or closed unless
changed explicitly; a race-safe per-action admission protocol for it is still
missing. Evidence existence, immutable reviewed versions and late associations
also remain unresolved. An all-actions count or distributed ledger is not a
solution under the clarified constraint.

For the original guarantee, consider two histories with identical finding and
closure inputs, but an independently admitted action is terminal in one and
open in the other. A decision that neither reads the action nor any authoritative
fact reflecting it must make the same choice in both. If it permits closure, it
violates the original rule in the second history; it could trivially preserve
safety by refusing all closures, which does not make the system usable. A human
attestation does not make the guarantee mechanically authoritative.

## Alternative: derived all-terminal close

Another candidate separates a small independently reviewed resolution from the
finding's **derived** closed state. A complete source-qualified read model can
show `closed_as_of` only when a current resolution is approved, action admission
is sealed, and every admitted action is completed or cancelled. Actions remain
independent owners; Finding records no per-action outcome. This retains the
all-terminal meaning for the displayed status without a write decision over the
children. It does not itself create an authoritative current closure event.

The proposal changes the prior lifecycle: previously both proposal and approval
were refused until all actions were terminal, then approval emitted
`FindingClosed`. Under this candidate, resolution review may happen earlier and
closure becomes a later read result. A certified as-of result is not equivalent
to a durable event or an atomic current-state guard when reopen can race. Product
and downstream consumers must explicitly accept or resolve those differences.
The existing M0-D15 queue additionally requires **at least one action and every
action completed**; cancellation suffices for AW-02's terminal predicate but not
for that work item. M0-D15 also excludes action owners and completers from
ordinary closure reviewers. Enforcing that separation without consulting all
action histories in a write decision remains unresolved. Do not conflate the
queue and closure rules or treat projected reviewer eligibility as authorization.

A potential source-order admission seal and its qualifications are recorded in
the [context](../contexts/assurance-work.md#alternative-independently-reviewed-resolution-and-derived-closure).
The projection's checkpoint can advance after a pre-seal proposal only if a
pending row for that proposal is retained atomically. Discovery through a marker
does not establish settlement: a pre-seal proposal with unknown outcome must
keep that finding pending. Duplicate outcome deliveries need idempotent effects;
loss, rebuild, scope gaps, retention, delayed target acceptance, and reopen or
return episodes require explicit testable treatment. Earlier-round open actions
still count unless an approved product rule says otherwise. These are proof
obligations, not claims that the deployed infrastructure already meets them.

## Jev evaluation

TypeSafe System One `jev-latest` returned `jev-1.14.0`. A noul is the
probability of *yes* for that question, not an overall confidence score. The
model is advisory. Requests and full responses, including exact wording, are
preserved beside this review:

- [Frozen action chain request](finding-boundaries-jev-request.json) and
  [response](finding-boundaries-jev-response.json).
- [Concrete failure traces request](finding-boundaries-traces-request.json) and
  [response](finding-boundaries-traces-response.json).
- [Scalar count candidate request](finding-boundaries-counter-request.json) and
  [response](finding-boundaries-counter-response.json).
- [Independent-resolution request](finding-boundaries-independent-request.json)
  and [response](finding-boundaries-independent-response.json).
- [Indistinguishable histories request](finding-boundaries-independent-traces-request.json)
  and [response](finding-boundaries-independent-traces-response.json).
- [Derived-status request](finding-boundaries-derived-status-jev-request.json)
  and [response](finding-boundaries-derived-status-jev-response.json).

| Judgment | Jev yes | Interpretation |
| --- | ---: | --- |
| New reviewed-resolution policy changes the original closure meaning | 0.96 | Supports making this a visible product decision. |
| Existing action can remain open and complete after finding closure under the candidate | 0.96 | Supports separate lifecycles, not a guarantee that every finding is substantively resolved. |
| Reviewed resolution can be decided without querying or folding all actions under the candidate | 0.94 | Conditional on sufficient exact evidence and meaningful review. |
| Finding closure under the candidate proves all actions terminal | 0.03 | Supports the explicit non-claim. |
| Independent closure automatically preserves the separate no-new-action admission rule under races | 0.09 | Admission coordination remains necessary. |
| Candidate still has consequential unspecified domain, authorization, evidence or admission decisions | 0.96 | Keep the proposal partial. |
| Identical decision inputs in the two action histories yield the same decision | 0.83 | Supports the direct indistinguishability argument. |
| Approval in both histories violates the earlier all-terminal rule in the open-action history | 0.90 | Supports the conflict with the earlier invariant. |
| Frozen complete action chain and irreversible action outcomes prevent unsafe closure, given all stated assumptions | 0.92 | Supports the conditional claim, not the implementation or acceptability of a logical child ledger. |
| Complete chain can be checked in bounded memory with paged source iteration | 0.85 | Supports streaming rather than collecting or sorting actions. |
| Frozen chain candidate still lacks consequential protocol details | 0.90 | Further work required. |
| Duplicate deliveries of a terminal event can make an unprotected count reach zero while another action is open | 0.89 | Supports the concrete exactly-once concern. |
| Releasing freeze after proposal timeout can allow a late proposal after a new action | 0.90 | Supports retaining freeze or proving a target-side fence. |
| A verifier can commit a stale proof after freeze is released | 0.87 | Proof creation and freeze release need explicit ordering/fencing. |
| Reading an empty target as refusal permits a delayed create in the stated trace | 0.54 | Inconclusive. The described ordering itself is a counterexample: a later create can commit to the still-empty stream. It is not evidence that an unfenced empty read is safe. |
| Duplicate delivery or lost settlement acknowledgement double-decrements under the pinned-turn candidate | 0.07 | Conditional support, assuming every stated pin, immutability and replay rule can actually be implemented. |
| Candidate can propose closure while an accepted or pending action remains unresolved | 0.23 | Inconclusive; do not claim high-confidence qualification. |
| Candidate leaves material implementation details unspecified | 0.96 | Strong signal to keep it partial. |
| Candidate's turns collectively form one aggregate owning all child actions | 0.35 | Inconclusive. Review the semantic ownership and event histories directly; splitting a roster across streams does not by itself remediate the smell. |
| An independently owned action can record its terminal outcome without appending to Finding | 0.93 | Supports the owner split; admission remains unproved. |
| Resolution approval can be recorded without reading all actions when it is explicitly not closure | 0.88 | Supports the conditional separation, not equivalence to original AW-02. |
| A complete, current projection can compute the all-terminal display without a parent roster | 0.75 | Directional only; completeness and cursor guarantees are assumed, not established. |
| A lagging projection that has missed an admitted open action can incorrectly show closed | 0.72 | The stated missed-action trace is unsafe regardless of this intermediate score. |
| Approving before an action is terminal changes the original review ordering | 0.51 | Inconclusive model signal; the documented command precondition directly establishes the change. |
| A derived display alone equals a durable authoritative `FindingClosed` event | 0.04 | Supports the distinction; exact downstream requirements still need review. |
| An admission seal alone proves all actions terminal | 0.04 | Supports maintaining separate settlement outcomes and pending state. |
| A cancelled only action requires the M0-D15 completed-only review item | 0.21 | Supports the distinction from the AW-02 terminal predicate. |
| Considering only a new round can omit an earlier associated open action | 0.79 | Supports carrying the earlier action into the all-terminal predicate. |

An abstract question asking whether the original rule can be guaranteed without
whole-child information or a policy change returned 0.06 for yes. Its wording
also permits the degenerate case of refusing all closures; a follow-up on that
case returned 0.71. The explicit histories returned 0.83 and 0.90 as above, and
the direct reasoning is recorded rather than treating either score as proof.

The concrete failure traces also exposed inconsistent model judgments: an
abstract late-proposal question returned 0.15 but the explicit race returned
0.90; an abstract empty-target question returned 0.28 while the explicit trace
returned 0.54. Do not average or choose the convenient score. Those races follow
from their stated orderings and must be tested against the eventual implementation.
Jev alone does not establish source retention, exact stream reads, atomic append,
ID uniqueness, fencing, liveness, authorization, or recovery.

## Disposition

The earlier scalar-count and action-admission-chain candidates are historical
adversarial probes, not active alternatives: both encode an all-actions closure
decision and do not meet the clarified constraint. A distributed chain also
forms a logical child ledger. The independent-resolution proposal removes that
aggregation but intentionally changes AW-02 and needs a product decision. The
derived-close alternative retains the all-terminal meaning of a qualified read,
while changing review timing and the durable-close fact. Neither resolves action
admission, reviewed evidence, authorization, source qualification or recovery.
No candidate is locked or ready to implement by this review.
