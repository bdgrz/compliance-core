# Domain boundaries: refinement checkpoint

Status: **in progress, not a design lock**, 2026-11-05. The context documents
were reviewed for parent rosters, manifests, per-child histories, wide event
payloads and cross-owner invariants. Their new alternatives are candidates; they
are not implementation approval. The [shared conventions](../contexts/conventions.md)
record the governing boundary rules.

## Direction

Portia intentionally hydrates an aggregate from its entire event stream. A
parent that records each child pays for the whole growing history on each
decision. A chain of small per-child coordinator streams may simply distribute
that same ledger while adding serialization and traversal. Finite work, a
per-run identity, a hash, a manifest or a source cut does not solve that design
problem by itself.

Each independently governed record owns its own invariant and lifecycle. Write
decisions should use the few facts necessary for that decision, not enumerate all
children. Small fixed sets can remain together when a real atomic invariant
requires them. Lists, exports and reports are reads, and can enumerate independent
records incrementally. An individual cross-owner decision still needs real
authority, explicit outcomes and correct failure handling; a stale projection is
not permission to violate an invariant. If a current product promise appears to
require a collection-wide write decision, examine the promise and possible local
invariants explicitly. Do not silently weaken accepted behavior.

Several context proposals still describe completion using a full source cut,
participant-wide settlement or a chain of admission windows. Those are open
questions, not endorsed solutions. In particular the criteria and methodology
chains remain distributed child ledgers and are unresolved fallbacks, not the
desired design. A paged source cut may serve a read, but does not automatically
make an all-child write decision acceptable.

## Local-decision refinement

- Criteria publication has a candidate that checks one immutable nonempty
  witness and records a marker. Individual entry outcomes may settle later;
  draft-correction semantics and the shared source order remain unqualified.
  Program and description review separately explore selecting a reviewer before
  edits, which changes reviewer selection and waiver workflows.
- Findings, policy campaigns, risk work and readiness now distinguish an
  individual decision from a coverage-aware progress view. That view cannot
  replace an accepted durable completion decision or authorize the next stage
  merely because a projected count is zero. Where those guarantees are required,
  the authoritative mechanism or a product change remains open.
- Holds, evidence links, inventory instances and access decisions are modeled as
  independently governed facts. This does not eliminate the need to order a hold
  against disposal, disclosure against revocation, or target admission against
  retirement. The proposed per-record fences and indexed checks remain
  conditional on infrastructure capabilities not yet demonstrated.
- M0-D05 requires tenant-wide alias and active provider-key uniqueness. The old
  application proposal described those checks as advisory; the context now
  preserves the accepted uniqueness requirements and leaves authoritative claim,
  transfer and retry design open.
- Required CSV and ZIP output is a delivery/read concern. Its bytes, counts or
  hashes must not make the aggregate own the population; a marker or hash also
  does not establish that the source is complete or a signer reviewed it.

The broader reasoning and conditions for these patterns are in
[local write invariants](../local-invariants.md). `PrimaryOrgAdmins` is an
appropriate fixed-seat, low-churn owner: it records its own infrequent decisions
to protect the organization from losing its last primary administrator. It does
not track ordinary administrator membership and is not an aggregate to redesign
on the basis of its own lifecycle history.

## Cross-domain findings

| Areas | Refinement and outstanding work |
| --- | --- |
| [Criteria](../contexts/criteria.md), [program](../contexts/program-and-scope.md), [methodology](../contexts/firm-methodology.md) | Independently owned entries and scope decisions replace monolithic sets. Publication, parent-reference validity, edits racing publication, and template application need simpler local invariants and qualified authority. Prior windows and per-operation chains are not locked. |
| [Platform access](../contexts/platform-access.md), [workforce](../contexts/workforce-assurance.md), [policy](../contexts/policies.md), [access governance](../contexts/external-access-governance.md) | Member owns its person link; memberships, people, assignments and review items have independent authority. Operator continuity, policy audience eligibility, exact review semantics and authorization/revocation races remain open. M0-D12's access qualification and its prohibition on inferring audience from application access require an explicit interpretation. M0-D07's single effective row containing all paths conflicts with the proposed independent paths and has not been amended. |
| [Controls](../contexts/control-environment.md), [assurance](../contexts/assurance-work.md), [readiness](../contexts/readiness.md), [risk and provider oversight](../contexts/risk-and-provider-oversight.md) | Results, evidence links, deviations, actions and gaps need their own focused owners. Revisit closing and approval rules that currently require every related result; preserve the accepted business meaning or explicitly resolve a change. A fixed two-role decision and a fixed risk matrix are genuinely bounded candidates. |
| [Application inventory](../contexts/application-inventory.md), [technology and information inventory](../contexts/technology-and-information-inventory.md) | Separate aliases, instances and data-flow links. The application merge lifecycle must not require retiring a source before moving its instances, while a published flow's required asset and relationship authority need qualification. |
| [Evidence](../contexts/evidence.md), [retention](../contexts/retention.md), [firm services](../contexts/firm-services.md) | Separate artifacts, holds and service facts; a no-hold read cannot authorize a later disposal if hold placement can race it. The actual source ordering, hold scope, impairment/acceptance race and recovery need proof. |
| [Examinations](../contexts/examinations.md), [auditor collaboration](../contexts/auditor-collaboration.md), [exports](../contexts/exports.md), [system description](../contexts/system-description.md), [advisor collaboration](../contexts/advisor-collaboration.md) | Separate items, citations, replies and deliveries. A complete report or external archive is a read/delivery concern; revocation, exact accepted versions, late changes and close semantics still need authoritative design. Streaming a customer-facing ZIP does not turn the parent into a manifest aggregate. |
| [Commitments](../contexts/commitments.md), [changes and incidents](../contexts/change-and-incident-oversight.md), [work queue](../contexts/work-queue.md) | Separate relationship and digest items. Resolve current-boundary/assignment races and preserve the accepted rule that an ambiguous email dispatch is not automatically resent. |

## Product semantics to resolve

The criteria behavior below has since been selected. The other entries remain
alternatives for review, not decisions already made:

- **Criteria drafts:** Resolved by product direction: removing a parent makes
  references to that incarnation ineffective, and re-adding it does not silently
  restore them. Publication uses one pinned criterion witness; program creation
  can proceed after valid publication while unrelated entries remain pending.
  Source ordering, actor admission, fencing and recovery require implementation
  qualification; see [criteria](../contexts/criteria.md).
- **Findings:** An independent action can stay open after a finding's resolution
  is reviewed. That changes the current all-actions-terminal closure promise;
  it is not an equivalent implementation. See the
  [finding review](finding-boundaries-review.md).
- **Methodology and program scope:** Independent entry or scope decisions avoid
  a global child decision, but can change whole-draft reviewer independence,
  immutable whole-version semantics or atomic application. Those changes require
  explicit product review. See [methodology](../contexts/firm-methodology.md)
  and the [program review](program-boundaries.md).
- **Access campaigns and control evaluations:** Existing rules describe
  completion over every item or step. Independent outcomes and a projected
  worklist may have a different meaning; do not call them equivalent without
  resolving the accepted completion and review requirements.

## Evidence and limits

The live Jev 1.14.0 request and full response for the shared review are
[here](domain-boundaries-jev-request.json) and
[here](domain-boundaries-jev-response.json). For the explicit synthetic cases,
Jev rated growing parent outcome history 0.94, a projection's absence as proof
0.04, an unfenced cross-stream race 0.94, and a hash alone as completeness proof
0.04. Its 0.87 for independent owners assumed a complete frozen admission set and
all outcomes; those premises are precisely what still require justification and
may be an undesirable all-child decision. Per-context requests, responses, adverse
examples and inconclusive scores are linked from the context documents or their
boundary reviews. Some answers contradict concrete traces. Historical numerical
thresholds were triage conventions, not measured probabilities that an
architecture is correct. Jev cannot establish provider semantics or prove safety.

Portia `v0.10.0` source's `AggregateRepository.HydrateAsync` collects a stream's
events before loading the aggregate; this repository pins Portia 0.10.0. This is
intentional. Confirm the actual deployed package and configuration when
qualifying an implementation.
The design must account for the growth of an owner's own history as well as
avoid unrelated children accumulating in it. A model response does not prove a
source frontier, cross-stream transaction, fencing primitive or bounded replay.

No backend implementation, build, test, CI or merge is asserted by this review.
The [usability-based implementation order](../implementation-order.md) remains:
access, criteria and program creation, then people and the workflows that produce
evidence. The next design step is to turn the highest-priority unresolved rules
into local, testable invariants with explicit lifecycle and product tradeoffs.
