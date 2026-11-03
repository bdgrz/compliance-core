# ReassessFirmStaffIndependenceAfterChangesReactor

Selected source root name: `FirmStaffStatusReevaluationReactor`.
## Why this reaction exists

Keep client-service independence decisions current when a firm's staff status changes, and block affected acceptances that relied on outdated staff information.

Selected choreography rows: **1**. Required proofs, recovery, effect identity, and implementation evidence are preserved in [reactor row JSON](../data/reactors/firm-staff-status-reevaluation-reactor.json).

| Row | Kind | Prerequisite facts | Command | Target owner | Emitted facts |
| --- | --- | --- | --- | --- | --- |
| S017 | business_receiver | FirmStaffChangeRecorded | RecordDirectoryIndependenceReevaluation | ServiceEngagementIndependence | DirectoryIndependenceReevaluated |

## Selected row details

### S017

| Selected choreography detail | Value |
| --- | --- |
| id | S017 |
| kind | business_receiver |
| status | SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED |
| Source owner (target name) | FirmStaffRecord (source: FirmStaffRecord) |
| logicalPrerequisiteFacts (target names) | FirmStaffChangeRecorded |
| physicalRootEvents (target names) | FirmStaffChangeRecorded |
| Target command (selected source name: RecordDirectoryIndependenceReevaluation) | RecordDirectoryIndependenceReevaluation |
| Target owner | ServiceEngagementIndependence |
| targetAuthorityKind | business_aggregate |
| emittedSemanticFacts (target names) | DirectoryIndependenceReevaluated |
| currentRegisteredRootTypes (target names) | ReassessFirmStaffIndependenceAfterChangesReactor |
| requiredProofContract | `{"proofGate":"Verify actual status event and exact immutable historical acceptance/assignment pair. Directory locator only narrows discovery; complete authoritative source set, cursor coverage, row identities and unchanged post-page source set are checked. No receipt reactivates an acceptance.","verification":{"admission":"AdmissionReference is a locator only. Resolve and independently verify actual typed admitted fact, exact owner/tenant/program, saved metadata and physical/hydrated cut, original root/episode/actor/intent and immutable content.","stageAndTerminal":"Materialize each required typed stage/terminal proof from actual committed facts and full required immutable selections; prove complete grant/fence/provisional/child sets, not counts/checkpoints. Guard sets are not weakened by carrier choice.","earlyReplay":"If native receipt binding does not cover a supplied/interpreted command term, bind that term through the required actual typed proof/body before early replay. Exact receipt cannot bypass proof manifest or source tuple guards.","resultAndRecovery":"Current public authorization precedes replay. Original result/receipt and unknown-append recovery stay exact; requested cancellation propagates. Unavailable proof/content remains transient; corruption/changed intent is permanent; no inferred absence/release.","implementationStatus":"Installed pointer/handler/process presence is not proof of conformance. Field/wire migration and required behavioral tests remain separately verifiable implementation work."},"prerequisiteVerification":{"admission":"The receiver resolves the original admitted root fact by its exact owner, tenant, episode, actor and immutable intent, and verifies the saved fact and content hash before acting; a reference alone grants nothing.","stageProofs":"Each required stage proof (grant, hold, review, or terminal fact) is materialised from the actual committed fact and its full immutable selection; counts, checkpoints and stage order are not accepted as proof."},"stageProofStatus":"specified","requiredTypedStageProofSets":[{"factKinds":["ServiceEngagementAcceptanceRecorded","ServiceEngagementStaffAssigned"],"role":"Exact immutable historical accepted fact (and accepted result) containing an assignment of the staff member and user with DirectoryStaffRevision lower than the change revision","quantifier":"all_of_selection","condition":"engagement still in accepted status","requiredVerification":"Resolve the actual saved fact and its full immutable selection; verify the typed payload and the exact admission, root, target, generation, actor and intent binding. A pointer, checkpoint or stage order alone is not authority."},{"factKinds":["FirmStaffRegistrationAccepted"],"role":"Directory identity binding of staff member to user and practice for the record whose status changed","quantifier":"exactly_one","condition":"","requiredVerification":"Resolve the actual saved fact and its full immutable selection; verify the typed payload and the exact admission, root, target, generation, actor and intent binding. A pointer, checkpoint or stage order alone is not authority."}],"stageProofRationale":"Handler verifies directory event id, acceptance event id, payload digests, original operation root, completed fact equality and tenant activity; engagement.PrepareDirectoryReevaluation requires status accepted.","stageProofSource":"authored 2026-10-29 from reactor and target source"}` |
| recoveryContract | `{"lostAcknowledgement":"If the receiver's append succeeded but the acknowledgement was lost, redelivery resolves the same deterministic effect identity and returns the original committed result without a second append.","retryAfterPartialProgress":"A retry re-reads the target stream; a proven superseded or already-applied operation is a terminal no-op; an unavailable dependency or unknown append outcome is transient and retried; changed intent or an invalid proof is a permanent conflict and the operation is quarantined.","concurrencyConflict":"On an optimistic-concurrency conflict at the target the receiver re-reads and re-decides under the target's current state; it never overwrites a newer decision.","abortOfAdmittedRoot":"If the admitted root is cancelled or the process is aborted, the bound abort receiver fences the target so a delayed delivery cannot apply, and any held reservation is released only after the fence is durable.","source":"Canonical recovery contract stated in four cases on 2026-10-29; semantically equal to the former one-sentence contract, which is retained as `recoveryContractSummary`."}` |
| effectIdentity | `{"decisionId":"EFFECT_IDENTITIES","existingOccupiedV1":"Preserve exact source-event UUID namespace, current EffectIdentity UTF8 delimiter bytes, process/receiver text, each frozen target selector and exact episode. Retain root-derived IDs when adding required proofs; no source-event substitution or silent selector rewrite.","newAlgorithmPolicy":"Any new length-prefixed algorithm is explicitly versioned and uses injective field encoding with explicit process/effect/target/episode domains and compatibility/fencing. It never changes occupied v1 IDs.","implementationQualification":"Selected policy only; individual new algorithm/wire implementation must prove exact vectors and domain/compatibility fences before emission."}` |
| implementationConformance | NOT_ASSERTED; current source evidence only |
| sourceOwners | FirmStaffRecord |
| canonicalTargetOwner | ServiceEngagementIndependence |
| recoveryContractSummary | At-least-once checkpoint replay resends the same source-derived command. Missing dependencies remain retryable; actual terminal/superseded episodes are skipped only with the owning proof. |
| logicalPrerequisiteRule | The source is a FirmStaffChangeRecorded with ChangeKind status, verified against its authoritative record history; for every active tenant and every accepted engagement acceptance/assignment pair naming that staff member whose recorded directory staff revision is older than the change, a denial-only receipt is recorded against the exact historical acceptance and change pair. A receipt never reactivates an acceptance. |
| prerequisiteRuleSource | authored 2026-10-29 from reactor and target source |


## Root and recovery contract

Per-row proof, identity, recovery, conformance, and implementation fields are shown above and preserved in the linked JSON record.
