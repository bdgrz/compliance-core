# Domain model

This folder is the source of truth for Compliance Core's domain rules and write
model. The write model is designed one bounded context at a time, in
hand-authored context docs; code follows the docs.

Implementation resumed on 2026-11-05. Read the [latest restart checkpoint](restart-handoff.md)
for verified state and the [implementation order](implementation-order.md) for the
recommended remaining queue. Source checkpoints and audits are not completed domain acceptance.

- [Domain model](domain-model.md): modeling rules, process spine and context map.
- [Context conventions](contexts/conventions.md): rules every context cites.
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
| [Platform access](contexts/platform-access.md) | Locked 2026-11-03 |
| [Firm services](contexts/firm-services.md) | Locked 2026-11-03 |
| [Program and scope](contexts/program-and-scope.md) | Locked 2026-11-03 |
| [Workforce assurance](contexts/workforce-assurance.md) | Locked 2026-11-03 |
| [Application inventory](contexts/application-inventory.md) | Locked 2026-11-03 |
| [Technology and information inventory](contexts/technology-and-information-inventory.md) | Locked 2026-11-03 |
| [Examinations](contexts/examinations.md) | Locked 2026-11-03 |
| [Auditor collaboration](contexts/auditor-collaboration.md) | Locked 2026-11-03 |
| [Commitments](contexts/commitments.md) | Locked 2026-11-03 |
| [Criteria](contexts/criteria.md) | Locked; authority revised 2026-11-05 |
| [Control environment](contexts/control-environment.md) | Locked 2026-11-03 |
| [Evidence](contexts/evidence.md) | Locked 2026-11-03 |
| [Retention](contexts/retention.md) | Locked 2026-11-03 |
| [Assurance work](contexts/assurance-work.md) | Locked 2026-11-03 |
| [Policies](contexts/policies.md) | Locked 2026-11-03 |
| [Risk and provider oversight](contexts/risk-and-provider-oversight.md) | Locked 2026-11-03 |
| [External access governance](contexts/external-access-governance.md) | Locked 2026-11-03 |
| [Change and incident oversight](contexts/change-and-incident-oversight.md) | Locked 2026-11-03 |
| [System description](contexts/system-description.md) | Locked 2026-11-03 |
| [Readiness](contexts/readiness.md) | Locked 2026-11-03 |
| [Advisor collaboration](contexts/advisor-collaboration.md) | Locked 2026-11-03 |
| [Exports](contexts/exports.md) | Locked 2026-11-03 |
| [Firm methodology](contexts/firm-methodology.md) | Locked 2026-11-03 |
| [Work queue](contexts/work-queue.md) | Locked 2026-11-03 |

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
| Organization creation and primary administration | Implemented in source across API/MCP/write/read; 152 focused and 8 transport cases pass; native/full acceptance blocked | [Review](reviews/organization-bootstrap.md) |
| Scoped team access | Partial: source-backed scoped permissions, custom roles, guest caps and public cutover; native/full acceptance and primary continuity remain | [Review](reviews/scoped-team-access.md) |
| Program | Partial: all-present target-date ordering passes focused/native tests; full gate and canonical Program workflow remain | [Date invariant](reviews/program-target-date-ordering.md) |
| Person | Partial: named details and generic-writer retirement locally qualified; member linkage and accepted HRIS application remain | [Writer cutover](reviews/person-named-writer-cutover.md) |
| RiskControlTreatment (`RiskTreatmentAssertion` in current source) | Partial: reviewer independence enforced in the owner; control-retirement coordination and proposed-owner migration remain | [Review](reviews/risk-control-treatment.md) |
| WorkRelationship | Partial: named lifecycle/changes and computed sponsorship; initial/source conformance and whole-owner qualification remain | [Terminal review](reviews/work-relationship-terminal-status.md), [named changes and sponsorship](reviews/workforce-named-and-sponsorship.md) |
| ServiceIdentity | Partial: named owner/purpose/review/lifecycle operations assembled; full proposed owner and workflow qualification remain | [Review](reviews/service-identity-retirement.md), [assembled evidence](implementation-progress.md) |
| MethodologyTemplateDraft | Partial: source-bound draft, personal review and published line reads assembled; name admission, ordered applications and full qualification remain | [Review](reviews/methodology-template-draft.md), [assembled evidence](implementation-progress.md) |
| CorrectiveAction / Finding | Partial: cancellation and severity vocabulary; finding-owned outcomes and proposed closure migration remain | [Review](reviews/corrective-action-cancellation.md) |
