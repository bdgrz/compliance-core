# ApplyArtifactInspectionOutcomeReactor

Selected source root name: `EvidenceArtifactInspectionEffectsReactor`.
## Why this reaction exists

Protect users from unsafe evidence by applying the inspector's decision to the registered artifact before it can be used or downloaded.

Selected choreography rows: **1**. Required proofs, recovery, effect identity, and implementation evidence are preserved in [reactor row JSON](../data/reactors/apply-artifact-inspection-outcome-reactor.json).

| Row | Kind | Prerequisite facts | Command | Target owner | Emitted facts |
| --- | --- | --- | --- | --- | --- |
| S000 | external_storage_reconciliation | EvidenceArtifactInspected | [Placeholder: value not recorded] | [Placeholder: value not recorded] | None listed |

## Selected row details

### S000

| Selected choreography detail | Value |
| --- | --- |
| id | S000 |
| kind | external_storage_reconciliation |
| status | SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED |
| Source owner (target name) | EvidenceArtifact (source: EvidenceArtifact) |
| logicalPrerequisiteFacts (target names) | EvidenceArtifactInspected |
| physicalRootEvents (target names) | EvidenceArtifactInspected |
| targetAuthorityKind | external_content_port |
| emittedSemanticFacts (target names) | None listed |
| currentRegisteredRootTypes (target names) | ApplyArtifactInspectionOutcomeReactor |
| requiredProofContract | `{"proofGate":"Rehydrate canonical tenant artifact registration and current state; content hash/length identify exact object. Malware quarantines; secret/invalid rejection deletes; clean rescan can release quarantine. A current state change never becomes a fabricated domain verdict.","verification":{"admission":"AdmissionReference is a locator only. Resolve and independently verify actual typed admitted fact, exact owner/tenant/program, saved metadata and physical/hydrated cut, original root/episode/actor/intent and immutable content.","stageAndTerminal":"Materialize each required typed stage/terminal proof from actual committed facts and full required immutable selections; prove complete grant/fence/provisional/child sets, not counts/checkpoints. Guard sets are not weakened by carrier choice.","earlyReplay":"If native receipt binding does not cover a supplied/interpreted command term, bind that term through the required actual typed proof/body before early replay. Exact receipt cannot bypass proof manifest or source tuple guards.","resultAndRecovery":"Current public authorization precedes replay. Original result/receipt and unknown-append recovery stay exact; requested cancellation propagates. Unavailable proof/content remains transient; corruption/changed intent is permanent; no inferred absence/release.","implementationStatus":"Installed pointer/handler/process presence is not proof of conformance. Field/wire migration and required behavioral tests remain separately verifiable implementation work."},"prerequisiteVerification":{"admission":"The receiver resolves the original admitted root fact by its exact owner, tenant, episode, actor and immutable intent, and verifies the saved fact and content hash before acting; a reference alone grants nothing.","stageProofs":"Each required stage proof (grant, hold, review, or terminal fact) is materialised from the actual committed fact and its full immutable selection; counts, checkpoints and stage order are not accepted as proof."},"stageProofStatus":"specified","requiredTypedStageProofSets":[{"factKinds":["EvidenceArtifactRegistered"],"role":"Canonical tenant registration identifying the exact object by content hash and length","quantifier":"exactly_one","condition":"always","requiredVerification":"Resolve the actual saved fact and its full immutable selection; verify the typed payload and the exact admission, root, target, generation, actor and intent binding. A pointer, checkpoint or stage order alone is not authority."},{"factKinds":["EvidenceArtifactQuarantineReleased"],"role":"Prior attributed clean rescan; only relevant when the effect is releasing a quarantine","quantifier":"at_least_one","condition":"only when the effect is a clean-rescan release of a malware quarantine","requiredVerification":"Resolve the actual saved fact and its full immutable selection; verify the typed payload and the exact admission, root, target, generation, actor and intent binding. A pointer, checkpoint or stage order alone is not authority."}],"stageProofRationale":"External-storage reconciliation with no business receiver; proofs are those needed to bind the storage action to the exact object.","stageProofSource":"authored 2026-10-29 from reactor and target source"}` |
| recoveryContract | `{"lostAcknowledgement":"If the receiver's append succeeded but the acknowledgement was lost, redelivery resolves the same deterministic effect identity and returns the original committed result without a second append.","retryAfterPartialProgress":"A retry re-reads the target stream; a proven superseded or already-applied operation is a terminal no-op; an unavailable dependency or unknown append outcome is transient and retried; changed intent or an invalid proof is a permanent conflict and the operation is quarantined.","concurrencyConflict":"On an optimistic-concurrency conflict at the target the receiver re-reads and re-decides under the target's current state; it never overwrites a newer decision.","abortOfAdmittedRoot":"If the admitted root is cancelled or the process is aborted, the bound abort receiver fences the target so a delayed delivery cannot apply, and any held reservation is released only after the fence is durable.","source":"Canonical recovery contract stated in four cases on 2026-10-29; semantically equal to the former one-sentence contract, which is retained as `recoveryContractSummary`."}` |
| effectIdentity | `{"decisionId":"EFFECT_IDENTITIES","existingOccupiedV1":"Preserve exact source-event UUID namespace, current EffectIdentity UTF8 delimiter bytes, process/receiver text, each frozen target selector and exact episode. Retain root-derived IDs when adding required proofs; no source-event substitution or silent selector rewrite.","newAlgorithmPolicy":"Any new length-prefixed algorithm is explicitly versioned and uses injective field encoding with explicit process/effect/target/episode domains and compatibility/fencing. It never changes occupied v1 IDs.","implementationQualification":"Selected policy only; individual new algorithm/wire implementation must prove exact vectors and domain/compatibility fences before emission."}` |
| implementationConformance | NOT_ASSERTED; current source evidence only |
| sourceOwners | EvidenceArtifact |
| targetPort | IArtifactContentStore |
| recoveryContractSummary | Storage operations are idempotently reconciled from current durable verdict; failures prevent checkpoint advance. No receiver command or new domain fact is emitted. |
| logicalPrerequisiteRule | The inspected artifact must be the exact tenant-registered artifact (same content hash and length) and still in the state the inspection result applies to; the effect only quarantines, deletes or releases per that result and never fabricates a domain verdict. |
| prerequisiteRuleSource | authored 2026-10-29 from reactor and target source |


## Root and recovery contract

Per-row proof, identity, recovery, conformance, and implementation fields are shown above and preserved in the linked JSON record.
