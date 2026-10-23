# Reactor contracts and choreography flows

Audited source head: `b7a2bd02cc90c6734c94391399a55c0da1621521`. This catalog lists every current registered reactor class, its committed trigger fact(s), the command requests and target aggregates it invokes, and the recovery guards/tests required for safe at-least-once execution. Selected future receiver guards are in [target-reactor-contracts.md](target-reactor-contracts.md); current discovery gaps remain evidence, not implementation acceptance.

## Reactor catalog

Every receiver must be idempotent on a stable business key. Portia reaction execution can replay effects before checkpoint save, so random attempt identity is not a business key. For each reactor test exact replay after an unknown append outcome, transient dependency failure with checkpoint retained, terminal/superseded source handling, and delayed source facts.

### AcceptedStaffDirectoryReconciliationReactor

- **Trigger:** `ServiceEngagementAcceptanceRecorded`
- **Commands:** No typed command call detected; list direct owner/external effects explicitly.
- **Target aggregates:** none mapped
- **Flow:** DirectoryReevaluationDiscovery -> RecordDirectoryIndependenceReevaluation -> client IndependenceLedger owner.
- **Guards:** Not extracted by the source inventory; inspect the linked source and selected target receiver guard.
- **Replay/recovery:** Discovery validates retained source acceptance and scans authoritative directory changes; command failure propagates. Per-tenant registration uses effectively unbounded failed-pass retry with short delay. Pinned runner invokes reaction before checkpoint write; failure/crash before checkpoint permits replay, so owner/effect-key idempotency is required independently of child RequestId.
- **Tests:** No direct test filename reference found; add focused reactor recovery coverage.
- **Open observations:** none in the baseline choreography matrix.

### FirmStaffStatusReevaluationReactor

- **Trigger:** `FirmStaffChangeRecorded`
- **Commands:** No typed command call detected; list direct owner/external effects explicitly.
- **Target aggregates:** none mapped
- **Flow:** DirectoryReevaluationDiscovery -> RecordDirectoryIndependenceReevaluation -> client IndependenceLedger owner for each registered tenant.
- **Guards:** Not extracted by the source inventory; inspect the linked source and selected target receiver guard.
- **Replay/recovery:** Discovery checks locator checkpoint/source coverage, deduplicates immutable locator IDs, validates expected set and rereads source history; lag throws transient to keep checkpoint. Opposite acceptance-side trigger covers later acceptances. Pinned runner invokes reaction before checkpoint write; failure/crash before checkpoint permits replay, so owner/effect-key idempotency is required independently of child RequestId.
- **Tests:** No direct test filename reference found; add focused reactor recovery coverage.
- **Open observations:** none in the baseline choreography matrix.

### MemberRetirementReactor

- **Trigger:** `MemberDeprovisioned`
- **Commands:** `CompleteMemberRetirement` → AccessGrant, Member, TeamMember
- **Target aggregates:** `AccessGrant`, `Member`, `TeamMember`
- **Flow:** Validates the exact native MemberDeprovisioned source and sends CompleteMemberRetirement with a deterministic key derived from the termination event, member and episode. The receiver scans authoritative assignment/grant streams and marks completion only after exact effects.
- **Guards:** Reject wrong tenant/member stream, audit facts or source position. Bind the command to MembershipEpisodeId and TerminationEventId. Receiver derives child keys from source event, effect kind, canonical owner and lifecycle; no elapsed-time release.
- **Replay/recovery:** Exact receiver-owned source proof and deterministic IDs; retain checkpoint on transient dependency or unknown outcome.
- **Tests:** MemberRetirementRecoveryTests: exact source proof, crashes/unknown outcomes before and after receiver append, complete full scan, retry after later episode, late source facts, and >1000 assignments.
- **Open observations:** CHOREO-03.

### RetiredMemberGrantReactor

- **Trigger:** `AccessGrantIssued`
- **Commands:** No typed command call detected; list direct owner/external effects explicitly.
- **Target aggregates:** none mapped
- **Flow:** On member AccessGrantIssued, reads Member at the exact source membership episode and dispatches cleanup only after an immutable retirement proof; nonmember grants are ignored.
- **Guards:** Require nonempty source episode, canonical member principal and exact issuance event/position; receiver binds tenant, grant, principal, episode and termination event. Missing source authority is transient.
- **Replay/recovery:** Exact receiver-owned source proof and deterministic IDs; retain checkpoint on transient dependency or unknown outcome.
- **Tests:** MemberRetirementRecoveryTests: active grant dispatches nothing; exact old grant revoked; new episode grant preserved; source mismatch rejected; unknown outcome recovers.
- **Open observations:** CHOREO-03, CHOREO-06.

### RetiredTeamAssignmentReactor

- **Trigger:** `TeamMemberAssigned`
- **Commands:** No typed command call detected; list direct owner/external effects explicitly.
- **Target aggregates:** none mapped
- **Flow:** On TeamMemberAssigned, reads Member at the assignment episode. Active matching episode means no cleanup; old assignment with accepted termination proof emits CleanupRetiredMembershipRelationship using assignment source position and exact termination event. Missing proof is transient.
- **Guards:** The receiving TeamMember command re-reads the exact assignment source, binds episode and termination event, and cannot delete newer assignment authority. Only dispatch after a retirement proof exists.
- **Replay/recovery:** Exact receiver-owned source proof and deterministic IDs; retain checkpoint on transient dependency or unknown outcome.
- **Tests:** MemberRetirementRecoveryTests: active assignments dispatch nothing; old exact assignment removed; newer episode preserved; unknown outcome retries; wrong source/position/episode rejected.
- **Open observations:** CHOREO-03, CHOREO-06.

### RoleCleanupReactor

- **Trigger:** `RoleDeleted`
- **Commands:** No typed command call detected; list direct owner/external effects explicitly.
- **Target aggregates:** none mapped
- **Flow:** RemoveRolePermission / RemoveTeamRole -> relationship owners discovered from permission/team directories.
- **Guards:** Not extracted by the source inventory; inspect the linked source and selected target receiver guard.
- **Replay/recovery:** Both discovery readers may lag deletion cause and no coverage check; false empty enumeration checkpoints permanently. Partial cleanup replay can hit non-idempotent NotFound removal until projection catches up. Pinned runner invokes reaction before checkpoint write; failure/crash before checkpoint permits replay, so owner/effect-key idempotency is required independently of child RequestId.
- **Tests:** test/Compliance.Tests/Features/AccessControl/RoleCleanupReactorTests.cs
- **Open observations:** CHOREO-06.

### TeamCleanupReactor

- **Trigger:** `TeamDeleted`
- **Commands:** `RemoveTeamMember` → TeamMember, TenantManagerGuard
- **Target aggregates:** `TeamMember`, `TenantManagerGuard`
- **Flow:** RemoveTeamMember -> deterministic team+member assignment owners discovered from team member directory.
- **Guards:** Not extracted by the source inventory; inspect the linked source and selected target receiver guard.
- **Replay/recovery:** No directory checkpoint/source completeness check before enumeration. Empty/lagging page returns success and checkpoints; no later source trigger guarantees missed children revisited. Replayed removals return NotFound if stale directory still lists removed rows. Pinned runner invokes reaction before checkpoint write; failure/crash before checkpoint permits replay, so owner/effect-key idempotency is required independently of child RequestId.
- **Tests:** test/Compliance.Tests/Features/AccessControl/TeamCleanupReactorTests.cs
- **Open observations:** CHOREO-06.

### TenantRbacBootstrapReactor

- **Trigger:** `TenantRegistered`
- **Commands:** `AssignTeamMember` → AccessGrant, TeamMember, TenantManagerGuard; `DefineRole` → Role; `DefineTeam` → Team; `RegisterMember` → AccessGrant, Member, TeamMember
- **Target aggregates:** `AccessGrant`, `Member`, `Role`, `Team`, `TeamMember`, `TenantManagerGuard`
- **Flow:** DefineTeam/RegisterMember/AssignTeamRole/AssignRolePermission/AssignTeamMember -> their distinct RBAC owners.
- **Guards:** Not extracted by the source inventory; inspect the linked source and selected target receiver guard.
- **Replay/recovery:** Awaited whole sequence replay after failure; final admin TeamMemberAssigned fact is separate activation trigger. Independent role catalog/backfill reactors can run concurrently; activation waits required source/projection prerequisites. Pinned runner invokes reaction before checkpoint write; failure/crash before checkpoint permits replay, so owner/effect-key idempotency is required independently of child RequestId.
- **Tests:** test/Compliance.Tests/Features/AccessControl/TenantRbacBootstrapReactorTests.cs; test/Compliance.Tests/Features/Providers/ProviderAuthorizationTests.cs; test/Compliance.Tests/Features/AccessReviews/AccessReviewInfrastructureTests.cs
- **Open observations:** none in the baseline choreography matrix.

### ApplicationImportExecutionReactor

- **Trigger:** `ApplicationImportPlanSealed`
- **Commands:** `ExecuteApplicationImport` → ApplicationImportLedger
- **Target aggregates:** `ApplicationImportLedger`
- **Flow:** ExecuteApplicationImport -> source ApplicationImportLedger coordinator; ApplyApplicationImportEffect -> DeclaredApplication owners.
- **Guards:** Not extracted by the source inventory; inspect the linked source and selected target receiver guard.
- **Replay/recovery:** Durable ApplicationImportPlanSealed triggers replay after partial effect crash. Executor checks tenant active each row, transient lag preserves checkpoint, permanent rejection writes rollback fact that logically releases pending effects. Target acquisition order is plan row order, not canonical target order, so cross-source cycles remain. Pinned runner invokes reaction before checkpoint write; failure/crash before checkpoint permits replay, so owner/effect-key idempotency is required independently of child RequestId.
- **Tests:** test/Compliance.Tests/Features/Applications/ApplicationImportExecutionTests.cs
- **Open observations:** CHOREO-05.

### LinkImpactReviewWorkReactor

- **Trigger:** `SignificantChangeImpactAssessed`
- **Commands:** `LinkImpactReviewWork` → SignificantChange
- **Target aggregates:** `SignificantChange`
- **Flow:** LinkImpactReviewWork -> same SignificantChange owner after source assessment fact.
- **Guards:** Not extracted by the source inventory; inspect the linked source and selected target receiver guard.
- **Replay/recovery:** No-op on no work. If newer content supersedes trigger before processing, receiver returns permanent Conflict instead of terminal superseded no-op; per-tenant queue can stall on obsolete cause. Current owner command otherwise idempotently recognizes already linked set. Pinned runner invokes reaction before checkpoint write; failure/crash before checkpoint permits replay, so owner/effect-key idempotency is required independently of child RequestId.
- **Tests:** No direct test filename reference found; add focused reactor recovery coverage.
- **Open observations:** CHOREO-09.

### SystemDescriptionDecisionReactor

- **Trigger:** `SystemDescriptionApproved, SystemDescriptionNoChangeRecorded`
- **Commands:** `RecordComplianceIncidentSystemDescriptionDecision` → ComplianceIncident; `RecordSignificantChangeSystemDescriptionDecision` → SignificantChange
- **Target aggregates:** `ComplianceIncident`, `SignificantChange`
- **Flow:** RecordSignificantChangeSystemDescriptionDecision / RecordComplianceIncidentSystemDescriptionDecision -> owning change/incident.
- **Guards:** Not extracted by the source inventory; inspect the linked source and selected target receiver guard.
- **Replay/recovery:** Some superseded current assessments are skipped before dispatch; race after reactor read still causes receiver permanent Conflict. Required review-work linking may not yet have caught up: receiver conflict has no transient flag. These paths can stall or fault per-tenant description workload rather than retry as pending/superseded. Pinned runner invokes reaction before checkpoint write; failure/crash before checkpoint permits replay, so owner/effect-key idempotency is required independently of child RequestId.
- **Tests:** No direct test filename reference found; add focused reactor recovery coverage.
- **Open observations:** CHOREO-09.

### ControlEvaluationDeviationFindingReactor

- **Trigger:** `ControlEvaluationSubmitted`
- **Commands:** `RaiseEvaluationDeviationFinding` → no handler resolved; see [command-handler-gaps.md](command-handler-gaps.md).
- **Target aggregates:** none mapped
- **Flow:** RaiseEvaluationDeviationFinding -> FindingAggregate owner through RoutedFinding.
- **Guards:** Not extracted by the source inventory; inspect the linked source and selected target receiver guard.
- **Replay/recovery:** Each material deviation send awaited; repeated submissions use same finding identity. Failure retries existing batch from checkpoint; receiver owns local finding creation rule. Pinned runner invokes reaction before checkpoint write; failure/crash before checkpoint permits replay, so owner/effect-key idempotency is required independently of child RequestId.
- **Tests:** test/Compliance.Tests/Features/Evaluations/EvaluationDeviationFindingTests.cs
- **Open observations:** none in the baseline choreography matrix.

### EvidenceArtifactInspectionEffectsReactor

- **Trigger:** `EvidenceArtifactInspected`
- **Commands:** No typed command call detected; list direct owner/external effects explicitly.
- **Target aggregates:** none mapped
- **Flow:** EvidenceArtifactStorageReconciler -> artifact content-store quarantine/delete/release effects based on current EvidenceArtifact owner state.
- **Guards:** Not extracted by the source inventory; inspect the linked source and selected target receiver guard.
- **Replay/recovery:** Reconciler uses idempotent storage outcomes (quarantine/open fallback, delete NotFound success, release/open fallback). Failed reconciliation throws keeping checkpoint. Direct storage effect is appropriate recovery; it is not a projector deciding policy. Pinned runner invokes reaction before checkpoint write; failure/crash before checkpoint permits replay, so owner/effect-key idempotency is required independently of child RequestId.
- **Tests:** test/Compliance.Tests/Features/Evidence/EvidenceArtifactInspectionEffectsReactorTests.cs
- **Open observations:** none in the baseline choreography matrix.

### ControlOccurrenceFindingReactor

- **Trigger:** `ControlOccurrenceAttested, ControlOccurrenceReviewed`
- **Commands:** `RaiseOccurrenceFinding` → FindingAggregate
- **Target aggregates:** `FindingAggregate`
- **Flow:** RaiseOccurrenceFinding -> FindingAggregate owner through RoutedFinding.
- **Guards:** Not extracted by the source inventory; inspect the linked source and selected target receiver guard.
- **Replay/recovery:** Awaited sends; failure propagates checkpoint; repeated native finding command must compare same routed identity/intent. Receiver reads authoritative occurrence before deriving owner/admission values. Not direct aggregate mutation. Pinned runner invokes reaction before checkpoint write; failure/crash before checkpoint permits replay, so owner/effect-key idempotency is required independently of child RequestId.
- **Tests:** test/Compliance.Tests/Features/Remediation/OccurrenceFindingTests.cs
- **Open observations:** none in the baseline choreography matrix.

### BoundaryRiskReassessmentReactor

- **Trigger:** `BoundaryApproved`
- **Commands:** `RaiseRiskReassessmentTriggers` → RiskGovernanceLedger
- **Target aggregates:** `RiskGovernanceLedger`
- **Flow:** RaiseRiskReassessmentTriggers -> per-program RiskGovernanceLedger owner.
- **Guards:** Not extracted by the source inventory; inspect the linked source and selected target receiver guard.
- **Replay/recovery:** Receiver caught-up discovery and stable trigger IDs make whole cause replay safe. Reads source SystemBoundary to identify owning program; aggregate-to-aggregate live read not inside target owner. Pinned runner invokes reaction before checkpoint write; failure/crash before checkpoint permits replay, so owner/effect-key idempotency is required independently of child RequestId.
- **Tests:** No direct test filename reference found; add focused reactor recovery coverage.
- **Open observations:** none in the baseline choreography matrix.

### RiskMethodReassessmentReactor

- **Trigger:** `RiskMethodVersionPublished`
- **Commands:** `RaiseRiskReassessmentTriggers` → RiskGovernanceLedger
- **Target aggregates:** `RiskGovernanceLedger`
- **Flow:** RaiseRiskReassessmentTriggers -> per-program RiskGovernanceLedger owner.
- **Guards:** Not extracted by the source inventory; inspect the linked source and selected target receiver guard.
- **Replay/recovery:** Receiver validates risk directory caught up, reads each current evaluation, appends stable triggers once. Whole cause replay is safe through trigger identity; no arbitrary technical receipt event added by reactor. Pinned runner invokes reaction before checkpoint write; failure/crash before checkpoint permits replay, so owner/effect-key idempotency is required independently of child RequestId.
- **Tests:** No direct test filename reference found; add focused reactor recovery coverage.
- **Open observations:** none in the baseline choreography matrix.

### TenantInvitationDeliveryReactor

- **Trigger:** `TenantMemberInvited`
- **Commands:** No typed command call detected; list direct owner/external effects explicitly.
- **Target aggregates:** none mapped
- **Flow:** ITenantInvitationDelivery external SMTP; directly record outcome in same TenantInvitation owner.
- **Guards:** Not extracted by the source inventory; inspect the linked source and selected target receiver guard.
- **Replay/recovery:** Skips expired/superseded/accepted/terminal attempts. Sends before durable in-flight claim and records result after send; exception is terminal failed; crash after SMTP accept before outcome can resend. Stable Message-ID is not provider idempotency evidence. Pinned runner invokes reaction before checkpoint write; failure/crash before checkpoint permits replay, so owner/effect-key idempotency is required independently of child RequestId.
- **Tests:** test/Compliance.Tests/Features/Tenants/TenantInvitationDeliveryReactorTests.cs
- **Open observations:** CHOREO-07.

### TenantInvitationReactor

- **Trigger:** `TenantInvitationAccepted`
- **Commands:** `ActivateTenant` → Tenant; `AssignTeamMember` → AccessGrant, TeamMember, TenantManagerGuard; `ProvisionGuestProgramAccess` → AccessGrant; `RegisterMember` → AccessGrant, Member, TeamMember
- **Target aggregates:** `AccessGrant`, `Member`, `TeamMember`, `Tenant`, `TenantManagerGuard`
- **Flow:** RegisterMember; ProvisionGuestProgramAccess per invitation scope; AssignTeamMember to role/admin; ActivateTenant -> distinct owners.
- **Guards:** Not extracted by the source inventory; inspect the linked source and selected target receiver guard.
- **Replay/recovery:** Committed acceptance drives replay of partially completed sequence. TenantActivationPolicy locally retries transient activation then propagates to durable retry. After permanent failure budget it logs and advances checkpoint, leaving partial provisioning for explicit new invitation/operator action. Pinned runner invokes reaction before checkpoint write; failure/crash before checkpoint permits replay, so owner/effect-key idempotency is required independently of child RequestId.
- **Tests:** test/Compliance.Tests/Features/Tenants/TenantInvitationReactorTests.cs; test/Compliance.Tests/E2E/SplitHostTenantE2ETests.cs
- **Open observations:** none in the baseline choreography matrix.

### TenantRegistrationReactor

- **Trigger:** `TenantRegistered, TenantSlugChangeRequested, TenantSlugChanged, TenantSlugSurrenderRequested`
- **Commands:** `InviteTenantMember` → TenantInvitation; `RegisterTenantOwner` → TenantOwner; `RegisterTenantSlug` → TenantSlug; `SurrenderTenantSlug` → TenantSlug
- **Target aggregates:** `TenantInvitation`, `TenantOwner`, `TenantSlug`
- **Flow:** RegisterTenantSlug -> TenantSlug, RegisterTenantOwner -> TenantOwner, optional InviteTenantMember -> TenantInvitation; changes/surrenders -> slug owner.
- **Guards:** Not extracted by the source inventory; inspect the linked source and selected target receiver guard.
- **Replay/recovery:** Owner registration idempotent; slug Register can emit repeated registration/rejection facts. New slug is claimed before old slug surrender. Receivers do not all recognize already-processed outcomes after state progresses. Pinned runner invokes reaction before checkpoint write; failure/crash before checkpoint permits replay, so owner/effect-key idempotency is required independently of child RequestId.
- **Tests:** No direct test filename reference found; add focused reactor recovery coverage.
- **Open observations:** CHOREO-08.

### TenantSelfServiceActivationReactor

- **Trigger:** `TeamMemberAssigned`
- **Commands:** `ActivateTenant` → Tenant
- **Target aggregates:** `Tenant`
- **Flow:** ActivateTenant -> Tenant owner, after exact administrators-team assignment for registered creator.
- **Guards:** Not extracted by the source inventory; inspect the linked source and selected target receiver guard.
- **Replay/recovery:** Per-tenant checkpoint with TenantActivationPolicy; transient projection lag keeps durable checkpoint; permanent budget logs/abandons. Activation itself guards slug confirmation and current admin prerequisite. Pinned runner invokes reaction before checkpoint write; failure/crash before checkpoint permits replay, so owner/effect-key idempotency is required independently of child RequestId.
- **Tests:** test/Compliance.Tests/Features/Tenants/TenantSelfServiceActivationReactorTests.cs
- **Open observations:** none in the baseline choreography matrix.

### TenantSlugReactor

- **Trigger:** `TenantSlugRegistered, TenantSlugRegistrationRejected, TenantSlugSurrenderRejected, TenantSlugSurrendered`
- **Commands:** `ConfirmTenantSlug` → Tenant; `ConfirmTenantSlugSurrender` → Tenant; `RejectTenantSlug` → Tenant; `RejectTenantSlugSurrender` → Tenant
- **Target aggregates:** `Tenant`
- **Flow:** ConfirmTenantSlug/RejectTenantSlug/ConfirmTenantSlugSurrender/RejectTenantSlugSurrender -> Tenant owner.
- **Guards:** Not extracted by the source inventory; inspect the linked source and selected target receiver guard.
- **Replay/recovery:** Failure propagates on global checkpoint. Tenant.RejectSlug lacks no-op path after rejected outcome and ConfirmSlug fails after later surrender/change; exact replay can become permanent conflict. Pinned runner invokes reaction before checkpoint write; failure/crash before checkpoint permits replay, so owner/effect-key idempotency is required independently of child RequestId.
- **Tests:** No direct test filename reference found; add focused reactor recovery coverage.
- **Open observations:** CHOREO-08.

### EmailChallengeDeliveryReactor

- **Trigger:** `EmailChallengeIssued, IdentityRecoveryChallengeIssued`
- **Commands:** No typed command call detected; list direct owner/external effects explicitly.
- **Target aggregates:** none mapped
- **Flow:** IEmailChallengeDelivery external verification/recovery SMTP; same EmailAddress owner direct outcome save.
- **Guards:** Not extracted by the source inventory; inspect the linked source and selected target receiver guard.
- **Replay/recovery:** Skips expired/superseded/terminal owner state. External SMTP occurs before durable attempt claim; exception becomes failed, replay after accepted send+save failure can resend. Matching owner outcome idempotency does not make SMTP send idempotent. Pinned runner invokes reaction before checkpoint write; failure/crash before checkpoint permits replay, so owner/effect-key idempotency is required independently of child RequestId.
- **Tests:** test/Compliance.Tests/Features/UserIdentities/EmailChallengeDeliveryReactorTests.cs
- **Open observations:** CHOREO-07.

### EmailReservationReactor

- **Trigger:** `UserIdentityRegistered`
- **Commands:** `ReserveEmail` → EmailAddress
- **Target aggregates:** `EmailAddress`
- **Flow:** ReserveEmail -> normalized EmailAddress owner.
- **Guards:** Not extracted by the source inventory; inspect the linked source and selected target receiver guard.
- **Replay/recovery:** Conflict is explicitly terminal for one contested email so checkpoint advances; other command failures throw. Owner EmailAddress.Reserve decides uniqueness, so no projection authority. Pinned runner invokes reaction before checkpoint write; failure/crash before checkpoint permits replay, so owner/effect-key idempotency is required independently of child RequestId.
- **Tests:** test/Compliance.Tests/Features/UserIdentities/EmailReservationReactorTests.cs
- **Open observations:** none in the baseline choreography matrix.

### IdentityRecoveryNoticeReactor

- **Trigger:** `IdentityRecoveryChallengeCompleted`
- **Commands:** No typed command call detected; list direct owner/external effects explicitly.
- **Target aggregates:** none mapped
- **Flow:** IEmailChallengeDelivery.SendRecoveryCompletedAsync external SMTP only.
- **Guards:** Not extracted by the source inventory; inspect the linked source and selected target receiver guard.
- **Replay/recovery:** Notice exceptions propagate dedicated checkpoint. Crash after SMTP accept before checkpoint repeats send; no unknown/authorization state. Trigger is completion only, so it cannot recover uncompleted identity replacement. Pinned runner invokes reaction before checkpoint write; failure/crash before checkpoint permits replay, so owner/effect-key idempotency is required independently of child RequestId.
- **Tests:** test/Compliance.Tests/Features/UserIdentities/IdentityRecoveryNoticeReactorTests.cs
- **Open observations:** CHOREO-01, CHOREO-07.
