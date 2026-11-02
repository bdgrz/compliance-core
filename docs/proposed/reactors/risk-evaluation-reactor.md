# RiskEvaluationReactor

Selected choreography rows: **1**. Required proofs, recovery, effect identity, and implementation evidence are preserved in [reactor row JSON](../data/reactors/risk-evaluation-reactor.json).

| Row | Kind | Prerequisite facts | Command | Target owner | Emitted facts |
| --- | --- | --- | --- | --- | --- |
| X009 | planned_reaction | RiskAccepted, RiskAssessmentRecorded | RecordRiskReassessmentObligation | RiskGovernance | RiskReassessmentTriggered |

## Selected row details

### X009

| Selected choreography detail | Value |
| --- | --- |
| id | X009 |
| kind | planned_reaction |
| status | PLANNED_TARGET_DEFINITION_2026-10-29 |
| sourceOwner | RiskEvaluation |
| logicalPrerequisiteFacts | RiskAccepted, RiskAssessmentRecorded |
| logicalPrerequisiteRule | Planned: an acceptance expiry (RiskAccepted with an expiry reached) or a residual assessment above appetite (RiskAssessmentRecorded) creates the reassessment obligation on that risk's governance owner with the exact evaluation revision as reason, only once per semantic obligation. |
| physicalRootEvents | RiskAccepted, RiskAssessmentRecorded |
| originalAdmissionKinds | RiskAccepted |
| receiverCommand | RecordRiskReassessmentObligation |
| targetOwner | RiskGovernance |
| targetAuthorityKind | business_aggregate |
| emittedSemanticFacts | RiskReassessmentTriggered |
| emittedEventContracts | None listed |
| currentRegisteredRootTypes | None listed |
| plannedRootRole | RiskEvaluationReactor |
| processes | RiskReassessmentProcess |
| requiredProofContract | `{"canonicalCarrier":"original admitted root reference + required independently verified typed stage/terminal proof sets","admissionReferenceFactKinds":["LocationChangeAccepted"],"admissionReferenceRole":"original business admission/first-owner fact, distinct from subscription trigger and intermediate/terminal prerequisites","verification":{"admission":"AdmissionReference is a locator only. Resolve and independently verify actual typed admitted fact, exact owner/tenant/program, saved metadata and physical/hydrated cut, original root/episode/actor/intent and immutable content.","stageAndTerminal":"Materialize each required typed stage/terminal proof from actual committed facts and full required immutable selections; prove complete grant/fence/provisional/child sets, not counts/checkpoints. Guard sets are not weakened by carrier choice.","earlyReplay":"If native receipt binding does not cover a supplied/interpreted command term, bind that term through the required actual typed proof/body before early replay. Exact receipt cannot bypass proof manifest or source tuple guards.","resultAndRecovery":"Current public authorization precedes replay. Original result/receipt and unknown-append recovery stay exact; requested cancellation propagates. Unavailable proof/content remains transient; corruption/changed intent is permanent; no inferred absence/release.","implementationStatus":"Installed pointer/handler/process presence is not proof of conformance. Field/wire migration and required behavioral tests remain separately verifiable implementation work."},"requiredTypedStageProofSets":[{"factKinds":["RiskAccepted"],"role":"The acceptance has an expiry that has passed and is the current acceptance of the risk","quantifier":"exactly_one","condition":"Condition: expired-acceptance obligation","requiredVerification":"Resolve the actual saved fact and its full immutable selection; verify the typed payload and the exact admission, root, target, generation, actor and intent binding. A pointer, checkpoint or stage order alone is not authority."},{"factKinds":["RiskAssessmentRecorded"],"role":"The exact residual assessment revision exceeds appetite","quantifier":"exactly_one","condition":"Condition: above-appetite obligation","requiredVerification":"Resolve the actual saved fact and its full immutable selection; verify the typed payload and the exact admission, root, target, generation, actor and intent binding. A pointer, checkpoint or stage order alone is not authority."},{"factKinds":["RiskReassessmentTriggered"],"role":"No trigger with the same risk, kind and evaluation revision exists","quantifier":"none_outstanding","condition":"","requiredVerification":"Resolve the actual saved fact and its full immutable selection; verify the typed payload and the exact admission, root, target, generation, actor and intent binding. A pointer, checkpoint or stage order alone is not authority."}],"pinnedSourceRequirements":"Every exact source owner/event/content revision and complete immutable manifest required by intendedProofGate remains mandatory; no absent optional source is fabricated.","proofGate":"An acceptance expiry or a residual assessment above appetite creates the reassessment obligation on the risk governance owner with the exact evaluation revision as reason.","requiredLogicalPrerequisiteProofs":"Every applicable logical prerequisite is independently verified as an actual typed saved fact (or actual technical proof where explicitly technical); a terminal proof may establish an earlier stage only through a verified native chain that includes that required typed stage. Stage order/checkpoint alone never substitutes.","prerequisiteVerification":{"admission":"The receiver resolves the original admitted root fact by its exact owner, tenant, episode, actor and immutable intent, and verifies the saved fact and content hash before acting; a reference alone grants nothing.","stageProofs":"Each required stage proof (grant, hold, review, or terminal fact) is materialised from the actual committed fact and its full immutable selection; counts, checkpoints and stage order are not accepted as proof."},"stageProofStatus":"specified","stageProofRationale":"","stageProofSource":"authored 2026-10-29 from reactor and target source"}` |
| effectIdentity | `{"version":"effect-identity-v2","namespace":"actual committed RiskAccepted.Metadata.EventId","fieldsInOrder":["effectKind","canonical target EventStreamAddress","episode"],"rule":"explicitly versioned injective length-prefixed encoding for this new edge; no reuse of occupied v1 bytes"}` |
| recoveryContract | `{"lostAcknowledgement":"If the receiver's append succeeded but the acknowledgement was lost, redelivery resolves the same deterministic effect identity and returns the original committed result without a second append.","retryAfterPartialProgress":"A retry re-reads the target stream; a proven superseded or already-applied operation is a terminal no-op; an unavailable dependency or unknown append outcome is transient and retried; changed intent or an invalid proof is a permanent conflict and the operation is quarantined.","concurrencyConflict":"On an optimistic-concurrency conflict at the target the receiver re-reads and re-decides under the target's current state; it never overwrites a newer decision.","abortOfAdmittedRoot":"If the admitted root is cancelled or the process is aborted, the bound abort receiver fences the target so a delayed delivery cannot apply, and any held reservation is released only after the fence is durable.","source":"Canonical recovery contract stated in four cases on 2026-10-29; semantically equal to the former one-sentence contract, which is retained as `recoveryContractSummary`."}` |
| implementationConformance | UNIMPLEMENTED_PLANNED_TARGET |
| rationale | Pass F: RiskEvaluation needs outbound 0.62 to RiskGovernance 0.76. |
| jev | `{"needs_outbound":0.62,"target":"RiskGovernance 0.76"}` |
| recoveryContractSummary | Exact replay returns original result; proven superseded operation is terminal no-op; unavailable dependency/unknown append is transient; changed intent or invalid proof is permanent conflict/quarantine. |
| logicalPrerequisiteRuleGuard | An acceptance expiry or a residual assessment above appetite creates the reassessment obligation on the risk governance owner with the exact evaluation revision as reason. |
| prerequisiteRuleSource | authored 2026-10-29 from reactor and target source |


## Root and recovery contract

Per-row proof, identity, recovery, conformance, and implementation fields are shown above and preserved in the linked JSON record.
