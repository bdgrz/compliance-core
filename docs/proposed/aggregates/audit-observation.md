# AuditObservation

- Classification: `business_aggregate`
- Source area: `Examinations` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant / audit-observations / ObservationId, with immutable ProgramId and examination/source lineage; the exact selected command selector addresses a mutation.

**Invariant:** Received auditor source corrections preserve examination/snapshot and exact source lineage; response/link commands must name current source version, and each selected command identity has immutable intent.

**Additional decision rule:** Each imported row keeps its original source identifier as provenance separate from the observation id; a missing source identifier is recorded as source_identifier_absent and never synthesized from row order. Consultant-authored content keeps imported_from provenance (source file, row, stated author text) and is never attributed to a platform actor, with the accepting member recorded as actor of the acceptance only. Auditor-identified exceptions keep the auditor's wording exactly as received.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordAuditObservation](../commands/audit-observation.md#recordauditobservation) | creation | ObservationId |
| [CorrectAuditObservation](../commands/audit-observation.md#correctauditobservation) | command | CommandId |
| [RecordAuditObservationManagementResponse](../commands/audit-observation.md#recordauditobservationmanagementresponse) | command | CommandId |
| [LinkAuditObservationFinding](../commands/audit-observation.md#linkauditobservationfinding) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [AuditObservationCorrected](../events/audit-observation.md#auditobservationcorrected) | bdgrz.examinations.audit-observation-corrected | 1 |
| [AuditObservationFindingLinked](../events/audit-observation.md#auditobservationfindinglinked) | bdgrz.examinations.audit-observation-finding-linked | 2 |
| [AuditObservationManagementResponseRecorded](../events/audit-observation.md#auditobservationmanagementresponserecorded) | bdgrz.examinations.audit-observation-management-response-recorded | 1 |
| [AuditObservationRecorded](../events/audit-observation.md#auditobservationrecorded) | bdgrz.examinations.audit-observation-recorded | 2 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D04-04** — Consultant-authored imported content keeps imported_from provenance (source file, row, stated author text) and is never attributed to a platform actor who did not act; the accepting member is the actor of the acceptance decision only. Decision: [m0-d04-readiness-material.md](../decisions/m0-d04-readiness-material.md).
- **D27-03** — Auditor-identified exceptions are recorded as AuditTestException preserving the auditor's wording as received. Decision: [m0-d27-attest-scope.md](../decisions/m0-d27-attest-scope.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/audit-observation.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
