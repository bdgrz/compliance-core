# Aggregate boundary notes

The selected aggregate-detail summary contains the following explicit overrides. These are kept verbatim as boundary rationale alongside the per-owner records.

- **ApplicationSourceClaim → ApplicationImportSource**: Source-wide exact revision/generation and published-batch ordering already own mutable replace/remove mappings.
- **PlatformOperatorGrant → PlatformOperatorRoster**: Keep exact active operator identities/assignment episodes and acting-operator/last-operator checks in one atomic roster decision.
- **AuditPopulationRowDecision → AuditPopulation**: Classification, reconciliation invalidation, complete-source proof and freeze seal are one conflict set on AuditPopulation.
- **AccessExpectationScopeGuard → AccessExpectation**: Required exclusion is predecessor/successor competition within the same expectation lineage.
- **ApplicationNameClaim (added 2026-10-29)**: tenant-wide application name/alias uniqueness and merge release, the same pattern as the location, provider and operational-process name claims.
- **WorkDigestPreference → technical state (2026-10-29)**: settings holder; emission retained.

## Technical responsibilities

14 declared technical/storage labels remain separate:

- **AuditPackageBuildProcess**: Technical build attempts, validation and archive evidence.
- **AuditPopulationExportProcess**: Immutable approved population manifest, format, exact source proofs, output content ref and attempt state; retention is proof-only input.
- **EmailChallengeDeliveryLedger**: [Placeholder: identity and delivery rules are not recorded.]
- **FindingMigrationAggregate**: Technical process invariant: migration has one exact immutable source fence, advances one contiguous physical source offset after target import, and completes only with exact parity count/digest at the fence.
- **IdentityRecoveryNoticeProcess**: Independent completion notice payload hash/recipient and delivery attempts; does not control completed recovery.
- **PopulationSnapshotChunk**: Technical invariant only: one snapshot/index stores one immutable kind+row digest, changed bytes conflict. No independent business decision or lifecycle.
- **ProgramScopeSnapshotProcess**: Complete program/boundary source proofs and immutable snapshot/manifest content; regeneration reproduces that exact content and changes no domain decision.
- **RemediationMigrationFenceAggregate**: Technical fence invariant: exactly one migration can seal the exact legacy remediation head and subsequent legacy writers must observe that fence.
- **SystemDescriptionExportProcess**: Technical export generation and binding attempt state.
- **TenantInvitationDeliveryProcess**: Technical delivery process with one immutable effect payload and multiple attempt records. Attempt identity is state, never a new unconstrained process owner.
- **WeeklyWorkDigest**: Technical delivery process invariant: exactly one schedule per member-week; only next deterministic attempt can run and callback must match its exact request; terminal outcomes/suppression stop further delivery.
- **WorkDigestDispatch**: Technical dispatch invariant: one reservation/message per member-week, one in-flight send, retries only after definite transient rejection, and ambiguous external outcome freezes until authorized evidence-backed retry.
- **WorkforceRosterSnapshotProcess**: Complete authoritative workforce source cut and immutable roster content; amendments create a new snapshot identity, never rewrite an accepted snapshot.
- **WorkDigestPreference**: One member stores current notification consent and valid scheduling time zone; identical current setting is no-op. This is chiefly a settings state holder, with no multi-part business invariant identified.

**WeeklyScheduling** is the separate logical capture/page-result journal. It adds no business owner.
