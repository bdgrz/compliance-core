# KeepRiskAcceptanceWithProviderGapReactor

Selected source root name: `ProviderRiskAcceptanceLinkReactor`.
## Why this reaction exists

Keep the exact accepted risk linked to the provider gap that admitted it, even if the gap later closes.

Selected choreography rows: **1**. Required proofs, recovery, effect identity, and implementation evidence are preserved in [reactor row JSON](../data/reactors/provider-risk-acceptance-link-reactor.json).

| Row | Kind | Prerequisite facts | Command | Target owner | Emitted facts |
| --- | --- | --- | --- | --- | --- |
| T088 | reaction_or_regular_process_stage | ProviderCoverageGapRiskAcceptanceAdmitted | LinkProviderRiskAcceptanceToGap | ProviderRiskAcceptance | ProviderCoverageGapRiskAcceptanceLinked |

## Selected row details

### T088

| Selected choreography detail | Value |
| --- | --- |
| id | T088 |
| kind | reaction_or_regular_process_stage |
| status | SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED |
| sourceOwner | ProviderAssuranceGap |
| logicalPrerequisiteFacts (target names) | ProviderCoverageGapRiskAcceptanceAdmitted |
| logicalPrerequisiteRule | Exact open-gap admission and active-at-admission risk acceptance proof. Later gap closure/expiry cannot lose admitted historical link; never change gap to closed. |
| physicalRootEvents (target names) | ProviderCoverageGapRiskAcceptanceAdmitted |
| originalAdmissionKinds (target names) | AdvisorFeedbackAdmitted, AuditorResponseAdmitted, AuditorCommentAdmitted, CorrectiveActionAdmitted, AuditorSampleSelectionAdmitted, ProviderCoverageGapRiskAcceptanceAdmitted, PolicyParticipantResultAdmitted |
| Target command (selected source name: MaterializeProviderRiskAcceptanceLink) | LinkProviderRiskAcceptanceToGap |
| targetOwner | ProviderRiskAcceptance |
| targetAuthorityKind | business_aggregate |
| emittedSemanticFacts (target names) | ProviderCoverageGapRiskAcceptanceLinked |
| emittedEventContracts | None listed |
| currentRegisteredRootTypes (target names) | KeepRiskAcceptanceWithProviderGapReactor |
| plannedRootRole | null (recorded) |
| processes | IndependentChildMaterialization |
| requiredProofContract | `{"canonicalCarrier":"original admitted root reference + required independently verified typed stage/terminal proof sets","admissionReferenceFactKinds":["AdvisorFeedbackAdmitted","AuditorResponseAdmitted","AuditorCommentAdmitted","CorrectiveActionAdmitted","AuditorSampleSelectionAdmitted","ProviderCoverageGapRiskAcceptanceAdmitted","PolicyParticipantResultAdmitted"],"admissionReferenceRole":"original business admission/first-owner fact, distinct from subscription trigger and intermediate/terminal prerequisites","verification":{"admission":"AdmissionReference is a locator only. Resolve and independently verify actual typed admitted fact, exact owner/tenant/program, saved metadata and physical/hydrated cut, original root/episode/actor/intent and immutable content.","stageAndTerminal":"Materialize each required typed stage/terminal proof from actual committed facts and full required immutable selections; prove complete grant/fence/provisional/child sets, not counts/checkpoints. Guard sets are not weakened by carrier choice.","earlyReplay":"If native receipt binding does not cover a supplied/interpreted command term, bind that term through the required actual typed proof/body before early replay. Exact receipt cannot bypass proof manifest or source tuple guards.","resultAndRecovery":"Current public authorization precedes replay. Original result/receipt and unknown-append recovery stay exact; requested cancellation propagates. Unavailable proof/content remains transient; corruption/changed intent is permanent; no inferred absence/release.","implementationStatus":"Installed pointer/handler/process presence is not proof of conformance. Field/wire migration and required behavioral tests remain separately verifiable implementation work."},"requiredTypedStageProofSets":[{"factKinds":["RiskAccepted"],"role":"actual captured historical risk+acceptance tuple at native scoped locator; no new live-risk/expiry gate","quantifier":"exactly_one_selected_variant","condition":"always","requiredVerification":"Resolve actual saved owner/source fact and full metadata/cut, verify typed payload/immutable content and exact admission/root/target/generation/actor/intent binding. Pointer, checkpoint and stage order alone are not authority."}],"pinnedSourceRequirements":"Every exact source owner/event/content revision and complete immutable manifest required by intendedProofGate remains mandatory; no absent optional source is fabricated.","proofGate":"Exact open-gap admission and active-at-admission risk acceptance proof. Later gap closure/expiry cannot lose admitted historical link; never change gap to closed.","requiredLogicalPrerequisiteProofs":"Every applicable logical prerequisite is independently verified as an actual typed saved fact (or actual technical proof where explicitly technical); a terminal proof may establish an earlier stage only through a verified native chain that includes that required typed stage. Stage order/checkpoint alone never substitutes.","prerequisiteVerification":{"admission":"The receiver resolves the original admitted root fact by its exact owner, tenant, episode, actor and immutable intent, and verifies the saved fact and content hash before acting; a reference alone grants nothing.","stageProofs":"Each required stage proof (grant, hold, review, or terminal fact) is materialised from the actual committed fact and its full immutable selection; counts, checkpoints and stage order are not accepted as proof."}}` |
| effectIdentity | `{"decisionId":"EFFECT_IDENTITIES","existingOccupiedV1":"Preserve exact source-event UUID namespace, current EffectIdentity UTF8 delimiter bytes, process/receiver text, each frozen target selector and exact episode. Retain root-derived IDs when adding required proofs; no source-event substitution or silent selector rewrite.","newAlgorithmPolicy":"Any new length-prefixed algorithm is explicitly versioned and uses injective field encoding with explicit process/effect/target/episode domains and compatibility/fencing. It never changes occupied v1 IDs.","implementationQualification":"Selected policy only; individual new algorithm/wire implementation must prove exact vectors and domain/compatibility fences before emission.","newArtifactAlgorithm":{"version":"effect-identity-v2","namespace":"actual committed EvidenceArtifactDownloadRequested.Metadata.EventId","fieldsInOrder":["effectKind (canonical receiver command kind)","canonical target EventStreamAddress","episode (exact selected value, initial when absent)"],"bytes":"UTF8(effect-identity:v2) followed by each field UTF8 bytes prefixed by its unsigned32-bit big-endian byte length; UUIDv5 over these exact name bytes and the actual source EventId namespace.","rule":"Use only for this new unoccupied process identity domain; occupied v1 algorithms/target-key selections remain unchanged. Implementation must provide fixed byte/UUID vectors before activation."}}` |
| recoveryContract | `{"lostAcknowledgement":"If the receiver's append succeeded but the acknowledgement was lost, redelivery resolves the same deterministic effect identity and returns the original committed result without a second append.","retryAfterPartialProgress":"A retry re-reads the target stream; a proven superseded or already-applied operation is a terminal no-op; an unavailable dependency or unknown append outcome is transient and retried; changed intent or an invalid proof is a permanent conflict and the operation is quarantined.","concurrencyConflict":"On an optimistic-concurrency conflict at the target the receiver re-reads and re-decides under the target's current state; it never overwrites a newer decision.","abortOfAdmittedRoot":"If the admitted root is cancelled or the process is aborted, the bound abort receiver fences the target so a delayed delivery cannot apply, and any held reservation is released only after the fence is durable.","source":"Canonical recovery contract stated in four cases on 2026-10-29; semantically equal to the former one-sentence contract, which is retained as `recoveryContractSummary`."}` |
| implementationConformance | NOT_ASSERTED; source evidence only |
| sourceOwners | ProviderAssuranceGap |
| canonicalTargetOwner | ProviderRiskAcceptance |
| logicalPrerequisiteRuleTemplate | selected applicable typed variant/set; see required proof contract |
| prerequisiteRuleSource | receiver guard from docs/product/audits/2026-10-23/target-reactor-contracts.json, lifted 2026-10-29 |
| recoveryContractSummary | Exact replay returns original result; proven superseded operation is terminal no-op; unavailable dependency/unknown append is transient; changed intent or invalid proof is permanent conflict/quarantine. |


## Root and recovery contract

Per-row proof, identity, recovery, conformance, and implementation fields are shown above and preserved in the linked JSON record.
