# RemoveRelationshipsAfterApplicationRetirementReactor

Selected source root name: `ApplicationRetirementReactor`.
## Why this reaction exists

Remove active application relationships when the exact application is retired or merged.

Selected choreography rows: **1**. Required proofs, recovery, effect identity, and implementation evidence are preserved in [reactor row JSON](../data/reactors/remove-relationships-after-application-retirement-reactor.json).

| Row | Kind | Prerequisite facts | Command | Target owner | Emitted facts |
| --- | --- | --- | --- | --- | --- |
| X005 | planned_reaction | ApplicationRetired | RemoveApplicationRelationship | ApplicationRelationship | ApplicationRelationshipRemoved |

## Selected row details

### X005

| Selected choreography detail | Value |
| --- | --- |
| id | X005 |
| kind | planned_reaction |
| status | PLANNED_TARGET_DEFINITION_2026-10-29 |
| Source owner (target name) | DeclaredApplication (source: DeclaredApplication) |
| logicalPrerequisiteFacts (target names) | ApplicationRetired |
| logicalPrerequisiteRule | For each active directed relationship with the retired application as source or target, remove it with the retirement as reason; tuple identity is never recycled. |
| physicalRootEvents (target names) | ApplicationRetired |
| originalAdmissionKinds (target names) | ApplicationRetired |
| Target command (selected source name: RemoveApplicationRelationship) | RemoveApplicationRelationship |
| Target owner | ApplicationRelationship |
| targetAuthorityKind | business_aggregate |
| emittedSemanticFacts (target names) | ApplicationRelationshipRemoved |
| emittedEventContracts | None listed |
| currentRegisteredRootTypes (target names) | None listed |
| Planned root role (target name) | RemoveRelationshipsAfterApplicationRetirementReactor |
| processes | ApplicationRetirementProcess |
| requiredProofContract | `{"canonicalCarrier":"original admitted root reference + required independently verified typed stage/terminal proof sets","admissionReferenceFactKinds":["LocationChangeAccepted"],"admissionReferenceRole":"original business admission/first-owner fact, distinct from subscription trigger and intermediate/terminal prerequisites","verification":{"admission":"AdmissionReference is a locator only. Resolve and independently verify actual typed admitted fact, exact owner/tenant/program, saved metadata and physical/hydrated cut, original root/episode/actor/intent and immutable content.","stageAndTerminal":"Materialize each required typed stage/terminal proof from actual committed facts and full required immutable selections; prove complete grant/fence/provisional/child sets, not counts/checkpoints. Guard sets are not weakened by carrier choice.","earlyReplay":"If native receipt binding does not cover a supplied/interpreted command term, bind that term through the required actual typed proof/body before early replay. Exact receipt cannot bypass proof manifest or source tuple guards.","resultAndRecovery":"Current public authorization precedes replay. Original result/receipt and unknown-append recovery stay exact; requested cancellation propagates. Unavailable proof/content remains transient; corruption/changed intent is permanent; no inferred absence/release.","implementationStatus":"Installed pointer/handler/process presence is not proof of conformance. Field/wire migration and required behavioral tests remain separately verifiable implementation work."},"requiredTypedStageProofSets":[{"factKinds":["ApplicationRelationshipAdded"],"role":"Relationship exists and is active","quantifier":"all_of_selection","condition":"every active relationship touching the retired application","requiredVerification":"Resolve the actual saved fact and its full immutable selection; verify the typed payload and the exact admission, root, target, generation, actor and intent binding. A pointer, checkpoint or stage order alone is not authority."},{"factKinds":["ApplicationRelationshipRemoved"],"role":"Not already removed","quantifier":"none_outstanding","condition":"always","requiredVerification":"Resolve the actual saved fact and its full immutable selection; verify the typed payload and the exact admission, root, target, generation, actor and intent binding. A pointer, checkpoint or stage order alone is not authority."}],"pinnedSourceRequirements":"Every exact source owner/event/content revision and complete immutable manifest required by intendedProofGate remains mandatory; no absent optional source is fabricated.","proofGate":"For each active directed relationship whose source or target is the retired application, remove it with the retirement as attributed reason; identity is never recycled.","requiredLogicalPrerequisiteProofs":"Every applicable logical prerequisite is independently verified as an actual typed saved fact (or actual technical proof where explicitly technical); a terminal proof may establish an earlier stage only through a verified native chain that includes that required typed stage. Stage order/checkpoint alone never substitutes.","prerequisiteVerification":{"admission":"The receiver resolves the original admitted root fact by its exact owner, tenant, episode, actor and immutable intent, and verifies the saved fact and content hash before acting; a reference alone grants nothing.","stageProofs":"Each required stage proof (grant, hold, review, or terminal fact) is materialised from the actual committed fact and its full immutable selection; counts, checkpoints and stage order are not accepted as proof."},"stageProofStatus":"specified","stageProofRationale":"","stageProofSource":"authored 2026-10-29 from reactor and target source"}` |
| effectIdentity | `{"version":"effect-identity-v2","namespace":"actual committed ApplicationRetired.Metadata.EventId","fieldsInOrder":["effectKind","canonical target EventStreamAddress","episode"],"rule":"explicitly versioned injective length-prefixed encoding for this new edge; no reuse of occupied v1 bytes"}` |
| recoveryContract | `{"lostAcknowledgement":"If the receiver's append succeeded but the acknowledgement was lost, redelivery resolves the same deterministic effect identity and returns the original committed result without a second append.","retryAfterPartialProgress":"A retry re-reads the target stream; a proven superseded or already-applied operation is a terminal no-op; an unavailable dependency or unknown append outcome is transient and retried; changed intent or an invalid proof is a permanent conflict and the operation is quarantined.","concurrencyConflict":"On an optimistic-concurrency conflict at the target the receiver re-reads and re-decides under the target's current state; it never overwrites a newer decision.","abortOfAdmittedRoot":"If the admitted root is cancelled or the process is aborted, the bound abort receiver fences the target so a delayed delivery cannot apply, and any held reservation is released only after the fence is durable.","source":"Canonical recovery contract stated in four cases on 2026-10-29; semantically equal to the former one-sentence contract, which is retained as `recoveryContractSummary`."}` |
| implementationConformance | UNIMPLEMENTED_PLANNED_TARGET |
| rationale | Pass F: ApplicationRelationship needs inbound 0.67 from DeclaredApplication 0.87. |
| jev | `{"needs_inbound":0.67,"source":"DeclaredApplication 0.87"}` |
| recoveryContractSummary | Exact replay returns original result; proven superseded operation is terminal no-op; unavailable dependency/unknown append is transient; changed intent or invalid proof is permanent conflict/quarantine. |
| logicalPrerequisiteRuleGuard | For each active directed relationship whose source or target is the retired application, remove it with the retirement as attributed reason; identity is never recycled. |
| prerequisiteRuleSource | authored 2026-10-29 from reactor and target source |


## Root and recovery contract

Per-row proof, identity, recovery, conformance, and implementation fields are shown above and preserved in the linked JSON record.
