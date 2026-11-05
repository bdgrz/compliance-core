# Cassie read-model migration

User direction: use `Portia.Cassie` for read-model repositories. Fitz remains the event store. This is a staged conversion of application SQL and repository queries; Portia owns transactions, checkpoints, concurrency, delivery and retries.

The initial source inventory at `ae193e31` found **82** Core classes deriving from `FitzKvProjectionStore`. That count identifies stores to inspect; it does not establish feature coverage or imply that authoritative event readers should become projections.

The first conversion batch owns **9** repositories: six Workforce directories, the methodology publication directory, and two corrective-action/closure Work directories. **73** other inventory stores remain. The first nine passed the assembled backend gate and exact-head CI, then merged in PR #1186 as `baffd1ba`; independent post-merge Backend CI also passed. Superseded stores may retire during approved owner cutovers; this inventory is not a final target store count.

Application schema startup, native Cassie configuration, a pinned development/test service, and an isolated native test-database seam are implemented. Healthy test leases clear their own rows and remove custom tables before reuse. Incomplete or failed leases are discarded without further DDL; the dedicated memory-backed test service reclaims their isolated data and metadata when it stops. The pinned engine's DROP DATABASE flush error is parked in [#1183](https://github.com/bdgrz/compliance-core/issues/1183). Native schema/data/database isolation, repository queries, tenant/pattern/rebuild separation and assembled business paths pass in the complete local backend gate. Hosted final-head qualification still precedes merge. Rebuilds must start against empty Cassie checkpoints so source events populate SQL; an existing Fitz KV checkpoint does not prove Cassie data exists.

## Initial inventory

| Existing source | Conversion state |
| --- | --- |
| [CassiePermissionAuthorizer.cs](../../src/Compliance.Core/Features/AccessControl/Authorizers/CassiePermissionAuthorizer.cs) | Remaining |
| [FitzAccessGrantDirectory.cs](../../src/Compliance.Core/Features/AccessControl/Readers/FitzAccessGrantDirectory.cs) | Remaining |
| [FitzFirmProfessionalDutyDirectory.cs](../../src/Compliance.Core/Features/AccessControl/Readers/FitzFirmProfessionalDutyDirectory.cs) | Remaining |
| [FitzFirmStaffDirectory.cs](../../src/Compliance.Core/Features/AccessControl/Readers/FitzFirmStaffDirectory.cs) | Remaining |
| [CassieRoleDirectoryReader.cs](../../src/Compliance.Core/Features/AccessControl/Readers/CassieRoleDirectoryReader.cs) | Remaining |
| [CassieRolePermissionDirectoryReader.cs](../../src/Compliance.Core/Features/AccessControl/Readers/CassieRolePermissionDirectoryReader.cs) | Remaining |
| [CassieRoleTeamDirectoryReader.cs](../../src/Compliance.Core/Features/AccessControl/Readers/CassieRoleTeamDirectoryReader.cs) | Remaining |
| [CassieTeamDirectoryReader.cs](../../src/Compliance.Core/Features/AccessControl/Readers/CassieTeamDirectoryReader.cs) | Remaining |
| [CassieTeamMemberDirectoryReader.cs](../../src/Compliance.Core/Features/AccessControl/Readers/CassieTeamMemberDirectoryReader.cs) | Remaining |
| [CassieTeamRoleDirectoryReader.cs](../../src/Compliance.Core/Features/AccessControl/Readers/CassieTeamRoleDirectoryReader.cs) | Remaining |
| [FitzActualStaffEngagementLocator.cs](../../src/Compliance.Core/Features/AccessControl/Services/FitzActualStaffEngagementLocator.cs) | Remaining |
| [FitzAccessPopulationDirectory.cs](../../src/Compliance.Core/Features/AccessReviews/Readers/FitzAccessPopulationDirectory.cs) | Remaining |
| [FitzAccessReviewCampaignDirectory.cs](../../src/Compliance.Core/Features/AccessReviews/Readers/FitzAccessReviewCampaignDirectory.cs) | Remaining |
| [FitzAccessReviewCampaignImpactDirectory.cs](../../src/Compliance.Core/Features/AccessReviews/Readers/FitzAccessReviewCampaignImpactDirectory.cs) | Remaining |
| [FitzAdvisorFeedbackWorkItemDirectory.cs](../../src/Compliance.Core/Features/AdvisorCollaboration/Readers/FitzAdvisorFeedbackWorkItemDirectory.cs) | Remaining |
| [FitzAdvisorHandoffDirectory.cs](../../src/Compliance.Core/Features/AdvisorCollaboration/Readers/FitzAdvisorHandoffDirectory.cs) | Remaining |
| [FitzAccessReviewScopeDirectory.cs](../../src/Compliance.Core/Features/Applications/Readers/FitzAccessReviewScopeDirectory.cs) | Remaining |
| [FitzApplicationBoundaryReferenceDirectory.cs](../../src/Compliance.Core/Features/Applications/Readers/FitzApplicationBoundaryReferenceDirectory.cs) | Remaining |
| [FitzApplicationControlDraftReferenceDirectory.cs](../../src/Compliance.Core/Features/Applications/Readers/FitzApplicationControlDraftReferenceDirectory.cs) | Remaining |
| [FitzApplicationControlVersionReferenceDirectory.cs](../../src/Compliance.Core/Features/Applications/Readers/FitzApplicationControlVersionReferenceDirectory.cs) | Remaining |
| [FitzApplicationDirectory.cs](../../src/Compliance.Core/Features/Applications/Readers/FitzApplicationDirectory.cs) | Remaining |
| [FitzApplicationEvidenceSourceReferenceDirectory.cs](../../src/Compliance.Core/Features/Applications/Readers/FitzApplicationEvidenceSourceReferenceDirectory.cs) | Remaining |
| [FitzApplicationImportDirectory.cs](../../src/Compliance.Core/Features/Applications/Readers/FitzApplicationImportDirectory.cs) | Remaining |
| [FitzApplicationIncomingRelationshipDirectory.cs](../../src/Compliance.Core/Features/Applications/Readers/FitzApplicationIncomingRelationshipDirectory.cs) | Remaining |
| [FitzApplicationPolicyReferenceDirectory.cs](../../src/Compliance.Core/Features/Applications/Readers/FitzApplicationPolicyReferenceDirectory.cs) | Remaining |
| [FitzApplicationRelationshipDirectory.cs](../../src/Compliance.Core/Features/Applications/Readers/FitzApplicationRelationshipDirectory.cs) | Remaining |
| [FitzAuditPackageDirectory.cs](../../src/Compliance.Core/Features/AuditPackages/Readers/FitzAuditPackageDirectory.cs) | Remaining |
| [FitzAuditorRequestDirectory.cs](../../src/Compliance.Core/Features/AuditorRequests/Readers/FitzAuditorRequestDirectory.cs) | Remaining |
| [CassieBoundaryDirectory.cs](../../src/Compliance.Core/Features/Boundaries/Readers/CassieBoundaryDirectory.cs) | Remaining |
| [FitzSignificantChangeDirectory.cs](../../src/Compliance.Core/Features/Changes/Readers/FitzSignificantChangeDirectory.cs) | Remaining |
| [FitzCommitmentDraftHistoryDirectoryV1.cs](../../src/Compliance.Core/Features/Commitments/History/FitzCommitmentDraftHistoryDirectoryV1.cs) | Remaining |
| [FitzCommitmentDraftDirectory.cs](../../src/Compliance.Core/Features/Commitments/Readers/FitzCommitmentDraftDirectory.cs) | Remaining |
| [FitzControlMappingDirectoryV1.cs](../../src/Compliance.Core/Features/ControlMappings/Services/FitzControlMappingDirectoryV1.cs) | Remaining |
| [FitzCriterionApplicabilityDirectoryV1.cs](../../src/Compliance.Core/Features/ControlMappings/Services/FitzCriterionApplicabilityDirectoryV1.cs) | Remaining |
| [FitzControlDraftHistoryDirectoryV1.cs](../../src/Compliance.Core/Features/Controls/History/FitzControlDraftHistoryDirectoryV1.cs) | Remaining |
| [FitzControlDraftDirectoryV2.cs](../../src/Compliance.Core/Features/Controls/Services/FitzControlDraftDirectoryV2.cs) | Remaining |
| [FitzCriteriaTextOverlayDirectory.cs](../../src/Compliance.Core/Features/Criteria/Readers/FitzCriteriaTextOverlayDirectory.cs) | Remaining |
| [FitzExaminationEngagementDirectory.cs](../../src/Compliance.Core/Features/Examinations/Readers/FitzExaminationEngagementDirectory.cs) | Remaining |
| [FitzComplianceIncidentDirectory.cs](../../src/Compliance.Core/Features/Incidents/Readers/FitzComplianceIncidentDirectory.cs) | Remaining |
| [FitzMethodologyTemplateDirectory.cs](../../src/Compliance.Core/Features/MethodologyTemplates/Readers/CassieMethodologyTemplateDirectory.cs) | First batch: native validation and exact-head CI passed; merged in PR #1186 |
| [FitzObservationPeriodDirectory.cs](../../src/Compliance.Core/Features/Operations/Readers/FitzObservationPeriodDirectory.cs) | Remaining |
| [FitzPolicyDirectory.cs](../../src/Compliance.Core/Features/Policies/Readers/FitzPolicyDirectory.cs) | Remaining |
| [FitzCampaignDirectory.cs](../../src/Compliance.Core/Features/PolicyDistribution/Readers/FitzCampaignDirectory.cs) | Remaining |
| [FitzTrainingRequirementDirectory.cs](../../src/Compliance.Core/Features/PolicyDistribution/Readers/FitzTrainingRequirementDirectory.cs) | Remaining |
| [CassieClientServiceDirectory.cs](../../src/Compliance.Core/Features/Programs/Readers/CassieClientServiceDirectory.cs) | Remaining |
| [CassieProgramDirectory.cs](../../src/Compliance.Core/Features/Programs/Readers/CassieProgramDirectory.cs) | Remaining |
| [FitzAssuranceDirectory.cs](../../src/Compliance.Core/Features/Providers/Readers/FitzAssuranceDirectory.cs) | Remaining |
| [FitzProviderDirectory.cs](../../src/Compliance.Core/Features/Providers/Readers/FitzProviderDirectory.cs) | Remaining |
| [FitzResponsibilitySetDirectory.cs](../../src/Compliance.Core/Features/Responsibilities/Readers/FitzResponsibilitySetDirectory.cs) | Remaining |
| [FitzMemberResponsibilityIndex.cs](../../src/Compliance.Core/Features/Responsibilities/Services/FitzMemberResponsibilityIndex.cs) | Remaining |
| [RiskDraftProjectionStore.cs](../../src/Compliance.Core/Features/Risks/Projections/RiskDraftProjectionStore.cs) | Remaining |
| [FitzRiskEvaluationDirectory.cs](../../src/Compliance.Core/Features/Risks/Readers/FitzRiskEvaluationDirectory.cs) | Remaining |
| [CassiePopulationSnapshotDirectory.cs](../../src/Compliance.Core/Features/Snapshots/Readers/CassiePopulationSnapshotDirectory.cs) | Remaining |
| [CassieSnapshotDirectory.cs](../../src/Compliance.Core/Features/Snapshots/Readers/CassieSnapshotDirectory.cs) | Remaining |
| [FitzInventoryRegisterDirectory.cs](../../src/Compliance.Core/Features/TechnologyInventory/Readers/FitzInventoryRegisterDirectory.cs) | Remaining |
| [FitzTechnologyInventoryDirectory.cs](../../src/Compliance.Core/Features/TechnologyInventory/Readers/FitzTechnologyInventoryDirectory.cs) | Remaining |
| [FitzTenantDirectoryReader.cs](../../src/Compliance.Core/Features/Tenants/Readers/FitzTenantDirectoryReader.cs) | Remaining |
| [FitzTenantInvitationDirectory.cs](../../src/Compliance.Core/Features/Tenants/Readers/FitzTenantInvitationDirectory.cs) | Remaining |
| [FitzTenantMembershipDirectoryReader.cs](../../src/Compliance.Core/Features/Tenants/Readers/FitzTenantMembershipDirectoryReader.cs) | Remaining |
| [FitzEmailAddressDirectory.cs](../../src/Compliance.Core/Features/UserIdentities/Readers/FitzEmailAddressDirectory.cs) | Remaining |
| [FitzPlatformUserDirectory.cs](../../src/Compliance.Core/Features/UserIdentities/Readers/FitzPlatformUserDirectory.cs) | Remaining |
| [FitzUserIdentityDirectory.cs](../../src/Compliance.Core/Features/UserIdentities/Readers/FitzUserIdentityDirectory.cs) | Remaining |
| [FitzAccessReviewCampaignWorkItemDirectory.cs](../../src/Compliance.Core/Features/Work/Readers/FitzAccessReviewCampaignWorkItemDirectory.cs) | Remaining |
| [FitzBoundaryDecisionWorkItemDirectory.cs](../../src/Compliance.Core/Features/Work/Readers/FitzBoundaryDecisionWorkItemDirectory.cs) | Remaining |
| [FitzCommitmentDecisionWorkItemDirectory.cs](../../src/Compliance.Core/Features/Work/Readers/FitzCommitmentDecisionWorkItemDirectory.cs) | Remaining |
| [FitzControlDecisionWorkItemDirectory.cs](../../src/Compliance.Core/Features/Work/Readers/FitzControlDecisionWorkItemDirectory.cs) | Remaining |
| [FitzControlEvaluationWorkItemDirectory.cs](../../src/Compliance.Core/Features/Work/Readers/FitzControlEvaluationWorkItemDirectory.cs) | Remaining |
| [FitzControlMappingWorkItemDirectory.cs](../../src/Compliance.Core/Features/Work/Readers/FitzControlMappingWorkItemDirectory.cs) | Remaining |
| [FitzControlOccurrenceWorkItemDirectory.cs](../../src/Compliance.Core/Features/Work/Readers/FitzControlOccurrenceWorkItemDirectory.cs) | Remaining |
| [FitzControlOperatingPlanWorkItemDirectory.cs](../../src/Compliance.Core/Features/Work/Readers/FitzControlOperatingPlanWorkItemDirectory.cs) | Remaining |
| [FitzCorrectiveActionWorkItemDirectory.cs](../../src/Compliance.Core/Features/Work/Readers/CassieCorrectiveActionWorkItemDirectory.cs) | First batch: native validation and exact-head CI passed; merged in PR #1186 |
| [FitzCriterionApplicabilityWorkItemDirectory.cs](../../src/Compliance.Core/Features/Work/Readers/FitzCriterionApplicabilityWorkItemDirectory.cs) | Remaining |
| [FitzEvidenceWorkItemDirectory.cs](../../src/Compliance.Core/Features/Work/Readers/FitzEvidenceWorkItemDirectory.cs) | Remaining |
| [FitzFindingClosureWorkItemDirectory.cs](../../src/Compliance.Core/Features/Work/Readers/CassieFindingClosureWorkItemDirectory.cs) | First batch: native validation and exact-head CI passed; merged in PR #1186 |
| [FitzPolicyCampaignWorkItemDirectory.cs](../../src/Compliance.Core/Features/Work/Readers/FitzPolicyCampaignWorkItemDirectory.cs) | Remaining |
| [FitzPolicyDecisionWorkItemDirectory.cs](../../src/Compliance.Core/Features/Work/Readers/FitzPolicyDecisionWorkItemDirectory.cs) | Remaining |
| [FitzPersonDirectory.cs](../../src/Compliance.Core/Features/Workforce/Readers/CassiePersonDirectory.cs) | First batch: native validation and exact-head CI passed; merged in PR #1186 |
| [FitzServiceIdentityDirectory.cs](../../src/Compliance.Core/Features/Workforce/Readers/CassieServiceIdentityDirectory.cs) | First batch: native validation and exact-head CI passed; merged in PR #1186 |
| [FitzWorkRelationshipDirectory.cs](../../src/Compliance.Core/Features/Workforce/Readers/CassieWorkRelationshipDirectory.cs) | First batch: native validation and exact-head CI passed; merged in PR #1186 |
| [FitzWorkforceObservationDirectory.cs](../../src/Compliance.Core/Features/Workforce/Readers/CassieWorkforceObservationDirectory.cs) | First batch: native validation and exact-head CI passed; merged in PR #1186 |
| [FitzWorkforceObservationResolutionDirectory.cs](../../src/Compliance.Core/Features/Workforce/Readers/CassieWorkforceObservationResolutionDirectory.cs) | First batch: native validation and exact-head CI passed; merged in PR #1186 |
| [FitzWorkforceSourceDirectory.cs](../../src/Compliance.Core/Features/Workforce/Readers/CassieWorkforceSourceDirectory.cs) | First batch: native validation and exact-head CI passed; merged in PR #1186 |

The Finding and Waiver readers currently reconstruct authoritative aggregate/native facts. They are separate from these KV stores and remain authoritative composition. A persisted Finding list/query repository is not implemented by merely migrating the two Work directories.

The first assembled native selection ran 70 cases: nine non-database cases passed and 61 cases failed during shared schema setup, before most repository assertions. The published engine rejects compound primary keys, so the Methodology and Work schemas were adapted to structurally encoded single primary keys and single-column uniqueness. A direct native-client probe confirmed single-column unique indexes, `ON CONFLICT` updates and duplicate-key refusal. Parallel catalog creation also conflicted on the global object-id counter. The native test fixture now serializes metadata setup/reset and the schema tests share that gate. Ordinary xUnit collection parallelism is restored for qualification; test data remains isolated by exclusive database leases. Concurrent production schema startup is an explicit unresolved gate in [#1184](https://github.com/bdgrz/compliance-core/issues/1184).

Subsequent native checkpoints: 70 cases ran with 45 passing and 25 failing (paging LIMIT syntax and a scoped-service test fixture); after those fixes, 72 ran with 60 passing and 12 failing (VALUES casts and duplicate physical column selection). The next 72-case checkpoint passed 69 and failed three: two source-observation cursor cases still selected the same physical column twice, and corrective-action refresh hit a native delete/reinsert primary-key conflict at commit. Narrow SQL fixes are prepared; the latest strict Release build passed with zero warnings and errors. This was an intermediate failure checkpoint; the later acceptance and PR #1186 delivery below supersede that pending status.

Test database schema reuse reduced the selected run from 5:02 for 70 cases to 2:53 for 72. Each active lease still owns a separate native database; returned leases clear all application rows and checkpoint rows. Missing standard tables or failed cleanup discard the lease. Shared application DDL is cached, while custom test tables are removed. These fixture changes do not implement or replace Portia projection execution.

Portia.Cassie 0.10.0 has no shared SQL paging API. The published Fitz path uses KvDirectory; native Cassie repositories currently own their SQL keyset queries and scope-bound cursors. This integration gap is not evidence that the existing feature-specific cursor implementations are fully qualified.


## Resumed assembled qualification

The final local strict Release build has zero warnings/errors. Whole-solution
whitespace verification, six Node tooling tests and both proposal/runtime fixture
validators pass. The complete backend unit gate passed **8,673/8,673** in 14m22s
against a fresh pinned native service with ordinary xUnit collection parallelism.
Compiler inputs remained unchanged throughout the run. Broker/Web integration,
final-head hosted CI and merge were separate gates; PR #1186 subsequently passed and merged, as recorded below.

Five production-composition fixtures now use isolated native leases and registered
Portia projectors, including fresh databases for replay. A deliberately incomplete
lease is discarded without further DDL (#1183). The mixed-source queue test now
uses historical decision setup and freezes its query clock after seeding; otherwise
an assignment becoming active correctly changes its fallback work identity during
a slow run. Production queue selection remains unchanged.

The Person migration now restores the previous replacement identity check. The
new native test first failed at its intended refusal assertion, then passed in the
assembled run, proving a mismatched prior PersonId cannot be overwritten and its
payload, revision and checkpoint remain unchanged. Independent source review found
no other confirmed defect on supported writer paths in the first nine repositories.
General poisoned-row/body binding remains a separate coverage item for future
migration slices, rather than an assumed guarantee of the old Fitz reads.

Live Jev fixture review adverse scores were 0.09–0.21; the Person guard/source
regression review returned 0.89–0.96 on its principal positive judgments. A broad
old-clock-cause score of 0.56 was inconclusive. These are advisory signals, not
acceptance; exact source timing and executable RED/GREEN supplied the evidence.


### Authorization refinement before PR1186 delivery

The first local8,673 pass did not establish current authorization on every path.
Exact-head review found linked-owner review could bypass committed Member changes,
and accepted corrective-action retries skipped preparation-only change authority.
The new27-case RED selection passed14 and failed13 at intended refusal assertions.
Repairs passed58 relevant cases, then the complete **8,700/8,700** backend gate in
14m15s with unchanged compiler inputs. Strict build and whitespace checks pass.

Owner-fallback service reviews now source-check current canonical membership,
UTC guest expiry and team episode. Their privileged grant branch retains the
production permission authorizer's authoritative membership checks. Corrective
changes source-check the actor and current owner/permission before native replay;
new writes retain their preparation checks and authorized lead retries retain
frozen-response independence from current Finding content. Live Jev review was
advisory; narrower no-append judgments were0.98 after a broad0.54 question proved
ambiguous, and actual version/fact assertions passed.

The initial hosted run failed before test discovery on readiness connection reset,
after a successful build. CI now retries all errors from the idempotent readiness
GET. Exact final-head CI/merge status is tracked in
[PR1186](https://github.com/bdgrz/compliance-core/pull/1186).

### PR #1186 merged delivery

Final reviewed head `adfe165a` passed Backend CI with 8,700 units and merged as
`baffd1ba`. A tree comparison confirmed the merged source matches that reviewed
head. The first nine repositories are delivered; concurrent multi-host schema
startup (#1184), native teardown behavior (#1183), remaining store cutovers and
whole-context coverage remain explicit qualifications. Post-merge Backend CI also
passed on `baffd1ba`.
