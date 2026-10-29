# Definitive aggregate, event and choreography inventory

This catalog defines the intended backend architecture for the existing product scope. The inventory is complete. Existing unfinished implementation is being integrated into the default branch at the user’s explicit direction; no new feature implementation is part of this integration. The source snapshot is `8074fb02e1f5137fb3c9653231e1551f5fde34f2`; source presence and a successful source inventory do not establish implementation completion.

## Read the catalog

- [Aggregates and the events they emit](aggregates.md): 129 business/authority boundaries, 14 technical labels and the WeeklyScheduling technical role.
- [Canonical event dictionary](events.md): 497 emissions, comprising 436 business, 48 reservation, 10 technical, 2 audit and 1 migration-import semantic facts.
- [Required choreography](choreography.md):162 reaction/stage rows,22 abort/fence bindings,2 artifact entry/read rows and1 rollover execution row.
- [Physical registration plan](choreography/physical-plan.json):65 existing +2 planned root reactors;28 existing +2 planned process definitions; no new hosted worker.
- [Every historical/current/proposed event name](event-reconciliation.json): 570 names with explicit emission, compatibility, alias, cutover, externalization or prune disposition. No silent omissions.

These are selected target definitions, not a claim that every target producer or receiver is delivered. The [selected architecture decisions](decisions.json) resolve contradictory earlier ownership proposals, stale event names, download admission, proof carriers and occupied effect identities. These explicit decisions supersede the cited conflicting candidate/locked rows; historical audit files remain evidence.

Business/authority aggregates enforce real consistency boundaries. Technical process state, migration state, content/chunk storage, immutable values, transient rules, projections and compatibility readers are listed separately. A physical C# aggregate class is not automatically another business owner. The approved rollover content also pins NextExaminationId; its process waits for the actual readiness, examination, Type I baseline/auditor outcome and system-description proofs before it accepts a Type II plan.

A reactor subscription, a logical prerequisite and a process stage are distinct. One registered root reactor can start an ordered process whose receivers verify later prerequisite/terminal facts. A list of logical effects must not be implemented as one independent stream subscriber per stage.

## Fixed ownership decisions

- `ApplicationImportSource` owns exact source-revision admission and durable published replace/remove mapping authority. `ApplicationImportBatch` owns the complete frozen plan and publication; `DeclaredApplication` owns target-local effects. A separate `ApplicationSourceClaim` is excluded.
- `PlatformOperatorRoster` owns the complete current active identities and assignment episodes, acting-operator eligibility and last-operator protection in one atomic decision. A count or projected membership is insufficient. A separate `PlatformOperatorGrant` is excluded.
- `AuditPopulation` owns current row classification, reconciliation invalidation and complete freeze sealing. Row/version bodies are immutable values outside command state. A separate `AuditPopulationRowDecision` is excluded.
- `AccessExpectation` owns predecessor/successor competition within one lineage. Different lines may coexist. No unspecified system-wide exclusion rule or separate `AccessExpectationScopeGuard` is introduced.

## Fixed event and proof rules

- Assertion identity claims are permanent; there is no claim-release writer.
- Package delivery is `AuditPackageDeliveryAdmitted` on its manifest, then `AuditPackageDelivered` on its child. A separate `AuditPackageDeliveryAuthorized` or automated send stage is excluded.
- Application relationships use the selected deterministic directed source/type/target identity; removal never releases/recycles that identity. Separate relationship identity claim/release facts are excluded.
- `WorkDigestPreferenceRecorded` records the authored choice, including accepted no-change. Actual preference `ChangedAt` advances only on a value change. Occupied `WorkDigestPreferenceChanged` v1 remains compatibility input.
- Tenant RBAC bootstrap is one ordered reactor flow: define the administrator role and team, optionally register the owner as a member, assign the role to the team, commit `TenantAccess`, `TenantRbacManage`, and `ProgramManage`, then assign the owner to the administrators team only when the owner is the selected first administrator. The member assignment is the tenant-activation trigger and cannot precede all three grants.
- Every continuation must bind its original admission, root, actor, target, episode, immutable intent and required actual stage/terminal proofs. A bare source reference never grants authority. Missing required data is transient; corrupt or mismatched proof is permanent. Required terms outside native acceptance binding are checked before early receipt replay.
- Occupied v1 effect UUID byte algorithms and frozen target selections remain exact compatibility identities. A new algorithm must be explicitly versioned and injectively encoded; it must not silently generate a second effect for an old command.

## Artifact-download correction

The personal identified command is admitted first on `EvidenceArtifact` with original HTTP202 and an atomic local availability reservation in `EvidenceArtifactDownloadRequested`. Its receiving grant uses an artifact-qualified deterministic identity. An ordered process acquires exact optional advisor and explicit handling-policy uses, verifies temporal authority at the frozen issue decision, freezes and verifies the encrypted original ticket before native issuance, and settles only from actual issued/canceled facts. Cancellation fences delayed issuance before releasing uses.

The scoped read requires the same original personal actor and current exact rights. It returns pending, issued or canceled status; an issued read returns the original protected ticket and original expiry, with no renewal under the same command. There is no generic/global operation lookup. The receiving-owned issued event is v2; the occupied artifact-owned v1 is retained read-only. See the decision record for the exact receiving-ID derivation and the new process's explicitly versioned effect-ID bytes.

## Preservation and integration

All worktree changes, including unfinished work, are committed and pushed as [source checkpoint 8074fb02](https://github.com/bdgrz/compliance-core/commit/8074fb02e1f5137fb3c9653231e1551f5fde34f2). All available preparation directories and control scripts are preserved in 102 verified archives containing 61,746 files on [artifact checkpoint 61049a8f](https://github.com/bdgrz/compliance-core/commit/61049a8f0d796e5ab707d73b02690105d26fef8e). No work was discarded by the checkpoint. Both checkpoint trees, the remaining written WIP and this catalog are included in the default-branch integration.

The architecture inventory does not authorize deployment, reset, migration or new GitHub issues. Existing code and frozen packets remain implementation evidence; a target row is complete only after its actual producer, source proof, receiver, registration and recovery behavior are verified against this catalog.

The rollover execution contract, including its cross-aggregate proof sequence, is specified once in [required choreography](choreography.md).

The conditional tenant bootstrap dependency decision is specified once in [required choreography](choreography.md).

## Polish 2026-10-29

A Jev-assisted review (jev-1.14.0; evidence in `jev-review-2026-10-29-polish/`) re-adopted every declared wire identity from source at `60029b8f`, labelled owner-held holds as a distinct `reservation` emission kind (EV-D06), added `ApplicationNameClaim` and moved `WorkDigestPreference` to technical state, planned `ProviderRetired` and `OperationalProcessRetired` (EV-D07), expanded bare identities with their creation-key rule, extended invariants with the concrete M0-D05/D07/D26/D27 rules they enforce, moved the member, team-member and campaign-participant commands to their actual owners, and added [product rule traceability](product-rule-traceability.md). Open items are listed under `status: OPEN` in `decisions.json` and at the end of [aggregate details](aggregate-details.md).

Round 3 (2026-10-29): every owner with traced product rules carries a `decides` clause stating the concrete values it refuses or guarantees; `CommitmentControlLink` and `ImportedOwnerResolution` were added as planned owners for the two rules with no owner (Jev 0.98, 0.85); D26-04 is decided in two steps (classification on `ClientIndependenceDecision`, acceptance gate on `ClientServiceIndependence`); the campaign and legal-hold natural-key question is recorded OPEN.

Round 4 (2026-10-29, commands and reactors): every owner's command contracts now carry kind (http, creation, command, receiver, abort), a stated exact-retry rule, and a concurrency-guard decision (CMD_ConcurrencyGuards); 78 receiver commands were imported from the October 23 reactor contracts and the inline PopulationSnapshot writers were replaced by a planned FreezePopulationSnapshot receiver. The choreography gained ten planned reactions (X000-X009) for the planned owners and the reactions Jev rated necessary, a concrete prerequisite rule and required stage proof set on every row, and the recovery contract in four named cases. Jev evidence: round4-occ-*.json in jev-review-2026-10-29-polish/; pass D/E/F requests and responses in the session scratchpad.

Open items closed on 2026-10-29: the 27 unresolved concurrency guards were decided (20 ExpectedRevision, 7 operation-bound replies; CMD_ConcurrencyGuards), the Tenant lifecycle is an explicit state machine (TENANT_Lifecycle), X008 is a proof read at sign-off rather than a reactor (CHOREO_X008_ProofRead), AuditObservation has four planned commands (CMD_AuditObservationPlanned), AuditPopulation states that PopulationId equals DefinitionId, and the 29 lowest-scoring original proof sets were re-authored from source. Remaining implementation backlog: `productRulesNotYetInSource` in aggregates.json and `stageProofsNotYetInSource` in choreography.json.
