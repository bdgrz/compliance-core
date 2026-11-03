# EvaluateClientIndependenceForAcceptanceReactor

Selected source root name: `ClientIndependencePreviewReactor`.
## Why this reaction exists

Give the service-acceptance decision a complete independence result so impaired or unresolved relationships block acceptance.

Selected choreography rows: **1**. Required proofs, recovery, effect identity, and implementation evidence are preserved in [reactor row JSON](../data/reactors/client-independence-preview-reactor.json).

| Row | Kind | Prerequisite facts | Command | Target owner | Emitted facts |
| --- | --- | --- | --- | --- | --- |
| T106 | reaction_or_regular_process_stage | ClientIndependenceEvaluationAdmitted | RecordClientIndependenceEvaluation | ClientIndependenceDecision | ClientIndependenceEvaluated |

## Selected row details

### T106

| Selected choreography detail | Value |
| --- | --- |
| id | T106 |
| kind | reaction_or_regular_process_stage |
| status | SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED |
| sourceOwner | ClientServiceIndependence |
| logicalPrerequisiteFacts (target names) | ClientIndependenceEvaluationAdmitted |
| logicalPrerequisiteRule | A pure evaluation is computed from complete frozen as-of snapshots with no live reads; result recorded is compatible, conditionally_compatible or impaired with rule-set version and services considered; no production acceptance. |
| physicalRootEvents (target names) | ClientIndependenceEvaluationAdmitted |
| originalAdmissionKinds (target names) | ClientIndependenceEvaluationAdmitted |
| Target command (selected source name: MaterializeClientIndependencePreview) | RecordClientIndependenceEvaluation |
| targetOwner | ClientIndependenceDecision |
| targetAuthorityKind | business_aggregate |
| emittedSemanticFacts (target names) | ClientIndependenceEvaluated |
| emittedEventContracts | None listed |
| currentRegisteredRootTypes (target names) | EvaluateClientIndependenceForAcceptanceReactor |
| plannedRootRole | null (recorded) |
| processes | ClientIndependencePreviewProcess |
| requiredProofContract | `{"canonicalCarrier":"original admitted root reference + required independently verified typed stage/terminal proof sets","admissionReferenceFactKinds":["ClientIndependenceEvaluationAdmitted"],"admissionReferenceRole":"original business admission/first-owner fact, distinct from subscription trigger and intermediate/terminal prerequisites","verification":{"admission":"AdmissionReference is a locator only. Resolve and independently verify actual typed admitted fact, exact owner/tenant/program, saved metadata and physical/hydrated cut, original root/episode/actor/intent and immutable content.","stageAndTerminal":"Materialize each required typed stage/terminal proof from actual committed facts and full required immutable selections; prove complete grant/fence/provisional/child sets, not counts/checkpoints. Guard sets are not weakened by carrier choice.","earlyReplay":"If native receipt binding does not cover a supplied/interpreted command term, bind that term through the required actual typed proof/body before early replay. Exact receipt cannot bypass proof manifest or source tuple guards.","resultAndRecovery":"Current public authorization precedes replay. Original result/receipt and unknown-append recovery stay exact; requested cancellation propagates. Unavailable proof/content remains transient; corruption/changed intent is permanent; no inferred absence/release.","implementationStatus":"Installed pointer/handler/process presence is not proof of conformance. Field/wire migration and required behavioral tests remain separately verifiable implementation work."},"requiredTypedStageProofSets":[{"factKinds":["ClientIndependenceEvaluationAdmitted"],"role":"Evaluation request with frozen rule/service/practice snapshot","quantifier":"exactly_one","condition":"always","requiredVerification":"Resolve the actual saved fact and its full immutable selection; verify the typed payload and the exact admission, root, target, generation, actor and intent binding. A pointer, checkpoint or stage order alone is not authority."},{"factKinds":["IndependenceRulesRevised"],"role":"Rule-set version recorded in the evaluation","quantifier":"exactly_one","condition":"as frozen in snapshot","requiredVerification":"Resolve the actual saved fact and its full immutable selection; verify the typed payload and the exact admission, root, target, generation, actor and intent binding. A pointer, checkpoint or stage order alone is not authority."},{"factKinds":["PartnerIndependenceEvaluationRecorded"],"role":"Partner evaluation reference","quantifier":"exactly_one","condition":"only for conditionally_compatible result","requiredVerification":"Resolve the actual saved fact and its full immutable selection; verify the typed payload and the exact admission, root, target, generation, actor and intent binding. A pointer, checkpoint or stage order alone is not authority."}],"pinnedSourceRequirements":"Every exact source owner/event/content revision and complete immutable manifest required by intendedProofGate remains mandatory; no absent optional source is fabricated.","proofGate":"Pure evaluation from complete frozen as-of source/rule/practice snapshots; no live source reads and no production acceptance authority.","requiredLogicalPrerequisiteProofs":"Every applicable logical prerequisite is independently verified as an actual typed saved fact (or actual technical proof where explicitly technical); a terminal proof may establish an earlier stage only through a verified native chain that includes that required typed stage. Stage order/checkpoint alone never substitutes.","prerequisiteVerification":{"admission":"The receiver resolves the original admitted root fact by its exact owner, tenant, episode, actor and immutable intent, and verifies the saved fact and content hash before acting; a reference alone grants nothing.","stageProofs":"Each required stage proof (grant, hold, review, or terminal fact) is materialised from the actual committed fact and its full immutable selection; counts, checkpoints and stage order are not accepted as proof."},"stageProofStatus":"specified","stageProofRationale":"","stageProofSource":"authored 2026-10-29 from reactor and target source"}` |
| effectIdentity | `{"decisionId":"EFFECT_IDENTITIES","existingOccupiedV1":"Preserve exact source-event UUID namespace, current EffectIdentity UTF8 delimiter bytes, process/receiver text, each frozen target selector and exact episode. Retain root-derived IDs when adding required proofs; no source-event substitution or silent selector rewrite.","newAlgorithmPolicy":"Any new length-prefixed algorithm is explicitly versioned and uses injective field encoding with explicit process/effect/target/episode domains and compatibility/fencing. It never changes occupied v1 IDs.","implementationQualification":"Selected policy only; individual new algorithm/wire implementation must prove exact vectors and domain/compatibility fences before emission.","newArtifactAlgorithm":{"version":"effect-identity-v2","namespace":"actual committed EvidenceArtifactDownloadRequested.Metadata.EventId","fieldsInOrder":["effectKind (canonical receiver command kind)","canonical target EventStreamAddress","episode (exact selected value, initial when absent)"],"bytes":"UTF8(effect-identity:v2) followed by each field UTF8 bytes prefixed by its unsigned32-bit big-endian byte length; UUIDv5 over these exact name bytes and the actual source EventId namespace.","rule":"Use only for this new unoccupied process identity domain; occupied v1 algorithms/target-key selections remain unchanged. Implementation must provide fixed byte/UUID vectors before activation."}}` |
| recoveryContract | `{"lostAcknowledgement":"If the receiver's append succeeded but the acknowledgement was lost, redelivery resolves the same deterministic effect identity and returns the original committed result without a second append.","retryAfterPartialProgress":"A retry re-reads the target stream; a proven superseded or already-applied operation is a terminal no-op; an unavailable dependency or unknown append outcome is transient and retried; changed intent or an invalid proof is a permanent conflict and the operation is quarantined.","concurrencyConflict":"On an optimistic-concurrency conflict at the target the receiver re-reads and re-decides under the target's current state; it never overwrites a newer decision.","abortOfAdmittedRoot":"If the admitted root is cancelled or the process is aborted, the bound abort receiver fences the target so a delayed delivery cannot apply, and any held reservation is released only after the fence is durable.","source":"Canonical recovery contract stated in four cases on 2026-10-29; semantically equal to the former one-sentence contract, which is retained as `recoveryContractSummary`."}` |
| implementationConformance | NOT_ASSERTED; source evidence only |
| sourceOwners | ClientServiceIndependence |
| canonicalTargetOwner | ClientIndependenceDecision |
| logicalPrerequisiteRuleTemplate | selected applicable typed variant/set; see required proof contract |
| prerequisiteRuleSource | authored 2026-10-29 from reactor and target source |
| recoveryContractSummary | Exact replay returns original result; proven superseded operation is terminal no-op; unavailable dependency/unknown append is transient; changed intent or invalid proof is permanent conflict/quarantine. |
| logicalPrerequisiteRuleGuard | Pure evaluation from complete frozen as-of source/rule/practice snapshots; no live source reads and no production acceptance authority. |


## Root and recovery contract

Per-row proof, identity, recovery, conformance, and implementation fields are shown above and preserved in the linked JSON record.
