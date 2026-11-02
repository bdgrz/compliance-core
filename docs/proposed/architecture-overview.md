# Architecture overview

This page preserves the unique architecture rules from the earlier definitive-catalog README and points them at the structured pages. The selected target counts and unresolved source conflicts are in [the manifest](data/manifest.json) and [open gaps](open-gaps.md).

## Structured catalogs

- [Aggregates](aggregates/README.md) and [boundary decisions](aggregate-boundary-notes.md).
- [Canonical events](events/README.md) and the [historical event-name reconciliation](events/reconciliation.md).
- [Reactors and choreography](reactors/README.md), [processes](processes/README.md), and the [physical registration snapshot](reactors/registration-plan.md).
- [Architecture and product-rule decisions](decisions-log.md) and [product-rule traceability](product-rule-traceability.md).

The selected target describes intended backend architecture. Source presence, catalog completeness, or a planned row does not prove that its producer, receiver, persistence, replay, recovery, registration, or runtime behavior is implemented and qualified. This catalog does not authorize deployment, reset, migration, or new GitHub issues.

## Ownership and choreography

Business and authority aggregates own real consistency boundaries. Technical process state, migration state, content and chunk storage, immutable values, transient rules, projections, and compatibility readers are listed separately; a physical C# aggregate class does not automatically add a business owner.

A reactor subscription, a logical prerequisite, and a process stage are distinct. One registered root reactor can start an ordered process whose receivers verify later prerequisite and terminal facts. A list of logical effects must not be implemented as one independent stream subscriber per stage.

The selected target preserves these boundary decisions:

- `ApplicationImportSource` owns exact source-revision admission and durable published replace/remove mapping authority. `ApplicationImportBatch` owns the complete frozen plan and publication; `DeclaredApplication` owns target-local effects. A separate `ApplicationSourceClaim` is excluded.
- `PlatformOperatorRoster` owns the complete current active identities and assignment episodes, acting-operator eligibility, and last-operator protection in one atomic decision. A count or projection is insufficient. A separate `PlatformOperatorGrant` is excluded.
- `AuditPopulation` owns current row classification, reconciliation invalidation, and complete freeze sealing. Row/version bodies are immutable values outside command state. A separate `AuditPopulationRowDecision` is excluded.
- `AccessExpectation` owns predecessor/successor competition within one lineage. Different lines may coexist. No unspecified system-wide exclusion rule or separate `AccessExpectationScopeGuard` is introduced.

## Event and proof rules

- Assertion identity claims are permanent; there is no claim-release writer.
- Package delivery is `AuditPackageDeliveryAdmitted` on its manifest, then `AuditPackageDelivered` on its child. A separate `AuditPackageDeliveryAuthorized` or automated send stage is excluded.
- Application relationships use the selected deterministic directed source/type/target identity; removal never releases or recycles that identity. Separate relationship identity claim/release facts are excluded.
- `WorkDigestPreferenceRecorded` records the authored choice, including accepted no-change. Actual preference `ChangedAt` advances only on a value change. Occupied `WorkDigestPreferenceChanged` v1 remains compatibility input.
- Tenant RBAC bootstrap is one ordered flow from a single `TenantRegistered` admission: define the administrator role and team; optionally register the owner as a member; assign the role to the team; commit `TenantAccess`, `TenantRbacManage`, and `ProgramManage`; and assign the owner to the administrators team only when the owner is the selected first administrator. The member assignment is the tenant-activation trigger and cannot precede all three grants.
- Every continuation binds its original admission, root, actor, target, episode, immutable intent, and required actual stage/terminal proofs. A bare source reference never grants authority. Missing required data is transient; corrupt or mismatched proof is permanent. Required terms outside native acceptance binding are checked before early receipt replay.
- Occupied v1 effect UUID byte algorithms and frozen target selections remain exact compatibility identities. A new algorithm must be explicitly versioned and injectively encoded; it must not silently generate a second effect for an old command.

## Artifact download

The personal identified command is admitted first on `EvidenceArtifact` with original HTTP 202 and an atomic local availability reservation in `EvidenceArtifactDownloadRequested`. Its receiving grant uses an artifact-qualified deterministic identity. An ordered process acquires exact optional advisor and explicit handling-policy uses, verifies temporal authority at the frozen issue decision, freezes and verifies the encrypted original ticket before native issuance, and settles only from actual issued/canceled facts. Cancellation fences delayed issuance before releasing uses.

The scoped read requires the same original personal actor and current exact rights. It returns pending, issued, or canceled status; an issued read returns the original protected ticket and original expiry, with no renewal under the same command. There is no generic/global operation lookup. The receiving-owned issued event is v2; the occupied artifact-owned v1 is retained read-only. The receiving-ID derivation and versioned effect-ID bytes are preserved in the selected [decision records](data/decisions.json), [event catalog](events/README.md), and [reactor rows](reactors/README.md).

## Source history and qualification

The selected catalogs were polished on 2026-10-29. The owner, command, event, and choreography pages preserve those selections in structured form; implementation reviews and delivery evidence remain in [aggregate-status.md](../product/definitive-aggregate-choreography/aggregate-status.md) and [aggregate-backlog.md](../product/definitive-aggregate-choreography/aggregate-backlog.md). The older source README's counts and decision-status summary conflict with the later structured catalogs; those differences are listed in [open gaps](open-gaps.md), not treated as current rules.

A target row is complete only after its actual producer, source proof, receiver, registration, and recovery behavior are verified against the selected catalog. Existing code and frozen packets are implementation evidence; they do not authorize new implementation, integration, or deployment work.
