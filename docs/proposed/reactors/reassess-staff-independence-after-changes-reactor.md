# ReassessStaffIndependenceAfterChangesReactor

Selected source root name: `AcceptedStaffDirectoryReconciliationReactor`.
## Why this reaction exists

Prevent a client service from relying on an independence decision after a staff member's status or assignment has changed.

Selected choreography rows: **1**. Required proofs, recovery, effect identity, and implementation evidence are preserved in [reactor row JSON](../data/reactors/reassess-staff-independence-after-changes-reactor.json).

| Row | Kind | Prerequisite facts | Command | Target owner | Emitted facts |
| --- | --- | --- | --- | --- | --- |
| S018 | business_receiver | ServiceEngagementAcceptanceRecorded, ServiceEngagementStaffAssigned | RecordDirectoryIndependenceReevaluation | ServiceEngagementIndependence | DirectoryIndependenceReevaluated |

## Selected row details

### S018

| Selected choreography detail | Value |
| --- | --- |
| id | S018 |
| kind | business_receiver |
| status | SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED |
| Source owner (target name) | ServiceEngagementIndependence (source: ServiceEngagementIndependence) |
| logicalPrerequisiteFacts (target names) | ServiceEngagementAcceptanceRecorded, ServiceEngagementStaffAssigned |
| physicalRootEvents (target names) | ServiceEngagementAcceptanceRecorded, ServiceEngagementStaffAssigned |
| Target command (selected source name: RecordDirectoryIndependenceReevaluation) | RecordDirectoryIndependenceReevaluation |
| Target owner | ServiceEngagementIndependence |
| targetAuthorityKind | business_aggregate |
| emittedSemanticFacts (target names) | DirectoryIndependenceReevaluated |
| currentRegisteredRootTypes (target names) | ReassessStaffIndependenceAfterChangesReactor |
| requiredProofContract | `{"proofGate":"Verify actual status event and exact immutable historical acceptance/assignment pair. Directory locator only narrows discovery; complete authoritative source set, cursor coverage, row identities and unchanged post-page source set are checked. No receipt reactivates an acceptance.","verification":{"admission":"AdmissionReference is a locator only. Resolve and independently verify actual typed admitted fact, exact owner/tenant/program, saved metadata and physical/hydrated cut, original root/episode/actor/intent and immutable content.","stageAndTerminal":"Materialize each required typed stage/terminal proof from actual committed facts and full required immutable selections; prove complete grant/fence/provisional/child sets, not counts/checkpoints. Guard sets are not weakened by carrier choice.","earlyReplay":"If native receipt binding does not cover a supplied/interpreted command term, bind that term through the required actual typed proof/body before early replay. Exact receipt cannot bypass proof manifest or source tuple guards.","resultAndRecovery":"Current public authorization precedes replay. Original result/receipt and unknown-append recovery stay exact; requested cancellation propagates. Unavailable proof/content remains transient; corruption/changed intent is permanent; no inferred absence/release.","implementationStatus":"Installed pointer/handler/process presence is not proof of conformance. Field/wire migration and required behavioral tests remain separately verifiable implementation work."},"prerequisiteVerification":{"admission":"The receiver resolves the original admitted root fact by its exact owner, tenant, episode, actor and immutable intent, and verifies the saved fact and content hash before acting; a reference alone grants nothing.","stageProofs":"Each required stage proof (grant, hold, review, or terminal fact) is materialised from the actual committed fact and its full immutable selection; counts, checkpoints and stage order are not accepted as proof."},"stageProofStatus":"specified","requiredTypedStageProofSets":[{"factKinds":["FirmStaffChangeRecorded"],"role":"Directory status change of an assigned staff member with revision greater than the assignment's DirectoryStaffRevision and recorded after DirectoryStaffRecordedAt","quantifier":"all_of_selection","condition":"ChangeKind = status","requiredVerification":"Resolve the actual saved fact and its full immutable selection; verify the typed payload and the exact admission, root, target, generation, actor and intent binding. A pointer, checkpoint or stage order alone is not authority."},{"factKinds":["ServiceEngagementAssignmentRevoked"],"role":"Prior revocation of the same assignment makes the engagement no longer accepted for that staff; none may exist","quantifier":"none_outstanding","condition":"","requiredVerification":"Resolve the actual saved fact and its full immutable selection; verify the typed payload and the exact admission, root, target, generation, actor and intent binding. A pointer, checkpoint or stage order alone is not authority."}],"stageProofRationale":"Same handler and aggregate guard as S017 with causation id set to the acceptance event.","stageProofSource":"authored 2026-10-29 from reactor and target source"}` |
| recoveryContract | `{"lostAcknowledgement":"If the receiver's append succeeded but the acknowledgement was lost, redelivery resolves the same deterministic effect identity and returns the original committed result without a second append.","retryAfterPartialProgress":"A retry re-reads the target stream; a proven superseded or already-applied operation is a terminal no-op; an unavailable dependency or unknown append outcome is transient and retried; changed intent or an invalid proof is a permanent conflict and the operation is quarantined.","concurrencyConflict":"On an optimistic-concurrency conflict at the target the receiver re-reads and re-decides under the target's current state; it never overwrites a newer decision.","abortOfAdmittedRoot":"If the admitted root is cancelled or the process is aborted, the bound abort receiver fences the target so a delayed delivery cannot apply, and any held reservation is released only after the fence is durable.","source":"Canonical recovery contract stated in four cases on 2026-10-29; semantically equal to the former one-sentence contract, which is retained as `recoveryContractSummary`."}` |
| effectIdentity | `{"decisionId":"EFFECT_IDENTITIES","existingOccupiedV1":"Preserve exact source-event UUID namespace, current EffectIdentity UTF8 delimiter bytes, process/receiver text, each frozen target selector and exact episode. Retain root-derived IDs when adding required proofs; no source-event substitution or silent selector rewrite.","newAlgorithmPolicy":"Any new length-prefixed algorithm is explicitly versioned and uses injective field encoding with explicit process/effect/target/episode domains and compatibility/fencing. It never changes occupied v1 IDs.","implementationQualification":"Selected policy only; individual new algorithm/wire implementation must prove exact vectors and domain/compatibility fences before emission."}` |
| implementationConformance | NOT_ASSERTED; current source evidence only |
| sourceOwners | ServiceEngagementIndependence |
| canonicalTargetOwner | ServiceEngagementIndependence |
| recoveryContractSummary | At-least-once checkpoint replay resends the same source-derived command. Missing dependencies remain retryable; actual terminal/superseded episodes are skipped only with the owning proof. |
| logicalPrerequisiteRule | The source is a newly recorded ServiceEngagementAcceptanceRecorded or ServiceEngagementStaffAssigned; every earlier directory status change for each assigned staff member that postdates the recorded directory staff revision yields a denial-only receipt against that exact pair; the receipt never reactivates acceptance. |
| prerequisiteRuleSource | authored 2026-10-29 from reactor and target source |


## Root and recovery contract

Per-row proof, identity, recovery, conformance, and implementation fields are shown above and preserved in the linked JSON record.
