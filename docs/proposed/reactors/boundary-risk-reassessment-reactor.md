# BoundaryRiskReassessmentReactor

Selected choreography rows: **1**. Required proofs, recovery, effect identity, and implementation evidence are preserved in [reactor row JSON](../data/reactors/boundary-risk-reassessment-reactor.json).

| Row | Kind | Prerequisite facts | Command | Target owner | Emitted facts |
| --- | --- | --- | --- | --- | --- |
| S037 | business_receiver | BoundaryApproved | RaiseRiskReassessmentTriggers | RiskGovernance | RiskReassessmentTriggered |

## Selected row details

### S037

| Selected choreography detail | Value |
| --- | --- |
| id | S037 |
| kind | business_receiver |
| status | SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED |
| sourceOwner | SystemBoundary |
| logicalPrerequisiteFacts | BoundaryApproved |
| physicalRootEvents | BoundaryApproved |
| receiverCommand | RaiseRiskReassessmentTriggers |
| targetOwner | RiskGovernance |
| targetAuthorityKind | business_aggregate |
| emittedSemanticFacts | RiskReassessmentTriggered |
| currentRegisteredRootTypes | BoundaryRiskReassessmentReactor |
| requiredProofContract | `{"proofGate":"Verify actual selected publication/approval; initial boundary approval defines scope and is skipped. Risk directory must cover authoritative source tail; page all program risks and hydrate actual assessment. Skip unassessed risks and already-used method, retain exact source-derived target identity.","verification":{"admission":"AdmissionReference is a locator only. Resolve and independently verify actual typed admitted fact, exact owner/tenant/program, saved metadata and physical/hydrated cut, original root/episode/actor/intent and immutable content.","stageAndTerminal":"Materialize each required typed stage/terminal proof from actual committed facts and full required immutable selections; prove complete grant/fence/provisional/child sets, not counts/checkpoints. Guard sets are not weakened by carrier choice.","earlyReplay":"If native receipt binding does not cover a supplied/interpreted command term, bind that term through the required actual typed proof/body before early replay. Exact receipt cannot bypass proof manifest or source tuple guards.","resultAndRecovery":"Current public authorization precedes replay. Original result/receipt and unknown-append recovery stay exact; requested cancellation propagates. Unavailable proof/content remains transient; corruption/changed intent is permanent; no inferred absence/release.","implementationStatus":"Installed pointer/handler/process presence is not proof of conformance. Field/wire migration and required behavioral tests remain separately verifiable implementation work."},"prerequisiteVerification":{"admission":"The receiver resolves the original admitted root fact by its exact owner, tenant, episode, actor and immutable intent, and verifies the saved fact and content hash before acting; a reference alone grants nothing.","stageProofs":"Each required stage proof (grant, hold, review, or terminal fact) is materialised from the actual committed fact and its full immutable selection; counts, checkpoints and stage order are not accepted as proof."},"stageProofStatus":"specified","requiredTypedStageProofSets":[{"factKinds":["BoundaryApproved"],"role":"Approved boundary version that is not the first approved version","quantifier":"exactly_one","condition":"","requiredVerification":"Resolve the actual saved fact and its full immutable selection; verify the typed payload and the exact admission, root, target, generation, actor and intent binding. A pointer, checkpoint or stage order alone is not authority."},{"factKinds":["BoundaryApproved"],"role":"No earlier approval exists, which would make this the initial scope definition and trigger nothing","quantifier":"none_outstanding","condition":"Condition: first approved version skips","requiredVerification":"Resolve the actual saved fact and its full immutable selection; verify the typed payload and the exact admission, root, target, generation, actor and intent binding. A pointer, checkpoint or stage order alone is not authority."},{"factKinds":["RiskAssessmentRecorded"],"role":"The risk is assessed (has an inherent method version)","quantifier":"exactly_one","condition":"Per risk of the program","requiredVerification":"Resolve the actual saved fact and its full immutable selection; verify the typed payload and the exact admission, root, target, generation, actor and intent binding. A pointer, checkpoint or stage order alone is not authority."},{"factKinds":["RiskReassessmentTriggered"],"role":"No trigger with the same risk, kind and source reference already exists","quantifier":"none_outstanding","condition":"","requiredVerification":"Resolve the actual saved fact and its full immutable selection; verify the typed payload and the exact admission, root, target, generation, actor and intent binding. A pointer, checkpoint or stage order alone is not authority."}],"stageProofRationale":"","stageProofSource":"authored 2026-10-29 from reactor and target source"}` |
| recoveryContract | `{"lostAcknowledgement":"If the receiver's append succeeded but the acknowledgement was lost, redelivery resolves the same deterministic effect identity and returns the original committed result without a second append.","retryAfterPartialProgress":"A retry re-reads the target stream; a proven superseded or already-applied operation is a terminal no-op; an unavailable dependency or unknown append outcome is transient and retried; changed intent or an invalid proof is a permanent conflict and the operation is quarantined.","concurrencyConflict":"On an optimistic-concurrency conflict at the target the receiver re-reads and re-decides under the target's current state; it never overwrites a newer decision.","abortOfAdmittedRoot":"If the admitted root is cancelled or the process is aborted, the bound abort receiver fences the target so a delayed delivery cannot apply, and any held reservation is released only after the fence is durable.","source":"Canonical recovery contract stated in four cases on 2026-10-29; semantically equal to the former one-sentence contract, which is retained as `recoveryContractSummary`."}` |
| effectIdentity | `{"decisionId":"EFFECT_IDENTITIES","existingOccupiedV1":"Preserve exact source-event UUID namespace, current EffectIdentity UTF8 delimiter bytes, process/receiver text, each frozen target selector and exact episode. Retain root-derived IDs when adding required proofs; no source-event substitution or silent selector rewrite.","newAlgorithmPolicy":"Any new length-prefixed algorithm is explicitly versioned and uses injective field encoding with explicit process/effect/target/episode domains and compatibility/fencing. It never changes occupied v1 IDs.","implementationQualification":"Selected policy only; individual new algorithm/wire implementation must prove exact vectors and domain/compatibility fences before emission."}` |
| implementationConformance | NOT_ASSERTED; current source evidence only |
| sourceOwners | SystemBoundary |
| canonicalTargetOwner | RiskGovernance |
| recoveryContractSummary | At-least-once checkpoint replay resends the same source-derived command. Missing dependencies remain retryable; actual terminal/superseded episodes are skipped only with the owning proof. |
| logicalPrerequisiteRuleGuard | null (recorded) |
| logicalPrerequisiteRule | The source is BoundaryApproved for a successor boundary version (not the first approval of the boundary); a boundary-changed reassessment trigger is raised for each assessed risk of the program, and only if no trigger with the same deterministic id exists. |
| prerequisiteRuleSource | authored 2026-10-29 from reactor and target source |


## Root and recovery contract

Per-row proof, identity, recovery, conformance, and implementation fields are shown above and preserved in the linked JSON record.
