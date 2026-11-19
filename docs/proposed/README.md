# Domain model

This folder is the source of truth for Compliance Core's domain rules and write
model. The write model is designed one bounded context at a time, in
hand-authored context docs; code follows the docs.

Implementation resumed on 2026-11-05. Read the [latest restart checkpoint](restart-handoff.md)
for verified state and the [implementation order](implementation-order.md) for the
recommended remaining queue. Source checkpoints and audits are not completed domain acceptance.

The [domain boundary refinement](reviews/domain-boundaries-review.md) reopens
prior context locks where child collections, source authority or stream replay
need redesign. Product decisions remain in force. The selected criteria and
program-creation design can guide implementation; its stated source, recovery
and provider guarantees are acceptance gates, not established capabilities.
Other context alternatives remain provisional where their docs say so.

- [Domain model](domain-model.md): modeling rules, process spine and context map.
- [Context conventions](contexts/conventions.md): rules every context cites.
- [Local write invariants](local-invariants.md): patterns for focused decisions,
  cross-owner authority, and derived status.
- [Decisions](decisions): product decisions the contexts apply.
- [Read models](read-models.md): the read-layer plan. Where its names differ
  from a context doc, the context doc wins.
- [External-source boundary](partner-seam.md).
- [Implementation order](implementation-order.md): remaining capabilities, dependencies,
  immediate queue and separate qualification gates.

## Bounded contexts (source of truth)

Each context doc covers its aggregates, commands, events and reactions, its
Mermaid flow diagrams, its decisions with Jev evidence, and its open questions.

| Context | Status |
| --- | --- |
| [Platform access](contexts/platform-access.md) | Refining boundaries |
| [Firm services](contexts/firm-services.md) | Refining boundaries |
| [Program and scope](contexts/program-and-scope.md) | Refining boundaries |
| [Workforce assurance](contexts/workforce-assurance.md) | Refining boundaries |
| [Application inventory](contexts/application-inventory.md) | Refining boundaries |
| [Technology and information inventory](contexts/technology-and-information-inventory.md) | Refining boundaries |
| [Examinations](contexts/examinations.md) | Refining boundaries |
| [Auditor collaboration](contexts/auditor-collaboration.md) | Refining boundaries |
| [Commitments](contexts/commitments.md) | Refining boundaries |
| [Criteria](contexts/criteria.md) | Product behavior selected; authority qualification pending |
| [Control environment](contexts/control-environment.md) | Refining boundaries |
| [Evidence](contexts/evidence.md) | Refining boundaries |
| [Retention](contexts/retention.md) | Refining boundaries |
| [Assurance work](contexts/assurance-work.md) | Refining boundaries |
| [Policies](contexts/policies.md) | Refining boundaries |
| [Risk and provider oversight](contexts/risk-and-provider-oversight.md) | Refining boundaries |
| [External access governance](contexts/external-access-governance.md) | Refining boundaries |
| [Change and incident oversight](contexts/change-and-incident-oversight.md) | Refining boundaries |
| [System description](contexts/system-description.md) | Refining boundaries |
| [Readiness](contexts/readiness.md) | Refining boundaries |
| [Advisor collaboration](contexts/advisor-collaboration.md) | Refining boundaries |
| [Exports](contexts/exports.md) | Refining boundaries |
| [Firm methodology](contexts/firm-methodology.md) | Refining boundaries |
| [Work queue](contexts/work-queue.md) | Refining boundaries |

Every context doc follows the shared [context conventions](contexts/conventions.md)
(diagram notation, reactor rules, guards and reviews). The
[flow audit of 2026-11-03](flow-audit-2026-11-03.md) records the findings those
conventions and the later decision rows fixed.

Organization commercial operations (subscription, billing and add-ons) is not
designed yet; it awaits the business-operations decisions.

The [implementation progress ledger](implementation-progress.md) tracks assembled
behavior, deferred wiring, remaining work and validation failures across contexts.
The [requirement coverage map](requirement-coverage.md) records reviewed behavior
and concrete remaining acceptance per requirement; its initial eight contexts are
source-reviewed and still require execution at the acceptance head.

## Aggregate implementation evidence

Context design status does not establish implementation or workflow delivery.
Each bounded implementation review records its invariant, tests and remaining
work.

| Owner | Implementation status | Evidence |
| --- | --- | --- |
| Organization creation and primary administration | Implemented in source across API/MCP/write/read; 152 focused and 8 transport cases pass; native/full acceptance blocked | [Review](reviews/organization-bootstrap.md), [platform access](contexts/platform-access.md) |
| Scoped team access | Partial: source-backed scoped permissions, custom roles, guest caps and public cutover; native/full acceptance and primary continuity remain | [Review](reviews/scoped-team-access.md) |
| Program | Partial: all-present target-date ordering passes focused/native tests; full gate and canonical Program workflow remain | [Date invariant](reviews/program-target-date-ordering.md) |
| Person | Partial: named details and generic-writer retirement locally qualified; member linkage and accepted HRIS application remain | [Writer cutover](reviews/person-named-writer-cutover.md) |
| RiskControlTreatment (`RiskTreatmentAssertion` in current source) | Partial: reviewer independence enforced in the owner; control-retirement coordination and proposed-owner migration remain | [Review](reviews/risk-control-treatment.md) |
| WorkRelationship | Partial: named lifecycle/changes and computed sponsorship; initial/source conformance and whole-owner qualification remain | [Terminal review](reviews/work-relationship-terminal-status.md), [named changes and sponsorship](reviews/workforce-named-and-sponsorship.md) |
| ServiceIdentity | Partial: named owner/purpose/review/lifecycle operations assembled; full proposed owner and workflow qualification remain | [Review](reviews/service-identity-retirement.md), [assembled evidence](implementation-progress.md) |
| MethodologyTemplateDraft | Partial: source-bound draft, personal review and published line reads assembled; name admission, ordered applications and full qualification remain | [Review](reviews/methodology-template-draft.md), [assembled evidence](implementation-progress.md) |
| CorrectiveAction / Finding | Partial: cancellation and severity vocabulary; closure semantics and independent action authority are being refined | [Review](reviews/corrective-action-cancellation.md), [boundary review](reviews/finding-boundaries-review.md) |
