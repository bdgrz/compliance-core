# Program and scope boundary review

2026-11-05. Design review; not implementation or proof that the event provider
enforces any protocol. Jev is advisory. Exact requests and responses are retained
in `program-boundaries-jev-{request,response}.json`,
`program-boundaries-followup-jev-{request,response}.json`, and
`program-boundaries-independent-jev-{request,response}.json`. Additional
reviewer-candidate and explicit-race requests and responses are
`program-boundaries-reviewer-jev-{request,response}.json` and
`program-boundaries-reviewer-traces-jev-{request,response}.json`.

## Outcome

The prior `SystemBoundary` design embedded all scope decisions and authors in
parent state. A first refinement split decisions into individual owners and
introduced short-lived admission windows and a complete source-cut scan at
approval. That still requires the approving write to account for all decisions:
the linked windows are a distributed per-decision ledger. Bounded memory and
small records do not remove the whole-collection write invariant. **That proposed
protocol is withdrawn, not a completed remediation.**

The existing product policy requires one review of an exact whole draft, by a
person who authored none of its revisions unless covered by a waiver. Independently
reviewing and publishing each subject's scope decision would keep write invariants
local. It would change that policy: two people can each author one subject and
review the other's, although neither may review the whole draft under the current
rule. Independent publication also does not establish an exact immutable version
of the complete scope; its historical use, cutoff semantics and draft discard
remain open. The candidate is therefore not approved or implementation-ready.
No source-cut, index, cursor, digest or projection has been shown to preserve the
existing semantics without an all-child write dependency.

Two more focused candidates are now described in the context. Designating an
immutable reviewer before edits makes reviewer separation locally decidable if
all writers reject that person's revisions, while retaining a single human's
attestation over the exact whole version. It restricts reviewer selection timing
and the waived-author workflow. Alternatively, one author claim keyed by the
prospective `(draft, reviewer)` can preserve late selection in principle, with
no parent list; its first-claim/first-edit atomicity and fencing are unproved.
Neither candidate may be implemented as if chosen, and the cost and correctness
of presenting the complete version to the reviewer are still open. In
particular, waiting for a projection to reach a marker still depends on
processing all relevant preceding facts, even if the approval handler reads
only a cursor. The team must decide whether and how that read-side completeness
obligation fits the no-collection-write rule.

The current assembled [SystemBoundary source](../../../src/Compliance.Core/Features/Boundaries/Aggregates/SystemBoundary.cs)
also accepts external authority content containing assignment and decision
constraint collections. A small aggregate header alone does not remove this
existing collection-bearing authority path; the candidate is a future design,
not a claim of current source conformance.

The other locked product rules remain: one open draft, strictly increasing
approved effective dates, mandatory security category, exact subject versions,
active in-program client services at citation, and source-bound subject owner
eligibility. A later service retirement must remain recordable and any dependent
gap visible.

## Jev evidence

The independent review (`jev-1.14.0`) rated the full-source-scan approval as a
write dependent on every decision 0.95, that splitting the chain avoids checking
every decision 0.20, that independent reviews equal the whole-draft review rule
0.09, that local reviewer independence can be checked without other decisions
0.82, and that a read-only paged report can avoid an aggregate write 0.90. These
support the distinction above but do not decide product policy.

The earlier review rated independent scope ownership 0.91, bounded-memory author
checking 0.92, immutable cut references 0.87, that an earlier service read proves
activity at approval 0.08, and that a terminal-window ID alone proves completeness
0.04. These answers assumed retained complete source facts and did **not** address
the user's later restriction on write decisions depending on all children. They
are not evidence that the withdrawn source-cut protocol satisfies that restriction.

The first delayed-write question returned 0.23 despite its explicit successful
late write; its fence question returned 0.74. A narrower repeat rated an explicit
post-freeze accepted write 0.94 and acceptance after a durable token-specific
fence 0.10. The scenario itself shows the race and the hypothesized fence excludes
the later acceptance; neither establishes an implemented provider guarantee.

In the reviewer-candidate request, Jev rated multiple other authors under a
fixed reviewer 0.88, the ability of a prior author to become that immutable
reviewer 0.16, waiver-workflow equivalence 0.03, a complete projection proving
personal review 0.15, and the named attestation 0.88. It rated the approval
handler needing to scan all decisions 0.05 under the stipulated assumptions.
The concrete trace request rated a post-freeze append 0.81, progress waiting on
all relevant earlier facts 0.81, and a bare checkpoint proving projector
completeness 0.04. It was inconclusive or contradicted explicit first-claim
counterexamples: false exclusion after an unsuccessful first edit 0.25, an
unclaimed committed edit making the person an author 0.47, and resulting
self-approval 0.51. The counterexamples follow from the stated trace regardless
of scores; the model does not establish the missing atomic protocol.

## Remaining qualifications

- Reconcile exact whole-draft approval, separation across all draft authors, and
  immutable whole-boundary versions with the no-all-child-write requirement, or
  obtain an explicit product decision changing those semantics. Discard and
  historical examination-time scope need the same treatment.
- Evaluate whether early reviewer selection and the waiver restriction are
  acceptable, or specify an exact one-person claim without false refusals,
  unclaimed committed revisions, stale negative reads, or per-child records.
  Qualify exactly what a complete human review view requires and whether its
  frontier creates a prohibited collection-wide write dependency.
- [M0-D22](../decisions/m0-d22-canonical-ownership.md) requires a draft to cite
  only active in-program client services. A read is not an atomic citation
  against concurrent retirement. Specify authoritative source-owner admission,
  its linearization, failures, replay and pending outcome. M0-D22 does not require
  blocking later retirement or expressly add approval-time activity.
- Qualify exact retries, durable token fences, competing appends, source retention
  and independent per-decision review without introducing a per-child parent
  ledger or trusting a projection for write authority.
