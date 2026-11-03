# Domain model

This folder is the source of truth for Compliance Core's domain rules and write
model. The write model is designed one bounded context at a time, in
hand-authored context docs; code follows the docs.

- [Domain model](domain-model.md): modeling rules, process spine and context map.
- [Context conventions](contexts/conventions.md): rules every context cites.
- [Decisions](decisions): product decisions the contexts apply.
- [Read models](read-models.md): the read-layer plan. Where its names differ
  from a context doc, the context doc wins.
- [External-source boundary](partner-seam.md).

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
| [Criteria](contexts/criteria.md) | Locked 2026-11-03 |
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

## Aggregate implementation evidence

Context design status does not establish implementation or workflow delivery.
Each bounded implementation review records its invariant, tests and remaining
work.

| Owner | Implementation status | Evidence |
| --- | --- | --- |
| RiskControlTreatment (`RiskTreatmentAssertion` in current source) | Partial: reviewer independence enforced in the owner; control-retirement coordination and proposed-owner migration remain | [Review](reviews/risk-control-treatment.md) |
| WorkRelationship | Partial: ending is terminal for new revisions; full identity/lifecycle and named-event migration remain | [Review](reviews/work-relationship-terminal-status.md) |
| ServiceIdentity | Partial: retirement freezes changed terms; full owner/review model and named-event migration remain | [Review](reviews/service-identity-retirement.md) |
