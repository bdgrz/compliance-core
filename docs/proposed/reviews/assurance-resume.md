# Assurance resumed implementation

Tracking: #1176. Specification: [Assurance work](../contexts/assurance-work.md).
Status: source implemented, uncompiled and unexecuted. The user prioritized code
completion as an iteration platform; no authored case is described as observed
RED or GREEN. The coordinator owns assembled execution and shared wiring.

## Implemented source

- Named `ReviseFindingAnalysis` appends `FindingAnalysisRevised` on the open
  finding, retaining its original source wording, ownership and due date. Analysis
  is bounded, severity uses low/moderate/high, affected records are bounded, no-op
  and consumed-revision writes refuse without a fact. Owner replay and exact
  history understand the event; current reads and frozen responses expose analysis.
- The analysis handler admits the current owner or the dedicated Lead/Contributor
  permission. It checks authoritative current client membership before native
  replay and checks owner authority again at the prepared owner cut.
- Propose, review, withdraw and reopen handlers check authoritative current client
  membership before native receipt replay. Proposal owner authority and dedicated
  Lead review/reopen permissions precede accepted retries. Withdrawal remains the
  exact proposer's decision. Existing exact current-proposal and waiver source
  proofs remain in the held capability.
- `FindingClosureProposalView.Authors` exposes every author of that closure's
  proposal revisions, sorted and deduplicated. Both immediate frozen proposal
  responses and authoritative current reads carry the same lineage for Work.
- Control occurrences raise findings only from committed explicit reviewer
  decisions. Attestation failure/skipping is retained for review and emits no
  finding. Explicit requested actions retain deterministic per-action identities;
  a raise decision without requested actions uses the committed review ID.

Authored tests cover immutable source and owner, invalid/no-op analysis, competing
analysis appends, suspended accepted proposal retry, dedicated reopen refusal,
full proposal author lineage, failed/skipped no-finding reactions, and deterministic
raise/none review replay. Existing held source has proposal, independence, waiver,
withdrawal/final-proof/source-settlement and native retry tests.

## Exact integration

Apply only held closure commits `62b592f8`, `06f54d04`, `d38f0231`, `e6b1913c`,
`bd248387`, `95ba1654`, `f0cc18ae`, `527fda47`, then resumed commits
`ef3fb5ed`, `25e1c464`, `eca3076a`, `8832e242`, `753b18dc`, `1de53779`,
`f15b3a7a`. Intervening old native-storage and already-delivered commits are not
part of this selection. Preserve main's newer ChangeCorrectiveAction current
membership/accepted-retry authorization fix; this branch's old ancestor lacks it.

`8832e242` requires Controls `a99e0927` (review decision/rationale fields).
Work owns receiving refresh/eligibility code. Examinations owns current closure
consumption. Neither is proved by the source here.

Coordinator wiring:

- Core JSON: named analysis command/event, held proposal/withdraw/review
  commands/events, proposal terms/artifact versions/review binding/cancellation,
  and their actual immutable-content proof types.
- DI: analysis, propose, review and withdraw handlers; held withdrawal reactor.
  Reopen now injects aggregate reader and permission authorizer; withdraw injects
  aggregate reader.
- Dedicated grants: `finding_analysis.revise` to Compliance Lead and Contributor;
  `finding_closure.propose`, `.review`, `.reopen` to Lead. Owner admission remains
  source checked in its handler. Generic `program.manage` is insufficient.
- Public named analysis/proposal/withdraw and HTTP-only personal review surfaces;
  use the actual native typed response contract (review returns accepted status).
  Retire public generic `ReviseFinding` and one-step `CloseFinding` writers in the
  same cutover. Historical decoding and completion of recorded accepted work are
  separate from accepting new legacy operations.
- Include full author lineage and new facts in Work current-state refresh and
  project source fixtures/catalogs as appropriate.

Focused filter:
`FindingAnalysisTests|FindingCurrentAuthorityTests|FindingClosureProposalTests|ProposeFindingClosureHandlerTests|FindingClosureReviewHandlerTests|FindingClosureWithdrawalWorkflowTests|OccurrenceFindingTests`.
Run with FullyQualifiedName selectors in the coordinator lane, then the assembled
unit suite/build/format. No .NET command ran in this worker.

## Bounded Jev source review

Live `jev-latest` resolved `jev-1.14.0`. Adverse probabilities: named analysis
mutates original source/owner/due date .06; accepted retry skips current actor
check .18; reopen omits dedicated permission .08; earlier proposal author omitted
from view .09; analysis admits another scope/consumed predecessor .15. Source
reasoning and authored cases independently support those boundaries. Jev is not
execution proof. Request/response are preserved at
`/tmp/compliance-assurance-resume-jev-{request,response}.json`; no credentials
appear in artifacts.

## Remaining context qualification

This is not whole-Assurance completion. Shared public retirement/wiring,
receiving Work/Examinations qualification and observed tests remain. Tenant-wide
Finding/CorrectiveAction identity and permanent one-Finding action binding, full
person/team owner assignment, canonical source vocabulary (#1180), and downstream
current waiver coverage require explicit remaining acceptance. Existing old
program/finding-derived stream identities are not evidence of those target rules.
