# Aggregate boundary and reactor audit — 2026-10-23

Current head: `b7a2bd02cc90c6734c94391399a55c0da1621521`. Scope: all 103 aggregate owners and all 24 reactor classes present on `develop`, with the 81 observations from the immutable 2026-10-22 audit reconciled to current delivery status.

## Deliverables

The command, handler, guard and test specification is in [aggregate-boundary-specification.md](aggregate-boundary-specification.md), with shared [handler execution and recovery rules](handler-execution-contract.md); missing handler contracts and direct writer cleanup are in [command-handler-gaps.md](command-handler-gaps.md); target owners for all 29 split roots are in [aggregate-split-targets.md](aggregate-split-targets.md); all 24 current reactor contracts are in [reactor-contract-catalog.md](reactor-contract-catalog.md); and the 20 required Mermaid flows are in [choreography-flows.md](choreography-flows.md). [README.md](README.md) indexes the machine-readable catalogs and Jev evidence.

## Method and evidence

- The full 103-owner source review is pinned at [`audit.json`](../2026-10-22/audit.json) from `4c255f56bfec727ac1f6e841c09935a4ce69ec4c`. That commit is an ancestor of the current head. Its method reviewed aggregate source, selected facts, handlers, tests and composition; it distinguishes atomic boundaries from implementation and migration issues. This report preserves that baseline and updates all source paths, counts, merged slices and changed access/retirement owners.
- Current source map: [`aggregate-command-coverage.json`](../../aggregate-command-coverage.json), with 103 roots and 418 event contracts. Every source path in the 103-root audit exists at current head. A current source scan has 24 reactor classes and 24 explicit registrations.
- Jev used `jev-1.14.0` via TypeSafe System One. The four root batches assessed each root’s prior source-reviewed invariant, identity, retained state, contention, event shape and cross-owner decision. Current aggregate code for the seven changed access owners was separately sent as source text. All 24 current reactor class implementations were sent as source text. Raw prompts and responses are retained beside this report.
- Jev is a review signal, not proof. Its Noul scores rank possible boundary or choreography problems. Every actionable finding below remains grounded in the pinned source review or current code and recorded regression references. No test suite or CI run was part of this read-only audit.

## Current result

- The root inventory classifies 34 owners as retain, 29 as split, 28 as narrow, 9 as process, 2 as legacy and 1 as project/read-model. The 29 split dispositions and 28 narrow dispositions are migration decisions and do not mean the replacement has shipped.
- The tracking ledger contains 81 original observations: 2 complete, 3 in progress and 76 pending. That leaves 79 open (6 P1 and 73 P2). Completed: CORE-01 persistence preflight and ACCESS-17 profile audit separation. In progress: ACCESS-08, CHOREO-03 and CHOREO-06; the merged PRs have only closed their bounded slices, not the shared observations.
- The prior choreography matrix listed 33 reactors; 12 legacy/backfill reactor classes have since been removed and 3 membership-retirement reactors added. Current count is 24. Do not use the stale 33-row count as current coverage.
- [`aggregate-boundary-ledger.md`](../../aggregate-boundary-ledger.md), [`aggregate-command-coverage-audit.md`](../../aggregate-command-coverage-audit.md) and [`domain-event-aggregate-gap-analysis.md`](../../domain-event-aggregate-gap-analysis.md) still report 102 roots (and the latter two retain the earlier 409-contract count). Reconcile these baseline summaries in a follow-up documentation change; this report uses the current 103-root/418-contract inventory.

## Jev screening

- Full-root screening: 69/103 Noul probabilities were at least 0.50; 42/103 were at least 0.80. This broad screen is intentionally sensitive and is not a count of confirmed defects. Highest signals align with already identified cross-owner/replay growth concerns.
- Current-code review of the seven changed access owners returned 0.16–0.37, indicating Jev did not find a reason to split those specific roots. This does not close ACCESS-01/08/10: the remaining problems are command identity, lifecycle fencing and recovery, not evidence that Member/RolePermission/TeamMember need to be fused or split.
- Current-source reactor review signaled 15/24 at least 0.50, none above 0.68. Six moderate reactor signals had no standalone choreography finding; source review confirmed their existing owner/proof behavior or linked them to an already tracked owner observation. Jev added no unlinked aggregate finding. These are weak ranking signals, not a defect count.

### Highest aggregate risk signals

- **DeclaredApplication (0.93)** — split; ROOT-01, ROOT-02, ROOT-04, ROOT-05. An application has immutable creation intent, versioned metadata and retirement eligibility; retirement requires an approved successor for the exact source revision. Import reservation must prevent conflicting local changes until durable settlement.
- **ReadinessLedger (0.93)** — split; OPS-08, OPS-11, OPS-16. Management decides only latest immutable assessment, proceed requires every gap planned; Type I entry can be approved once per program, on latest current-rule assessment after proceed with every exception acknowledged/planned.
- **RiskGovernanceLedger (0.93)** — split; OPS-04, OPS-11, OPS-15, OPS-16. Within each risk/control tuple, one pending treatment assertion and one accepted version per control; independent review controls acceptance. Within one treatment action, only unfinished work can edit/cancel and completion needs current fulfilled evidence plus independent review.
- **FirmProfessionalDutyCatalog (0.91)** — split; ACCESS-04. One active signed designation per user+duty+tenant; refresh/revoke must supersede exact assertion with increasing authority revision and stable subject.
- **FirmStaffDirectory (0.91)** — split; ACCESS-03. Canonical user maps to exactly one immutable staff identity/practice; each staff status changes at its revision.
- **AccessReviewSystemLedger (0.91)** — split; OPS-07, OPS-11, OPS-16. Each expectation can be independently approved by someone other than its proposer unless exactly waived; an approved successor changes the effective expectation line. Exceptions require an approved expectation and future expiry.
- **TypeIIPeriod (0.91)** — narrow; OPS-09, OPS-10, OPS-12, OPS-17, CORE-01. One period binds exact approved opening plan/population; close must reconcile the approved expected population, independently acknowledge all unresolved material items, and freeze the same approved sources. Close amendments preserve original expected population/acknowledgements and chain exact changed sources.
- **PolicyDistributionCampaign (0.91)** — split; OPS-07, OPS-16. One campaign freezes exact material and launch audience; results must name that version/hash/text and a currently included person, and closure freezes totals and rejects later results.
- **IndependenceLedger (0.90)** — split; ACCESS-02. Permanent tenant+person practice exclusivity and acceptance only against exact client service history, reviewed draft, personal acknowledgement and ratified-rule proofs.
- **AdvisorHandoff (0.90)** — split; ROOTASSURE-01, ROOTASSURE-04. Feedback admission must occur while this handoff is ready and unrevoked/closed, against an exactly selected source version. Feedback corrections/responses preserve original contribution; selection change makes feedback historical.
- **ProviderAssuranceRegister (0.90)** — split; ROOTTECH-03, ROOTTECH-04. Each report preserves provider identity/revision; personal review binds exact same-provider evidence kind/current report revision and valid period/currency/cadence; each gap closes once only on newer/different source, and accepted-risk adoption cannot close gap or occur after closure.
- **FindingAggregate (0.90)** — split; OPS-06, OPS-11, OPS-12. A finding closes only when at least one corrective action exists, every action is completed, evidence and rationales exist, and the closer is independent of finding/action owners and completers unless exactly waived; closed finding is immutable until reopened.
- **AccessPopulation (0.89)** — split; OPS-07, OPS-15. An accepted population is an immutable, internally valid snapshot; each accepted principal classification has a contiguous decision count and valid identity shape.
- **AccessReviewCampaign (0.89)** — split; OPS-07, OPS-11, OPS-16. Completion is admitted only after every frozen review item is decided and each required remediation is verified or covered by a live exception; reviewer and remediation owner stay distinct per item.
- **AuditPackage (0.89)** — split; ASSURE-02, ASSURE-10, CORE-01. A delivery must use exact selected manifest and successful archive, passed/current validation or independent complete overrides, and approval for recipient; delivered version content cannot change.
- **AuditorRequest (0.89)** — split; ASSURE-03, ASSURE-09, ASSURE-10, ASSURE-11. A response must be independently reviewed if required before exact delivery; request resolution requires at least one response and accepted external disposition for every admitted response.
- **ProviderRegister (0.89)** — split; ROOTTECH-01. A governed provider name is unique within tenant; each stable provider identity has coherent attributed content and immutable logical request intent with revision concurrency.
- **OperationalProcessRegister (0.89)** — split; ROOTTECH-01, ROOTTECH-02. Within tenant, active normalized process names unique and each governed process content/lifecycle changes at its exact revision.

Jev results for changed access roots are from the current source prompt and supersede the earlier inventory-only signal: AccessGrant 0.21, Member 0.29, RolePermission 0.19, TeamMember 0.26, TeamRole 0.16, PlatformOperatorRoster 0.25, UserIdentity 0.37.

## Open boundary and choreography problems

These remain the actionable design risks from the full source review. “Split” means the invariant needs a distinct owner while preserving the named coordinator/guard; “narrow” means preserve the current atomic owner but remove unrelated lifetime/process state.
- **ACCESS-01 (P1, pending) — Restoration can release a tenant-manager withdrawal before its delayed source mutation commits** Owners: TenantManagerGuard, Member, TeamMember.
- **ACCESS-02 (P1, pending) — Tenant-wide independence history has invented lifetime caps that can forbid revocation** Owners: IndependenceLedger.
- **ACCESS-08 (P1, in_progress) — Old successful RBAC requests can resurrect removed authority or mutate a new membership episode** Owners: RolePermission, TeamRole, TeamMember, Member, PlatformOperatorRoster.
- **ACCESS-12 (P1, pending) — Identity-recovery cross-stream sequence can be orphaned and its durable claim overwritten** Owners: EmailAddress, UserIdentity.
- **ROOT-01 (P1, pending) — Application business state is reconstructed from live source ledger settlement** Owners: DeclaredApplication.
- **ROOT-08 (P1, pending) — Incident resolution treats caller supplied work ids as completed-work proof** Owners: ComplianceIncident.
- **ACCESS-03 (P2, pending) — Firm staff catalog combines unrelated staff lifecycles and permanently stops at1000 identities** Owners: FirmStaffDirectory.
- **ACCESS-04 (P2, pending) — Global professional-duty catalog serializes unrelated signed designations and retains every assertion/decision** Owners: FirmProfessionalDutyCatalog.
- **ACCESS-05 (P2, pending) — Independence rule sequencing retains a full history and invents a100-version lifetime limit** Owners: IndependenceRuleCatalog.
- **ACCESS-06 (P2, pending) — Rule-ratification ordering is valid but lifetime history caps prevent future governance** Owners: IndependenceRuleRatificationCatalog.
- **ACCESS-07 (P2, pending) — OIDC connection has a lifetime ceiling on tests and credential rotation** Owners: TenantIdentityProvider.
- **ACCESS-09 (P2, pending) — Team and role creation silently accept changed content on the same identity** Owners: Team, Role.
- **ACCESS-10 (P2, pending) — Several owner methods reject exact successful retries or lose original intent across later revisions** Owners: AccessGrant, Tenant, Person, ServiceIdentity, WorkRelationship, TenantIdentityProvider, Member.
- **ACCESS-11 (P2, pending) — Mailbox-keyed invitation conflates all future invitation lifecycles and reissues exact retries** Owners: TenantInvitation.
- **ACCESS-13 (P2, pending) — Email and invitation roots persist transport delivery receipts as domain facts** Owners: EmailAddress, TenantInvitation.
- **ACCESS-14 (P2, pending) — Slug owner appends duplicate claims and pure rejection response receipts** Owners: TenantSlug.
- **ACCESS-15 (P2, pending) — TenantOwner is an idempotent duplicate of tenant registration with no independent rule** Owners: TenantOwner.
- **ACCESS-16 (P2, pending) — Tenant root accumulates previous slug history solely to swallow late saga acknowledgements** Owners: Tenant.
- **ASSURE-01 (P2, pending) — Program evidence-request ledger is a parent roster of independent request lifecycles** Owners: EvidenceRequestLedger.
- **ASSURE-02 (P2, pending) — AuditPackage accumulates independent versions and fulfillment lifecycles behind caps** Owners: AuditPackage.
- **ASSURE-03 (P2, pending) — AuditorRequest owns comment, response and sample histories that need independent boundaries** Owners: AuditorRequest.
- **ASSURE-04 (P2, pending) — AuditPopulation bundles immutable versions and exports into the live reconciliation owner** Owners: AuditPopulation.
- **ASSURE-05 (P2, pending) — Handling classification is permanently blocked after32 historical decisions** Owners: EvidenceHandlingPolicy.
- **ASSURE-06 (P2, pending) — Redaction history capacity can prevent approval of a valid current preparation** Owners: EvidenceRedaction.
- **ASSURE-07 (P2, pending) — EvidenceRedaction receives and reads a live waiver aggregate** Owners: EvidenceRedaction.
- **ASSURE-08 (P2, pending) — Every artifact download extends the artifact stream and identical request can append again** Owners: EvidenceArtifact.
- **ASSURE-09 (P2, pending) — Auditor comment creation has no stable identity and repeats on exact request retry** Owners: AuditorRequest.
- **ASSURE-10 (P2, pending) — Server-generated timestamps are part of immutable retry intent** Owners: ArtifactDisclosure, ArtifactEngagementHold, EvidenceHandlingPolicy, AuditPackage, AuditPopulation, AuditorRequest.
- **ASSURE-11 (P2, pending) — Several mutations reject an already accepted exact retry before recognizing its command** Owners: ClientService, ComplianceProgram, EvidenceRequestLedger, ExaminationEngagement, AuditPopulation, AuditorRequest, ManagementAssertion, TypeIIPlan, ProgramRollover.
- **ASSURE-12 (P2, pending) — Retention owner accumulates independent legal-hold histories and technical binding facts** Owners: ArtifactRetention.
- **ASSURE-13 (P2, pending) — Eight-link amendment limits invent lifetime business limits despite immutable snapshot identities** Owners: ExaminationEngagement, ImmutableSnapshot, PopulationSnapshot.
- **ASSURE-14 (P2, pending) — Population storage chunk is modeled as a domain aggregate and event** Owners: PopulationSnapshotChunk.
- **ASSURE-15 (P2, pending) — Domain decisions carry bounded technical execution and successor receipts** Owners: ArtifactDisposition, ClientExport, ProgramRollover.
- **ASSURE-16 (P2, pending) — Program retains criteria-selection history and shares revision with unrelated plan edits** Owners: ComplianceProgram.
- **ASSURE-17 (P2, pending) — Approval workflows retain complete immutable version histories and impose version caps** Owners: ManagementAssertion, TypeIIPlan, ProgramRollover.
- **CHOREO-03 (P2, in_progress) — Guard withdrawal and membership retirement obligations lack recovery fact consumers** Owners: TenantManagerGuard.
- **CHOREO-04 (P2, pending) — Type II date reservation choreography has holds but no recovery reactor** Owners: TypeIIObservationCalendar, TypeIIPeriod.
- **CHOREO-05 (P2, pending) — Different source imports acquire target holds in opposite order and can deadlock** Owners: ApplicationImportLedger, DeclaredApplication.
- **CHOREO-06 (P2, in_progress) — Relationship cleanup checkpoints discovery without proving directories cover source facts** Owners: see baseline.
- **CHOREO-07 (P2, pending) — Email delivery has at-least-once effects and conflates uncertain transport failure with rejection** Owners: see baseline.
- **CHOREO-08 (P2, pending) — Tenant slug outcome receiver is not replay-safe and can block the global checkpoint** Owners: Tenant.
- **CHOREO-09 (P2, pending) — Obsolete or dependency-blocked impact reactions return permanent conflicts** Owners: see baseline.
- **CHOREO-10 (P2, pending) — Generated due-occurrence fact has no downstream materialization consumer** Owners: TypeIIPeriod.
- **OPS-01 (P2, pending) — Program mapping and criterion ledgers serialize independent tuple lifecycles** Owners: ControlCriterionMappingLedger, CriterionApplicabilityLedger.
- **OPS-02 (P2, pending) — ControlEvaluationLedger retains every independent evaluation and retest in a program stream** Owners: ControlEvaluationLedger.
- **OPS-03 (P2, pending) — Control operations combines program plan roster and unbounded occurrence lifecycles** Owners: ControlOperationsLedger.
- **OPS-04 (P2, pending) — RiskGovernanceLedger pools unrelated risks and child action lifecycles** Owners: RiskGovernanceLedger.
- **OPS-05 (P2, pending) — Catalog-shaped owners retain unrelated training and overlay version lines** Owners: TrainingCatalog, CriteriaTextOverlayLedger.
- **OPS-06 (P2, pending) — Finding still hides corrective-action ownership behind a fifty-action lifetime cap** Owners: FindingAggregate.
- **OPS-07 (P2, pending) — Access and policy campaign parents retain independent decisions, people and histories** Owners: AccessPopulation, AccessReviewCampaign, AccessReviewSystemLedger, PolicyDistributionCampaign.
- **OPS-08 (P2, pending) — Readiness comments, gap plans and assessment history share management entry revision** Owners: ReadinessLedger.
- **OPS-09 (P2, pending) — Observation lifecycle capacity is limited by retained historical count** Owners: TypeIIObservationCalendar, TypeIIPeriod.
- **OPS-10 (P2, pending) — Type II calendar holds have no automatic recovery for interrupted multi-stream sequences** Owners: TypeIIObservationCalendar, TypeIIPeriod.
- **OPS-11 (P2, pending) — Owner methods execute live SeparationOfDutiesWaiver aggregate behavior** Owners: AccessReviewCampaign, AccessReviewSystemLedger, ControlCriterionMappingLedger, ControlDraft, ControlEvaluationLedger, ControlOperationsLedger, CriterionApplicabilityLedger, FindingAggregate, Policy, ReadinessLedger, RiskEvaluation, RiskGovernanceLedger.
- **OPS-12 (P2, pending) — Domain event streams carry migration and transport process records** Owners: FindingAggregate, FindingMigrationAggregate, RemediationMigrationFenceAggregate, TypeIIPeriod, WeeklyWorkDigest, WorkDigestDispatch, WorkItemAssignment.
- **OPS-13 (P2, pending) — Several single-lifecycle owners retain full read histories as command state** Owners: ControlDraft, ControlEvaluationPlan, Policy, RiskEvaluation, RiskMethod, WorkItemAssignment.
- **OPS-15 (P2, pending) — Exact successful command retries conflict, duplicate facts, or overwrite newer settings** Owners: AccessPopulation, ControlCriterionMappingLedger, ControlDraft, ControlEvaluationLedger, ControlOperationsLedger, CriterionApplicabilityLedger, Policy, RiskDraft, RiskGovernanceLedger, RiskMethod, TrainingCatalog, WorkDigestPreference, WorkItemAssignment.
- **OPS-16 (P2, pending) — Reused decision keys acknowledge changed intent without comparing the accepted fact** Owners: AccessReviewCampaign, AccessReviewSystemLedger, ControlEvaluationLedger, ControlOperationsLedger, PolicyDistributionCampaign, ReadinessLedger, RiskEvaluation, RiskGovernanceLedger, TypeIIObservationCalendar.
- **OPS-17 (P2, pending) — Type II state-snapshot events grow with all prior close snapshots and expected rows** Owners: TypeIIPeriod.
- **ROOT-02 (P2, pending) — Import source stream combines lifetime claims, execution process and acceptance authority** Owners: ApplicationImportLedger, DeclaredApplication, ImportBatch.
- **ROOT-03 (P2, pending) — Scope retains complete history, calls live owners and ignores changed retry intent** Owners: SystemInstanceAccessReviewScope.
- **ROOT-04 (P2, pending) — Application accumulates relationship identities and lifecycle histories** Owners: DeclaredApplication.
- **ROOT-05 (P2, pending) — Instance/application revision and legacy cancel paths lack exact logical retry handling** Owners: DeclaredSystemInstance, DeclaredApplication, ImportBatch.
- **ROOT-06 (P2, pending) — Change and incident copy lifetime chronology and cap versions at32** Owners: SignificantChange, ComplianceIncident.
- **ROOT-07 (P2, pending) — Fresh server timestamps defeat retry equality for change and incident commands** Owners: SignificantChange, ComplianceIncident.
- **ROOTACCESS-01 (P2, pending) — Boundary and commitment owners invoke another live aggregate to decide waiver authority** Owners: SystemBoundary, CommitmentDraft.
- **ROOTACCESS-02 (P2, pending) — Continuing boundary and commitment roots retain all historical approvals and responsibility lifecycles** Owners: SystemBoundary, CommitmentDraft.
- **ROOTACCESS-03 (P2, pending) — System-description exports and binding histories share the revision of narrative drafting and approval** Owners: SystemDescription.
- **ROOTACCESS-04 (P2, pending) — Decision and mutation retry handling differs from the required exact-intent contract** Owners: SystemBoundary, CommitmentDraft, SystemDescription.
- **ROOTACCESS-05 (P2, pending) — System-description source and period-change count caps turn storage limits into business limits** Owners: SystemDescription.
- **ROOTASSURE-01 (P2, pending) — Advisor handoff retains all independently identified feedback and discussion history** Owners: AdvisorHandoff.
- **ROOTASSURE-02 (P2, pending) — Methodology template keeps every independently identified version behind64-version lifetime cap** Owners: MethodologyTemplate.
- **ROOTASSURE-03 (P2, pending) — Template application mixes downstream creation receipts with successor adoption histories** Owners: TemplateApplication.
- **ROOTASSURE-04 (P2, pending) — Retry identities are lost or compared with newly generated presentation/time** Owners: AdvisorHandoff, MethodologyTemplate, MethodologyTemplateContribution, TemplateApplication.
- **ROOTTECH-01 (P2, pending) — Location, process and provider registers make name uniqueness serialize every record lifecycle** Owners: LocationRegister, OperationalProcessRegister, ProviderRegister.
- **ROOTTECH-02 (P2, pending) — Inventory create retries compare mutable current state and revision retries always become stale** Owners: TechnologyComponent, InformationAsset, DataFlow, LocationRegister, OperationalProcessRegister.
- **ROOTTECH-03 (P2, pending) — Tenant-wide provider assurance histories and200-record caps block future independent evidence decisions** Owners: ProviderAssuranceRegister.
- **ROOTTECH-04 (P2, pending) — Provider gaps retain every expired risk acceptance and stop accepting links after50** Owners: ProviderAssuranceRegister.
- **ROOTTECH-05 (P2, pending) — Data-flow complete carried-asset facts are limited by an unexplained100-item cap** Owners: DataFlow.

## Full aggregate inventory

| Aggregate | Feature | Boundary decision | Original observations | Jev Noul risk | Source |
|---|---|---|---|---:|---|
| AccessGrant | AccessControl | retain | ACCESS-10 (pending) | 0.21 | [source](../../../../src/Compliance.Core/Features/AccessControl/AccessGrant.cs) |
| FirmProfessionalDutyCatalog | AccessControl | split | ACCESS-04 (split into FirmProfessionalDutyDesignation and ProfessionalAssertionIdentityClaim) | 0.91 | `FirmProfessionalDutyCatalog.cs` (removed by the FirmProfessionalDutyDesignation split, #1022) |
| FirmStaffDirectory | AccessControl | split | ACCESS-03 (split into FirmStaffRecord and FirmStaffIdentityClaim) | 0.91 | `FirmStaffDirectory.cs` (removed by the FirmStaffRecord split, #1022) |
| IndependenceLedger | AccessControl | split | ACCESS-02 (split into ServiceEngagementIndependence, ClientServiceIndependence, PracticeIndependenceCommitment and the independent decision owners) | 0.90 | `IndependenceLedger.cs` (removed by the PracticeIndependenceCommitment split, #1022) |
| IndependenceRuleCatalog | AccessControl | narrow | ACCESS-05 (pending) | 0.67 | [source](../../../../src/Compliance.Core/Features/AccessControl/IndependenceRuleCatalog.cs) |
| IndependenceRuleRatificationCatalog | AccessControl | narrow | ACCESS-06 (pending) | 0.76 | [source](../../../../src/Compliance.Core/Features/AccessControl/IndependenceRuleRatificationCatalog.cs) |
| Member | AccessControl | retain | ACCESS-01 (pending), ACCESS-08 (in_progress), ACCESS-10 (pending) | 0.29 | [source](../../../../src/Compliance.Core/Features/AccessControl/Member.cs) |
| Role | AccessControl | retain | ACCESS-09 (pending) | 0.23 | [source](../../../../src/Compliance.Core/Features/AccessControl/Role.cs) |
| RolePermission | AccessControl | retain | ACCESS-08 (in_progress) | 0.19 | [source](../../../../src/Compliance.Core/Features/AccessControl/RolePermission.cs) |
| SeparationOfDutiesWaiver | AccessControl | retain | — | 0.17 | [source](../../../../src/Compliance.Core/Features/AccessControl/SeparationOfDutiesWaiver.cs) |
| Team | AccessControl | retain | ACCESS-09 (pending) | 0.15 | [source](../../../../src/Compliance.Core/Features/AccessControl/Team.cs) |
| TeamMember | AccessControl | retain | ACCESS-01 (pending), ACCESS-08 (in_progress) | 0.26 | [source](../../../../src/Compliance.Core/Features/AccessControl/TeamMember.cs) |
| TeamRole | AccessControl | retain | ACCESS-08 (in_progress) | 0.16 | [source](../../../../src/Compliance.Core/Features/AccessControl/TeamRole.cs) |
| TenantManagerGuard | AccessControl | retain | ACCESS-01 (pending) | 0.68 | [source](../../../../src/Compliance.Core/Features/AccessControl/TenantManagerGuard.cs) |
| AccessPopulation | AccessReviews | split | OPS-07 (pending), OPS-15 (pending) | 0.89 | [source](../../../../src/Compliance.Core/Features/AccessReviews/AccessPopulation.cs) |
| AccessReviewCampaign | AccessReviews | split | OPS-07 (pending), OPS-11 (pending), OPS-16 (pending) | 0.89 | split into campaign and item owners (#1022); [campaign](../../../../src/Compliance.Core/Features/AccessReviews/AccessReviewCampaign.cs), [item](../../../../src/Compliance.Core/Features/AccessReviews/AccessReviewItem.cs) |
| AccessReviewSystemLedger | AccessReviews | split | OPS-07 (pending), OPS-11 (pending), OPS-16 (pending) | 0.91 | retired by the expectation line and population exception split (#1022) |
| AdvisorHandoff | AdvisorCollaboration | split | ROOTASSURE-01 (pending), ROOTASSURE-04 (pending) | 0.90 | [source](../../../../src/Compliance.Core/Features/AdvisorCollaboration/AdvisorHandoff.cs) |
| ApplicationImportLedger | Applications | split | ROOT-02 (pending) | 0.86 |  |
| DeclaredApplication | Applications | split | ROOT-01 (pending), ROOT-02 (pending), ROOT-04 (pending), ROOT-05 (pending) | 0.93 |  |
| DeclaredSystemInstance | Applications | retain | ROOT-05 (pending) | 0.57 | [source](../../../../src/Compliance.Core/Features/Applications/DeclaredSystemInstance.cs) |
| ImportBatch | Applications | retain | ROOT-02 (pending), ROOT-05 (pending) | 0.45 |  |
| SystemInstanceAccessReviewScope | Applications | narrow | ROOT-03 (pending) | 0.84 | [source](../../../../src/Compliance.Core/Features/Applications/SystemInstanceAccessReviewScope.cs) |
| AuditPackage | AuditPackages | split | ASSURE-02 (pending), ASSURE-10 (pending), CORE-01 (complete) | 0.89 | [source](../../../../src/Compliance.Core/Features/AuditPackages/AuditPackage.cs) |
| AuditPopulation | AuditPopulations | narrow | ASSURE-04 (pending), ASSURE-10 (pending), ASSURE-11 (pending), CORE-01 (complete) | 0.83 | [source](../../../../src/Compliance.Core/Features/AuditPopulations/AuditPopulation.cs) |
| AuditorRequest | AuditorRequests | split | ASSURE-03 (pending), ASSURE-09 (pending), ASSURE-10 (pending), ASSURE-11 (pending) | 0.89 | [source](../../../../src/Compliance.Core/Features/AuditorRequests/AuditorRequest.cs) |
| SystemBoundary | Boundaries | narrow | ROOTACCESS-01 (pending), ROOTACCESS-02 (pending), ROOTACCESS-04 (pending) | 0.73 | [source](../../../../src/Compliance.Core/Features/Boundaries/SystemBoundary.cs) |
| SignificantChange | Changes | narrow | ROOT-06 (pending), ROOT-07 (pending), CORE-01 (complete) | 0.82 | [source](../../../../src/Compliance.Core/Features/Changes/SignificantChange.cs) |
| CommitmentDraft | Commitments | narrow | ROOTACCESS-01 (pending), ROOTACCESS-02 (pending), ROOTACCESS-04 (pending) | 0.69 | [source](../../../../src/Compliance.Core/Features/Commitments/CommitmentDraft.cs) |
| ControlCriterionMappingLedger | ControlMappings | split | OPS-01 (pending), OPS-11 (pending), OPS-15 (pending) | 0.85 | [source](../../../../src/Compliance.Core/Features/ControlMappings/ControlCriterionMappingLedger.cs) |
| CriterionApplicabilityLedger | ControlMappings | split | OPS-01 (pending), OPS-11 (pending), OPS-15 (pending) | 0.85 | [source](../../../../src/Compliance.Core/Features/ControlMappings/CriterionApplicabilityLedger.cs) |
| ControlDraft | Controls | narrow | OPS-11 (pending), OPS-13 (pending), OPS-15 (pending) | 0.63 | [source](../../../../src/Compliance.Core/Features/Controls/ControlDraft.cs) |
| CriteriaTextOverlayLedger | Criteria | split | OPS-05 (pending) | 0.76 | [source](../../../../src/Compliance.Core/Features/Criteria/CriteriaTextOverlayLedger.cs) |
| ControlEvaluationLedger | Evaluations | split | OPS-02 (pending), OPS-11 (pending), OPS-15 (pending), OPS-16 (pending) | 0.84 | [source](../../../../src/Compliance.Core/Features/Evaluations/ControlEvaluationLedger.cs) |
| ControlEvaluationPlan | Evaluations | narrow | OPS-13 (pending) | 0.52 | [source](../../../../src/Compliance.Core/Features/Evaluations/ControlEvaluationPlan.cs) |
| EvidenceArtifact | Evidence | narrow | ASSURE-08 (pending) | 0.76 | [source](../../../../src/Compliance.Core/Features/Evidence/EvidenceArtifact.cs) |
| EvidenceRedaction | Evidence | narrow | ASSURE-06 (pending), ASSURE-07 (pending) | 0.85 | [source](../../../../src/Compliance.Core/Features/Evidence/EvidenceRedaction.cs) |
| EvidenceRequestLedger | Evidence | split | ASSURE-01 (pending), ASSURE-11 (pending) | 0.82 | [source](../../../../src/Compliance.Core/Features/Evidence/EvidenceRequestLedger.cs) |
| EvidenceSupport | Evidence | retain | — | 0.11 | [source](../../../../src/Compliance.Core/Features/Evidence/EvidenceSupport.cs) |
| AuditObservation | Examinations | retain | — | 0.26 | [source](../../../../src/Compliance.Core/Features/Examinations/AuditObservation.cs) |
| AuditOutcome | Examinations | retain | CORE-01 (complete) | 0.12 | [source](../../../../src/Compliance.Core/Features/Examinations/AuditOutcome.cs) |
| ExaminationEngagement | Examinations | narrow | ASSURE-13 (pending), ASSURE-11 (pending) | 0.81 | [source](../../../../src/Compliance.Core/Features/Examinations/ExaminationEngagement.cs) |
| ExaminationSignOff | Examinations | retain | — | 0.16 | [source](../../../../src/Compliance.Core/Features/Examinations/ExaminationSignOff.cs) |
| ManagementAssertion | Examinations | narrow | ASSURE-17 (pending), ASSURE-11 (pending), CORE-01 (complete) | 0.80 | [source](../../../../src/Compliance.Core/Features/Examinations/ManagementAssertion.cs) |
| ManagementRepresentationLetter | Examinations | retain | CORE-01 (complete) | 0.15 | [source](../../../../src/Compliance.Core/Features/Examinations/ManagementRepresentationLetter.cs) |
| TypeIIPlan | Examinations | narrow | ASSURE-17 (pending), ASSURE-11 (pending), CORE-01 (complete) | 0.72 | [source](../../../../src/Compliance.Core/Features/Examinations/TypeIIPlan.cs) |
| ClientExport | Exports | process | ASSURE-15 (pending), CORE-01 (complete) | 0.71 | [source](../../../../src/Compliance.Core/Features/Exports/ClientExport.cs) |
| ComplianceIncident | Incidents | narrow | ROOT-06 (pending), ROOT-07 (pending), CORE-01 (complete), ROOT-08 (pending) | 0.84 | [source](../../../../src/Compliance.Core/Features/Incidents/ComplianceIncident.cs) |
| MethodologyTemplate | MethodologyTemplates | narrow | ROOTASSURE-02 (pending), ROOTASSURE-04 (pending), CORE-01 (complete) | 0.86 | [source](../../../../src/Compliance.Core/Features/MethodologyTemplates/MethodologyTemplate.cs) |
| MethodologyTemplateContribution | MethodologyTemplates | retain | ROOTASSURE-04 (pending), CORE-01 (complete) | 0.16 | [source](../../../../src/Compliance.Core/Features/MethodologyTemplates/MethodologyTemplateContribution.cs) |
| TemplateApplication | MethodologyTemplates | process | ROOTASSURE-03 (pending), ROOTASSURE-04 (pending), CORE-01 (complete) | 0.88 | [source](../../../../src/Compliance.Core/Features/MethodologyTemplates/TemplateApplication.cs) |
| ControlOperationsLedger | Operations | split | OPS-03 (pending), OPS-11 (pending), OPS-15 (pending), OPS-16 (pending) | 0.88 | retired by the plan/occurrence split (#1022) |
| TypeIIObservationCalendar | Operations | narrow | OPS-09 (pending), OPS-10 (pending), OPS-16 (pending) | 0.87 | [source](../../../../src/Compliance.Core/Features/Operations/TypeIIObservationCalendar.cs) |
| TypeIIPeriod | Operations | narrow | OPS-09 (pending), OPS-10 (pending), OPS-12 (pending), OPS-17 (pending), CORE-01 (complete) | 0.91 | [source](../../../../src/Compliance.Core/Features/Operations/TypeIIPeriod.cs) |
| Policy | Policies | narrow | OPS-11 (pending), OPS-13 (pending), OPS-15 (pending) | 0.78 | [source](../../../../src/Compliance.Core/Features/Policies/Policy.cs) |
| PolicyDistributionCampaign | PolicyDistribution | split | OPS-07 (pending), OPS-16 (pending) | 0.91 | [source](../../../../src/Compliance.Core/Features/PolicyDistribution/PolicyDistributionCampaign.cs) |
| TrainingCatalog | PolicyDistribution | split | OPS-05 (pending), OPS-15 (pending) | 0.88 | retired by the training requirement split (#1022) |
| ClientService | Programs | retain | ASSURE-11 (pending) | 0.15 | [source](../../../../src/Compliance.Core/Features/Programs/ClientService.cs) |
| ComplianceProgram | Programs | narrow | ASSURE-16 (pending), ASSURE-11 (pending) | 0.79 | [source](../../../../src/Compliance.Core/Features/Programs/ComplianceProgram.cs) |
| ProgramRollover | Programs | process | ASSURE-15 (pending), ASSURE-17 (pending), ASSURE-11 (pending), CORE-01 (complete) | 0.78 | [source](../../../../src/Compliance.Core/Features/Programs/ProgramRollover.cs) |
| ProviderAssuranceRegister | Providers | split | ROOTTECH-03 (split into ProviderAssuranceReport, ProviderAssuranceReview, ProviderAssuranceGap and ProviderRiskAcceptance), ROOTTECH-04 (split into ProviderAssuranceGap and ProviderRiskAcceptance) | 0.90 | `ProviderAssuranceRegister.cs` (removed by the provider assurance split, #1022) |
| ProviderRegister | Providers | split | ROOTTECH-01 (pending) | 0.89 | source retired by S18 (`Providers/ProviderRegister.cs`) |
| ReadinessLedger | Readiness | split | OPS-08 (pending), OPS-11 (pending), OPS-16 (pending) | 0.93 | [source](../../../../src/Compliance.Core/Features/Readiness/ReadinessLedger.cs) |
| FindingAggregate | Remediation | split | OPS-06 (pending), OPS-11 (pending), OPS-12 (pending) | 0.90 | source |
| FindingMigrationAggregate | Remediation | process | OPS-12 (pending) | 0.18 | retired by the finding and corrective action split (#1022) |
| RemediationLedger | Remediation | legacy | — | 0.19 | retired by the finding and corrective action split (#1022) |
| RemediationMigrationFenceAggregate | Remediation | process | OPS-12 (pending) | 0.20 | retired by the finding and corrective action split (#1022) |
| ArtifactDisclosure | Retention | retain | ASSURE-10 (pending) | 0.17 | [source](../../../../src/Compliance.Core/Features/Retention/ArtifactDisclosure.cs) |
| ArtifactDisposition | Retention | process | ASSURE-15 (pending) | 0.81 | [source](../../../../src/Compliance.Core/Features/Retention/ArtifactDisposition.cs) |
| ArtifactEngagementHold | Retention | retain | ASSURE-10 (pending) | 0.15 | [source](../../../../src/Compliance.Core/Features/Retention/ArtifactEngagementHold.cs) |
| ArtifactRetention | Retention | split | ASSURE-12 (pending) | 0.88 | [source](../../../../src/Compliance.Core/Features/Retention/ArtifactRetention.cs) |
| EvidenceHandlingPolicy | Retention | narrow | ASSURE-05 (pending), ASSURE-10 (pending) | 0.67 | [source](../../../../src/Compliance.Core/Features/Retention/EvidenceHandlingPolicy.cs) |
| RiskDraft | Risks | retain | OPS-14 (not in active ledger), OPS-15 (pending) | 0.24 | [source](../../../../src/Compliance.Core/Features/Risks/RiskDraft.cs) |
| RiskEvaluation | Risks | narrow | OPS-11 (pending), OPS-13 (pending), OPS-16 (pending) | 0.78 | [source](../../../../src/Compliance.Core/Features/Risks/RiskEvaluation.cs) |
| RiskGovernanceLedger | Risks | split | OPS-04 (pending), OPS-11 (pending), OPS-15 (pending), OPS-16 (pending) | 0.93 | [source](../../../../src/Compliance.Core/Features/Risks/RiskGovernanceLedger.cs) |
| RiskMethod | Risks | narrow | OPS-13 (pending), OPS-15 (pending) | 0.39 | [source](../../../../src/Compliance.Core/Features/Risks/RiskMethod.cs) |
| ImmutableSnapshot | Snapshots | retain | ASSURE-13 (pending) | 0.35 | [source](../../../../src/Compliance.Core/Features/Snapshots/ImmutableSnapshot.cs) |
| PopulationSnapshot | Snapshots | retain | ASSURE-13 (pending) | 0.28 | [source](../../../../src/Compliance.Core/Features/Snapshots/PopulationSnapshot.cs) |
| PopulationSnapshotChunk | Snapshots | process | ASSURE-14 (pending) | 0.31 | [source](../../../../src/Compliance.Core/Features/Snapshots/PopulationSnapshotChunk.cs) |
| SystemDescription | SystemDescription | split | ROOTACCESS-03 (pending), ROOTACCESS-04 (pending), ROOTACCESS-05 (pending) | 0.80 | [source](../../../../src/Compliance.Core/Features/SystemDescription/SystemDescription.cs) |
| DataFlow | TechnologyInventory | retain | ROOTTECH-02 (pending), ROOTTECH-05 (pending) | 0.41 | [source](../../../../src/Compliance.Core/Features/TechnologyInventory/DataFlow.cs) |
| InformationAsset | TechnologyInventory | retain | ROOTTECH-02 (pending) | 0.53 | [source](../../../../src/Compliance.Core/Features/TechnologyInventory/InformationAsset.cs) |
| LocationRegister | TechnologyInventory | split | ROOTTECH-01 (pending), ROOTTECH-02 (pending) | 0.86 | source retired by S18 (`TechnologyInventory/LocationRegister.cs`) |
| OperationalProcessRegister | TechnologyInventory | split | ROOTTECH-01 (pending), ROOTTECH-02 (pending) | 0.89 | source retired by S18 (`TechnologyInventory/OperationalProcessRegister.cs`) |
| TechnologyComponent | TechnologyInventory | retain | ROOTTECH-02 (pending) | 0.52 | [source](../../../../src/Compliance.Core/Features/TechnologyInventory/TechnologyComponent.cs) |
| PlatformOperatorRoster | Tenants | retain | ACCESS-08 (in_progress) | 0.25 | [source](../../../../src/Compliance.Core/Features/Tenants/PlatformOperatorRoster.cs) |
| Tenant | Tenants | narrow | ACCESS-10 (pending), ACCESS-16 (pending) | 0.73 | [source](../../../../src/Compliance.Core/Features/Tenants/Tenant.cs) |
| TenantInvitation | Tenants | split | ACCESS-11 (pending), ACCESS-13 (pending) | 0.73 | [source](../../../../src/Compliance.Core/Features/Tenants/TenantInvitation.cs) |
| TenantOwner | Tenants | project | ACCESS-15 (pending) | 0.71 | source retired with the cutover |
| TenantSlug | Tenants | retain | ACCESS-14 (pending) | 0.45 | [source](../../../../src/Compliance.Core/Features/Tenants/TenantSlug.cs) |
| EmailAddress | UserIdentities | narrow | ACCESS-12 (pending), ACCESS-13 (pending) | 0.80 | [source](../../../../src/Compliance.Core/Features/UserIdentities/EmailAddress.cs) |
| TenantIdentityProvider | UserIdentities | narrow | ACCESS-07 (pending), ACCESS-10 (pending) | 0.83 | [source](../../../../src/Compliance.Core/Features/UserIdentities/TenantIdentityProvider.cs) |
| UserIdentity | UserIdentities | narrow | ACCESS-12 (pending), ACCESS-17 (complete) | 0.37 | [source](../../../../src/Compliance.Core/Features/UserIdentities/UserIdentity.cs) |
| LegacyWorkAssignmentLedger | Work | legacy | — | 0.26 | [source](../../../../src/Compliance.Core/Features/Work/LegacyWorkAssignmentLedger.cs) |
| WeeklyWorkDigest | Work | process | OPS-12 (pending) | 0.75 | [source](../../../../src/Compliance.Core/Features/Work/WeeklyWorkDigest.cs) |
| WorkDigestDispatch | Work | process | OPS-12 (pending) | 0.80 | [source](../../../../src/Compliance.Core/Features/Work/WorkDigestDispatch.cs) |
| WorkDigestPreference | Work | retain | OPS-14 (not in active ledger), OPS-15 (pending) | 0.50 | [source](../../../../src/Compliance.Core/Features/Work/WorkDigestPreference.cs) |
| WorkItemAssignment | Work | narrow | OPS-12 (pending), OPS-13 (pending), OPS-15 (pending) | 0.73 | [source](../../../../src/Compliance.Core/Features/Work/WorkItemAssignment.cs) |
| Person | Workforce | retain | ACCESS-10 (pending) | 0.60 | [source](../../../../src/Compliance.Core/Features/Workforce/Person.cs) |
| ServiceIdentity | Workforce | retain | ACCESS-10 (pending) | 0.57 | [source](../../../../src/Compliance.Core/Features/Workforce/ServiceIdentity.cs) |
| WorkRelationship | Workforce | retain | ACCESS-10 (pending) | 0.46 | [source](../../../../src/Compliance.Core/Features/Workforce/WorkRelationship.cs) |
| WorkforceObservationResolution | Workforce | retain | — | 0.25 | [source](../../../../src/Compliance.Core/Features/Workforce/WorkforceObservationResolution.cs) |
| WorkforceSourceObservation | Workforce | retain | — | 0.22 | [source](../../../../src/Compliance.Core/Features/Workforce/WorkforceSourceObservation.cs) |

## Current reactor inventory

The 21 surviving baseline reactor rows retain their original source-reviewed failure-mode mapping. New membership-retirement reactors were reviewed against current code; their exact-scope proof and replay tests are documented in [`membership-retirement-recovery.md`](../../../../docs/architecture/membership-retirement-recovery.md). Jev probabilities are a triage signal only.

| Reactor | Baseline finding(s) | Current Jev Noul risk | Current source |
|---|---|---:|---|
| AcceptedStaffDirectoryReconciliationReactor | — | 0.42 | [source](../../../../src/Compliance.Core/Features/AccessControl/AcceptedStaffDirectoryReconciliationReactor.cs) |
| FirmStaffStatusReevaluationReactor | — | 0.67 | [source](../../../../src/Compliance.Core/Features/AccessControl/FirmStaffStatusReevaluationReactor.cs) |
| MemberRetirementReactor | CHOREO-03 | 0.36 | [source](../../../../src/Compliance.Core/Features/AccessControl/MemberRetirementReactor.cs) |
| RetiredMemberGrantReactor | CHOREO-03, CHOREO-06 | 0.56 | [source](../../../../src/Compliance.Core/Features/AccessControl/RetiredMemberGrantReactor.cs) |
| RetiredTeamAssignmentReactor | CHOREO-03, CHOREO-06 | 0.54 | [source](../../../../src/Compliance.Core/Features/AccessControl/RetiredTeamAssignmentReactor.cs) |
| RoleCleanupReactor | CHOREO-06 | 0.64 | [source](../../../../src/Compliance.Core/Features/AccessControl/RoleCleanupReactor.cs) |
| TeamCleanupReactor | CHOREO-06 | 0.53 | [source](../../../../src/Compliance.Core/Features/AccessControl/TeamCleanupReactor.cs) |
| TenantRbacBootstrapReactor | — | 0.54 | [source](../../../../src/Compliance.Core/Features/AccessControl/TenantRbacBootstrapReactor.cs) |
| ApplicationImportExecutionReactor | CHOREO-05 | 0.35 |  |
| LinkImpactReviewWorkReactor | CHOREO-09 | 0.49 | [source](../../../../src/Compliance.Core/Features/Changes/LinkImpactReviewWorkReactor.cs) |
| SystemDescriptionDecisionReactor | CHOREO-09 | 0.57 | [source](../../../../src/Compliance.Core/Features/Changes/SystemDescriptionDecisionReactor.cs) |
| ControlEvaluationDeviationFindingReactor | — | 0.30 | [source](../../../../src/Compliance.Core/Features/Evaluations/ControlEvaluationDeviationFindingReactor.cs) |
| EvidenceArtifactInspectionEffectsReactor | — | 0.49 | [source](../../../../src/Compliance.Core/Features/Evidence/EvidenceArtifactInspectionEffectsReactor.cs) |
| ControlOccurrenceFindingReactor | — | 0.32 | [source](../../../../src/Compliance.Core/Features/Remediation/ControlOccurrenceFindingReactor.cs) |
| BoundaryRiskReassessmentReactor | — | 0.53 | [source](../../../../src/Compliance.Core/Features/Risks/BoundaryRiskReassessmentReactor.cs) |
| RiskMethodReassessmentReactor | — | 0.41 | [source](../../../../src/Compliance.Core/Features/Risks/RiskMethodReassessmentReactor.cs) |
| TenantInvitationDeliveryReactor | CHOREO-07 | 0.67 | (removed in #1022) |
| TenantInvitationReactor | — | 0.63 | [source](../../../../src/Compliance.Core/Features/Tenants/TenantInvitationReactor.cs) |
| TenantRegistrationReactor | CHOREO-08 | 0.60 | [source](../../../../src/Compliance.Core/Features/Tenants/TenantRegistrationReactor.cs) |
| TenantSelfServiceActivationReactor | — | 0.68 | [source](../../../../src/Compliance.Core/Features/Tenants/TenantSelfServiceActivationReactor.cs) |
| TenantSlugReactor | CHOREO-08 | 0.42 | [source](../../../../src/Compliance.Core/Features/Tenants/TenantSlugReactor.cs) |
| EmailChallengeDeliveryReactor (now the root reactor of EmailChallengeDeliveryProcess; delivery receipts are ledger state, #1022 S17) | CHOREO-07, ACCESS-13 | 0.62 | [source](../../../../src/Compliance.Core/Features/UserIdentities/EmailChallengeDeliveryProcess.cs) |
| EmailReservationReactor | — | 0.52 | [source](../../../../src/Compliance.Core/Features/UserIdentities/EmailReservationReactor.cs) |
| IdentityRecoveryNoticeReactor (replaced by the SendIdentityRecoveryNotice receiver of IdentityRecoveryProcess, #1022 S4) | CHOREO-01, CHOREO-07 | 0.58 | [source](../../../../src/Compliance.Core/Features/UserIdentities/IdentityRecoveryProcess.cs) |

### Removed baseline reactor classes

These 12 old backfill/migration classes are absent at current head and are not counted as current reactors: `AccessReviewGrantBackfillReactor`, `ApplicationImportGrantBackfillReactor`, `ApplicationInventoryGrantBackfillReactor`, `BuiltInRoleCatalogMigrationReactor`, `ComplianceRecordRestrictedAccessGrantBackfillReactor`, `EvidenceArtifactReadPermissionReactor`, `EvidenceRedactionPermissionsReactor`, `ProviderInventoryGrantBackfillReactor`, `TechnologyInventoryGrantBackfillReactor`, `WorkforceGrantBackfillReactor`, `WorkforcePersonalDetailsGrantBackfillReactor`, `WorkforceRestrictedFieldGrantBackfillReactor`.

## Boundary rules to preserve during the splits

- Keep RiskDraft and notification consent independently owned; keep SystemBoundary conflict-relevant scope atomic.
- Preserve permanent staff/user independence walls and complete client-service epochs when partitioning independence history.
- Imports publish only after every frozen target application has durable exact-revision proof; the publication owner remains the single gate.
- Preserve authoritative baseline, readiness, historical decision and disposal proofs. Projection reads do not authorize cross-owner writes.
- For every reactor, source event identity, stable effect identity, receiver idempotency, and retry-after-unknown-outcome are separate requirements. A random execution-attempt ID is not a business retry key.

## Jev evidence files

- `aggregates-001-026-request.json` / `aggregates-001-026-response.json`
- `aggregates-027-052-request.json` / `aggregates-027-052-response.json`
- `aggregates-053-078-request.json` / `aggregates-053-078-response.json`
- `aggregates-079-103-request.json` / `aggregates-079-103-response.json`
- `current-access-aggregates-request.json` / `current-access-aggregates-response.json`
- `current-reactors-request.json` / `current-reactors-response.json`

## Adopted design and naming decisions

Focused Jev comparisons favored these decisions: keep per-item campaign decisions under `AccessReviewItem` with a campaign-level completion gate; split import execution from the source-level publication gate; separate package manifest, build process, and delivery authority; bind independence to a permanent user/practice commitment plus exact service epochs; use three guard layers (authorizer, handler/proof protocol, aggregate-local invariant/OCC); and keep technical attempts out of business events. For the debated occurrence name, `ControlOccurrence` edged `ControlPerformanceOccurrence` (0.66 vs 0.32, confidence 0.55); the choice follows the existing event vocabulary and `(control, business window)` identity.

Jev’s naming review selected `ApplicationImportSource`, `ApplicationImportBatch`, `AccessReviewItem`, `ControlOccurrence`, `AuditPackageDelivery`, `AuditPackageBuildProcess`, `AdvisorFeedbackThread`, `ProviderNameClaim`, `PracticeIndependenceCommitment`, and `ReadinessGapPlan`. These names now appear in the target catalog. The machine-readable request and response artifacts preserve the alternatives and confidence. They are vocabulary/design inputs; aggregate boundaries and recovery rules remain justified by source invariants and the named tests.


## Implementation contract refinement

The [selected implementation design](implementation-decisions.md) now replaces speculative aliases with exact source command destinations and adds [source signatures](target-command-contracts.md), [remaining command identities](remaining-command-identities.md), [admission/abort contracts](source-admission-contracts.md), [future receiver effects](target-reactor-contracts.md), [native event payloads](target-event-contracts.md) and [owner protocols/tests](owner-protocols.md). These are selected target contracts; source catalogs above remain the pinned current-behavior inventory.

Additional finding DESIGN-01, outside the original 81 observations: cross-owner first acceptance under one realm/logical UUID needs an atomic native event uniqueness constraint. Current per-stream append/OCC does not provide it. The exact storage contract and acceptance tests are tracked by [#1042](https://github.com/bdgrz/compliance-core/issues/1042), under #1022. The original 81-observation counts above do not silently include this new dependency.

Protocol preflight Jev preferred atomic native indexing (0.88); its estimated hole probabilities for the described admission, cancellation, readiness and import-publication protocols were 0.17, 0.23, 0.35 and 0.29. These are signals for required races, not acceptance. Independent source checks corrected the earlier classification freeze sketch: classification is allowed only after raw population acceptance and remains separately writable against its immutable snapshot proof. They also preserved the existing TrainingCatalog identity algorithm and the default corrective action for reviewer-requested findings. Identity recovery now resumes from existing same-account registration proof even when no new registration event is emitted.
