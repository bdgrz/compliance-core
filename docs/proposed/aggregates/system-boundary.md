# SystemBoundary

- Classification: `business_aggregate`
- Source area: `Boundaries` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant + caller BoundaryId with exact immutable program scope and selected stable draft identities.

**Invariant:** A boundary has one current draft; approval requires latest accepted review of exact draft revision, an independent/waived actor and strictly increasing approved effective date; exact-scope responsibility changes cannot create retroactive unwaived decision conflict.

**Additional decision rule:** Trust Services categories are a set of 1 to 5 distinct values from security, availability, confidentiality, processing_integrity and privacy that must contain security, enforced on draft creation, revision, successor proposal and approval (a stale draft without security cannot be approved, earlier approved versions stay readable). Optional categories are recorded explicitly and never inferred from a program name or target path; dropping one is a successor version. A typed owned unresolved service reference may be kept but cannot claim governed identity from an arbitrary UUID, and cross-context references carry tenant ID, record ID and exact version.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ApproveBoundary](../commands/system-boundary.md#approveboundary) | http | CommandId |
| [CreateBoundary](../commands/system-boundary.md#createboundary) | http | BoundaryId |
| [DiscardBoundaryDraft](../commands/system-boundary.md#discardboundarydraft) | http | CommandId |
| [ProposeBoundarySuccessor](../commands/system-boundary.md#proposeboundarysuccessor) | http | CommandId |
| [ReviewBoundary](../commands/system-boundary.md#reviewboundary) | http | CommandId |
| [ReviseBoundaryDraft](../commands/system-boundary.md#reviseboundarydraft) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [BoundaryApproved](../events/system-boundary.md#boundaryapproved) | bdgrz.boundary.approved | 2 |
| [BoundaryDraftCreated](../events/system-boundary.md#boundarydraftcreated) | bdgrz.boundary.draft.created | 2 |
| [BoundaryDraftDiscarded](../events/system-boundary.md#boundarydraftdiscarded) | bdgrz.boundary.draft.discarded | 2 |
| [BoundaryDraftRevised](../events/system-boundary.md#boundarydraftrevised) | bdgrz.boundary.draft.revised | 2 |
| [BoundaryReviewed](../events/system-boundary.md#boundaryreviewed) | bdgrz.boundary.reviewed | 2 |
| [BoundarySuccessorProposed](../events/system-boundary.md#boundarysuccessorproposed) | bdgrz.boundary.successor.proposed | 2 |
| [ResponsibilityAssigned](../events/system-boundary.md#responsibilityassigned) | bdgrz.responsibility.assigned | 1 |
| [ResponsibilityRevoked](../events/system-boundary.md#responsibilityrevoked) | bdgrz.responsibility.revoked | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [S037](../reactors/boundary-risk-reassessment-reactor.md#s037) | business_receiver | SystemBoundary | RiskGovernance | RaiseRiskReassessmentTriggers |

## Product rules

- **D01-05** — A system boundary's Trust Services categories form a set of 1 to 5 distinct values drawn from security, availability, confidentiality, processing_integrity, and privacy, and the set must always contain security; boundary draft creation, revision, and successor proposal reject any other shape. Decision: [m0-d01-program-targets.md](../decisions/m0-d01-program-targets.md).
- **D01-06** — Approving a system boundary draft re-validates it, so a draft recorded before the security rule existed cannot be approved without security, while earlier approved boundary versions remain readable history. Decision: [m0-d01-program-targets.md](../decisions/m0-d01-program-targets.md).
- **D01-07** — Optional Trust Services categories are never inferred from a program name or target path; they must be recorded explicitly on the boundary, and if a selected category proves not applicable management revises the boundary through an attributed successor version. Decision: [m0-d01-program-targets.md](../decisions/m0-d01-program-targets.md).
- **D01-09** — A system boundary may carry a typed, owned, unresolved service reference before the client service register exists, but it cannot claim a governed service identity from an arbitrary UUID; the governed service and its exact boundary relationship must be validated later. Decision: [m0-d01-program-targets.md](../decisions/m0-d01-program-targets.md).
- **D22-09** — Cross-context references use tenant ID, stable record ID, and an exact version when a decision or snapshot relies on the record; before an owning inventory exists a boundary may keep a typed unresolved reference with owner and rationale. Decision: [m0-d22-canonical-ownership.md](../decisions/m0-d22-canonical-ownership.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/system-boundary.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
