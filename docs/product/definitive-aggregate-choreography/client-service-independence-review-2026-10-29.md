# ClientServiceIndependence aggregate review, 2026-10-29

Scope: aggregate/domain events. Source proof verification, authorization, reactors and acceptance policy remain their respective stages.

## Identity, boundary and lifecycle

One owner stream per client tenant serializes the complete nonattest service history and its epoch. A service advances the epoch; a preview evaluation admission leaves it unchanged. Keep the epoch and its source-use holds together, so an accepted hold pins exactly the service history and blocks new services until settlement. Practice walls, engagement acceptance and evaluation outcomes have distinct owners and require source-backed coordination. M0-D26 rules 3, 4 and 8 require full service history and ratified classifications, conditionally compatible partner evaluation, and excluding former advisory staff from the attest team; these are evaluated by the relevant admission and commitment process, not by assuming the epoch alone proves compatibility.

| State | Decision | Result |
|---|---|---|
| Any unheld epoch | Record service at current epoch | One service fact; epoch advances |
| Any epoch | Admit preview at current epoch with matching frozen epoch | One admission; epoch unchanged |
| Any held epoch | Record service | Transient conflict; no service fact |
| Any epoch | Stale epoch, foreign identity or invalid root | Refusal; no fact |
| Selected accepted operation | Exact retry | Original response through native acceptance; changed binding conflicts |
| Current epoch | Authorize or settle use | Hold or settlement/fence; epoch unchanged |

The aggregate keeps a compact latest service, selected evaluation and live use state. Durable history is not size-bounded; `ClientServiceNativeHistory` reads the full authority for admission. Source-use settlement and restart are distinct process and storage evidence.

## Changes and evidence

Direct root operations must match `ClientServiceNativeHistory`'s `NativeOwnerHistory.IsOwnRoot` checks; otherwise the owner could append a fact that a later verified read rejects. Both admission methods now check that root contract and require a nonempty tenant before raising a fact. The normal handlers already form the same root provenance; stale epochs remain conflicts through the existing guard. Tests were written for valid service and evaluation root operations, empty tenant and invalid provenance (empty identity, command, causation, first/affected/revision owner, episode, intent digest and timestamp). No schema or event change requested.

The coordinator ran the new tests before the guard with `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ClientServiceIndependenceOwnerTests|FullyQualifiedName~ClientIndependenceDecisionTests|FullyQualifiedName~EngagementManagementAcknowledgementOwnerTests'`: 14 failed and 8 passed across the combined selection; invalid ClientService roots raised exceptions and empty client identity was accepted. GREEN and the wider suite remain pending for the revised code.

Existing `ClientServiceIndependenceTests` cover concurrent equal service requests, stale epoch, exact retry after later service/evaluation, changed identity binding and long histories. Existing `EngagementAcceptanceProcessTests`, `ServiceEngagementAcceptanceTests` and `EngagementPersonalDecisionTests` exercise holds and process coordination; these were not rerun for this review. Actual storage, recovery and full-suite qualification are separate. Independent Jev review follows.

## Independent review before build

The actual diff, direct tests, native history and normal callers were inspected without running tests. Both production handlers construct same-owner root provenance with the expected command kind and an initial episode. The new guards reject malformed root provenance before `RaiseEvent`; the subsequent epoch check preserves stale-epoch refusal, and the prior verified native cut remains required. The direct tests exercise real refusal outcomes and no pending facts, but the loop stops at the first failure if any call throws and the tests have not yet been run green.

There is a catalog/code authority discrepancy requiring follow-up. The catalog attributes both D26-03's impairment refusal and D26-04's conditional partner-evaluation requirement to this owner. `AuthorizeEngagementEpoch` holds a pinned epoch but does not inspect an impairment or partner decision. `EventSourcedServiceEngagementAcceptanceEvidenceReader` checks complete service history against ratified rules and finds an exact matching partner evaluation when required before admission; `AcquireEngagementServiceEpochHandler` subsequently rejects an epoch or latest-event mismatch and acquires the hold. That is a distributed evidence-and-hold protocol, so the owner itself does not directly decide the two catalog-stated gates. The protocol's complete authority, races with other independently held sources and acceptance behavior need focused qualification or a source-backed catalog reconciliation; neither is proved by the new root tests. Do not mark the owner done on these tests alone.

Jev `jev-1.14.0` is advisory. Exact input and response are in `implementation-jev/clientserviceindependence-test-impl-review-2026-10-29-{request,response}.json`. The live TypeSafe docs were inaccessible, so the request used the repository's previously working schema.

| Question | Result | Disposition |
|---|---|---|
| D26-03 and D26-04 owner attribution in current code | distributed admission, not owner decision 0.80 | Source confirms difference; record as partial, qualify or reconcile with authority evidence |
| Does an exact epoch hold reject a service-set change before acquisition? | yes 0.81 | Consistent with `IsPinned`; does not prove the whole protocol |
| Root checks versus native history root contract | yes 0.74 | Source supports consistency for the fields checked |
| Repeated owner field disposition | retain shared envelope 0.65, needs source resolution 0.32; duplication premise 0.60 | Uncertain; retain current wire contract and leave open |

No event add, rename or removal requested in this pass. Existing `NonattestServiceRecorded` and `ClientIndependenceEvaluationAdmitted` carry identity both in the envelope and event payload, but reader compatibility must be established before any versioned contract change. GREEN, full suite, and exact-head hosted evidence remain pending.

## Coordinator validation (2026-10-29)

Release build and the focused three-owner selection passed (30 tests). Related client independence, acknowledgement and engagement tests passed (129 tests). The full non-broker, non-web suite passed 7,828 tests and failed five: the two ArtifactDownloadGrant coverage-map checks and three weekly-schedule original-result checks; those same five failed on hosted develop before wave 2. The suite is not green. Independent review checked current production operation builders; acknowledgement historical stream contents have not been inspected, and its replay now enforces child provenance earlier. Hosted CI and production qualification remain open.

Whitespace verification passed for the three owners’ changed C# files using `dotnet format whitespace Compliance.slnx --verify-no-changes --no-restore --include <files>`.
