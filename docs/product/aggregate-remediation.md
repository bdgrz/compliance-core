# Aggregate and reactor remediation

Delivery is tracked by [#1022](https://github.com/bdgrz/compliance-core/issues/1022).

The current 103-owner command/handler/guard/test specification, 29 split-target definitions, 24-reactor catalog and Mermaid choreography diagrams are indexed in [the 2026-10-23 boundary audit](audits/2026-10-23/README.md).
[The delivery ledger](aggregate-remediation.json) assigns all 81 active observations
(seven P1, 74 P2) to ordered delivery families. Each family is a sequence of focused
PRs, not permission to combine all its owners in one change. Shared observations
remain open until every affected owner and continuation has evidence.

The immutable baseline is [the 103-owner audit](audits/2026-10-22/audit.json),
[the 33-reactor and 13-workflow matrix](audits/2026-10-22/choreography.json), and
[the concrete serializer and SDK analysis](audits/2026-10-22/payload.json), at
`4c255f56bfec727ac1f6e841c09935a4ce69ec4c`. The ledger pins their SHA-256 hashes.
Baseline findings are observations, not tests or proof of a corrected boundary.
CHOREO-01/02 are aliases of ACCESS-12/01; OPS-14 is a closed design question and
does not count toward the 81. The baseline records its own scope and limitations.

## Accepted decisions

- Pre-release histories are disposable. Replace stream layouts and contracts
  directly and remove superseded runtime paths during each owning correction.
  This supersedes earlier migration requirements in #1022/#1026. Deployment and
  environment resets require separate authorization.
- Retry-sensitive commands require typed client identity, bound from a required
  route parameter and exposed to MCP. Creation identity suffices for exact
  creation retries; repeated updates and assignment episodes need `CommandId`.
  No server-generated substitute satisfies this contract. Conflicting body
  duplicates fail validation.
- Authorize before resolving previous intent. Compare command kind, owner,
  lifecycle episode, original actor, canonical authored input and original
  expected revision against the operation's accepted facts. Exact retries return
  the original result without appending; changed intent conflicts. New operations
  enforce expected revision. Keep execution-attempt identity separate and derive
  reactor effects from source event, effect kind, target owner and lifecycle.
- Use concrete generated serialization. Preflight the entire attributed batch
  before opening an append session, including the envelope and actual SDK
  overhead. Forward persistence notifications. A payload margin is not a frame
  proof; KV values also include route/key/operation overhead.
- Cross-owner decisions consume server-created immutable exact-scope proofs,
  including source identity, event/position, revision, digest and required facts.
  Baseline, readiness and historical decisions retain authoritative temporal reads.
- Write verified immutable content before publishing a manifest. Technical
  attempts, stages and cursors belong outside domain events. Document retained
  state and physical replay separately; removing arrays does not shorten history.
- Preserve permanent independence walls, complete import publication, fail-closed
  disposal and current approval policies. Keep RiskDraft and notification consent
  independently owned and SystemBoundary's conflict-relevant scope atomic.
- Accepted identity recovery automatically completes the exact proven swap:
  protect/register the replacement before revoking the old identity. Later
  challenges cannot replace an accepted operation. Notices recover independently.

## Ordered delivery

| Order | Family | Completion evidence |
| --- | --- | --- |
| 0 | Tracking and CI | Every observation assigned; Finding acceptance reconciled; final-head Backend required. |
| 1 | Persistence safety | Concrete serialization, attributed whole-batch preflight, stream/KV framing and validation mapping. |
| 2 | Explicit identity and retries | Required typed identities across HTTP, MCP, queues and reactors; selected-fact retries for every audited mutation. |
| 3 | Administrator safety | Fenced current-episode withdrawals/restorations and autonomous recovery. |
| 4 | Identity recovery | Immutable accepted operation, replacement protection, automatic completion and independent notice. |
| 5 | Independence | Permanent staff/user practice authorities and complete service epochs without lifetime caps. |
| 6 | Incidents and impact | Authoritative completion proofs, exact assessment correlation, superseded work and dependency retries. |
| 7 | Imports | Complete frozen batches, canonical holds, native target facts, settlement recovery and all-target publication. |
| 8 | Reactor recovery | Slug replay, source-complete cleanup, late relationships, Type II holds and occurrence materialization. |
| 9 | Identity and catalogs | Independent invitations, credentials/tests, delivery, staff/designations, normalized-name claims and assurance records. |
| 10 | Operations and campaigns | Independent plans, occurrences, evaluations, mappings, actions, campaign items and readiness gaps. |
| 11 | Evidence and assurance | Individual requests/feedback/responses, exact delivery authority, complete population reconciliation and disposal barriers. |
| 12 | Versioned content | Narrow versions/proofs for criteria, policy, redaction, assertions, methodology, commitments, descriptions, changes and incidents. |
| 13 | Process and legacy cleanup | Independent processes, hard-pruned superseded paths, full owner/event/command/reactor reconciliation. |

Acquire multiple reservations in canonical stream-key order. Persist immutable
intent sufficient to reconstruct each continuation. Cancellation fences the
receiving operation before releasing authority. If an executable permit cannot
be safely revoked, recover forward. Elapsed time, missing reads and unknown
append outcomes never justify release. Administrative restores wait for unfinished
withdrawals, restore the source first and release only after authoritative
eligibility proof. Cleanup targets old episodes and cannot remove newer authority.

Type II amendments retain both date ranges until applied. Recurring work has one
identity per control and business window; selected work is distinct from
materialized work. Import commit publishes every target together; target replay
does not mutate business state by consulting live settlement. Closure/freeze
gates require complete authoritative coverage. Feedback admission serializes
with handoff revocation; auditor requests seal admission before resolution.
Package delivery binds exact manifest, archive, validation and recipient approval.
Reconcile unknown external effects before retry, preserving at-least-once
challenge/invitation delivery and the stricter unknown-send digest rule.

## Acceptance and delivery record

For every behavior change, record focused RED and GREEN, whitespace validation,
Release build and full backend units. Test exact retries after later edits and
new lifecycle episodes, changed intent/actor/revision, malformed identities,
crash and unknown outcomes, cancellation races, projection lag and late facts,
former-cap boundaries, concrete frame boundaries, Unicode and attribution.
Use bounded Jev design and immutable-head preflight questions with source
evidence; record disagreements and the adopted decision.

Open one PR at a time, adversarially review its immutable head, require hosted CI,
squash merge, and read back merged source and issue acceptance. A ledger row may
be complete only with merged evidence for its full scope. A family is complete
only when all rows, resulting owners and recovery continuations are accounted for.
Incomplete replacements stay disabled. Completion ends on clean, synced
`develop`; deployment and environment resets are separate operations.

PR #1027 is accepted source evidence for the per-finding cutover and the
model-only AuditObservation. It did not resolve OPS-06/11/12, complete #301,
remove legacy paths, or run hosted CI. #1026 therefore remains open pending
the remaining acceptance and final-head verification.

Jev design evidence is retained as [request](audits/2026-10-22/tracking-jev-request.json)
and [response](audits/2026-10-22/tracking-jev-response.json). `jev-1.14.0` assigned
0.80 to this bounded tracking slice being ready for review and 1.00 to selected-fact
retry resolution. The adopted contract follows the accepted user decisions and
source evidence; no disagreement changed scope or closed an observation.
