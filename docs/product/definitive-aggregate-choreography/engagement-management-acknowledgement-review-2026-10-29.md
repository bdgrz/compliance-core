# EngagementManagementAcknowledgement aggregate review, 2026-10-29

Scope is the aggregate and its event. Tests are written, not executed; build and run are pending.

## Identity, invariant and boundary

One tenant/acknowledgement ID stream owns one immutable personal acknowledgement bound to an engagement revision and complete service history. It does not independently grant acceptance. Keep the decision and its immutable content reference in that owner: a second materialization on the same stream must not replace the first. `ServiceEngagementIndependence` admits and pins the personal decision; the source-use process and receiver verify exact engagement, service snapshot, actor and intent before requesting the acknowledgement. The reactor delivers the authorized decision; it does not infer source authority from a projection. The selected logical receiver ID chooses replay and does not alter the stream key.

## Lifecycle and changes

| State | Decision | Outcome |
|---|---|---|
| Fresh | Materialize from own owner and acknowledgement root | One `EngagementManagementAcknowledged`, revision 1 |
| Fresh | Materialize with foreign owner or different root | Validation failure, no fact |
| Acknowledged | Another direct materialization | Conflict, no fact |
| Acknowledged | Exact authorized receiver redelivery | Original fact resolved by receiver; no new fact |

The original `Materialize` checked the operation owner but could write a fact with its own acknowledgement ID and another root operation ID. The handler requires the acknowledgement ID and originating operation ID to match, and the receiver's child provenance copies the admitted root from the frozen intent. The aggregate now rejects the mismatch before raising a fact and enforces the same binding on replay. This does not replace the receiver's verification of the source, actor, engagement revision or service history.

## Evidence and remaining scope

| Criterion | Evidence |
|---|---|
| Tenant and acknowledgement identity; selected logical ID does not fork stream | new `ShouldKeepIdentityStableGivenTenantAndAcknowledgement` |
| Foreign owner and root refuse with no fact | new `ShouldRefuseMaterializationWithoutFactGivenDifferentRootOrOwner` |
| One valid fact, valid replay and no second materialization | new `ShouldPreserveOriginalAcknowledgementAndRefuseAnotherGivenValidReplay` |
| Exact retry, valid process materialization, frozen service snapshot and stale engagement revision/epoch | existing `EngagementManagementAcknowledgementTests` and `EngagementManagementAcknowledgementHandlerTests` |
| Competing deliveries and exact source use | receiver handles event-stream concurrency and rehydrates; independent race verification not established here |

The selected event is `EngagementManagementAcknowledged`, matching the code. The event includes the tenant, acknowledgement ID and native operation envelope; possible repeated identity fields require independent Jev review. No event addition, removal, rename or schema change is requested. `M0-D26` requires management's acknowledgement of nonattest responsibility; the authenticated original actor and content are verified in the receiver. Handler, reactor and projection changes are outside this aggregate round.

The coordinator ran `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ClientServiceIndependenceOwnerTests|FullyQualifiedName~ClientIndependenceDecisionTests|FullyQualifiedName~EngagementManagementAcknowledgementOwnerTests'` before the fix: 14 failed, 8 passed of 22 across the combined selection. The acknowledgement test demonstrated that a mismatched root was accepted. Build and green verification remain pending. Independent source-backed Jev review follows.

## Independent review before build

The original valid child fixture derived `FirstOwner` from the acknowledgement itself. The real `EngagementPersonalDecisionEffects.Child` copies the source engagement stream into `FirstOwner`; the fixture now uses a tenant-scoped `ServiceEngagementIndependence` stream. Additional tests cover empty tenant, acknowledgement and logical IDs, missing cause, wrong command kind, foreign-tenant source, and mismatched affected/revision owners.

The owner can compare its own tenant/acknowledgement and target addresses, require the command kind and a causation ID, and check that `FirstOwner` is a service-engagement stream in the same tenant. It cannot establish that a particular source event, engagement ID, actor or digest matches the parent without reading the authorized parent and immutable intent. The receiver and settlement verifier supply those cross-owner checks; no new cross-owner service is introduced here.

The coordinator ran `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~EngagementManagementAcknowledgementOwnerTests'` before these additional guards: 8 failed and 3 passed. The failures show empty identity or invalid child provenance was accepted. `Materialize` and replay now share the owner-decidable checks listed above; source identity is checked only for tenant and area, with the exact source proof left to the receiver. GREEN remains pending.

Jev `jev-1.14.0` (advisory; request and response in `implementation-jev/engagementmanagementacknowledgement-test-impl-review-2026-10-29-*.json`): empty identity guard premise Noul 0.38 (uncertain; the selected command contract and receiver explicitly require nonempty IDs), checking source stream kind and tenant Noul 0.85, verifying the exact source from owner-only state Noul 0.05, payload identity duplication Noul 0.91. Contract disposition is unresolved: keep existing 0.56, versioned change 0.11, source resolution 0.33 (confidence 0.34); no event change applied. The selected catalog materialization command also has unrelated `effectKind` `claim_professional_assertion_identity`; coordinator must reconcile this shared catalog discrepancy. Live TypeSafe documentation could not be reached; the request uses the repository's retained working request schema.

## Coordinator validation (2026-10-29)

Release build and the focused three-owner selection passed (30 tests). Related client independence, acknowledgement and engagement tests passed (129 tests). The full non-broker, non-web suite passed 7,828 tests and failed five: the two ArtifactDownloadGrant coverage-map checks and three weekly-schedule original-result checks; those same five failed on hosted develop before wave 2. The suite is not green. Independent review checked current production operation builders; acknowledgement historical stream contents have not been inspected, and its replay now enforces child provenance earlier. Hosted CI and production qualification remain open.

Whitespace verification passed for the three owners’ changed C# files using `dotnet format whitespace Compliance.slnx --verify-no-changes --no-restore --include <files>`.

## Engagement source identity follow-up

Review of the sibling partner decision found that checking the first owner's realm and area alone admitted a non-UUID or empty engagement resource. The coordinator ran `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~EngagementManagementAcknowledgementOwnerTests.ShouldRefuseMaterializationWithoutFactGivenInvalidIdentityOrChildProvenance'` before this fix: 2 failed, 8 passed, with both invalid resource cases accepted. `ValidChild` now also requires a canonical, nonempty engagement UUID in the source resource, matching `ServiceEngagementIndependence` stream construction. The source receiver remains responsible for verifying the exact authorized event and frozen intent. The same validation executes on replay, so existing persisted facts with a malformed source resource would no longer hydrate; normal checked-in producers use canonical engagement IDs, but production history has not been audited. Focused GREEN and review remain pending.

## Wave 10 coordinator validation (2026-10-29)

Release compilation and the contribution, partner decision, roster and acknowledgement selection passed 115 tests. The full non-broker, non-web suite passed 7,887 and failed five: two ArtifactDownloadGrant coverage-map tests and three weekly-schedule original-result tests; the same five failed on hosted develop before wave 2. The suite is not green. Checked-in producers were reviewed; persisted histories, legacy wire migration and production behavior remain unqualified.

Whitespace verification passed for the changed C# files in the combined second and third batches using `dotnet format whitespace Compliance.slnx --verify-no-changes --no-restore --include <files>`.
