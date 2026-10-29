# Required component inventory

Planning baseline: local product contracts and backend source inspected 2026-10-18.

For the reconciled command list, requirement coverage and remaining work, use [System command list](command-list.md), with rationale in the [command gap analysis](command-inventory-gap-analysis.md). Its command conclusions supersede the provisional completion suggestions below, including periodic-review subsystems and external-auditor account proposals.

For the read layer, use the [query and view-model list](query-and-view-model-list.md) for the pruned, story-backed backfill. Proposed query candidates later in this inventory are not all required; optional collection and unsettled commerce/capacity hypotheses are excluded there.

For the event layer, use the [domain event list](domain-event-list.md) for the pruned, story-backed backfill. It supersedes unvalidated event candidates in the proposed completion inventory below.

This inventory covers commands, queries, domain events, aggregates, models, HTTP/MCP APIs, reactors, and projectors for the documented Compliance product. It includes the complete local source inventory below and a proposed completion inventory for later workflows. Source presence is not proof of delivery, registration, tests, authorization correctness, or release acceptance. GitHub delivery status was not audited for this document.

## How to read the inventory

- **Commands** express an attributable intent and may change authoritative state.
- **Queries** read or preview state. HTTP POST does not necessarily mean a command.
- **Events** retain accepted domain facts; they do not replace operational logs.
- **Aggregates** enforce consistency and workflow transitions. A model is not automatically an aggregate.
- **Models** include entities, immutable versions, relationships, decisions, snapshots, request results, and read views.
- **APIs** expose authorized operations through HTTP and, where appropriate, MCP. Internal commands and human approvals are not automatically MCP tools.
- **Reactors** consume facts and perform follow-up commands or external effects. They require tenant scope, stable deduplication identity, safe replay, and inspectable failures.
- **Projectors** derive rebuildable read models. They do not own business decisions or execute side effects.

The existing inventory preserves actual source names, including older vocabulary. Proposed names below are design candidates, not approved aggregate boundaries or a declaration that similarly named existing behavior is missing. Compare capabilities and invariants before adding anything.

Sources: [product scope](README.md), [domain ownership](domain-model.md), [canonical models](canonical-entity-model.md), [backlog](backlog.md), [commercial scope](business-operations.md), [projection rules](../architecture/decisions/0007-read-models-projections-and-as-of-calculations.md), [work-source coverage](work-queue-source-coverage.md).

## Required capability checklist

Each capability needs its owning commands and queries, retained facts, domain state, authorized API surface, and read models. A reactor is required only where an asynchronous reaction is actually needed. The source appendix names the components already present for these capabilities.

| Capability | Required behavior and records | Product scope |
| --- | --- | --- |
| Sign-in and recovery | Platform users, federated identities, verified email challenges, identity linking, recovery and browser/API authentication | EN-01, R1-04, F1-06 |
| Organizations and membership | Organization creation/routing, lifecycle, invitations, membership, tenant issuer trust, operator grants | R1-04/15, F1-01/06 |
| Authorization and responsibilities | Roles, permissions, team membership, grants, responsibility intervals, self-review constraints and governed exceptions | EN-01/04, R1-04 |
| Firm engagements and independence | Staff directory, service engagements, acceptance, assignments, nonattest records, reevaluation and walls | F1-03/07/08 |
| Program and scope | Program, client services, versioned boundaries, inclusions/exclusions, approvals and impact previews | R1-01/02 |
| Criteria | Catalog editions, entries, permitted content overlays, selected criteria and applicability decisions | R1-03/06 |
| Workforce | People, work relationships, workforce authority, observations, source reconciliation, service-identity ownership | R1-11 |
| Application inventory | Logical applications, system instances, relationships, lifecycle, restricted visibility, review scope and bounded change-impact previews | R1-10 |
| Technology and information | Material components, locations, processes, information assets, data flows, effective history and classifications | R1-12 |
| Commitments | Service commitments, system requirements, CUECs/CSOCs, applicability, exact-version review and approval | R1-13 |
| Controls and mappings | Drafts, immutable approved versions, retirement, criteria mappings, applicability, owner and operating plan | R1-05/06, R2-01 |
| Risk governance | Methods, assessment rounds, treatment, control-treatment links, actions, residual risk, acceptance and reassessment | R1-07 |
| Provider assurance | Provider register, report coverage, materiality, boundary treatment, reviews and coverage gaps | R1-14 |
| Policies and training | Policy lifecycle and applicability, review cycles, frozen distribution audiences, acknowledgements, training and waivers | R2-02/10 |
| Control operation | Calendar, occurrences, performance submissions, corrections, independent reviews, gaps and recurring scheduling | R2-01/04, T2-02/03 |
| Control evaluation | Evaluation plans and rounds, test-step observations, submissions, independent review and deviations | R2-05 |
| Evidence | Requests, capture, immutable content, support links, inspection/quarantine, contextual sufficiency and recovery | EN-06, R2-03 |
| Evidence governance | Access/disclosure, redaction lineage, retention bases, holds, governed disposition and preserved metadata | R2-12 |
| External access | Accounts, groups, roles, entitlements, direct assignments, derived paths, expected access and source completeness | R2-06 |
| Access review | Frozen populations, campaign scope, personal/bulk decisions, missing populations, remediation and verification | R2-06, T2-05 |
| Remediation and waivers | Findings, source wording, corrective actions, closure review, reopening, waiver approval/expiry and risk-acceptance links | R2-07 |
| Readiness | Versioned rules, exact input sets, retained assessments, gaps, action plans, annotations and human entry decisions | R1-08, R2-09, T2-04 |
| Accountable work | Source-owned work, assignment, claim/delegation, eligible actors, blocked/overdue state, search, reminders and escalation | R2-11 |
| Imports and collection | Staging, preview, correlation, reconciliation, batch acceptance, durable execution/recovery and source provenance | EN-05, R1-09, T2-08 |
| Snapshots and history | Frozen exact versions, manifests, verification, amendments, regeneration and replay | EN-02/03 |
| Advisor collaboration | Scoped requests, feedback, response, resolution and independent advisory compartment | R2-08 |
| Type I preparation | Engagement, baseline, management-authored system description, auditor access, requests, samples, packages and outcome | T1-01–07 |
| Type II operations | Observation period, periodic reviews, change/incident assessment, completeness monitoring and management reviews | T2-01–09 |
| Examination and rollover | Period close, populations, samples, exceptions, management assertion/sign-off, indexed package, outcome and next period | T3-01–07 |
| Firm portfolio and methodology | Authorized multi-client summaries, staff capacity, personal cross-client queue, templates and application provenance | F1-02–05 |
| Delegation and commerce | Organization handover, billing administration, subscription, selected add-ons and entitlement/charge history | BO-01–03 |

## Proposed completion inventory

These candidates make later scope concrete. They are not established public contracts. Each row must be reconciled with existing components below and the linked story before implementation. Slash-separated names denote separate operations or records, not one generic workflow.

### Advisor collaboration — R2-08

- Aggregate/model candidates: AdvisorRequest; AdvisorFeedback; AdvisoryReadinessNote; FeedbackResponse.
- Commands: OpenAdvisorRequest; RecordAdvisorFeedback; RespondToAdvisorFeedback; ResolveAdvisorFeedback; WithdrawAdvisorRequest.
- Queries: GetAdvisorRequest; GetAdvisorSharedSource; PreviewAdvisorHandoff; ListAdvisorRequests; ListAdvisorFeedback.
- Events: AdvisorHandoffCreated; AdvisorFeedbackRecorded; AdvisorFeedbackCorrected; AdvisorFeedbackResponded; AdvisorFeedbackResolved; AdvisorHandoffSelectionRevised; AdvisorHandoffReadiedForReview; AdvisorHandoffClosed; AdvisorHandoffRevoked.
- Reactors: AdvisorRequestNotificationReactor.
- Projectors: AdvisorHandoffDirectoryProjector; AdvisorFeedbackWorkItemProjector (implemented; routes actionable comment and change-request feedback to program-manager work and removes it when resolved).
- API family: advisory requests, feedback, responses and resolution.

### Type I engagement — T1-01

- Aggregate/model candidates: ExaminationEngagement; TypeIBaseline.
- Commands: CreateExaminationEngagement; ReviseExaminationEngagement; FreezeTypeIBaseline; AmendTypeIBaseline.
- Queries: GetExaminationEngagement; GetTypeIBaseline; PreviewTypeIBaseline.
- Events: ExaminationEngagementOpened; TypeIBaselineFrozen; TypeIBaselineAmended.
- Reactors: TypeIBaselineAssemblyReactor.
- Projectors: ExaminationEngagementDirectoryProjector; TypeIBaselineProjector.
- API family: engagements and baselines.
- Implemented baseline source contributors: the approved boundary; the criteria edition and latest intact workforce roster snapshot selected at the Type I date; each approved control version effective on that date; application revisions and applicable retirement records; and reviewed-system revisions and applicable retirements, both selected from that exact boundary. The other 15 required source families remain explicit preview blockers.

### System description — T1-02

- Aggregate/model candidates: SystemDescription; SystemDescriptionVersion; SystemDescriptionApproval.
- Commands: CreateSystemDescriptionDraft; ReviseSystemDescriptionDraft; ReviewSystemDescription; ApproveSystemDescription; ProposeSystemDescriptionSuccessor; BindSystemDescriptionToEngagement.
- Queries: GetSystemDescription; ListSystemDescriptionVersions; PreviewSystemDescriptionImpact.
- Events: SystemDescriptionDraftCreated; SystemDescriptionDraftRevised; SystemDescriptionReviewed; SystemDescriptionApproved; SystemDescriptionSuccessorProposed; SystemDescriptionBound.
- Reactors: SystemDescriptionImpactReactor.
- Projectors: SystemDescriptionDirectoryProjector; SystemDescriptionHistoryProjector; SystemDescriptionDecisionWorkItemProjector.
- API family: descriptions, versions, reviews, approvals and engagement bindings.

### Auditor requests and samples — T1-04, T3-03

- Aggregate/model candidates: AuditorRequest; SampleSelection; RequestResponse; ResponseDisclosureDecision.
- Commands: RecordAuditorRequest; RecordSampleSelection; AssignAuditorRequest; SubmitAuditorResponse; ReviewAuditorResponseDisclosure; DeliverAuditorResponse; RecordAuditorResponseDisposition.
- Queries: GetAuditorRequest; ListAuditorRequests; GetSampleSelection; ListRequestResponses.
- Events: AuditorRequestRecorded; SampleSelectionRecorded; AuditorRequestAssigned; AuditorResponseSubmitted; AuditorResponseDisclosureReviewed; AuditorResponseDelivered; AuditorResponseDispositionRecorded.
- Reactors: AuditorResponseDeliveryReactor; AuditorRequestReminderReactor.
- Projectors: AuditorRequestDirectoryProjector; SampleSelectionDirectoryProjector; AuditorRequestWorkItemProjector.
- API family: requests, frozen sample selections, responses and delivery receipts.

### Audit packages — T1-05, T3-05

- Aggregate/model candidates: AuditPackage; AuditPackageManifest; PackageValidation; PackageDelivery.
- Commands: CreateAuditPackage; RequestAuditPackageBuild; ValidateAuditPackage; ApproveAuditPackageDisclosure; DeliverAuditPackage; AmendAuditPackage.
- Queries: GetAuditPackage; ListAuditPackages; GetAuditPackageManifest; GetAuditPackageValidation; GetAuditPackageDelivery.
- Events: AuditPackageCreated; AuditPackageBuildRequested; AuditPackageBuildProgressRecorded; AuditPackageBuilt; AuditPackageBuildFailed; AuditPackageValidated; AuditPackageValidationOverrideApproved; AuditPackageDisclosureApproved; AuditPackageDelivered; AuditPackageAmended.
- Reactors: AuditPackageBuildReactor; AuditPackageDeliveryReactor.
- Projectors: AuditPackageDirectoryProjector; AuditPackageValidationProjector; PackageDeliveryProjector.
- API family: packages, manifests, validation, disclosure and delivery.

### Audit observations and outcomes — T1-06/07, T3-04/06

- Aggregate/model candidates: AuditObservation; AuditFindingReference; AuditOutcome; TypeIIPlan; ManagementAssertion; RepresentationLetterReference.
- Commands: RecordAuditObservation; LinkAuditObservationFinding; RecordAuditOutcome; ProposeTypeIIPlan; ApproveTypeIIPlan; RecordManagementAssertion; RecordRepresentationLetter; SignOffExamination.
- Queries: ListAuditObservations; GetAuditOutcome; GetTypeIIPlan; GetManagementAssertion; GetExaminationSignOff.
- Events: AuditObservationRecorded; AuditObservationFindingLinked; AuditOutcomeRecorded; TypeIIPlanProposed; TypeIIPlanApproved; ManagementAssertionRecorded; RepresentationLetterRecorded; ExaminationSignedOff.
- Reactors: AuditOutcomeStageTransitionReactor.
- Projectors: AuditObservationDirectoryProjector; AuditOutcomeProjector; TypeIIPlanProjector; ExaminationSignOffProjector.
- API family: observations, outcomes, plans, assertions, letters and sign-offs.

### Observation periods and recurrence — T2-01/02/03

- Aggregate/model candidates: TypeIIPeriod (approved opening, expected population and generation batches); TypeIIObservationCalendar (program-wide reserved ranges); ControlOperatingPlan (one plan line per control); ControlOccurrence (one occurrence per control and business window).
- Commands: StartTypeIIObservationPeriod; AmendTypeIIObservationPeriod; ReviseFutureControlSchedule; GenerateDueControlOccurrences; RecordMissingOccurrence; EndObservationPeriod.
- Queries: GetObservationPeriod; ListObservationPeriods; GetControlCalendar; ListMissingOccurrences.
- Events: ObservationPeriodStarted; ObservationPeriodAmended; ObservationPeriodReserved; ObservationPeriodAmendmentReserved; ObservationPeriodAmendmentCompleted; ObservationPeriodAmendmentCancelled; ObservationPeriodReservationReleased; FutureControlScheduleRevised; DueControlOccurrencesGenerated; MissingOccurrenceRecorded; ObservationPeriodEnded.
- Reactors: ControlOccurrenceSchedulingReactor; MissingOccurrenceDetectionReactor.
- Projectors: ObservationPeriodDirectoryProjector; ControlCalendarProjector; OccurrenceCompletenessProjector.
- API family: periods, control calendar and occurrence completeness.

### Recurring governance — T2-06/09

- Aggregate/model candidates: GovernanceReview (control occurrence where accepted); ManagementReviewRecord; ReviewAction.
- Commands: ScheduleGovernanceReview; RecordManagementReview; RecordManagementReviewAction; CompleteManagementReviewAction.
- Queries: ListDueGovernanceReviews; GetManagementReview; ListManagementReviewActions.
- Events: GovernanceReviewScheduled; ManagementReviewRecorded; ManagementReviewActionRecorded; ManagementReviewActionCompleted.
- Reactors: GovernanceReviewSchedulingReactor.
- Projectors: GovernanceReviewWorkItemProjector; ManagementReviewDirectoryProjector.
- API family: governance review occurrences, management review records and actions.

### Changes and incidents — T2-07

- Aggregate/model candidates: SignificantChange; ComplianceIncident; ComplianceIncidentImpactAssessmentView.
- Commands: RecordSignificantChange; ReviseSignificantChange; AssessSignificantChange; RecordComplianceIncident; ReviseComplianceIncident; AssessComplianceIncident; RecordComplianceIncidentResolution; internal LinkImpactReviewWork; internal RecordSignificantChangeSystemDescriptionDecision; internal RecordComplianceIncidentSystemDescriptionDecision.
- Queries: GetSignificantChange; ListSignificantChanges; GetComplianceIncident; ListComplianceIncidents; GetComplianceImpactAssessment.
- Events: SignificantChangeRecorded; SignificantChangeRevised; SignificantChangeImpactAssessed; ImpactReviewWorkLinked; SignificantChangeSystemDescriptionDecisionRecorded; ComplianceIncidentRecorded; ComplianceIncidentRevised; ComplianceIncidentImpactAssessed; IncidentResolutionRecorded; ComplianceIncidentSystemDescriptionDecisionRecorded.
- Reactors: LinkImpactReviewWorkReactor; SystemDescriptionDecisionReactor. Incident approved-successor correlation remains incomplete because approved description content has no incident-ID field.
- Projectors: SignificantChangeDirectoryProjector; ComplianceIncidentDirectoryProjector.
- API family: native changes, native incidents, impact assessments and linked reviews.

### Optional collection — T2-08

- Aggregate/model candidates: IntegrationEndpoint; CollectionSchedule; CollectionRun; SourceCompletenessStatement.
- Commands: ConfigureIntegrationEndpoint; DisableIntegrationEndpoint; ConfigureCollectionSchedule; StartCollectionRun; CancelCollectionRun; RecordCollectionObservation; AcceptCollectionReconciliation.
- Queries: GetIntegrationEndpoint; ListIntegrationEndpoints; ListCollectionRuns; GetCollectionRun; PreviewCollectionReconciliation.
- Events: IntegrationEndpointConfigured; IntegrationEndpointDisabled; CollectionScheduleConfigured; CollectionRunStarted; CollectionObservationRecorded; CollectionRunCompleted; CollectionRunFailed; CollectionRunCanceled; CollectionReconciliationAccepted.
- Reactors: CollectionScheduleReactor; CollectionExecutionReactor; CollectionReconciliationReactor.
- Projectors: IntegrationEndpointProjector; CollectionRunProjector; SourceCompletenessProjector.
- API family: integration configuration, runs, observations and governed reconciliation.

### Period close and populations — T3-01/02

- Aggregate/model candidates: ObservationPeriodClose; AuditPopulationDefinition; AuditPopulation; AuditPopulationRow; PopulationReconciliation; PopulationAmendment.
- Commands: ProposeObservationPeriodClose; FreezeObservationPeriodClose; DefineAuditPopulation; RecordAuditPopulationFact; ClassifyAuditPopulationRow; ReconcileAuditPopulation; FreezeAuditPopulation; AmendAuditPopulation.
- Queries: PreviewObservationPeriodClose; GetObservationPeriodClose; ListAuditPopulationDefinitions; GetAuditPopulation; ListAuditPopulationRows; GetPopulationReconciliation.
- Events: ObservationPeriodCloseProposed; ObservationPeriodCloseFrozen; AuditPopulationDefined; AuditPopulationFactRecorded; AuditPopulationRowClassified; AuditPopulationReconciled; AuditPopulationFrozen; AuditPopulationAmended.
- Reactors: ObservationPeriodCloseAssemblyReactor; AuditPopulationBuildReactor.
- Projectors: ObservationPeriodCloseProjector; AuditPopulationDirectoryProjector; PopulationCompletenessProjector.
- API family: period closes, population definitions, immutable populations, rows and reconciliation.

### Continuity — T3-07

- Aggregate/model candidates: ProgramRollover; NextPeriodPlan; CarryForwardReference.
- Commands: ProposeProgramRollover; ApproveProgramRollover; StartNextObservationPeriod; CarryForwardOpenRemediation.
- Queries: PreviewProgramRollover; GetProgramRollover; GetNextPeriodPlan; ListCarryForwardReferences.
- Events: ProgramRolloverProposed; ProgramRolloverApproved; NextObservationPeriodStarted; OpenRemediationCarriedForward.
- Reactors: ProgramRolloverReactor.
- Projectors: ProgramRolloverProjector; NextPeriodPlanProjector.
- API family: rollover proposals, approvals, successor periods and carry-forward links.

### Firm portfolio and staffing — F1-02/03/05

- Aggregate/model candidates: AuthorizedClientSummary; StaffCapacityPlan; StaffAssignment; CrossClientWorkPage.
- Commands: DefineStaffCapacity; ProposeStaffAssignment; AcceptStaffAssignment; EndStaffAssignment.
- Queries: ListAuthorizedClientPortfolio; GetAuthorizedClientSummary; ListStaffCapacity; PreviewStaffAssignmentConflicts; ListMyCrossClientWork.
- Events: StaffCapacityDefined; StaffAssignmentProposed; StaffAssignmentAccepted; StaffAssignmentEnded.
- Reactors: StaffAssignmentIndependenceReevaluationReactor.
- Projectors: StaffCapacityProjector; tenant-local PortfolioSummaryProjector; tenant-local PersonalWorkProjector.
- API family: explicitly authorized portfolio, capacity and personal work queries; no shared client-data cache by default.

### Methodology templates — F1-04

- Aggregate/model candidates: MethodologyTemplate; TemplateVersion; TemplateApplication; TemplateRecordProvenance.
- Commands: CreateMethodologyTemplate; ReviseMethodologyTemplateDraft; PublishMethodologyTemplateVersion; RetireMethodologyTemplate; ApplyMethodologyTemplate.
- Queries: ListMethodologyTemplates; GetMethodologyTemplateVersion; PreviewMethodologyTemplateApplication; GetTemplateApplication.
- Events: MethodologyTemplateCreated; MethodologyTemplateDraftRevised; MethodologyTemplateVersionPublished; MethodologyTemplateRetired; MethodologyTemplateApplied.
- Reactors: TemplateApplicationReactor.
- Projectors: MethodologyTemplateDirectoryProjector; TemplateApplicationProjector.
- API family: platform template library, versions, preview and tenant-specific application.

### Own-attest closure and offboarding — F1-01/07/08

- Aggregate/model candidates: OwnAttestRecordPackage; AttestRecordDeliveryReceipt; EngagementRetentionHold.
- Commands: PrepareOwnAttestRecordPackage; RecordOwnAttestRecordDelivery; VerifyOwnAttestRecordDelivery; ReleaseDeliveredEngagementHold.
- Queries: GetOwnAttestRecordPackage; GetOwnAttestRecordDelivery; ListOutstandingAttestDeliveryHolds.
- Events: OwnAttestRecordPackagePrepared; OwnAttestRecordDeliveryRecorded; OwnAttestRecordDeliveryVerified; DeliveredEngagementHoldReleased.
- Reactors: OwnAttestRecordPackageReactor; AttestRecordDeliveryReactor.
- Projectors: OwnAttestDeliveryProjector; EngagementDeliveryHoldProjector.
- API family: required record packages and attributed delivery into attest software; no native workpapers or report drafting.

### Organization handover — BO-01

- Aggregate/model candidates: OrganizationManagementHandover; HandoverGrantPlan; HandoverAcceptance.
- Commands: ProposeOrganizationManagementHandover; AcceptOrganizationManagementHandover; CompleteOrganizationManagementHandover; CancelOrganizationManagementHandover.
- Queries: PreviewOrganizationManagementHandover; GetOrganizationManagementHandover; ListOrganizationAdministrators.
- Events: OrganizationManagementHandoverProposed; OrganizationManagementHandoverAccepted; OrganizationManagementHandoverCompleted; OrganizationManagementHandoverCanceled.
- Reactors: OrganizationHandoverGrantReconciliationReactor.
- Projectors: OrganizationManagementHandoverProjector.
- API family: handover, acceptance and explicit retained/revoked administrative grants.

### Hosted billing — BO-02

- Aggregate/model candidates: OrganizationSubscription; BillingAccount; BillingAdministratorGrant; InvoiceReference; PaymentObservation; BillingPolicyVersion.
- Commands: ConfigureOrganizationBilling; AssignBillingAdministrator; RevokeBillingAdministrator; StartOrganizationSubscription; ChangeOrganizationSubscription; CancelOrganizationSubscription; RecordBillingProviderObservation; ReconcileBillingCharges.
- Queries: GetOrganizationSubscription; GetOrganizationBilling; ListBillingAdministrators; ListOrganizationInvoices; GetBillingChargeHistory.
- Events: OrganizationBillingConfigured; BillingAdministratorAssigned; BillingAdministratorRevoked; OrganizationSubscriptionStarted; OrganizationSubscriptionChanged; OrganizationSubscriptionCanceled; BillingProviderObservationRecorded; BillingChargesReconciled.
- Reactors: SubscriptionProvisioningReactor; BillingProviderWebhookReactor; BillingChargeReconciliationReactor.
- Projectors: OrganizationSubscriptionProjector; BillingHistoryProjector; BillingAdministratorProjector.
- API family: organization-specific subscription, billing administration and charge history; exact lifecycle awaits policy approval.

### Add-ons — BO-03

- Aggregate/model candidates: AddonOffering; OfferingVersion; OrganizationAddonSelection; AddonEntitlement; AddonAcceptance.
- Commands: PublishAddonOffering; SelectOrganizationAddon; AcceptOrganizationAddonTerms; ActivateOrganizationAddon; CancelOrganizationAddon; ReconcileAddonEntitlements.
- Queries: ListAddonOfferings; PreviewOrganizationAddonCharges; ListOrganizationAddons; GetOrganizationEntitlements.
- Events: AddonOfferingPublished; OrganizationAddonSelected; OrganizationAddonTermsAccepted; OrganizationAddonActivated; OrganizationAddonCanceled; AddonEntitlementsReconciled.
- Reactors: AddonProvisioningReactor; AddonEntitlementReconciliationReactor.
- Projectors: AddonOfferingProjector; OrganizationAddonProjector; OrganizationEntitlementProjector.
- API family: offerings, selection, acceptance, billing effects and entitlement lifecycle.

## Cross-cutting requirements and unresolved design

- Reuse the existing version, actor, decision, scope, content-identity and snapshot primitives. Keep review and approval transitions with the workflow; do not introduce a universal Review aggregate.
- Every owning entity needs only the lifecycle operations its contract permits: draft creation/revision, review, approval, successor, retirement and never-used-draft deletion where applicable. Historical use prevents destructive removal.
- Decisions bind exact versions, input identities, actor, time and rationale. Readiness calculations and population completeness remain distinct from auditor conclusions.
- Preserve manual capture for evidence, workforce/access populations, operational facts, professional handoffs and all compliance outcomes. Collection and integrations remain optional.
- The canonical catalog's finer device, resource, network and provider facets are not an instruction to build a full CMDB. Add persistence and APIs when an approved story consumes them.
- Every read must apply tenant, permission, sensitive-field and independence rules before rows, counts, cursors, exports or derived results become visible.
- T2-07 records with `handling_class: restricted` are visible and editable only to the current assessor or a member with the dedicated restricted-read or restricted-write grant; built-in Org Admin and Compliance Lead roles receive both grants.
- Use versioned projector identities and atomic rows/checkpoints. Freshness applies per required source/projection; reproducible history requires retained exact inputs or snapshots.
- Coordinate cross-aggregate business flows through event choreography: reactors consume committed facts and issue idempotent commands to each owning aggregate. Keep aggregates within one consistency boundary and projectors read-only.
- Schedule time-driven checks through an explicit clock/tick/job contract. Ordinary event consumption alone does not detect elapsed deadlines, expiry or missing occurrences. Reminders, waiver expiry, acceptance expiry, retention eligibility and recurring work each need an owning policy and safe scheduler.
- A reminder reactor must not silently approve work, expire a business decision, release a hold or dispose of content. Those transitions require their owning commands and authorization.
- Reactor follow-up commands and external effects need stable IDs, durable progress, retry/recovery and duplicate suppression. Distinguish committed intent from observed provider completion.
- Commercial command names and events remain provisional until pricing, payer, payment, lifecycle, handover and add-on acceptance policies are settled in business-operations.md.
- Portfolio queries require an explicit cross-client authority contract and independent per-client filtering. Existing tenant-local projections do not authorize shared cross-client storage.
- Offboarding retention duration, exact auditor deliverables, provider adapters and additional Privacy lifecycle coverage remain governed follow-up scope. Do not fill these gaps with invented policy.

## Exact existing backend inventory

The following is a static source inventory, including internal operations. Queries are classified by mapped HTTP GET operations and read/preview name conventions; remaining IRequest contracts are listed as commands. This is a planning classification, not an automated proof of purity. In particular, exports, previews and authentication POST operations require semantic review before exposure to automation. Supporting models include enums and interfaces in Compliance.Common; they are not all persistent business entities.

| Category | Unique feature/type names |
| --- | ---: |
| Commands | 238 |
| Queries | 272 |
| Events | 404 |
| Aggregates | 99 |
| Models and supporting contracts | 569 |
| Reactors | 33 |
| Projectors | 78 |
| Explicit MapPortia HTTP route registrations | 493 |
| Additional custom HTTP route registrations | 1 |

Reconciled 2026-10-19. All 404 declared event types now have a recorded requirement mapping or explicit exclusion in the [event source reconciliation](domain-event-list.md#source-declaration-reconciliation). The HTTP route table below mirrors the typed registrations in `Program.cs`; it does not imply that every command has or should have an HTTP route.

The aggregate count and source list were reconciled on 2026-10-21 against the
[aggregate and command-handler audit](aggregate-command-coverage-audit.md).

### Existing commands

#### AccessControl

- [AcknowledgeEngagementManagement](../../src/Compliance.Common/Features/AccessControl/Commands/AcknowledgeEngagementManagement.cs#L6) — `bdgrz.service-engagement.management-acknowledge` v1
- [AmendServiceEngagement](../../src/Compliance.Common/Features/AccessControl/Commands/AmendServiceEngagement.cs#L6) — `bdgrz.service-engagement.amend` v1
- [ApproveSeparationOfDutiesWaiver](../../src/Compliance.Common/Features/AccessControl/Commands/ApproveSeparationOfDutiesWaiver.cs#L6) — `bdgrz.sod-waiver.approve` v1
- [AssignRolePermission](../../src/Compliance.Common/Features/AccessControl/Commands/AssignRolePermission.cs#L9) — `bdgrz.rbac.role-permission.assign` v1
- [AssignTeamMember](../../src/Compliance.Common/Features/AccessControl/Commands/AssignTeamMember.cs#L7) — `bdgrz.rbac.team-member.assign` v1
- [AssignTeamRole](../../src/Compliance.Common/Features/AccessControl/Commands/AssignTeamRole.cs#L7) — `bdgrz.rbac.team-role.assign` v1
- [CloseServiceEngagement](../../src/Compliance.Common/Features/AccessControl/Commands/CloseServiceEngagement.cs#L6) — `bdgrz.service-engagement.close` v1
- [CreateServiceEngagement](../../src/Compliance.Common/Features/AccessControl/Commands/CreateServiceEngagement.cs#L6) — `bdgrz.service-engagement.create` v1
- [DefineRole](../../src/Compliance.Common/Features/AccessControl/Commands/DefineRole.cs#L7) — `bdgrz.rbac.role.define` v1
- [DefineTeam](../../src/Compliance.Common/Features/AccessControl/Commands/DefineTeam.cs#L7) — `bdgrz.rbac.team.define` v1
- [DeleteRole](../../src/Compliance.Common/Features/AccessControl/Commands/DeleteRole.cs#L7) — `bdgrz.rbac.role.delete` v1
- [DeleteTeam](../../src/Compliance.Common/Features/AccessControl/Commands/DeleteTeam.cs#L7) — `bdgrz.rbac.team.delete` v1
- [DeprovisionMember](../../src/Compliance.Common/Features/AccessControl/Commands/DeprovisionMember.cs#L6) — `bdgrz.tenant.member.deprovision` v1
- [EvaluateClientIndependence](../../src/Compliance.Common/Features/AccessControl/Commands/EvaluateClientIndependence.cs#L7) — `bdgrz.independence.evaluate` v1
- [GrantAccess](../../src/Compliance.Common/Features/AccessControl/Commands/GrantAccess.cs#L6) — `bdgrz.access-grant.issue` v1
- [ProvisionGuestProgramAccess](../../src/Compliance.Common/Features/AccessControl/Commands/ProvisionGuestProgramAccess.cs#L7) — `bdgrz.access-grant.guest-invitation.provision` v1 (internal reaction)
- [ProposeServiceEngagementStaff](../../src/Compliance.Common/Features/AccessControl/Commands/ProposeServiceEngagementStaff.cs#L6) — `bdgrz.service-engagement.staff.propose` v1
- [RecordDirectoryIndependenceReevaluation](../../src/Compliance.Core/Features/AccessControl/RecordDirectoryIndependenceReevaluation.cs#L7) — `bdgrz.independence.record-directory-reevaluation` v1
- [RecordNonattestService](../../src/Compliance.Common/Features/AccessControl/Commands/RecordNonattestService.cs#L6) — `bdgrz.independence.service.record` v1
- [RecordSeparationOfDutiesWaiver](../../src/Compliance.Common/Features/AccessControl/Commands/RecordSeparationOfDutiesWaiver.cs#L6) — `bdgrz.sod-waiver.record` v1
- [RegisterFirmStaff](../../src/Compliance.Common/Features/AccessControl/Commands/RegisterFirmStaff.cs#L6) — `bdgrz.firm-staff.register` v1
- [RegisterMember](../../src/Compliance.Common/Features/AccessControl/Commands/RegisterMember.cs#L6) — `bdgrz.rbac.member.register` v1
- [ReinstateMember](../../src/Compliance.Common/Features/AccessControl/Commands/ReinstateMember.cs#L6) — `bdgrz.tenant.member.reinstate` v1
- [RemoveRolePermission](../../src/Compliance.Common/Features/AccessControl/Commands/RemoveRolePermission.cs#L10) — `bdgrz.rbac.role-permission.remove` v1
- [RemoveTeamMember](../../src/Compliance.Common/Features/AccessControl/Commands/RemoveTeamMember.cs#L7) — `bdgrz.rbac.team-member.remove` v1
- [RemoveTeamRole](../../src/Compliance.Common/Features/AccessControl/Commands/RemoveTeamRole.cs#L7) — `bdgrz.rbac.team-role.remove` v1
- [RenameRole](../../src/Compliance.Common/Features/AccessControl/Commands/RenameRole.cs#L6) — `bdgrz.rbac.role.rename` v1
- [ReviseIndependenceRules](../../src/Compliance.Common/Features/AccessControl/Commands/ReviseIndependenceRules.cs#L6) — `bdgrz.independence.rules.revise` v1
- [RevokeAccessGrant](../../src/Compliance.Common/Features/AccessControl/Commands/RevokeAccessGrant.cs#L6) — `bdgrz.access-grant.revoke` v1
- [RevokeServiceEngagementActualStaff](../../src/Compliance.Common/Features/AccessControl/Commands/RevokeServiceEngagementActualStaff.cs#L5) — `bdgrz.service-engagement.actual-staff.revoke` v1
- [SetFirmStaffStatus](../../src/Compliance.Common/Features/AccessControl/Commands/SetFirmStaffStatus.cs#L6) — `bdgrz.firm-staff.status.set` v1
- [SuspendMember](../../src/Compliance.Common/Features/AccessControl/Commands/SuspendMember.cs#L6) — `bdgrz.tenant.member.suspend` v1
- [WithdrawServiceEngagementStaffProposal](../../src/Compliance.Common/Features/AccessControl/Commands/WithdrawServiceEngagementStaffProposal.cs#L6) — `bdgrz.service-engagement.staff.withdraw` v1

#### AdvisorCollaboration

- [CloseAdvisorHandoff](../../src/Compliance.Common/Features/AdvisorCollaboration/Commands/CloseAdvisorHandoff.cs#L7) — `bdgrz.advisor-collaboration.close-advisor-handoff` v1
- [CorrectAdvisorFeedback](../../src/Compliance.Common/Features/AdvisorCollaboration/Commands/CorrectAdvisorFeedback.cs#L7) — `bdgrz.advisor-collaboration.correct-advisor-feedback` v1
- [CreateAdvisorHandoff](../../src/Compliance.Common/Features/AdvisorCollaboration/Commands/CreateAdvisorHandoff.cs#L7) — `bdgrz.advisor-collaboration.create-advisor-handoff` v1
- [MarkAdvisorHandoffReadyForReview](../../src/Compliance.Common/Features/AdvisorCollaboration/Commands/MarkAdvisorHandoffReadyForReview.cs#L7) — `bdgrz.advisor-collaboration.mark-advisor-handoff-ready-for-review` v1
- [RecordAdvisorFeedback](../../src/Compliance.Common/Features/AdvisorCollaboration/Commands/RecordAdvisorFeedback.cs#L7) — `bdgrz.advisor-collaboration.record-advisor-feedback` v1
- [ResolveAdvisorFeedback](../../src/Compliance.Common/Features/AdvisorCollaboration/Commands/ResolveAdvisorFeedback.cs#L9) — `bdgrz.advisor-collaboration.resolve-advisor-feedback` v1
- [RespondToAdvisorFeedback](../../src/Compliance.Common/Features/AdvisorCollaboration/Commands/RespondToAdvisorFeedback.cs#L8) — `bdgrz.advisor-collaboration.respond-to-advisor-feedback` v1
- [ReviseAdvisorHandoffSelection](../../src/Compliance.Common/Features/AdvisorCollaboration/Commands/ReviseAdvisorHandoffSelection.cs#L7) — `bdgrz.advisor-collaboration.revise-advisor-handoff-selection` v1
- [RevokeAdvisorHandoff](../../src/Compliance.Common/Features/AdvisorCollaboration/Commands/RevokeAdvisorHandoff.cs#L7) — `bdgrz.advisor-collaboration.revoke-advisor-handoff` v1

#### AccessReviews

- [AcceptAccessPopulation](../../src/Compliance.Common/Features/AccessReviews/Commands/AcceptAccessPopulation.cs#L10) — `bdgrz.access_population.accept` v1
- [ApproveAccessExpectation](../../src/Compliance.Common/Features/AccessReviews/Commands/ApproveAccessExpectation.cs#L10) — `bdgrz.access_expectation.approve` v1
- [ClassifyAccessPrincipal](../../src/Compliance.Common/Features/AccessReviews/Commands/ClassifyAccessPrincipal.cs#L7) — `bdgrz.access_population.principal.classify` v1
- [CompleteAccessReviewCampaign](../../src/Compliance.Common/Features/AccessReviews/Commands/CompleteAccessReviewCampaign.cs#L11) — `bdgrz.access_review.campaign.complete` v1
- [ExemptAccessExpectation](../../src/Compliance.Common/Features/AccessReviews/Commands/ExemptAccessExpectation.cs#L10) — `bdgrz.access_expectation.exception.record` v1
- [ExemptAccessRemediation](../../src/Compliance.Common/Features/AccessReviews/Commands/ExemptAccessRemediation.cs#L10) — `bdgrz.access_review.remediation.exception.record` v1
- [ExemptMissingAccessPopulation](../../src/Compliance.Common/Features/AccessReviews/Commands/ExemptMissingAccessPopulation.cs#L10) — `bdgrz.access_population.exception.record` v1
- [LaunchAccessReviewCampaign](../../src/Compliance.Common/Features/AccessReviews/Commands/LaunchAccessReviewCampaign.cs#L10) — `bdgrz.access_review.campaign.launch` v1
- [OpenAccessPopulation](../../src/Compliance.Common/Features/AccessReviews/Commands/OpenAccessPopulation.cs#L7) — `bdgrz.access_population.open` v1
- [ProposeAccessExpectation](../../src/Compliance.Common/Features/AccessReviews/Commands/ProposeAccessExpectation.cs#L7) — `bdgrz.access_expectation.propose` v1
- [RecordAccessDecision](../../src/Compliance.Common/Features/AccessReviews/Commands/RecordAccessDecision.cs#L10) — `bdgrz.access_review.decision.record` v1
- [RecordAccessPopulationFacts](../../src/Compliance.Common/Features/AccessReviews/Commands/RecordAccessPopulationFacts.cs#L7) — `bdgrz.access_population.facts.record` v1
- [RecordAccessRemediationChange](../../src/Compliance.Common/Features/AccessReviews/Commands/RecordAccessRemediationChange.cs#L7) — `bdgrz.access_review.remediation.change.record` v1
- [RecordBulkAccessDecision](../../src/Compliance.Common/Features/AccessReviews/Commands/RecordBulkAccessDecision.cs#L10) — `bdgrz.access_review.decision.bulk_record` v1
- [VerifyAccessRemediation](../../src/Compliance.Common/Features/AccessReviews/Commands/VerifyAccessRemediation.cs#L7) — `bdgrz.access_review.remediation.verify` v1

#### Applications

- [AcceptApplicationImport](../../src/Compliance.Common/Features/Applications/Commands/AcceptApplicationImport.cs#L6) — `bdgrz.application_import.accept` v1
- [AddApplicationRelationship](../../src/Compliance.Common/Features/Applications/Commands/AddApplicationRelationship.cs#L6) — `bdgrz.application.relationship.add` v1
- [ApplyApplicationImportEffect](../../src/Compliance.Common/Features/Applications/Commands/ApplyApplicationImportEffect.cs#L7) — `bdgrz.application_import.apply_effect` v1
- [CancelApplicationImport](../../src/Compliance.Common/Features/Applications/Commands/CancelApplicationImport.cs#L6) — `bdgrz.application_import.cancel` v1
- [CorrelateApplicationImportRow](../../src/Compliance.Common/Features/Applications/Commands/CorrelateApplicationImportRow.cs#L6) — `bdgrz.application_import.correlate_row` v1
- [DecideAccessReviewScope](../../src/Compliance.Common/Features/Applications/Commands/DecideAccessReviewScope.cs#L10) — `bdgrz.system_instance.access_review_scope.decide` v1
- [DeclareApplication](../../src/Compliance.Common/Features/Applications/Commands/DeclareApplication.cs#L6) — `bdgrz.application.declare` v1
- [DeclareSystemInstance](../../src/Compliance.Common/Features/Applications/Commands/DeclareSystemInstance.cs#L6) — `bdgrz.system_instance.declare` v1
- [ExecuteApplicationImport](../../src/Compliance.Common/Features/Applications/Commands/ExecuteApplicationImport.cs#L7) — `bdgrz.application_import.execute` v1
- [FreezeApplicationImportOmissionProposal](../../src/Compliance.Common/Features/Applications/Commands/FreezeApplicationImportOmissionProposal.cs#L7) — `bdgrz.application_import.omission.freeze` v1
- [RemoveApplicationRelationship](../../src/Compliance.Common/Features/Applications/Commands/RemoveApplicationRelationship.cs#L6) — `bdgrz.application.relationship.remove` v1
- [RetireApplication](../../src/Compliance.Common/Features/Applications/Commands/RetireApplication.cs#L6) — `bdgrz.application.retire` v1
- [RetireSystemInstance](../../src/Compliance.Common/Features/Applications/Commands/RetireSystemInstance.cs#L6) — `bdgrz.system_instance.retire` v1
- [ReviseApplication](../../src/Compliance.Common/Features/Applications/Commands/ReviseApplication.cs#L7) — `bdgrz.application.revise` v1
- [ReviseApplicationMcpInput](../../src/Compliance.App/Features/Applications/ReviseApplicationMcpInput.cs#L7) — `bdgrz.application.revise.mcp-input` v1
- [StageApplicationImport](../../src/Compliance.Common/Features/Applications/Commands/StageApplicationImport.cs#L6) — `bdgrz.application_import.stage` v1

#### AuditPackages

- [CreateAuditPackage](../../src/Compliance.Common/Features/AuditPackages/Commands/CreateAuditPackage.cs#L9) — `bdgrz.audit-packages.create-audit-package` v1

#### Boundaries

- [ApproveBoundary](../../src/Compliance.Common/Features/Boundaries/Commands/ApproveBoundary.cs#L7) — `bdgrz.boundary.approve` v1
- [CreateBoundary](../../src/Compliance.Common/Features/Boundaries/Commands/CreateBoundary.cs#L8) — `bdgrz.boundary.create` v1
- [DiscardBoundaryDraft](../../src/Compliance.Common/Features/Boundaries/Commands/DiscardBoundaryDraft.cs#L7) — `bdgrz.boundary.draft.discard` v1
- [ProposeBoundarySuccessor](../../src/Compliance.Common/Features/Boundaries/Commands/ProposeBoundarySuccessor.cs#L7) — `bdgrz.boundary.successor.propose` v1
- [ReviewBoundary](../../src/Compliance.Common/Features/Boundaries/Commands/ReviewBoundary.cs#L7) — `bdgrz.boundary.review` v1
- [ReviseBoundaryDraft](../../src/Compliance.Common/Features/Boundaries/Commands/ReviseBoundaryDraft.cs#L7) — `bdgrz.boundary.draft.revise` v1

#### Changes

- [AssessSignificantChange](../../src/Compliance.Common/Features/Changes/Commands/AssessSignificantChange.cs#L8) — `bdgrz.changes.assess-significant-change` v2
- [RecordSignificantChange](../../src/Compliance.Common/Features/Changes/Commands/RecordSignificantChange.cs#L8) — `bdgrz.changes.record-significant-change` v2
- [ReviseSignificantChange](../../src/Compliance.Common/Features/Changes/Commands/ReviseSignificantChange.cs#L8) — `bdgrz.changes.revise-significant-change` v2
- [LinkImpactReviewWork](../../src/Compliance.Common/Features/Changes/Commands/LinkImpactReviewWork.cs#L6) — `bdgrz.changes.link-impact-review-work` v1 (internal reaction)
- [RecordSignificantChangeSystemDescriptionDecision](../../src/Compliance.Common/Features/Changes/Commands/RecordSignificantChangeSystemDescriptionDecision.cs#L6) — `bdgrz.changes.record-system-description-decision` v2 (internal reaction)

#### Commitments

- [ApproveCommitmentDraft](../../src/Compliance.Common/Features/Commitments/Commands/ApproveCommitmentDraft.cs#L12) — `bdgrz.commitment.draft.approve` v1
- [CreateCommitmentDraft](../../src/Compliance.Common/Features/Commitments/Commands/CreateCommitmentDraft.cs#L8) — `bdgrz.commitment.draft.create` v1
- [ReviewCommitmentDraft](../../src/Compliance.Common/Features/Commitments/Commands/ReviewCommitmentDraft.cs#L13) — `bdgrz.commitment.draft.review` v1
- [ReviseCommitmentDraft](../../src/Compliance.Common/Features/Commitments/Commands/ReviseCommitmentDraft.cs#L8) — `bdgrz.commitment.draft.revise` v1

#### ControlMappings

- [ProposeControlCriterionMapping](../../src/Compliance.Common/Features/ControlMappings/Commands/ProposeControlCriterionMapping.cs#L12) — `bdgrz.control_mapping.propose` v1
- [ProposeCriterionNotApplicable](../../src/Compliance.Common/Features/ControlMappings/Commands/ProposeCriterionNotApplicable.cs#L13) — `bdgrz.criterion_applicability.propose_not_applicable` v1
- [RetireControlCriterionMapping](../../src/Compliance.Common/Features/ControlMappings/Commands/RetireControlCriterionMapping.cs#L9) — `bdgrz.control_mapping.retire` v1
- [ReviewControlCriterionMapping](../../src/Compliance.Common/Features/ControlMappings/Commands/ReviewControlCriterionMapping.cs#L9) — `bdgrz.control_mapping.review` v1
- [ReviewCriterionApplicability](../../src/Compliance.Common/Features/ControlMappings/Commands/ReviewCriterionApplicability.cs#L13) — `bdgrz.criterion_applicability.review` v1
- [WithdrawCriterionNotApplicable](../../src/Compliance.Common/Features/ControlMappings/Commands/WithdrawCriterionNotApplicable.cs#L9) — `bdgrz.criterion_applicability.withdraw_not_applicable` v1

#### Controls

- [ApproveControl](../../src/Compliance.Common/Features/Controls/Commands/ApproveControl.cs#L8) — `bdgrz.control.approve` v1
- [CreateControlDraft](../../src/Compliance.Common/Features/Controls/Commands/CreateControlDraft.cs#L8) — `bdgrz.control.draft.create` v1
- [DesignateControlOwnerPerson](../../src/Compliance.Common/Features/Controls/Commands/DesignateControlOwnerPerson.cs#L13) — `bdgrz.control.owner_person.designate` v1
- [DiscardControlDraft](../../src/Compliance.Common/Features/Controls/Commands/DiscardControlDraft.cs#L8) — `bdgrz.control.draft.discard` v1
- [ProposeControlRetirement](../../src/Compliance.Common/Features/Controls/Commands/ProposeControlRetirement.cs#L8) — `bdgrz.control.retirement.propose` v1
- [ProposeControlSuccessor](../../src/Compliance.Common/Features/Controls/Commands/ProposeControlSuccessor.cs#L8) — `bdgrz.control.successor.propose` v1
- [RetireControl](../../src/Compliance.Common/Features/Controls/Commands/RetireControl.cs#L8) — `bdgrz.control.retire` v1
- [ReviewControl](../../src/Compliance.Common/Features/Controls/Commands/ReviewControl.cs#L8) — `bdgrz.control.review` v1
- [ReviseControlDraft](../../src/Compliance.Common/Features/Controls/Commands/ReviseControlDraft.cs#L8) — `bdgrz.control.draft.revise` v1
- [WithdrawControlProposal](../../src/Compliance.Common/Features/Controls/Commands/WithdrawControlProposal.cs#L12) — `bdgrz.control.proposal.withdraw` v1

#### Criteria

- [SetCriteriaTextOverlay](../../src/Compliance.Common/Features/Criteria/Commands/SetCriteriaTextOverlay.cs#L9) — `bdgrz.criteria.overlay.set` v1

#### Evaluations

- [DefineControlEvaluationPlan](../../src/Compliance.Common/Features/Evaluations/Commands/DefineControlEvaluationPlan.cs#L8) — `bdgrz.control.evaluation.plan.define` v1
- [DisposeControlEvaluationDeviation](../../src/Compliance.Common/Features/Evaluations/Commands/DisposeControlEvaluationDeviation.cs#L9) — `bdgrz.control.evaluation.deviation.dispose` v1
- [RaiseEvaluationDeviationFinding](../../src/Compliance.Common/Features/Evaluations/Commands/RaiseEvaluationDeviationFinding.cs#L11) — `bdgrz.finding.raise_from_evaluation_deviation` v1
- [RecordControlEvaluationStep](../../src/Compliance.Common/Features/Evaluations/Commands/RecordControlEvaluationStep.cs#L12) — `bdgrz.control.evaluation.step.record` v1
- [ReviewControlEvaluation](../../src/Compliance.Common/Features/Evaluations/Commands/ReviewControlEvaluation.cs#L12) — `bdgrz.control.evaluation.review` v1
- [StartControlEvaluation](../../src/Compliance.Common/Features/Evaluations/Commands/StartControlEvaluation.cs#L12) — `bdgrz.control.evaluation.start` v1
- [SubmitControlEvaluation](../../src/Compliance.Common/Features/Evaluations/Commands/SubmitControlEvaluation.cs#L12) — `bdgrz.control.evaluation.submit` v1

#### Incidents

- [AssessComplianceIncident](../../src/Compliance.Common/Features/Incidents/Commands/AssessComplianceIncident.cs#L8) — `bdgrz.incidents.assess-compliance-incident` v2
- [RecordComplianceIncident](../../src/Compliance.Common/Features/Incidents/Commands/RecordComplianceIncident.cs#L8) — `bdgrz.incidents.record-compliance-incident` v2
- [RecordComplianceIncidentResolution](../../src/Compliance.Common/Features/Incidents/Commands/RecordComplianceIncidentResolution.cs#L8) — `bdgrz.incidents.record-compliance-incident-resolution` v2
- [ReviseComplianceIncident](../../src/Compliance.Common/Features/Incidents/Commands/ReviseComplianceIncident.cs#L8) — `bdgrz.incidents.revise-compliance-incident` v2
- [RecordComplianceIncidentSystemDescriptionDecision](../../src/Compliance.Common/Features/Incidents/Commands/RecordComplianceIncidentSystemDescriptionDecision.cs#L7) — `bdgrz.incidents.record-system-description-decision` v2 (internal reaction)

#### Evidence

- [ApproveEvidenceRedaction](../../src/Compliance.Common/Features/Evidence/Commands/ApproveEvidenceRedaction.cs#L7) — `bdgrz.evidence.redaction.approve` v1
- [CancelEvidenceRequest](../../src/Compliance.Common/Features/Evidence/Commands/CancelEvidenceRequest.cs#L9) — `bdgrz.evidence.request.cancel` v1
- [FulfilEvidenceRequest](../../src/Compliance.Common/Features/Evidence/Commands/FulfilEvidenceRequest.cs#L10) — `bdgrz.evidence.request.fulfil` v1
- [OpenEvidenceRequest](../../src/Compliance.Common/Features/Evidence/Commands/OpenEvidenceRequest.cs#L9) — `bdgrz.evidence.request.open` v1
- [PrepareEvidenceRedaction](../../src/Compliance.Common/Features/Evidence/Commands/PrepareEvidenceRedaction.cs#L6) — `bdgrz.evidence.redaction.prepare` v1

#### Examinations

- [AmendTypeIBaseline](../../src/Compliance.Common/Features/Examinations/Commands/AmendTypeIBaseline.cs#L9) — `bdgrz.examinations.amend-type-i-baseline` v1
- [CreateExaminationEngagement](../../src/Compliance.Common/Features/Examinations/Commands/CreateExaminationEngagement.cs#L9) — `bdgrz.examinations.create-examination-engagement` v1
- [FreezeTypeIBaseline](../../src/Compliance.Common/Features/Examinations/Commands/FreezeTypeIBaseline.cs#L9) — `bdgrz.examinations.freeze-type-i-baseline` v1
- [ReviseExaminationEngagement](../../src/Compliance.Common/Features/Examinations/Commands/ReviseExaminationEngagement.cs#L9) — `bdgrz.examinations.revise-examination-engagement` v1
- [RecordManagementAssertion](../../src/Compliance.Common/Features/Examinations/Commands/RecordManagementAssertion.cs#L8) — `bdgrz.examinations.record-management-assertion` v2; [handler](../../src/Compliance.Core/Features/Examinations/Handlers/RecordManagementAssertionHandler.cs#L10) binds assertion wording to exact validated package and approved examination inputs
- [RecordManagementRepresentationLetter](../../src/Compliance.Common/Features/Examinations/Commands/RecordManagementRepresentationLetter.cs#L6) — `bdgrz.examinations.record-management-representation-letter` v2

#### Operations

- [ApproveControlOperatingPlan](../../src/Compliance.Common/Features/Operations/Commands/ApproveControlOperatingPlan.cs#L12) — `bdgrz.control.operating_plan.approve` v1
- [AttestControlOccurrence](../../src/Compliance.Common/Features/Operations/Commands/AttestControlOccurrence.cs#L11) — `bdgrz.control.occurrence.attest` v1
- [CorrectControlAttestation](../../src/Compliance.Common/Features/Operations/Commands/CorrectControlAttestation.cs#L8) — `bdgrz.control.occurrence.correct` v1
- [OpenControlOccurrence](../../src/Compliance.Common/Features/Operations/Commands/OpenControlOccurrence.cs#L8) — `bdgrz.control.occurrence.open` v1
- [ProposeControlOperatingPlan](../../src/Compliance.Common/Features/Operations/Commands/ProposeControlOperatingPlan.cs#L12) — `bdgrz.control.operating_plan.propose` v1
- [ReviewControlOccurrence](../../src/Compliance.Common/Features/Operations/Commands/ReviewControlOccurrence.cs#L11) — `bdgrz.control.occurrence.review` v1

#### Policies

- [ApprovePolicy](../../src/Compliance.Common/Features/Policies/Commands/ApprovePolicy.cs#L12) — `bdgrz.policy.approve` v1
- [ApprovePolicyRetirement](../../src/Compliance.Common/Features/Policies/Commands/ApprovePolicyRetirement.cs#L9) — `bdgrz.policy.retire` v1
- [ConfirmPolicyReview](../../src/Compliance.Common/Features/Policies/Commands/ConfirmPolicyReview.cs#L12) — `bdgrz.policy.periodic_review.confirm` v1
- [CreatePolicyDraft](../../src/Compliance.Common/Features/Policies/Commands/CreatePolicyDraft.cs#L8) — `bdgrz.policy.draft.create` v1
- [DiscardPolicyDraft](../../src/Compliance.Common/Features/Policies/Commands/DiscardPolicyDraft.cs#L9) — `bdgrz.policy.draft.discard` v1
- [ProposePolicyRetirement](../../src/Compliance.Common/Features/Policies/Commands/ProposePolicyRetirement.cs#L8) — `bdgrz.policy.retirement.propose` v1
- [ProposePolicySuccessor](../../src/Compliance.Common/Features/Policies/Commands/ProposePolicySuccessor.cs#L9) — `bdgrz.policy.successor.propose` v1
- [ReviewPolicyDraft](../../src/Compliance.Common/Features/Policies/Commands/ReviewPolicyDraft.cs#L12) — `bdgrz.policy.review` v1
- [RevisePolicyDraft](../../src/Compliance.Common/Features/Policies/Commands/RevisePolicyDraft.cs#L8) — `bdgrz.policy.draft.revise` v1

#### PolicyDistribution

- [AcknowledgePolicy](../../src/Compliance.Common/Features/PolicyDistribution/Queries/AcknowledgePolicy.cs#L12) — `bdgrz.policy.acknowledge` v1
- [ApproveCampaignWaiver](../../src/Compliance.Common/Features/PolicyDistribution/Commands/ApproveCampaignWaiver.cs#L12) — `bdgrz.campaign.waiver.approve` v1
- [CloseCampaign](../../src/Compliance.Common/Features/PolicyDistribution/Commands/CloseCampaign.cs#L9) — `bdgrz.campaign.close` v1
- [DefineTrainingRequirement](../../src/Compliance.Common/Features/PolicyDistribution/Commands/DefineTrainingRequirement.cs#L8) — `bdgrz.training_requirement.define` v1
- [LaunchPolicyCampaign](../../src/Compliance.Common/Features/PolicyDistribution/Commands/LaunchPolicyCampaign.cs#L12) — `bdgrz.policy_campaign.launch` v1
- [LaunchTrainingCampaign](../../src/Compliance.Common/Features/PolicyDistribution/Commands/LaunchTrainingCampaign.cs#L8) — `bdgrz.training_campaign.launch` v1
- [ReconcileCampaignAudience](../../src/Compliance.Common/Features/PolicyDistribution/Commands/ReconcileCampaignAudience.cs#L12) — `bdgrz.campaign.audience.reconcile` v1
- [RecordTrainingCompletion](../../src/Compliance.Common/Features/PolicyDistribution/Commands/RecordTrainingCompletion.cs#L9) — `bdgrz.training_completion.record` v1
- [ReviseTrainingRequirement](../../src/Compliance.Common/Features/PolicyDistribution/Commands/ReviseTrainingRequirement.cs#L9) — `bdgrz.training_requirement.revise` v1

#### Programs

- [CreateClientService](../../src/Compliance.Common/Features/Programs/Commands/CreateClientService.cs#L7) — `bdgrz.client-service.create` v2
- [CreateProgram](../../src/Compliance.Common/Features/Programs/Commands/CreateProgram.cs#L7) — `bdgrz.program.create` v1
- [ProposeProgramRollover](../../src/Compliance.Common/Features/Programs/Commands/ProposeProgramRollover.cs#L7) — `bdgrz.programs.propose-program-rollover` v2; [handler](../../src/Compliance.Core/Features/Programs/Handlers/ProposeProgramRolloverHandler.cs#L8) binds full rollover content to the exact closed-period snapshot and auditor outcome
- [ApproveProgramRollover](../../src/Compliance.Common/Features/Programs/Commands/ApproveProgramRollover.cs#L7) — `bdgrz.programs.approve-program-rollover` v1
- [RetireClientService](../../src/Compliance.Common/Features/Programs/Commands/RetireClientService.cs#L7) — `bdgrz.client-service.retire` v1
- [ReviseClientService](../../src/Compliance.Common/Features/Programs/Commands/ReviseClientService.cs#L7) — `bdgrz.client-service.revise` v1
- [ReviseProgram](../../src/Compliance.Common/Features/Programs/Commands/ReviseProgram.cs#L7) — `bdgrz.program.revise` v1
- [SelectProgramCriteriaEdition](../../src/Compliance.Common/Features/Programs/Commands/SelectProgramCriteriaEdition.cs#L7) — `bdgrz.program.criteria.select` v1

#### Providers

- [CloseProviderCoverageGap](../../src/Compliance.Common/Features/Providers/Commands/CloseProviderCoverageGap.cs#L8) — `bdgrz.provider.coverage_gap.close` v1
- [LinkProviderCoverageGapRiskAcceptance](../../src/Compliance.Common/Features/Providers/Commands/LinkProviderCoverageGapRiskAcceptance.cs#L9) — `bdgrz.provider.coverage_gap.link_risk_acceptance` v1
- [RecordAssuranceReport](../../src/Compliance.Common/Features/Providers/Commands/RecordAssuranceReport.cs#L8) — `bdgrz.provider.assurance_report.record` v1
- [RecordProvider](../../src/Compliance.Common/Features/Providers/Commands/RecordProvider.cs#L8) — `bdgrz.provider.record` v1
- [RecordProviderCoverageGap](../../src/Compliance.Common/Features/Providers/Commands/RecordProviderCoverageGap.cs#L7) — `bdgrz.provider.coverage_gap.record` v1
- [RecordProviderReview](../../src/Compliance.Common/Features/Providers/Commands/RecordProviderReview.cs#L11) — `bdgrz.provider.review.record` v1
- [ReviseAssuranceReport](../../src/Compliance.Common/Features/Providers/Commands/ReviseAssuranceReport.cs#L8) — `bdgrz.provider.assurance_report.revise` v1
- [ReviseProvider](../../src/Compliance.Common/Features/Providers/Commands/ReviseProvider.cs#L8) — `bdgrz.provider.revise` v1

#### Readiness

- [AnnotateReadinessGap](../../src/Compliance.Common/Features/Readiness/Commands/AnnotateReadinessGap.cs#L9) — `bdgrz.readiness.gap.annotate` v1
- [DecideReadiness](../../src/Compliance.Common/Features/Readiness/Commands/DecideReadiness.cs#L12) — `bdgrz.readiness.decide` v1
- [DecideTypeIEntry](../../src/Compliance.Common/Features/Readiness/Commands/DecideTypeIEntry.cs#L13) — `bdgrz.readiness.type_i_entry.decide` v1
- [PlanReadinessGap](../../src/Compliance.Common/Features/Readiness/Commands/PlanReadinessGap.cs#L9) — `bdgrz.readiness.gap.plan` v1
- [RunReadinessAssessment](../../src/Compliance.Common/Features/Readiness/Commands/RunReadinessAssessment.cs#L12) — `bdgrz.readiness.assessment.run` v1

#### Remediation

- [AddCorrectiveAction](../../src/Compliance.Common/Features/Remediation/Commands/AddCorrectiveAction.cs#L8) — `bdgrz.finding.corrective_action.add` v1
- [CloseFinding](../../src/Compliance.Common/Features/Remediation/Commands/CloseFinding.cs#L14) — `bdgrz.finding.close` v1
- [CompleteCorrectiveAction](../../src/Compliance.Common/Features/Remediation/Commands/CompleteCorrectiveAction.cs#L9) — `bdgrz.finding.corrective_action.complete` v1
- [LinkFindingAcceptance](../../src/Compliance.Common/Features/Remediation/Commands/LinkFindingAcceptance.cs#L12) — `bdgrz.finding.acceptance.link` v1
- [RaiseFinding](../../src/Compliance.Common/Features/Remediation/Commands/RaiseFinding.cs#L9) — `bdgrz.finding.raise` v1
- [RaiseOccurrenceFinding](../../src/Compliance.Common/Features/Remediation/Commands/RaiseOccurrenceFinding.cs#L11) — `bdgrz.finding.raise_from_occurrence` v1
- [ReopenFinding](../../src/Compliance.Common/Features/Remediation/Commands/ReopenFinding.cs#L8) — `bdgrz.finding.reopen` v1
- [ReviseFinding](../../src/Compliance.Common/Features/Remediation/Commands/ReviseFinding.cs#L9) — `bdgrz.finding.revise` v1

#### Responsibilities

- [AssignResponsibility](../../src/Compliance.Common/Features/Responsibilities/Commands/AssignResponsibility.cs#L8) — `bdgrz.responsibility.assign` v1
- [RevokeResponsibility](../../src/Compliance.Common/Features/Responsibilities/Commands/RevokeResponsibility.cs#L8) — `bdgrz.responsibility.revoke` v1

#### Retention

- [PlaceArtifactLegalHold](../../src/Compliance.Common/Features/Retention/Commands/PlaceArtifactLegalHold.cs#L7) — `bdgrz.artifact_retention.legal_hold.place` v1
- [RecordArtifactRetentionBasis](../../src/Compliance.Common/Features/Retention/Commands/RecordArtifactRetentionBasis.cs#L7) — `bdgrz.artifact_retention.basis.record` v1
- [ReleaseArtifactLegalHold](../../src/Compliance.Common/Features/Retention/Commands/ReleaseArtifactLegalHold.cs#L7) — `bdgrz.artifact_retention.legal_hold.release` v1

#### Risks

- [AcceptRisk](../../src/Compliance.Common/Features/Risks/Commands/AcceptRisk.cs#L13) — `bdgrz.risk.accept` v1
- [AddRiskTreatmentAction](../../src/Compliance.Common/Features/Risks/Commands/AddRiskTreatmentAction.cs#L14) — `bdgrz.risk.treatment_action.add` v1
- [AssignRiskOwner](../../src/Compliance.Common/Features/Risks/Commands/AssignRiskOwner.cs#L9) — `bdgrz.risk.owner.assign` v1
- [CancelRiskTreatmentAction](../../src/Compliance.Common/Features/Risks/Commands/CancelRiskTreatmentAction.cs#L11) — `bdgrz.risk.treatment_action.cancel` v1
- [ChooseRiskTreatment](../../src/Compliance.Common/Features/Risks/Commands/ChooseRiskTreatment.cs#L8) — `bdgrz.risk.treatment.choose` v1
- [CreateRiskDraft](../../src/Compliance.Common/Features/Risks/Commands/CreateRiskDraft.cs#L8) — `bdgrz.risk.draft.create` v1
- [ProposeRiskControlTreatment](../../src/Compliance.Common/Features/Risks/Commands/ProposeRiskControlTreatment.cs#L12) — `bdgrz.risk.control_treatment.propose` v1
- [PublishRiskMethodVersion](../../src/Compliance.Common/Features/Risks/Commands/PublishRiskMethodVersion.cs#L8) — `bdgrz.risk.method.publish` v1
- [RaiseRiskReassessmentTriggers](../../src/Compliance.Common/Features/Risks/Commands/RaiseRiskReassessmentTriggers.cs#L10) — `bdgrz.risk.reassessment.raise` v1
- [RecordRiskAssessment](../../src/Compliance.Common/Features/Risks/Commands/RecordRiskAssessment.cs#L8) — `bdgrz.risk.assessment.record` v1
- [RetireRiskControlTreatment](../../src/Compliance.Common/Features/Risks/Commands/RetireRiskControlTreatment.cs#L9) — `bdgrz.risk.control_treatment.retire` v1
- [ReviewRiskControlTreatment](../../src/Compliance.Common/Features/Risks/Commands/ReviewRiskControlTreatment.cs#L13) — `bdgrz.risk.control_treatment.review` v1
- [ReviewRiskTreatmentActionCompletion](../../src/Compliance.Common/Features/Risks/Commands/ReviewRiskTreatmentActionCompletion.cs#L13) — `bdgrz.risk.treatment_action.completion.review` v1
- [ReviseRiskDraft](../../src/Compliance.Common/Features/Risks/Commands/ReviseRiskDraft.cs#L8) — `bdgrz.risk.draft.revise` v1
- [ReviseRiskTreatmentAction](../../src/Compliance.Common/Features/Risks/Commands/ReviseRiskTreatmentAction.cs#L9) — `bdgrz.risk.treatment_action.revise` v1
- [SubmitRiskTreatmentActionCompletion](../../src/Compliance.Common/Features/Risks/Commands/SubmitRiskTreatmentActionCompletion.cs#L13) — `bdgrz.risk.treatment_action.completion.submit` v1

#### Snapshots

- [AmendProgramScopeSnapshot](../../src/Compliance.Common/Features/Snapshots/Commands/AmendProgramScopeSnapshot.cs#L8) — `bdgrz.snapshot.program_scope.amend` v1
- [FreezeProgramScopeSnapshot](../../src/Compliance.Common/Features/Snapshots/Commands/FreezeProgramScopeSnapshot.cs#L8) — `bdgrz.snapshot.program_scope.freeze` v1

#### TechnologyInventory

- [RecordDataFlow](../../src/Compliance.Common/Features/TechnologyInventory/Commands/RecordDataFlow.cs#L7) — `bdgrz.inventory.data_flow.record` v1
- [RecordInformationAsset](../../src/Compliance.Common/Features/TechnologyInventory/Commands/RecordInformationAsset.cs#L7) — `bdgrz.inventory.information_asset.record` v1
- [RecordLocation](../../src/Compliance.Common/Features/TechnologyInventory/Commands/RecordLocation.cs#L7) — `bdgrz.inventory.location.record` v1
- [RecordOperationalProcess](../../src/Compliance.Common/Features/TechnologyInventory/Commands/RecordOperationalProcess.cs#L7) — `bdgrz.inventory.operational_process.record` v1
- [RecordTechnologyComponent](../../src/Compliance.Common/Features/TechnologyInventory/Commands/RecordTechnologyComponent.cs#L7) — `bdgrz.inventory.component.record` v1
- [ReviseDataFlow](../../src/Compliance.Common/Features/TechnologyInventory/Commands/ReviseDataFlow.cs#L7) — `bdgrz.inventory.data_flow.revise` v1
- [ReviseInformationAsset](../../src/Compliance.Common/Features/TechnologyInventory/Commands/ReviseInformationAsset.cs#L6) — `bdgrz.inventory.information_asset.revise` v1
- [ReviseLocation](../../src/Compliance.Common/Features/TechnologyInventory/Commands/ReviseLocation.cs#L7) — `bdgrz.inventory.location.revise` v1
- [ReviseOperationalProcess](../../src/Compliance.Common/Features/TechnologyInventory/Commands/ReviseOperationalProcess.cs#L6) — `bdgrz.inventory.operational_process.revise` v1
- [ReviseTechnologyComponent](../../src/Compliance.Common/Features/TechnologyInventory/Commands/ReviseTechnologyComponent.cs#L7) — `bdgrz.inventory.component.revise` v1

#### Tenants

- [AcceptTenantInvitation](../../src/Compliance.Common/Features/Tenants/Commands/AcceptTenantInvitation.cs#L6) — `bdgrz.tenant-invitation.accept` v1
- [ActivateTenant](../../src/Compliance.Common/Features/Tenants/Commands/ActivateTenant.cs#L6) — `bdgrz.tenant.activate` v1
- [ChangeTenantSlug](../../src/Compliance.Common/Features/Tenants/Commands/ChangeTenantSlug.cs#L6) — `bdgrz.tenant.change-slug` v1
- [ConfirmTenantSlug](../../src/Compliance.Common/Features/Tenants/Commands/ConfirmTenantSlug.cs#L6) — `bdgrz.tenant-slug.confirm` v1
- [ConfirmTenantSlugSurrender](../../src/Compliance.Common/Features/Tenants/Commands/ConfirmTenantSlugSurrender.cs#L6) — `bdgrz.tenant-slug-surrender.confirm` v1
- [GrantPlatformOperator](../../src/Compliance.Common/Features/Tenants/Commands/GrantPlatformOperator.cs#L6) — `bdgrz.platform.operator.grant` v1
- [InviteOrganizationMember](../../src/Compliance.Common/Features/Tenants/Commands/InviteOrganizationMember.cs#L7) — `bdgrz.organization-member.invite` v1
- [InviteGuestConsultant](../../src/Compliance.Common/Features/Tenants/Commands/InviteGuestConsultant.cs#L7) — `bdgrz.tenant.guest-consultant.invite` v1
- [InviteTenantMember](../../src/Compliance.Common/Features/Tenants/Commands/InviteTenantMember.cs#L6) — `bdgrz.tenant-member.invite` v1
- [OffboardTenant](../../src/Compliance.Common/Features/Tenants/Commands/OffboardTenant.cs#L6) — `bdgrz.tenant.offboard` v1
- [ReactivateTenant](../../src/Compliance.Common/Features/Tenants/Commands/ReactivateTenant.cs#L6) — `bdgrz.tenant.reactivate` v2
- [RegisterTenant](../../src/Compliance.Common/Features/Tenants/Commands/RegisterTenant.cs#L10) — `bdgrz.tenant.register` v1
- [RegisterTenantOwner](../../src/Compliance.Common/Features/Tenants/RegisterTenantOwner.cs#L6) — `bdgrz.tenant-owner.register` v1
- [RegisterTenantSlug](../../src/Compliance.Common/Features/Tenants/Commands/RegisterTenantSlug.cs#L6) — `bdgrz.tenant-slug.register` v1
- [RejectTenantSlug](../../src/Compliance.Common/Features/Tenants/Commands/RejectTenantSlug.cs#L6) — `bdgrz.tenant-slug.reject` v1
- [RejectTenantSlugSurrender](../../src/Compliance.Common/Features/Tenants/Commands/RejectTenantSlugSurrender.cs#L6) — `bdgrz.tenant-slug-surrender.reject` v1
- [RequestTenantSlugSurrender](../../src/Compliance.Common/Features/Tenants/Commands/RequestTenantSlugSurrender.cs#L6) — `bdgrz.tenant-slug-surrender.request` v1
- [RevokePlatformOperator](../../src/Compliance.Common/Features/Tenants/Commands/RevokePlatformOperator.cs#L6) — `bdgrz.platform.operator.revoke` v1
- [SeedPlatformOperatorRoster](../../src/Compliance.Common/Features/Tenants/Commands/SeedPlatformOperatorRoster.cs#L5)
- [SurrenderTenantSlug](../../src/Compliance.Common/Features/Tenants/Commands/SurrenderTenantSlug.cs#L6) — `bdgrz.tenant-slug.surrender` v1
- [SuspendTenant](../../src/Compliance.Common/Features/Tenants/Commands/SuspendTenant.cs#L6) — `bdgrz.tenant.suspend` v2

#### UserIdentities

- [CompleteEmailChallenge](../../src/Compliance.Common/Features/UserIdentities/Commands/CompleteEmailChallenge.cs#L6) — `bdgrz.email-address.complete-challenge` v1
- [CompleteIdentityRecovery](../../src/Compliance.Common/Features/UserIdentities/Commands/CompleteIdentityRecovery.cs#L6) — `bdgrz.user-identity.complete-recovery` v1
- [ConfigureTenantIdentityProvider](../../src/Compliance.Common/Features/UserIdentities/Commands/ConfigureTenantIdentityProvider.cs#L7) — `bdgrz.user-identities.configure-tenant-identity-provider` v1
- [ContinueWithDeveloperIdentity](../../src/Compliance.Common/Features/UserIdentities/Commands/ContinueWithDeveloperIdentity.cs#L7) — `bdgrz.developer-identity.continue` v1
- [ContinueWithOidcProvider](../../src/Compliance.Common/Features/UserIdentities/Commands/ContinueWithOidcProvider.cs#L7) — `bdgrz.oidc-provider.continue` v1
- [DisableTenantIdentityProvider](../../src/Compliance.Common/Features/UserIdentities/Commands/DisableTenantIdentityProvider.cs#L7) — `bdgrz.user-identities.disable-tenant-identity-provider` v1
- [IssueEmailChallenge](../../src/Compliance.Common/Features/UserIdentities/Commands/IssueEmailChallenge.cs#L6) — `bdgrz.email-address.issue-challenge` v1
- [LinkOidcProviderIdentity](../../src/Compliance.Common/Features/UserIdentities/Commands/LinkOidcProviderIdentity.cs#L7) — `bdgrz.oidc-provider.link` v1
- [ReserveEmail](../../src/Compliance.Common/Features/UserIdentities/Commands/ReserveEmail.cs#L6) — `bdgrz.email-address.reserve` v1
- [RotateTenantIdentityProviderCredentials](../../src/Compliance.Common/Features/UserIdentities/Commands/RotateTenantIdentityProviderCredentials.cs#L7) — `bdgrz.user-identities.rotate-tenant-identity-provider-credentials` v1
- [RecordTenantIdentityProviderTestResult](../../src/Compliance.Common/Features/UserIdentities/Commands/RecordTenantIdentityProviderTestResult.cs#L7) — `bdgrz.user-identities.record-tenant-identity-provider-test-result` v1
- [StartIdentityRecovery](../../src/Compliance.Common/Features/UserIdentities/Commands/StartIdentityRecovery.cs#L6) — `bdgrz.user-identity.start-recovery` v1
- [TestTenantIdentityProvider](../../src/Compliance.Common/Features/UserIdentities/Commands/TestTenantIdentityProvider.cs#L7) — `bdgrz.user-identities.test-tenant-identity-provider` v2

#### Work

- [AssignWorkItem](../../src/Compliance.Common/Features/Work/Commands/AssignWorkItem.cs#L8) — `bdgrz.work.assign` v1
- [ClaimWorkItem](../../src/Compliance.Common/Features/Work/Commands/ClaimWorkItem.cs#L8) — `bdgrz.work.claim` v1
- [DelegateWorkItem](../../src/Compliance.Common/Features/Work/Commands/DelegateWorkItem.cs#L8) — `bdgrz.work.delegate` v1
- [EscalateWorkItem](../../src/Compliance.Common/Features/Work/Commands/EscalateWorkItem.cs#L8) — `bdgrz.work.escalate` v1
- [SetWorkDigestPreference](../../src/Compliance.Common/Features/Work/Commands/SetWorkDigestPreference.cs#L8) — `bdgrz.work.digest_preference.set` v1
- [ScheduleWeeklyWorkDigest](../../src/Compliance.Common/Features/Work/Commands/ScheduleWeeklyWorkDigest.cs#L7) — `bdgrz.work.schedule-weekly-work-digest` v2
- [DeliverWeeklyWorkDigest](../../src/Compliance.Common/Features/Work/Commands/DeliverWeeklyWorkDigest.cs#L7) — `bdgrz.work.deliver-weekly-work-digest` v2
- [RecordWorkDigestDelivery](../../src/Compliance.Common/Features/Work/Commands/RecordWorkDigestDelivery.cs#L7) — `bdgrz.work.record-work-digest-delivery` v2

#### Workforce

- [AmendWorkforceRosterSnapshot](../../src/Compliance.Common/Features/Workforce/Commands/AmendWorkforceRosterSnapshot.cs#L12) — `bdgrz.snapshot.workforce_roster.amend` v1
- [CorrelatePersonMembership](../../src/Compliance.Common/Features/Workforce/Commands/CorrelatePersonMembership.cs#L13) — `bdgrz.workforce.person.correlate-membership` v1
- [FreezeWorkforceRosterSnapshot](../../src/Compliance.Common/Features/Workforce/Commands/FreezeWorkforceRosterSnapshot.cs#L9) — `bdgrz.snapshot.workforce_roster.freeze` v1
- [ReconcileWorkforceSourceObservation](../../src/Compliance.Common/Features/Workforce/Commands/ReconcileWorkforceSourceObservation.cs#L12) — `bdgrz.workforce.source.reconcile` v1
- [RecordPerson](../../src/Compliance.Common/Features/Workforce/Commands/RecordPerson.cs#L8) — `bdgrz.workforce.person.record` v1
- [RecordServiceIdentity](../../src/Compliance.Common/Features/Workforce/Commands/RecordServiceIdentity.cs#L13) — `bdgrz.workforce.service-identity.record` v1
- [RecordWorkRelationship](../../src/Compliance.Common/Features/Workforce/Commands/RecordWorkRelationship.cs#L11) — `bdgrz.workforce.work-relationship.record` v1
- [RecordWorkforceSourceObservation](../../src/Compliance.Common/Features/Workforce/Commands/RecordWorkforceSourceObservation.cs#L8) — `bdgrz.workforce.source.record` v1
- [ResolveWorkforceObservation](../../src/Compliance.Common/Features/Workforce/Commands/ResolveWorkforceObservation.cs#L14) — `bdgrz.workforce.observation.resolve` v1
- [RevisePerson](../../src/Compliance.Common/Features/Workforce/Commands/RevisePerson.cs#L7) — `bdgrz.workforce.person.revise` v1
- [ReviseServiceIdentity](../../src/Compliance.Common/Features/Workforce/Commands/ReviseServiceIdentity.cs#L9) — `bdgrz.workforce.service-identity.revise` v1
- [ReviseWorkRelationship](../../src/Compliance.Common/Features/Workforce/Commands/ReviseWorkRelationship.cs#L8) — `bdgrz.workforce.work-relationship.revise` v1

### Existing queries

#### AccessControl

- [GetClientIndependenceHistory](../../src/Compliance.Common/Features/AccessControl/Queries/GetClientIndependenceHistory.cs#L6) — `bdgrz.independence.history.get` v1
- [GetClientIndependenceRules](../../src/Compliance.Common/Features/AccessControl/Queries/GetClientIndependenceRules.cs#L7) — `bdgrz.independence.client-rules.get` v1
- [GetEngagementManagementAcknowledgements](../../src/Compliance.Common/Features/AccessControl/Queries/GetEngagementManagementAcknowledgements.cs#L6) — `bdgrz.service-engagement.management-acknowledgements.get` v1
- [GetFirmStaffDirectory](../../src/Compliance.Common/Features/AccessControl/Queries/GetFirmStaffDirectory.cs#L6) — `bdgrz.firm-staff.list` v1
- [GetIndependenceRuleVersions](../../src/Compliance.Common/Features/AccessControl/Queries/GetIndependenceRuleVersions.cs#L6) — `bdgrz.independence.rules.list` v1
- [GetMemberAccess](../../src/Compliance.Common/Features/AccessControl/Queries/GetMemberAccess.cs#L6) — `bdgrz.member.access.get` v1
- [GetRole](../../src/Compliance.Common/Features/AccessControl/Queries/GetRole.cs#L7) — `bdgrz.rbac.role.get` v1
- [GetSeparationOfDutiesWaiver](../../src/Compliance.Common/Features/AccessControl/Queries/GetSeparationOfDutiesWaiver.cs#L6) — `bdgrz.sod-waiver.get` v1
- [GetServiceEngagement](../../src/Compliance.Common/Features/AccessControl/Queries/GetServiceEngagement.cs#L6) — `bdgrz.service-engagement.get` v1
- [GetServiceEngagementAcceptance](../../src/Compliance.Common/Features/AccessControl/Queries/GetServiceEngagementAcceptance.cs#L6) — `bdgrz.service-engagement.acceptance.get` v1
- [GetServiceEngagementAcceptanceHistory](../../src/Compliance.Common/Features/AccessControl/Queries/GetServiceEngagementAcceptanceHistory.cs#L6) — `bdgrz.service-engagement.acceptance-history.get` v1
- [GetServiceEngagementHistory](../../src/Compliance.Common/Features/AccessControl/Queries/GetServiceEngagementHistory.cs#L6) — `bdgrz.service-engagement.history.get` v1
- [GetTeam](../../src/Compliance.Common/Features/AccessControl/Queries/GetTeam.cs#L7) — `bdgrz.rbac.team.get` v1
- [GetTenantMember](../../src/Compliance.Common/Features/AccessControl/Queries/GetTenantMember.cs#L7) — `bdgrz.tenant.member.get` v1
- [ListAccessGrants](../../src/Compliance.Common/Features/AccessControl/Queries/ListAccessGrants.cs#L6) — `bdgrz.access-grant.list` v1
- [ListAssignableFirmStaff](../../src/Compliance.Common/Features/AccessControl/Queries/ListAssignableFirmStaff.cs#L6) — `bdgrz.firm-staff.assignable.list` v1
- [ListRolePermissions](../../src/Compliance.Common/Features/AccessControl/Queries/ListRolePermissions.cs#L13) — `bdgrz.rbac.role-permission.list` v1
- [ListRoleTeams](../../src/Compliance.Common/Features/AccessControl/Queries/ListRoleTeams.cs#L13) — `bdgrz.rbac.role-team.list` v1
- [ListRoles](../../src/Compliance.Common/Features/AccessControl/Queries/ListRoles.cs#L13) — `bdgrz.rbac.role.list` v1
- [ListServiceEngagements](../../src/Compliance.Common/Features/AccessControl/Queries/ListServiceEngagements.cs#L6) — `bdgrz.service-engagement.list` v1
- [ListTeamMembers](../../src/Compliance.Common/Features/AccessControl/Queries/ListTeamMembers.cs#L13) — `bdgrz.rbac.team-member.list` v1
- [ListTeamRoles](../../src/Compliance.Common/Features/AccessControl/Queries/ListTeamRoles.cs#L13) — `bdgrz.rbac.team-role.list` v1
- [ListTeams](../../src/Compliance.Common/Features/AccessControl/Queries/ListTeams.cs#L13) — `bdgrz.rbac.team.list` v1

#### AccessReviews

- [GetAccessPopulation](../../src/Compliance.Common/Features/AccessReviews/Queries/GetAccessPopulation.cs#L7) — `bdgrz.access_population.get` v1
- [GetAccessReviewCampaign](../../src/Compliance.Common/Features/AccessReviews/Queries/GetAccessReviewCampaign.cs#L10) — `bdgrz.access_review.campaign.get` v1
- [GetAccessReviewCoverage](../../src/Compliance.Common/Features/AccessReviews/Queries/GetAccessReviewCoverage.cs#L7) — `bdgrz.access_review.coverage.get` v1
- [GetAccessVariance](../../src/Compliance.Common/Features/AccessReviews/Queries/GetAccessVariance.cs#L7) — `bdgrz.access_population.variance.get` v1
- [ListAccessExpectations](../../src/Compliance.Common/Features/AccessReviews/Queries/ListAccessExpectations.cs#L7) — `bdgrz.access_expectation.list` v1
- [ListAccessPopulations](../../src/Compliance.Common/Features/AccessReviews/Queries/ListAccessPopulations.cs#L8) — `bdgrz.access_population.list` v1
- [ListAccessPrincipals](../../src/Compliance.Common/Features/AccessReviews/Queries/ListAccessPrincipals.cs#L8) — `bdgrz.access_population.principals.list` v1
- [ListAccessReviewCampaigns](../../src/Compliance.Common/Features/AccessReviews/Queries/ListAccessReviewCampaigns.cs#L8) — `bdgrz.access_review.campaign.list` v1
- [PreviewAccessPopulation](../../src/Compliance.Common/Features/AccessReviews/Queries/PreviewAccessPopulation.cs#L7) — `bdgrz.access_population.preview` v1
- [PreviewBulkAccessDecision](../../src/Compliance.Common/Features/AccessReviews/Queries/PreviewBulkAccessDecision.cs#L7) — `bdgrz.access_review.decision.bulk_preview` v1

#### Applications

- [GetAccessReviewScope](../../src/Compliance.Common/Features/Applications/Queries/GetAccessReviewScope.cs#L6) — `bdgrz.system_instance.access_review_scope.get` v1
- [GetApplication](../../src/Compliance.Common/Features/Applications/Queries/GetApplication.cs#L6) — `bdgrz.application.get` v1
- [GetApplicationImport](../../src/Compliance.Common/Features/Applications/Queries/GetApplicationImport.cs#L6) — `bdgrz.application_import.get` v1
- [GetApplicationImportOmissionProposal](../../src/Compliance.Common/Features/Applications/Queries/GetApplicationImportOmissionProposal.cs#L7) — `bdgrz.application_import.omission.get` v1
- [GetApplicationImportProgress](../../src/Compliance.Common/Features/Applications/Queries/GetApplicationImportProgress.cs#L6) — `bdgrz.application_import.progress` v1
- [GetApplicationImportRejectedReport](../../src/Compliance.Common/Features/Applications/Queries/GetApplicationImportRejectedReport.cs#L6) — `bdgrz.application_import.rejected_report` v1
- [GetApplicationRevision](../../src/Compliance.Common/Features/Applications/Queries/GetApplicationRevision.cs#L6) — `bdgrz.application.revision.get` v1
- [GetSystemInstance](../../src/Compliance.Common/Features/Applications/Queries/GetSystemInstance.cs#L6) — `bdgrz.system_instance.get` v1
- [ListAccessReviewScopes](../../src/Compliance.Common/Features/Applications/Queries/ListAccessReviewScopes.cs#L11) — `bdgrz.application.access_review_scopes.list` v1
- [ListApplicationBoundaryReferences](../../src/Compliance.Common/Features/Applications/Queries/ListApplicationBoundaryReferences.cs#L8) — `bdgrz.application.boundary_references.list` v1
- [ListApplicationImportRows](../../src/Compliance.Common/Features/Applications/Queries/ListApplicationImportRows.cs#L7) — `bdgrz.application_import.rows.list` v1
- [ListApplicationRelationships](../../src/Compliance.Common/Features/Applications/Queries/ListApplicationRelationships.cs#L6) — `bdgrz.application.relationship.list` v1
- [ListApplicationRevisions](../../src/Compliance.Common/Features/Applications/Queries/ListApplicationRevisions.cs#L7) — `bdgrz.application.revision.list` v1
- [ListApplications](../../src/Compliance.Common/Features/Applications/Queries/ListApplications.cs#L7) — `bdgrz.application.list` v1
- [ListSystemInstanceBoundaryReferences](../../src/Compliance.Common/Features/Applications/Queries/ListSystemInstanceBoundaryReferences.cs#L8) — `bdgrz.system_instance.boundary_references.list` v1
- [ListSystemInstances](../../src/Compliance.Common/Features/Applications/Queries/ListSystemInstances.cs#L7) — `bdgrz.system_instance.list` v1
- [PreviewApplicationChange](../../src/Compliance.Common/Features/Applications/Queries/PreviewApplicationChange.cs#L8) — `bdgrz.application.change.preview` v1
- [PreviewApplicationChangeMcpInput](../../src/Compliance.App/Features/Applications/PreviewApplicationChangeMcpInput.cs#L7) — `bdgrz.application.change.preview.mcp-input` v1
- [PreviewApplicationImport](../../src/Compliance.Common/Features/Applications/Queries/PreviewApplicationImport.cs#L7) — `bdgrz.application_import.preview` v1
- [PreviewApplicationImportOmissionProposal](../../src/Compliance.Common/Features/Applications/Queries/PreviewApplicationImportOmissionProposal.cs#L6) — `bdgrz.application_import.omission.preview` v1
- [PreviewMissingApplicationImportRows](../../src/Compliance.Common/Features/Applications/Queries/PreviewMissingApplicationImportRows.cs#L7) — `bdgrz.application_import.missing.preview` v1

#### Boundaries

- [GetBoundary](../../src/Compliance.Common/Features/Boundaries/Queries/GetBoundary.cs#L6) — `bdgrz.boundary.get` v1
- [GetBoundaryDecision](../../src/Compliance.Common/Features/Boundaries/Queries/GetBoundaryDecision.cs#L6) — `bdgrz.boundary.decision.get` v1
- [GetBoundaryVersion](../../src/Compliance.Common/Features/Boundaries/Queries/GetBoundaryVersion.cs#L6) — `bdgrz.boundary.version.get` v1
- [GetEffectiveBoundaryVersion](../../src/Compliance.Common/Features/Boundaries/Queries/GetEffectiveBoundaryVersion.cs#L6) — `bdgrz.boundary.version.effective.get` v1
- [ListBoundaryDecisions](../../src/Compliance.Common/Features/Boundaries/Queries/ListBoundaryDecisions.cs#L7) — `bdgrz.boundary.decisions.list` v1
- [ListBoundaryVersions](../../src/Compliance.Common/Features/Boundaries/Queries/ListBoundaryVersions.cs#L7) — `bdgrz.boundary.versions.list` v1
- [ListProgramBoundaries](../../src/Compliance.Common/Features/Boundaries/Queries/ListProgramBoundaries.cs#L9) — `bdgrz.boundary.program.list` v1
- [PreviewBoundaryImpact](../../src/Compliance.Common/Features/Boundaries/Queries/PreviewBoundaryImpact.cs#L6) — `bdgrz.boundary.impact.preview` v1

#### Commitments

- [GetCommitmentDraft](../../src/Compliance.Common/Features/Commitments/Queries/GetCommitmentDraft.cs#L7) — `bdgrz.commitment.draft.get` v1
- [GetCommitmentDraftRevision](../../src/Compliance.Common/Features/Commitments/Queries/GetCommitmentDraftRevision.cs#L7) — `bdgrz.commitment.draft.revision.get` v1
- [GetCommitmentVersion](../../src/Compliance.Common/Features/Commitments/Queries/GetCommitmentVersion.cs#L7) — `bdgrz.commitment.version.get` v1
- [GetEffectiveCommitmentVersion](../../src/Compliance.Common/Features/Commitments/Queries/GetEffectiveCommitmentVersion.cs#L7) — `bdgrz.commitment.version.effective.get` v1
- [ListCommitmentDecisions](../../src/Compliance.Common/Features/Commitments/Queries/ListCommitmentDecisions.cs#L8) — `bdgrz.commitment.decision.list` v1
- [ListCommitmentDraftRevisions](../../src/Compliance.Common/Features/Commitments/Queries/ListCommitmentDraftRevisions.cs#L8) — `bdgrz.commitment.draft.revision.list` v1
- [ListCommitmentDrafts](../../src/Compliance.Common/Features/Commitments/Queries/ListCommitmentDrafts.cs#L8) — `bdgrz.commitment.draft.list` v1
- [ListCommitmentVersions](../../src/Compliance.Common/Features/Commitments/Queries/ListCommitmentVersions.cs#L8) — `bdgrz.commitment.version.list` v1
- [PreviewCommitmentImpact](../../src/Compliance.Common/Features/Commitments/Queries/PreviewCommitmentImpact.cs#L7) — `bdgrz.commitment.impact.preview` v1

#### ControlMappings

- [GetControlCriterionMapping](../../src/Compliance.Common/Features/ControlMappings/Queries/GetControlCriterionMapping.cs#L7) — `bdgrz.control_mapping.get` v1
- [GetCriterionApplicability](../../src/Compliance.Common/Features/ControlMappings/Queries/GetCriterionApplicability.cs#L7) — `bdgrz.criterion_applicability.get` v1
- [ListControlCriterionMappings](../../src/Compliance.Common/Features/ControlMappings/Queries/ListControlCriterionMappings.cs#L9) — `bdgrz.control_mappings.list` v1
- [ListCriteriaCoverage](../../src/Compliance.Common/Features/ControlMappings/Queries/ListCriteriaCoverage.cs#L12) — `bdgrz.control_mappings.coverage.list` v1
- [ListCriterionApplicability](../../src/Compliance.Common/Features/ControlMappings/Queries/ListCriterionApplicability.cs#L9) — `bdgrz.criterion_applicability.list` v1

#### Controls

- [GetControlDecision](../../src/Compliance.Common/Features/Controls/Queries/GetControlDecision.cs#L7) — `bdgrz.control.decision.get` v1
- [GetControlDraft](../../src/Compliance.Common/Features/Controls/Queries/GetControlDraft.cs#L7) — `bdgrz.control.draft.get` v1
- [GetControlDraftOwnerPerson](../../src/Compliance.Common/Features/Controls/Queries/GetControlDraftOwnerPerson.cs#L7) — `bdgrz.control.draft.owner_person.get` v1
- [GetControlDraftRevision](../../src/Compliance.Common/Features/Controls/Queries/GetControlDraftRevision.cs#L7) — `bdgrz.control.draft.revision.get` v1
- [GetControlVersion](../../src/Compliance.Common/Features/Controls/Queries/GetControlVersion.cs#L7) — `bdgrz.control.version.get` v1
- [GetCurrentControlVersion](../../src/Compliance.Common/Features/Controls/Queries/GetCurrentControlVersion.cs#L7) — `bdgrz.control.version.current.get` v1
- [GetEffectiveControlVersion](../../src/Compliance.Common/Features/Controls/Queries/GetEffectiveControlVersion.cs#L7) — `bdgrz.control.version.effective.get` v1
- [ListControlDecisions](../../src/Compliance.Common/Features/Controls/Queries/ListControlDecisions.cs#L8) — `bdgrz.control.decisions.list` v1
- [ListControlDraftRevisions](../../src/Compliance.Common/Features/Controls/Queries/ListControlDraftRevisions.cs#L8) — `bdgrz.control.draft.revisions.list` v1
- [ListControlDrafts](../../src/Compliance.Common/Features/Controls/Queries/ListControlDrafts.cs#L8) — `bdgrz.control.draft.list` v1
- [ListControlVersions](../../src/Compliance.Common/Features/Controls/Queries/ListControlVersions.cs#L8) — `bdgrz.control.versions.list` v1
- [PreviewControlImpact](../../src/Compliance.Common/Features/Controls/Queries/PreviewControlImpact.cs#L8) — `bdgrz.control.impact.preview` v1

#### Criteria

- [ExportCriteriaCatalogEntries](../../src/Compliance.Common/Features/Criteria/Commands/ExportCriteriaCatalogEntries.cs#L8) — `bdgrz.criteria.entries.export` v1
- [GetCriteriaCatalogEdition](../../src/Compliance.Common/Features/Criteria/Queries/GetCriteriaCatalogEdition.cs#L8) — `bdgrz.criteria.edition.get` v1
- [GetCriteriaCatalogEntry](../../src/Compliance.Common/Features/Criteria/Queries/GetCriteriaCatalogEntry.cs#L8) — `bdgrz.criteria.entry.get` v1
- [ListCriteriaCatalogEditions](../../src/Compliance.Common/Features/Criteria/Queries/ListCriteriaCatalogEditions.cs#L8) — `bdgrz.criteria.editions.list` v1
- [ListCriteriaCatalogEntries](../../src/Compliance.Common/Features/Criteria/Queries/ListCriteriaCatalogEntries.cs#L8) — `bdgrz.criteria.entries.list` v1

#### Evaluations

- [GetControlEvaluation](../../src/Compliance.Common/Features/Evaluations/Queries/GetControlEvaluation.cs#L7) — `bdgrz.control.evaluation.get` v1
- [GetControlEvaluationPlanVersion](../../src/Compliance.Common/Features/Evaluations/Queries/GetControlEvaluationPlanVersion.cs#L7) — `bdgrz.control.evaluation.plan.version.get` v1
- [ListControlEvaluationPlanVersions](../../src/Compliance.Common/Features/Evaluations/Queries/ListControlEvaluationPlanVersions.cs#L8) — `bdgrz.control.evaluation.plan.versions.list` v1
- [ListControlEvaluations](../../src/Compliance.Common/Features/Evaluations/Queries/ListControlEvaluations.cs#L9) — `bdgrz.control.evaluations.list` v1

#### Evidence

- [GetEvidenceArtifactMetadata](../../src/Compliance.Common/Features/Evidence/Queries/GetEvidenceArtifactMetadata.cs#L6) — `bdgrz.evidence.artifact.metadata.get` v1
- [GetEvidenceRedaction](../../src/Compliance.Common/Features/Evidence/Queries/GetEvidenceRedaction.cs#L6) — `bdgrz.evidence.redaction.get` v1
- [GetEvidenceRequest](../../src/Compliance.Common/Features/Evidence/Queries/GetEvidenceRequest.cs#L7) — `bdgrz.evidence.request.get` v1
- [ListEvidenceRequests](../../src/Compliance.Common/Features/Evidence/Queries/ListEvidenceRequests.cs#L9) — `bdgrz.evidence.requests.list` v1

#### Operations

- [GetControlOccurrence](../../src/Compliance.Common/Features/Operations/Queries/GetControlOccurrence.cs#L7) — `bdgrz.control.occurrence.get` v1
- [GetControlOperatingPlan](../../src/Compliance.Common/Features/Operations/Queries/GetControlOperatingPlan.cs#L7) — `bdgrz.control.operating_plan.get` v1
- [ListControlOccurrences](../../src/Compliance.Common/Features/Operations/Queries/ListControlOccurrences.cs#L9) — `bdgrz.control.occurrences.list` v1
- [ListControlOperatingBlockers](../../src/Compliance.Common/Features/Operations/Queries/ListControlOperatingBlockers.cs#L9) — `bdgrz.control.operating_blockers.list` v1
- [ListMyControlWork](../../src/Compliance.Common/Features/Operations/Queries/ListMyControlWork.cs#L8) — `bdgrz.control.work.mine` v1
- [PreviewControlOperatingPlan](../../src/Compliance.Common/Features/Operations/Queries/PreviewControlOperatingPlan.cs#L8) — `bdgrz.control.operating_plan.preview` v1

#### Policies

- [GetEffectivePolicyVersion](../../src/Compliance.Common/Features/Policies/Queries/GetEffectivePolicyVersion.cs#L8) — `bdgrz.policy.version.effective.get` v1
- [GetPolicy](../../src/Compliance.Common/Features/Policies/Queries/GetPolicy.cs#L7) — `bdgrz.policy.get` v1
- [GetPolicyVersion](../../src/Compliance.Common/Features/Policies/Queries/GetPolicyVersion.cs#L7) — `bdgrz.policy.version.get` v1
- [ListPolicies](../../src/Compliance.Common/Features/Policies/Queries/ListPolicies.cs#L8) — `bdgrz.policies.list` v1
- [ListPolicyDecisions](../../src/Compliance.Common/Features/Policies/Queries/ListPolicyDecisions.cs#L8) — `bdgrz.policy.decisions.list` v1
- [ListPolicyVersions](../../src/Compliance.Common/Features/Policies/Queries/ListPolicyVersions.cs#L8) — `bdgrz.policy.versions.list` v1
- [PreviewPolicyImpact](../../src/Compliance.Common/Features/Policies/Queries/PreviewPolicyImpact.cs#L7) — `bdgrz.policy.impact.preview` v1

#### PolicyDistribution

- [GetCampaign](../../src/Compliance.Common/Features/PolicyDistribution/Queries/GetCampaign.cs#L8) — `bdgrz.campaign.get` v1
- [GetTrainingRequirement](../../src/Compliance.Common/Features/PolicyDistribution/Queries/GetTrainingRequirement.cs#L8) — `bdgrz.training_requirement.get` v1
- [ListCampaignAmendments](../../src/Compliance.Common/Features/PolicyDistribution/Queries/ListCampaignAmendments.cs#L8) — `bdgrz.campaign.amendments.list` v1
- [ListCampaignParticipants](../../src/Compliance.Common/Features/PolicyDistribution/Queries/ListCampaignParticipants.cs#L12) — `bdgrz.campaign.participants.list` v1
- [ListCampaigns](../../src/Compliance.Common/Features/PolicyDistribution/Queries/ListCampaigns.cs#L8) — `bdgrz.campaigns.list` v1
- [ListTrainingRequirements](../../src/Compliance.Common/Features/PolicyDistribution/Queries/ListTrainingRequirements.cs#L8) — `bdgrz.training_requirements.list` v1

#### Programs

- [GetClientService](../../src/Compliance.Common/Features/Programs/Queries/GetClientService.cs#L6) — `bdgrz.client-service.get` v1
- [GetClientServiceRevision](../../src/Compliance.Common/Features/Programs/Queries/GetClientServiceRevision.cs#L6) — `bdgrz.client-service.revision.get` v1
- [GetProgram](../../src/Compliance.Common/Features/Programs/Queries/GetProgram.cs#L7) — `bdgrz.program.get` v1
- [GetProgramRevision](../../src/Compliance.Common/Features/Programs/Queries/GetProgramRevision.cs#L7) — `bdgrz.program.revision.get` v1
- [GetProgramSetupWork](../../src/Compliance.Common/Features/Programs/Queries/GetProgramSetupWork.cs#L7) — `bdgrz.program.setup-work.get` v1
- [ListClientServiceRevisions](../../src/Compliance.Common/Features/Programs/Queries/ListClientServiceRevisions.cs#L7) — `bdgrz.client-service.revisions.list` v1
- [ListClientServices](../../src/Compliance.Common/Features/Programs/Queries/ListClientServices.cs#L7) — `bdgrz.client-service.list` v1
- [ListProgramClientServices](../../src/Compliance.Common/Features/Programs/Queries/ListProgramClientServices.cs#L8) — `bdgrz.client-service.program.list` v1
- [ListProgramRevisions](../../src/Compliance.Common/Features/Programs/Queries/ListProgramRevisions.cs#L8) — `bdgrz.program.revisions.list` v1
- [ListPrograms](../../src/Compliance.Common/Features/Programs/Queries/ListPrograms.cs#L8) — `bdgrz.program.list` v1

#### Providers

- [GetProvider](../../src/Compliance.Common/Features/Providers/Queries/GetProvider.cs#L6) — `bdgrz.provider.get` v1
- [GetProviderAssuranceCoverage](../../src/Compliance.Common/Features/Providers/Queries/GetProviderAssuranceCoverage.cs#L7) — `bdgrz.provider.assurance_coverage.get` v1
- [GetProviderCoverageGap](../../src/Compliance.Common/Features/Providers/Queries/GetProviderCoverageGap.cs#L6) — `bdgrz.provider.coverage_gap.get` v1
- [GetProviderRevision](../../src/Compliance.Common/Features/Providers/Queries/GetProviderRevision.cs#L6) — `bdgrz.provider.revision.get` v1
- [ListProviderAssuranceReports](../../src/Compliance.Common/Features/Providers/Queries/ListProviderAssuranceReports.cs#L7) — `bdgrz.provider.assurance_reports.list` v1
- [ListProviderCoverageGaps](../../src/Compliance.Common/Features/Providers/Queries/ListProviderCoverageGaps.cs#L7) — `bdgrz.provider.coverage_gaps.list` v1
- [ListProviderReviews](../../src/Compliance.Common/Features/Providers/Queries/ListProviderReviews.cs#L7) — `bdgrz.provider.reviews.list` v1
- [ListProviderRevisions](../../src/Compliance.Common/Features/Providers/Queries/ListProviderRevisions.cs#L7) — `bdgrz.provider.revisions.list` v1
- [ListProviders](../../src/Compliance.Common/Features/Providers/Queries/ListProviders.cs#L7) — `bdgrz.providers.list` v1
- [PreviewProviderChange](../../src/Compliance.Common/Features/Providers/Queries/PreviewProviderChange.cs#L7) — `bdgrz.provider.change.preview` v1

#### Readiness

- [GetReadinessAssessment](../../src/Compliance.Common/Features/Readiness/Queries/GetReadinessAssessment.cs#L7) — `bdgrz.readiness.assessment.get` v1
- [ListReadinessAnnotations](../../src/Compliance.Common/Features/Readiness/Queries/ListReadinessAnnotations.cs#L9) — `bdgrz.readiness.annotations.list` v1
- [ListReadinessAssessments](../../src/Compliance.Common/Features/Readiness/Queries/ListReadinessAssessments.cs#L8) — `bdgrz.readiness.assessments.list` v1
- [ListReadinessGaps](../../src/Compliance.Common/Features/Readiness/Queries/ListReadinessGaps.cs#L12) — `bdgrz.readiness.gaps.list` v1
- [ListTypeIEntryDecisions](../../src/Compliance.Common/Features/Readiness/Queries/ListTypeIEntryDecisions.cs#L9) — `bdgrz.readiness.type_i_entry.list` v1

#### Remediation

- [GetFinding](../../src/Compliance.Common/Features/Remediation/Queries/GetFinding.cs#L7) — `bdgrz.finding.get` v1
- [ListFindings](../../src/Compliance.Common/Features/Remediation/Queries/ListFindings.cs#L9) — `bdgrz.findings.list` v1

#### Responsibilities

- [ListMemberResponsibilities](../../src/Compliance.Common/Features/Responsibilities/Queries/ListMemberResponsibilities.cs#L7) — `bdgrz.member.responsibilities.list` v1
- [ListResponsibilities](../../src/Compliance.Common/Features/Responsibilities/Queries/ListResponsibilities.cs#L8) — `bdgrz.responsibilities.list` v1
- [PreviewResponsibilityConflicts](../../src/Compliance.Common/Features/Responsibilities/Queries/PreviewResponsibilityConflicts.cs#L8) — `bdgrz.responsibilities.conflicts.preview` v1

#### Retention

- [GetApplicationImportRetention](../../src/Compliance.Common/Features/Retention/Queries/GetApplicationImportRetention.cs#L7) — `bdgrz.application_import.retention.get` v1
- [GetArtifactRetention](../../src/Compliance.Common/Features/Retention/Queries/GetArtifactRetention.cs#L6) — `bdgrz.artifact_retention.get` v1
- [ListArtifactLegalHolds](../../src/Compliance.Common/Features/Retention/Queries/ListArtifactLegalHolds.cs#L7) — `bdgrz.artifact_retention.legal_holds.list` v1

#### Risks

- [GetRiskDraft](../../src/Compliance.Common/Features/Risks/Queries/GetRiskDraft.cs#L7) — `bdgrz.risk.draft.get` v1
- [GetRiskDraftRevision](../../src/Compliance.Common/Features/Risks/Queries/GetRiskDraftRevision.cs#L7) — `bdgrz.risk.draft.revision.get` v1
- [GetRiskEvaluation](../../src/Compliance.Common/Features/Risks/Queries/GetRiskEvaluation.cs#L7) — `bdgrz.risk.evaluation.get` v1
- [GetRiskGovernance](../../src/Compliance.Common/Features/Risks/Queries/GetRiskGovernance.cs#L7) — `bdgrz.risk.governance.get` v1
- [GetRiskMethod](../../src/Compliance.Common/Features/Risks/Queries/GetRiskMethod.cs#L7) — `bdgrz.risk.method.get` v1
- [GetRiskMethodVersion](../../src/Compliance.Common/Features/Risks/Queries/GetRiskMethodVersion.cs#L7) — `bdgrz.risk.method.version.get` v1
- [ListRiskDraftRevisions](../../src/Compliance.Common/Features/Risks/Queries/ListRiskDraftRevisions.cs#L8) — `bdgrz.risk.draft.revisions.list` v1
- [ListRiskDrafts](../../src/Compliance.Common/Features/Risks/Queries/ListRiskDrafts.cs#L8) — `bdgrz.risk.draft.list` v1
- [ListRiskEvaluationHistory](../../src/Compliance.Common/Features/Risks/Queries/ListRiskEvaluationHistory.cs#L8) — `bdgrz.risk.evaluation.history.list` v1

#### Snapshots

- [GetSnapshot](../../src/Compliance.Common/Features/Snapshots/Queries/GetSnapshot.cs#L6) — `bdgrz.snapshot.get` v1
- [ListProgramSnapshots](../../src/Compliance.Common/Features/Snapshots/Queries/ListProgramSnapshots.cs#L9) — `bdgrz.snapshot.program.list` v1
- [RegenerateProgramScopeSnapshotManifest](../../src/Compliance.Common/Features/Snapshots/Commands/RegenerateProgramScopeSnapshotManifest.cs#L6) — `bdgrz.snapshot.program_scope.manifest_regenerate` v1
- [VerifyProgramScopeSnapshot](../../src/Compliance.Common/Features/Snapshots/Queries/VerifyProgramScopeSnapshot.cs#L6) — `bdgrz.snapshot.program_scope.verify` v1

#### TechnologyInventory

- [GetDataFlow](../../src/Compliance.Common/Features/TechnologyInventory/Queries/GetDataFlow.cs#L6) — `bdgrz.inventory.data_flow.get` v1
- [GetInformationAsset](../../src/Compliance.Common/Features/TechnologyInventory/Queries/GetInformationAsset.cs#L6) — `bdgrz.inventory.information_asset.get` v1
- [GetLocation](../../src/Compliance.Common/Features/TechnologyInventory/Queries/GetLocation.cs#L6) — `bdgrz.inventory.location.get` v1
- [GetOperationalProcess](../../src/Compliance.Common/Features/TechnologyInventory/Queries/GetOperationalProcess.cs#L6) — `bdgrz.inventory.operational_process.get` v1
- [GetTechnologyComponent](../../src/Compliance.Common/Features/TechnologyInventory/Queries/GetTechnologyComponent.cs#L6) — `bdgrz.inventory.component.get` v1
- [ListDataFlowRevisions](../../src/Compliance.Common/Features/TechnologyInventory/Queries/ListDataFlowRevisions.cs#L7) — `bdgrz.inventory.data_flow.revision.list` v1
- [ListDataFlows](../../src/Compliance.Common/Features/TechnologyInventory/Queries/ListDataFlows.cs#L7) — `bdgrz.inventory.data_flow.list` v1
- [ListInformationAssetBoundaryReferences](../../src/Compliance.Common/Features/TechnologyInventory/Queries/ListInformationAssetBoundaryReferences.cs#L9) — `bdgrz.inventory.asset.boundary_references.list` v1
- [ListInformationAssetRevisions](../../src/Compliance.Common/Features/TechnologyInventory/Queries/ListInformationAssetRevisions.cs#L7) — `bdgrz.inventory.information_asset.revision.list` v1
- [ListInformationAssets](../../src/Compliance.Common/Features/TechnologyInventory/Queries/ListInformationAssets.cs#L7) — `bdgrz.inventory.information_asset.list` v1
- [ListLocationRevisions](../../src/Compliance.Common/Features/TechnologyInventory/Queries/ListLocationRevisions.cs#L7) — `bdgrz.inventory.location.revision.list` v1
- [ListLocations](../../src/Compliance.Common/Features/TechnologyInventory/Queries/ListLocations.cs#L7) — `bdgrz.inventory.location.list` v1
- [ListOperationalProcessRevisions](../../src/Compliance.Common/Features/TechnologyInventory/Queries/ListOperationalProcessRevisions.cs#L7) — `bdgrz.inventory.operational_process.revision.list` v1
- [ListOperationalProcesses](../../src/Compliance.Common/Features/TechnologyInventory/Queries/ListOperationalProcesses.cs#L7) — `bdgrz.inventory.operational_process.list` v1
- [ListTechnologyComponentBoundaryReferences](../../src/Compliance.Common/Features/TechnologyInventory/Queries/ListTechnologyComponentBoundaryReferences.cs#L9) — `bdgrz.inventory.component.boundary_references.list` v1
- [ListTechnologyComponentRevisions](../../src/Compliance.Common/Features/TechnologyInventory/Queries/ListTechnologyComponentRevisions.cs#L7) — `bdgrz.inventory.component.revision.list` v1
- [ListTechnologyComponents](../../src/Compliance.Common/Features/TechnologyInventory/Queries/ListTechnologyComponents.cs#L7) — `bdgrz.inventory.component.list` v1
- [PreviewInformationAssetChange](../../src/Compliance.Common/Features/TechnologyInventory/Queries/PreviewInformationAssetChange.cs#L10) — `bdgrz.inventory.information_asset.change.preview` v1

#### Tenants

- [GetTenant](../../src/Compliance.Common/Features/Tenants/Queries/GetTenant.cs#L6) — `bdgrz.tenant.get` v1
- [GetTenantProvisioningStatus](../../src/Compliance.Common/Features/Tenants/Queries/GetTenantProvisioningStatus.cs#L6) — `bdgrz.tenant.provisioning_status.get` v1
- [ListMyTenants](../../src/Compliance.Common/Features/Tenants/Queries/ListMyTenants.cs#L14) — `bdgrz.tenant-membership.list-mine` v1
- [ListPlatformOperators](../../src/Compliance.Common/Features/Tenants/Queries/ListPlatformOperators.cs#L6) — `bdgrz.platform.operator.list` v1
- [ListTenantInvitations](../../src/Compliance.Common/Features/Tenants/Queries/ListTenantInvitations.cs#L8) — `bdgrz.tenant-invitation.list` v1
- [ListTenantMembers](../../src/Compliance.Common/Features/Tenants/Queries/ListTenantMembers.cs#L7) — `bdgrz.tenant-member.list` v1
- [ListTenants](../../src/Compliance.Common/Features/Tenants/Queries/ListTenants.cs#L11) — `bdgrz.platform.tenant.list` v1
- [ResolveMyTenantSlug](../../src/Compliance.Common/Features/Tenants/Commands/ResolveMyTenantSlug.cs#L6) — `bdgrz.tenant-slug.resolve-mine` v1

#### UserIdentities

- [GetEmailAddress](../../src/Compliance.Common/Features/UserIdentities/Queries/GetEmailAddress.cs#L6) — `bdgrz.email-address.get` v1
- [GetEmailChallengeStatus](../../src/Compliance.Common/Features/UserIdentities/Queries/GetEmailChallengeStatus.cs#L5)
- [GetIdentityRecoveryOptions](../../src/Compliance.Common/Features/UserIdentities/Queries/GetIdentityRecoveryOptions.cs#L7) — `bdgrz.user-identity.recovery-options` v1
- [ListEmailAddresses](../../src/Compliance.Common/Features/UserIdentities/Queries/ListEmailAddresses.cs#L7) — `bdgrz.email-address.list` v1

#### Work

- [GetWorkDigest](../../src/Compliance.Common/Features/Work/Queries/GetWorkDigest.cs#L8) — `bdgrz.work.digest.get` v1
- [GetWorkDigestPreference](../../src/Compliance.Common/Features/Work/Queries/GetWorkDigestPreference.cs#L8) — `bdgrz.work.digest_preference.get` v1
- [GetWorkItem](../../src/Compliance.Common/Features/Work/Queries/GetWorkItem.cs#L8) — `bdgrz.work.get` v1
- [ListWork](../../src/Compliance.Common/Features/Work/Queries/ListWork.cs#L13) — `bdgrz.work.list` v1
- [ListWorkReminders](../../src/Compliance.Common/Features/Work/Queries/ListWorkReminders.cs#L11) — `bdgrz.work.reminders.list` v1

#### Workforce

- [GetPerson](../../src/Compliance.Common/Features/Workforce/Queries/GetPerson.cs#L6) — `bdgrz.workforce.person.get` v1
- [GetServiceIdentity](../../src/Compliance.Common/Features/Workforce/Queries/GetServiceIdentity.cs#L6) — `bdgrz.workforce.service-identity.get` v1
- [GetWorkRelationship](../../src/Compliance.Common/Features/Workforce/Queries/GetWorkRelationship.cs#L6) — `bdgrz.workforce.work-relationship.get` v1
- [GetWorkforceRosterSnapshot](../../src/Compliance.Common/Features/Workforce/Queries/GetWorkforceRosterSnapshot.cs#L7) — `bdgrz.snapshot.workforce_roster.get` v1
- [GetWorkforceRosterSnapshotAsOf](../../src/Compliance.Common/Features/Workforce/Queries/GetWorkforceRosterSnapshotAsOf.cs#L7) — `bdgrz.snapshot.workforce_roster.as_of` v1
- [GetWorkforceSourceObservation](../../src/Compliance.Common/Features/Workforce/Queries/GetWorkforceSourceObservation.cs#L7) — `bdgrz.workforce.source.get` v1
- [ListPeople](../../src/Compliance.Common/Features/Workforce/Queries/ListPeople.cs#L7) — `bdgrz.workforce.person.list` v1
- [ListServiceIdentities](../../src/Compliance.Common/Features/Workforce/Queries/ListServiceIdentities.cs#L11) — `bdgrz.workforce.service-identity.list` v1
- [ListWorkRelationships](../../src/Compliance.Common/Features/Workforce/Queries/ListWorkRelationships.cs#L8) — `bdgrz.workforce.work-relationship.list` v1
- [ListWorkforceObservations](../../src/Compliance.Common/Features/Workforce/Queries/ListWorkforceObservations.cs#L11) — `bdgrz.workforce.observation.list` v1
- [ListWorkforceReconciliationObservations](../../src/Compliance.Common/Features/Workforce/Queries/ListWorkforceReconciliationObservations.cs#L12) — `bdgrz.workforce.reconciliation.list` v1
- [ListWorkforceRosterSnapshots](../../src/Compliance.Common/Features/Workforce/Queries/ListWorkforceRosterSnapshots.cs#L9) — `bdgrz.snapshot.workforce_roster.list` v1
- [ListWorkforceSourceObservations](../../src/Compliance.Common/Features/Workforce/Queries/ListWorkforceSourceObservations.cs#L8) — `bdgrz.workforce.source.list` v1
- [PreviewWorkforceSourceObservation](../../src/Compliance.Common/Features/Workforce/Queries/PreviewWorkforceSourceObservation.cs#L7) — `bdgrz.workforce.source.preview` v1
- [RegenerateWorkforceRosterSnapshotManifest](../../src/Compliance.Common/Features/Workforce/Commands/RegenerateWorkforceRosterSnapshotManifest.cs#L8) — `bdgrz.snapshot.workforce_roster.manifest_regenerate` v1

#### Requirement-backed read contracts added 2026-10-17 to 2026-10-18

##### AdvisorCollaboration

- [GetAdvisorRequest](../../src/Compliance.Common/Features/AdvisorCollaboration/Queries/GetAdvisorRequest.cs#L9) — `bdgrz.advisor-collaboration.get-advisor-request` v1
- [GetAdvisorSharedSource](../../src/Compliance.Common/Features/AdvisorCollaboration/Queries/GetAdvisorSharedSource.cs#L9) — `bdgrz.advisor-collaboration.get-shared-source` v1
- [PreviewAdvisorHandoff](../../src/Compliance.Common/Features/AdvisorCollaboration/Queries/PreviewAdvisorHandoff.cs#L7) — `bdgrz.advisor-collaboration.preview-advisor-handoff` v1
- [ListAdvisorRequests](../../src/Compliance.Common/Features/AdvisorCollaboration/Queries/ListAdvisorRequests.cs#L9) — `bdgrz.advisor-collaboration.list-advisor-requests` v1
- [ListAdvisorFeedback](../../src/Compliance.Common/Features/AdvisorCollaboration/Queries/ListAdvisorFeedback.cs#L9) — `bdgrz.advisor-collaboration.list-advisor-feedback` v1

##### Examinations

- [GetExaminationEngagement](../../src/Compliance.Common/Features/Examinations/Queries/GetExaminationEngagement.cs#L9) — `bdgrz.examinations.get-examination-engagement` v1
- [GetTypeIBaseline](../../src/Compliance.Common/Features/Examinations/Queries/GetTypeIBaseline.cs#L9) — `bdgrz.examinations.get-type-i-baseline` v1
- [PreviewTypeIBaseline](../../src/Compliance.Common/Features/Examinations/Queries/PreviewTypeIBaseline.cs#L9) — `bdgrz.examinations.preview-type-i-baseline` v1

##### SystemDescription

- [GetSystemDescription](../../src/Compliance.Common/Features/SystemDescription/Queries/GetSystemDescription.cs#L9) — `bdgrz.system-description.get-system-description` v1
- [ListSystemDescriptionVersions](../../src/Compliance.Common/Features/SystemDescription/Queries/ListSystemDescriptionVersions.cs#L9) — `bdgrz.system-description.list-system-description-versions` v1
- [PreviewSystemDescriptionImpact](../../src/Compliance.Common/Features/SystemDescription/Queries/PreviewSystemDescriptionImpact.cs#L9) — `bdgrz.system-description.preview-system-description-impact` v1

##### AuditorRequests

- [GetAuditorRequest](../../src/Compliance.Common/Features/AuditorRequests/Queries/GetAuditorRequest.cs#L9) — `bdgrz.auditor-requests.get-auditor-request` v1
- [ListAuditorRequests](../../src/Compliance.Common/Features/AuditorRequests/Queries/ListAuditorRequests.cs#L9) — `bdgrz.auditor-requests.list-auditor-requests` v1
- [GetSampleSelection](../../src/Compliance.Common/Features/AuditorRequests/Queries/GetSampleSelection.cs#L9) — `bdgrz.auditor-requests.get-sample-selection` v1
- [ListRequestResponses](../../src/Compliance.Common/Features/AuditorRequests/Queries/ListRequestResponses.cs#L9) — `bdgrz.auditor-requests.list-request-responses` v1

##### AuditPackages

- [GetAuditPackage](../../src/Compliance.Common/Features/AuditPackages/Queries/GetAuditPackage.cs#L9) — `bdgrz.audit-packages.get-audit-package` v1
- [ListAuditPackages](../../src/Compliance.Common/Features/AuditPackages/Queries/ListAuditPackages.cs#L9) — `bdgrz.audit-packages.list-audit-packages` v1
- [GetAuditPackageManifest](../../src/Compliance.Common/Features/AuditPackages/Queries/GetAuditPackageManifest.cs#L9) — `bdgrz.audit-packages.get-audit-package-manifest` v1
- [GetAuditPackageValidation](../../src/Compliance.Common/Features/AuditPackages/Queries/GetAuditPackageValidation.cs#L9) — `bdgrz.audit-packages.get-audit-package-validation` v1
- [GetAuditPackageDelivery](../../src/Compliance.Common/Features/AuditPackages/Queries/GetAuditPackageDelivery.cs#L9) — `bdgrz.audit-packages.get-audit-package-delivery` v1

##### Examinations

- [ListAuditObservations](../../src/Compliance.Common/Features/Examinations/Queries/ListAuditObservations.cs#L9) — `bdgrz.examinations.list-audit-observations` v1
- [GetAuditOutcome](../../src/Compliance.Common/Features/Examinations/Queries/GetAuditOutcome.cs#L9) — `bdgrz.examinations.get-audit-outcome` v1
- [GetTypeIIPlan](../../src/Compliance.Common/Features/Examinations/Queries/GetTypeIIPlan.cs#L9) — `bdgrz.examinations.get-type-i-i-plan` v1
- [GetManagementAssertion](../../src/Compliance.Common/Features/Examinations/Queries/GetManagementAssertion.cs#L9) — `bdgrz.examinations.get-management-assertion` v1
- [GetExaminationSignOff](../../src/Compliance.Common/Features/Examinations/Queries/GetExaminationSignOff.cs#L9) — `bdgrz.examinations.get-examination-sign-off` v1

##### Operations

- [GetObservationPeriod](../../src/Compliance.Common/Features/Operations/Queries/GetObservationPeriod.cs#L9) — `bdgrz.operations.get-observation-period` v1
- [ListObservationPeriods](../../src/Compliance.Common/Features/Operations/Queries/ListObservationPeriods.cs#L9) — `bdgrz.operations.list-observation-periods` v1
- [GetControlCalendar](../../src/Compliance.Common/Features/Operations/Queries/GetControlCalendar.cs#L9) — `bdgrz.operations.get-control-calendar` v1
- [ListMissingOccurrences](../../src/Compliance.Common/Features/Operations/Queries/ListMissingOccurrences.cs#L9) — `bdgrz.operations.list-missing-occurrences` v1
- [ListDueGovernanceReviews](../../src/Compliance.Common/Features/Operations/Queries/ListDueGovernanceReviews.cs#L9) — `bdgrz.operations.list-due-governance-reviews` v1
- [GetManagementReview](../../src/Compliance.Common/Features/Operations/Queries/GetManagementReview.cs#L9) — `bdgrz.operations.get-management-review` v1
- [ListManagementReviewActions](../../src/Compliance.Common/Features/Operations/Queries/ListManagementReviewActions.cs#L9) — `bdgrz.operations.list-management-review-actions` v1

##### Changes

- [GetSignificantChange](../../src/Compliance.Common/Features/Changes/Queries/GetSignificantChange.cs#L9) — `bdgrz.changes.get-significant-change` v2
- [ListSignificantChanges](../../src/Compliance.Common/Features/Changes/Queries/ListSignificantChanges.cs#L9) — `bdgrz.changes.list-significant-changes` v1

##### Incidents

- [GetComplianceIncident](../../src/Compliance.Common/Features/Incidents/Queries/GetComplianceIncident.cs#L9) — `bdgrz.incidents.get-compliance-incident` v2
- [ListComplianceIncidents](../../src/Compliance.Common/Features/Incidents/Queries/ListComplianceIncidents.cs#L9) — `bdgrz.incidents.list-compliance-incidents` v1
- [GetComplianceImpactAssessment](../../src/Compliance.Common/Features/Incidents/Queries/GetComplianceImpactAssessment.cs#L9) — `bdgrz.incidents.get-compliance-impact-assessment` v2

##### AuditPopulations

- [PreviewObservationPeriodClose](../../src/Compliance.Common/Features/AuditPopulations/Queries/PreviewObservationPeriodClose.cs#L9) — `bdgrz.audit-populations.preview-observation-period-close` v1
- [GetObservationPeriodClose](../../src/Compliance.Common/Features/AuditPopulations/Queries/GetObservationPeriodClose.cs#L9) — `bdgrz.audit-populations.get-observation-period-close` v1
- [ListAuditPopulationDefinitions](../../src/Compliance.Common/Features/AuditPopulations/Queries/ListAuditPopulationDefinitions.cs#L9) — `bdgrz.audit-populations.list-audit-population-definitions` v1
- [GetAuditPopulation](../../src/Compliance.Common/Features/AuditPopulations/Queries/GetAuditPopulation.cs#L9) — `bdgrz.audit-populations.get-audit-population` v1
- [ListAuditPopulationRows](../../src/Compliance.Common/Features/AuditPopulations/Queries/ListAuditPopulationRows.cs#L9) — `bdgrz.audit-populations.list-audit-population-rows` v1
- [GetPopulationReconciliation](../../src/Compliance.Common/Features/AuditPopulations/Queries/GetPopulationReconciliation.cs#L9) — `bdgrz.audit-populations.get-population-reconciliation` v1

##### Programs

- [PreviewProgramRollover](../../src/Compliance.Common/Features/Programs/Queries/PreviewProgramRollover.cs#L9) — `bdgrz.programs.preview-program-rollover` v1
- [GetProgramRollover](../../src/Compliance.Common/Features/Programs/Queries/GetProgramRollover.cs#L9) — `bdgrz.programs.get-program-rollover` v1
- [GetNextPeriodPlan](../../src/Compliance.Common/Features/Programs/Queries/GetNextPeriodPlan.cs#L9) — `bdgrz.programs.get-next-period-plan` v1
- [ListCarryForwardReferences](../../src/Compliance.Common/Features/Programs/Queries/ListCarryForwardReferences.cs#L9) — `bdgrz.programs.list-carry-forward-references` v1

##### FirmPortfolio

- [ListAuthorizedClientPortfolio](../../src/Compliance.Common/Features/FirmPortfolio/Queries/ListAuthorizedClientPortfolio.cs#L9) — `bdgrz.firm-portfolio.list-authorized-client-portfolio` v1
- [GetAuthorizedClientSummary](../../src/Compliance.Common/Features/FirmPortfolio/Queries/GetAuthorizedClientSummary.cs#L9) — `bdgrz.firm-portfolio.get-authorized-client-summary` v1
- [ListMyCrossClientWork](../../src/Compliance.Common/Features/FirmPortfolio/Queries/ListMyCrossClientWork.cs#L9) — `bdgrz.firm-portfolio.list-my-cross-client-work` v1

##### MethodologyTemplates

- [ListMethodologyTemplates](../../src/Compliance.Common/Features/MethodologyTemplates/Queries/ListMethodologyTemplates.cs#L9) — `bdgrz.methodology-templates.list-methodology-templates` v1
- [GetMethodologyTemplateVersion](../../src/Compliance.Common/Features/MethodologyTemplates/Queries/GetMethodologyTemplateVersion.cs#L9) — `bdgrz.methodology-templates.get-methodology-template-version` v1
- [PreviewMethodologyTemplateApplication](../../src/Compliance.Common/Features/MethodologyTemplates/Queries/PreviewMethodologyTemplateApplication.cs#L9) — `bdgrz.methodology-templates.preview-methodology-template-application` v1
- [GetTemplateApplication](../../src/Compliance.Common/Features/MethodologyTemplates/Queries/GetTemplateApplication.cs#L9) — `bdgrz.methodology-templates.get-template-application` v1

##### AttestRecords

- [GetOwnAttestRecordPackage](../../src/Compliance.Common/Features/AttestRecords/Queries/GetOwnAttestRecordPackage.cs#L9) — `bdgrz.attest-records.get-own-attest-record-package` v1
- [GetOwnAttestRecordDelivery](../../src/Compliance.Common/Features/AttestRecords/Queries/GetOwnAttestRecordDelivery.cs#L9) — `bdgrz.attest-records.get-own-attest-record-delivery` v1
- [ListOutstandingAttestDeliveryHolds](../../src/Compliance.Common/Features/AttestRecords/Queries/ListOutstandingAttestDeliveryHolds.cs#L9) — `bdgrz.attest-records.list-outstanding-attest-delivery-holds` v1

### Backfilled event contracts

These 140 requirement-backed event contracts have been added since the existing-event inventory below was captured. The eight readiness-import events remain deferred. Contract presence does not mean command handlers emit them or projectors consume them.

#### AccessControl

- [IndependenceRuleVersionRatified](../../src/Compliance.Common/Features/AccessControl/Events/IndependenceRuleVersionRatified.cs#L6)

- [PartnerIndependenceEvaluationRecorded](../../src/Compliance.Common/Features/AccessControl/Events/PartnerIndependenceEvaluationRecorded.cs#L6)

- [ServiceEngagementStaffAssigned](../../src/Compliance.Common/Features/AccessControl/Events/ServiceEngagementStaffAssigned.cs#L6)



#### AdvisorCollaboration

- [AdvisorFeedbackCorrected](../../src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorFeedbackCorrected.cs#L6)

- [AdvisorFeedbackRecorded](../../src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorFeedbackRecorded.cs#L6)

- [AdvisorFeedbackResolved](../../src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorFeedbackResolved.cs#L6)

- [AdvisorFeedbackResponded](../../src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorFeedbackResponded.cs#L6)

- [AdvisorHandoffClosed](../../src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorHandoffClosed.cs#L6)

- [AdvisorHandoffReadiedForReview](../../src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorHandoffReadiedForReview.cs#L6)

- [AdvisorHandoffRevoked](../../src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorHandoffRevoked.cs#L6)

- [AdvisorHandoffSelectionRevised](../../src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorHandoffSelectionRevised.cs#L6)

- [AdvisorHandoffCreated](../../src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorHandoffCreated.cs#L8)


#### Applications









- [SystemInstanceRevised](../../src/Compliance.Common/Features/Applications/Events/SystemInstanceRevised.cs#L6)



#### AttestRecords

- [DeliveredEngagementHoldReleased](../../src/Compliance.Common/Features/AttestRecords/Events/DeliveredEngagementHoldReleased.cs#L6)

- [OwnAttestRecordDeliveryRecorded](../../src/Compliance.Common/Features/AttestRecords/Events/OwnAttestRecordDeliveryRecorded.cs#L6)

- [OwnAttestRecordDeliveryVerified](../../src/Compliance.Common/Features/AttestRecords/Events/OwnAttestRecordDeliveryVerified.cs#L6)

- [OwnAttestRecordPackagePrepared](../../src/Compliance.Common/Features/AttestRecords/Events/OwnAttestRecordPackagePrepared.cs#L6)



#### AuditPackages

- [AuditPackageBuildProgressRecorded](../../src/Compliance.Common/Features/AuditPackages/AuditPackageBuildProgressRecorded.cs#L6)

- [AuditPackageCreated](../../src/Compliance.Common/Features/AuditPackages/Events/AuditPackageCreated.cs#L6)

- [AuditPackageAmended](../../src/Compliance.Common/Features/AuditPackages/Events/AuditPackageAmended.cs#L6)

- [AuditPackageBuildFailed](../../src/Compliance.Common/Features/AuditPackages/AuditPackageBuildFailed.cs#L6)

- [AuditPackageBuildRequested](../../src/Compliance.Common/Features/AuditPackages/Events/AuditPackageBuildRequested.cs#L6)

- [AuditPackageBuilt](../../src/Compliance.Common/Features/AuditPackages/Events/AuditPackageBuilt.cs#L6)

- [AuditPackageDelivered](../../src/Compliance.Common/Features/AuditPackages/Events/AuditPackageDelivered.cs#L6)

- [AuditPackageDisclosureApproved](../../src/Compliance.Common/Features/AuditPackages/Events/AuditPackageDisclosureApproved.cs#L6)

- [AuditPackageValidated](../../src/Compliance.Common/Features/AuditPackages/Events/AuditPackageValidated.cs#L6)

- [AuditPackageValidationOverrideApproved](../../src/Compliance.Common/Features/AuditPackages/Events/AuditPackageValidationOverrideApproved.cs#L6)



#### AuditPopulations

- [AuditPopulationAmended](../../src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationAmended.cs#L6)

- [AuditPopulationDefined](../../src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationDefined.cs#L6)

- [AuditPopulationExportGenerated](../../src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationExportGenerated.cs#L6)

- [AuditPopulationFactRecorded](../../src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationFactRecorded.cs#L6)

- [AuditPopulationFrozen](../../src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationFrozen.cs#L6)

- [AuditPopulationGenerated](../../src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationGenerated.cs#L6)

- [AuditPopulationReconciled](../../src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationReconciled.cs#L6)

- [AuditPopulationRowClassified](../../src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationRowClassified.cs#L6)

- [ObservationPeriodCloseFrozen](../../src/Compliance.Common/Features/AuditPopulations/Events/ObservationPeriodCloseFrozen.cs#L6)

- [ObservationPeriodCloseProposed](../../src/Compliance.Common/Features/AuditPopulations/Events/ObservationPeriodCloseProposed.cs#L6)

- [TypeIIPeriodCloseAmended](../../src/Compliance.Common/Features/AuditPopulations/Events/TypeIIPeriodCloseAmended.cs#L6)

- [TypeIIPeriodCloseApproved](../../src/Compliance.Common/Features/AuditPopulations/Events/TypeIIPeriodCloseApproved.cs#L6)



#### AuditorRequests

- [AuditResponseDraftCreated](../../src/Compliance.Common/Features/AuditorRequests/Events/AuditResponseDraftCreated.cs#L6)

- [AuditResponseDraftRevised](../../src/Compliance.Common/Features/AuditorRequests/Events/AuditResponseDraftRevised.cs#L6)

- [AuditorRequestAssigned](../../src/Compliance.Common/Features/AuditorRequests/Events/AuditorRequestAssigned.cs#L6)

- [AuditorRequestCommentRecorded](../../src/Compliance.Common/Features/AuditorRequests/Events/AuditorRequestCommentRecorded.cs#L6)

- [AuditorRequestRecorded](../../src/Compliance.Common/Features/AuditorRequests/Events/AuditorRequestRecorded.cs#L6)

- [AuditorRequestReopened](../../src/Compliance.Common/Features/AuditorRequests/Events/AuditorRequestReopened.cs#L6)

- [AuditorRequestResolved](../../src/Compliance.Common/Features/AuditorRequests/Events/AuditorRequestResolved.cs#L6)

- [AuditorRequestRevised](../../src/Compliance.Common/Features/AuditorRequests/Events/AuditorRequestRevised.cs#L6)

- [AuditorResponseDelivered](../../src/Compliance.Common/Features/AuditorRequests/Events/AuditorResponseDelivered.cs#L6)

- [AuditorResponseDisclosureReviewed](../../src/Compliance.Common/Features/AuditorRequests/Events/AuditorResponseDisclosureReviewed.cs#L6)

- [AuditorResponseDispositionRecorded](../../src/Compliance.Common/Features/AuditorRequests/Events/AuditorResponseDispositionRecorded.cs#L6)

- [AuditorResponseSubmitted](../../src/Compliance.Common/Features/AuditorRequests/Events/AuditorResponseSubmitted.cs#L6)

- [SampleSelectionRecorded](../../src/Compliance.Common/Features/AuditorRequests/Events/SampleSelectionRecorded.cs#L6)



#### Changes




- [ImpactReviewWorkLinked](../../src/Compliance.Common/Features/Changes/Events/ImpactReviewWorkLinked.cs#L6)
- [SignificantChangeSystemDescriptionDecisionRecorded](../../src/Compliance.Common/Features/Changes/Events/SignificantChangeSystemDescriptionDecisionRecorded.cs#L6)


- [SignificantChangeImpactAssessed](../../src/Compliance.Common/Features/Changes/Events/SignificantChangeImpactAssessed.cs#L6)

- [SignificantChangeRecorded](../../src/Compliance.Common/Features/Changes/Events/SignificantChangeRecorded.cs#L6)

- [SignificantChangeRevised](../../src/Compliance.Common/Features/Changes/Events/SignificantChangeRevised.cs#L6)



#### Commitments

- [CommitmentRetired](../../src/Compliance.Common/Features/Commitments/Events/CommitmentRetired.cs#L6)



#### Evidence

- [EvidenceArtifactDownloadIssued](../../src/Compliance.Common/Features/Evidence/Events/EvidenceArtifactDownloadIssued.cs#L6)

- [EvidenceSupportLinked](../../src/Compliance.Common/Features/Evidence/Events/EvidenceSupportLinked.cs#L6)



#### Examinations

- [AuditObservationFindingLinked](../../src/Compliance.Common/Features/Examinations/Events/AuditObservationFindingLinked.cs#L6)

- [AuditObservationRecorded](../../src/Compliance.Common/Features/Examinations/Events/AuditObservationRecorded.cs#L6)

- [AuditOutcomeRecorded](../../src/Compliance.Common/Features/Examinations/Events/AuditOutcomeRecorded.cs#L6)

- [ManagementAssertionApproved](../../src/Compliance.Common/Features/Examinations/Events/ManagementAssertionApproved.cs#L6)

- [ManagementAssertionRevised](../../src/Compliance.Common/Features/Examinations/Events/ManagementAssertionRevised.cs#L6)

- [ManagementRepresentationLetterSigned](../../src/Compliance.Common/Features/Examinations/Events/ManagementRepresentationLetterSigned.cs#L6)

- [ExaminationEngagementOpened](../../src/Compliance.Common/Features/Examinations/Events/ExaminationEngagementOpened.cs#L6)

- [ExaminationEngagementRevised](../../src/Compliance.Common/Features/Examinations/Events/ExaminationEngagementRevised.cs#L6)

- [ExaminationSignedOff](../../src/Compliance.Common/Features/Examinations/Events/ExaminationSignedOff.cs#L6)

- [ManagementAssertionRecorded](../../src/Compliance.Common/Features/Examinations/Events/ManagementAssertionRecorded.cs#L6)

- [RepresentationLetterRecorded](../../src/Compliance.Common/Features/Examinations/Events/RepresentationLetterRecorded.cs#L6)

- [TypeIBaselineAmended](../../src/Compliance.Common/Features/Examinations/Events/TypeIBaselineAmended.cs#L6)

- [TypeIBaselineFrozen](../../src/Compliance.Common/Features/Examinations/Events/TypeIBaselineFrozen.cs#L6)

- [TypeIIPlanApproved](../../src/Compliance.Common/Features/Examinations/Events/TypeIIPlanApproved.cs#L6)

- [TypeIIPlanProposed](../../src/Compliance.Common/Features/Examinations/Events/TypeIIPlanProposed.cs#L6)

- [TypeIIPlanReviewed](../../src/Compliance.Common/Features/Examinations/Events/TypeIIPlanReviewed.cs#L6)

- [TypeIIPlanRevised](../../src/Compliance.Common/Features/Examinations/Events/TypeIIPlanRevised.cs#L6)



#### Exports

- [ClientExportBuildFailed](../../src/Compliance.Common/Features/Exports/Events/ClientExportBuildFailed.cs#L6)

- [ClientExportBuilt](../../src/Compliance.Common/Features/Exports/Events/ClientExportBuilt.cs#L6)

- [ClientExportDelivered](../../src/Compliance.Common/Features/Exports/Events/ClientExportDelivered.cs#L6)

- [ClientExportRequested](../../src/Compliance.Common/Features/Exports/Events/ClientExportRequested.cs#L6)



#### MethodologyTemplates

- [MethodologyTemplateApplied](../../src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateApplied.cs#L6)

- [MethodologyTemplateContributionAccepted](../../src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateContributionAccepted.cs#L6)

- [MethodologyTemplateContributionProposed](../../src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateContributionProposed.cs#L6)

- [MethodologyTemplateContributionReviewed](../../src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateContributionReviewed.cs#L6)

- [MethodologyTemplateCreated](../../src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateCreated.cs#L6)

- [MethodologyTemplateDraftRevised](../../src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateDraftRevised.cs#L6)

- [MethodologyTemplateRetired](../../src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateRetired.cs#L6)

- [MethodologyTemplateVersionPublished](../../src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateVersionPublished.cs#L6)

- [TemplateSuccessorAdoptionProposed](../../src/Compliance.Common/Features/MethodologyTemplates/Events/TemplateSuccessorAdoptionProposed.cs#L6)

- [TemplateSuccessorDispositionRecorded](../../src/Compliance.Common/Features/MethodologyTemplates/Events/TemplateSuccessorDispositionRecorded.cs#L6)



#### Operations

- [DueControlOccurrencesGenerated](../../src/Compliance.Common/Features/Operations/Events/DueControlOccurrencesGenerated.cs#L6)

- [FutureControlScheduleRevised](../../src/Compliance.Common/Features/Operations/Events/FutureControlScheduleRevised.cs#L6)

- [GovernanceReviewScheduled](../../src/Compliance.Common/Features/Operations/Events/GovernanceReviewScheduled.cs#L6)

- [ManagementReviewActionCompleted](../../src/Compliance.Common/Features/Operations/Events/ManagementReviewActionCompleted.cs#L6)

- [ManagementReviewActionRecorded](../../src/Compliance.Common/Features/Operations/Events/ManagementReviewActionRecorded.cs#L6)

- [ManagementReviewRecorded](../../src/Compliance.Common/Features/Operations/Events/ManagementReviewRecorded.cs#L6)

- [MissingOccurrenceRecorded](../../src/Compliance.Common/Features/Operations/Events/MissingOccurrenceRecorded.cs#L6)

- [ObservationPeriodAmendmentCancelled](../../src/Compliance.Common/Features/Operations/ObservationPeriodAmendmentCancelled.cs#L6)

- [ObservationPeriodAmendmentCompleted](../../src/Compliance.Common/Features/Operations/ObservationPeriodAmendmentCompleted.cs#L6)

- [ObservationPeriodAmendmentReserved](../../src/Compliance.Common/Features/Operations/ObservationPeriodAmendmentReserved.cs#L6)

- [ObservationPeriodReservationReleased](../../src/Compliance.Common/Features/Operations/ObservationPeriodReservationReleased.cs#L6)

- [ObservationPeriodAmended](../../src/Compliance.Common/Features/Operations/Events/ObservationPeriodAmended.cs#L6)

- [ObservationPeriodEnded](../../src/Compliance.Common/Features/Operations/Events/ObservationPeriodEnded.cs#L6)

- [ObservationPeriodStarted](../../src/Compliance.Common/Features/Operations/Events/ObservationPeriodStarted.cs#L6)

- [ObservationPeriodReserved](../../src/Compliance.Common/Features/Operations/ObservationPeriodReserved.cs#L6)

- [TypeIIOperatingPlanApproved](../../src/Compliance.Common/Features/Operations/Events/TypeIIOperatingPlanApproved.cs#L6)

- [TypeIIOperatingPlanProposed](../../src/Compliance.Common/Features/Operations/Events/TypeIIOperatingPlanProposed.cs#L6)

- [TypeIIOperatingPlanReviewed](../../src/Compliance.Common/Features/Operations/Events/TypeIIOperatingPlanReviewed.cs#L6)



#### Programs

- [NextObservationPeriodStarted](../../src/Compliance.Common/Features/Programs/Events/NextObservationPeriodStarted.cs#L6)

- [OpenRemediationCarriedForward](../../src/Compliance.Common/Features/Programs/Events/OpenRemediationCarriedForward.cs#L6)

- [ProgramRolloverApproved](../../src/Compliance.Common/Features/Programs/Events/ProgramRolloverApproved.cs#L6)

- [ProgramRolloverProposed](../../src/Compliance.Common/Features/Programs/Events/ProgramRolloverProposed.cs#L6)



#### Retention

- [ArtifactDisclosureApproved](../../src/Compliance.Common/Features/Retention/Events/ArtifactDisclosureApproved.cs#L6)

- [ArtifactDisclosureDelivered](../../src/Compliance.Common/Features/Retention/Events/ArtifactDisclosureDelivered.cs#L6)

- [ArtifactDisclosureRevoked](../../src/Compliance.Common/Features/Retention/Events/ArtifactDisclosureRevoked.cs#L6)

- [ArtifactDispositionApproved](../../src/Compliance.Common/Features/Retention/Events/ArtifactDispositionApproved.cs#L6)

- [ArtifactDispositionExecutionRequested](../../src/Compliance.Common/Features/Retention/Events/ArtifactDispositionExecutionRequested.cs#L6)

- [ArtifactDispositionResultRecorded](../../src/Compliance.Common/Features/Retention/Events/ArtifactDispositionResultRecorded.cs#L6)

- [ArtifactEngagementHoldPlaced](../../src/Compliance.Common/Features/Retention/Events/ArtifactEngagementHoldPlaced.cs#L6)

- [ArtifactEngagementHoldReleased](../../src/Compliance.Common/Features/Retention/Events/ArtifactEngagementHoldReleased.cs#L6)

- [ArtifactHandlingPolicyAssigned](../../src/Compliance.Common/Features/Retention/Events/ArtifactHandlingPolicyAssigned.cs#L6)

- [ArtifactHandlingPolicyRevised](../../src/Compliance.Common/Features/Retention/Events/ArtifactHandlingPolicyRevised.cs#L6)

- [EngagementReportIssued](../../src/Compliance.Common/Features/Retention/Events/EngagementReportIssued.cs#L6)



#### SystemDescription

- [SystemDescriptionApproved](../../src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionApproved.cs#L6)

- [SystemDescriptionBound](../../src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionBound.cs#L6)

- [SystemDescriptionDraftCreated](../../src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionDraftCreated.cs#L6)

- [SystemDescriptionDraftRevised](../../src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionDraftRevised.cs#L6)

- [SystemDescriptionExportGenerated](../../src/Compliance.Common/Features/SystemDescription/SystemDescriptionExportGenerated.cs#L6)

- [SystemDescriptionNoChangeRecorded](../../src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionNoChangeRecorded.cs#L6)

- [SystemDescriptionReviewed](../../src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionReviewed.cs#L6)

- [SystemDescriptionSourcesBound](../../src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionSourcesBound.cs#L6)

- [SystemDescriptionSuccessorProposed](../../src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionSuccessorProposed.cs#L6)



#### Tenants

- [TenantOffboardingCompleted](../../src/Compliance.Common/Features/Tenants/Events/TenantOffboardingCompleted.cs#L6)



#### UserIdentities

- [TenantIdentityProviderConfigured](../../src/Compliance.Common/Features/UserIdentities/Events/TenantIdentityProviderConfigured.cs#L6)

- [TenantIdentityProviderCredentialsRotated](../../src/Compliance.Common/Features/UserIdentities/Events/TenantIdentityProviderCredentialsRotated.cs#L6)

- [TenantIdentityProviderDisabled](../../src/Compliance.Common/Features/UserIdentities/Events/TenantIdentityProviderDisabled.cs#L6)

- [TenantIdentityProviderTested](../../src/Compliance.Common/Features/UserIdentities/Events/TenantIdentityProviderTested.cs#L6)

- [TenantIdentityProviderTestRequested](../../src/Compliance.Common/Features/UserIdentities/Events/TenantIdentityProviderTestRequested.cs#L6)


#### Work

- [WeeklyWorkDigestScheduled](../../src/Compliance.Common/Features/Work/Events/WeeklyWorkDigestScheduled.cs#L7)
- [WeeklyWorkDigestDeliveryRequested](../../src/Compliance.Common/Features/Work/Events/WeeklyWorkDigestDeliveryRequested.cs#L7)
- [WeeklyWorkDigestDeliveryRecorded](../../src/Compliance.Common/Features/Work/Events/WeeklyWorkDigestDeliveryRecorded.cs#L7)
- [WeeklyWorkDigestDeliverySuppressed](../../src/Compliance.Common/Features/Work/Events/WeeklyWorkDigestDeliverySuppressed.cs#L7)


### Incidents

- [ComplianceIncidentImpactAssessed](../../src/Compliance.Common/Features/Incidents/Events/ComplianceIncidentImpactAssessed.cs#L6)
- [ComplianceIncidentRecorded](../../src/Compliance.Common/Features/Incidents/Events/ComplianceIncidentRecorded.cs#L6)
- [ComplianceIncidentRevised](../../src/Compliance.Common/Features/Incidents/Events/ComplianceIncidentRevised.cs#L6)
- [ComplianceIncidentSystemDescriptionDecisionRecorded](../../src/Compliance.Common/Features/Incidents/Events/ComplianceIncidentSystemDescriptionDecisionRecorded.cs#L6)
- [IncidentResolutionRecorded](../../src/Compliance.Common/Features/Incidents/Events/IncidentResolutionRecorded.cs#L6)

### Existing events

#### AccessControl

- [AccessGrantIssued](../../src/Compliance.Common/Features/AccessControl/Events/AccessGrantIssued.cs#L6) — `bdgrz.access-grant.issued` v1
- [AccessGrantRevoked](../../src/Compliance.Common/Features/AccessControl/Events/AccessGrantRevoked.cs#L6) — `bdgrz.access-grant.revoked` v1
- [AssignmentIndependenceReevaluated](../../src/Compliance.Common/Features/AccessControl/Events/AssignmentIndependenceReevaluated.cs#L6) — `bdgrz.independence.assignment-reevaluated` v1
- [ClientIndependenceEvaluated](../../src/Compliance.Common/Features/AccessControl/Events/ClientIndependenceEvaluated.cs#L6) — `bdgrz.independence.evaluated` v1
- [DirectoryIndependenceReevaluated](../../src/Compliance.Common/Features/AccessControl/Events/DirectoryIndependenceReevaluated.cs#L6) — `bdgrz.independence.directory-reevaluated` v1
- [EngagementManagementAcknowledged](../../src/Compliance.Common/Features/AccessControl/Events/EngagementManagementAcknowledged.cs#L6) — `bdgrz.service-engagement.management-acknowledged` v1
- [FirmStaffChangeRecorded](../../src/Compliance.Common/Features/AccessControl/Events/FirmStaffChangeRecorded.cs#L6) — `bdgrz.firm-staff.changed` v1
- [IndependenceRulesRevised](../../src/Compliance.Common/Features/AccessControl/Events/IndependenceRulesRevised.cs#L6) — `bdgrz.independence.rules.revised` v1
- [MemberDeprovisionCleanupCompleted](../../src/Compliance.Common/Features/AccessControl/Events/MemberDeprovisionCleanupCompleted.cs#L6) — `bdgrz.rbac.member.deprovision_cleanup_completed` v2
- [MemberDeprovisioned](../../src/Compliance.Common/Features/AccessControl/Events/MemberDeprovisioned.cs#L6) — `bdgrz.rbac.member.deprovisioned` v2
- [MemberRegistered](../../src/Compliance.Common/Features/AccessControl/Events/MemberRegistered.cs#L6) — `bdgrz.rbac.member.registered` v1
- [MemberReinstated](../../src/Compliance.Common/Features/AccessControl/Events/MemberReinstated.cs#L6) — `bdgrz.rbac.member.reinstated` v1
- [MemberSuspended](../../src/Compliance.Common/Features/AccessControl/Events/MemberSuspended.cs#L6) — `bdgrz.rbac.member.suspended` v1
- [NonattestServiceRecorded](../../src/Compliance.Common/Features/AccessControl/Events/NonattestServiceRecorded.cs#L6) — `bdgrz.independence.service.recorded` v1
- [RoleDefined](../../src/Compliance.Common/Features/AccessControl/Events/RoleDefined.cs#L6) — `bdgrz.rbac.role.defined` v1
- [RoleDeleted](../../src/Compliance.Common/Features/AccessControl/Events/RoleDeleted.cs#L6) — `bdgrz.rbac.role.deleted` v1
- [RolePermissionAssigned](../../src/Compliance.Common/Features/AccessControl/Events/RolePermissionAssigned.cs#L6) — `bdgrz.rbac.role-permission.assigned` v1
- [RolePermissionRemoved](../../src/Compliance.Common/Features/AccessControl/Events/RolePermissionRemoved.cs#L6) — `bdgrz.rbac.role-permission.removed` v1
- [RoleRenamed](../../src/Compliance.Common/Features/AccessControl/Events/RoleRenamed.cs#L6) — `bdgrz.rbac.role.renamed` v1
- [SeparationOfDutiesWaiverApproved](../../src/Compliance.Common/Features/AccessControl/Events/SeparationOfDutiesWaiverApproved.cs#L7) — `bdgrz.sod-waiver.approved` v1
- [SeparationOfDutiesWaiverRecorded](../../src/Compliance.Common/Features/AccessControl/Events/SeparationOfDutiesWaiverRecorded.cs#L8) — `bdgrz.sod-waiver.recorded` v1
- [ServiceEngagementAcceptanceRecorded](../../src/Compliance.Common/Features/AccessControl/Events/ServiceEngagementAcceptanceRecorded.cs#L6) — `bdgrz.service-engagement.acceptance-recorded` v1
- [ServiceEngagementAssignmentRevoked](../../src/Compliance.Common/Features/AccessControl/Events/ServiceEngagementAssignmentRevoked.cs#L6) — `bdgrz.service-engagement.assignment-revoked` v1
- [ServiceEngagementMutationRecorded](../../src/Compliance.Common/Features/AccessControl/Events/ServiceEngagementMutationRecorded.cs#L6) — `bdgrz.service-engagement.changed` v1
- [ServiceIndependenceReevaluated](../../src/Compliance.Common/Features/AccessControl/Events/ServiceIndependenceReevaluated.cs#L6) — `bdgrz.independence.service-reevaluated` v1
- [TeamDefined](../../src/Compliance.Common/Features/AccessControl/Events/TeamDefined.cs#L6) — `bdgrz.rbac.team.defined` v1
- [TeamDeleted](../../src/Compliance.Common/Features/AccessControl/Events/TeamDeleted.cs#L6) — `bdgrz.rbac.team.deleted` v1
- [TeamMemberAssigned](../../src/Compliance.Common/Features/AccessControl/Events/TeamMemberAssigned.cs#L6) — `bdgrz.rbac.team-member.assigned` v1
- [TeamMemberRemoved](../../src/Compliance.Common/Features/AccessControl/Events/TeamMemberRemoved.cs#L6) — `bdgrz.rbac.team-member.removed` v2
- [TeamRoleAssigned](../../src/Compliance.Common/Features/AccessControl/Events/TeamRoleAssigned.cs#L6) — `bdgrz.rbac.team-role.assigned` v1
- [TeamRoleRemoved](../../src/Compliance.Common/Features/AccessControl/Events/TeamRoleRemoved.cs#L6) — `bdgrz.rbac.team-role.removed` v1
- [TenantManagerRestored](../../src/Compliance.Common/Features/AccessControl/Events/TenantManagerRestored.cs#L7) — `bdgrz.rbac.manager_guard.restored` v1
- [TenantManagerWithdrawn](../../src/Compliance.Common/Features/AccessControl/TenantManagerWithdrawn.cs#L10) — `bdgrz.rbac.manager_guard.withdrawn` v1

#### AccessReviews

- [AccessDecisionsRecorded](../../src/Compliance.Common/Features/AccessReviews/Events/AccessDecisionsRecorded.cs#L8) — `bdgrz.access_review.decisions.recorded` v1
- [AccessExpectationApproved](../../src/Compliance.Common/Features/AccessReviews/Events/AccessExpectationApproved.cs#L8) — `bdgrz.access_expectation.approved` v1
- [AccessExpectationExceptionRecorded](../../src/Compliance.Common/Features/AccessReviews/Events/AccessExpectationExceptionRecorded.cs#L8) — `bdgrz.access_expectation.exception_recorded` v1
- [AccessExpectationProposed](../../src/Compliance.Common/Features/AccessReviews/Events/AccessExpectationProposed.cs#L8) — `bdgrz.access_expectation.proposed` v1
- [AccessPopulationAccepted](../../src/Compliance.Common/Features/AccessReviews/Events/AccessPopulationAccepted.cs#L8) — `bdgrz.access_population.accepted` v1
- [AccessPopulationExceptionRecorded](../../src/Compliance.Common/Features/AccessReviews/Events/AccessPopulationExceptionRecorded.cs#L8) — `bdgrz.access_population.exception_recorded` v1
- [AccessPopulationFactsRecorded](../../src/Compliance.Common/Features/AccessReviews/Events/AccessPopulationFactsRecorded.cs#L8) — `bdgrz.access_population.facts_recorded` v1
- [AccessPopulationOpened](../../src/Compliance.Common/Features/AccessReviews/Events/AccessPopulationOpened.cs#L8) — `bdgrz.access_population.opened` v1
- [AccessPrincipalClassified](../../src/Compliance.Common/Features/AccessReviews/Events/AccessPrincipalClassified.cs#L8) — `bdgrz.access_population.principal_classified` v1
- [AccessRemediationChangeRecorded](../../src/Compliance.Common/Features/AccessReviews/Events/AccessRemediationChangeRecorded.cs#L8) — `bdgrz.access_review.remediation.change_recorded` v1
- [AccessRemediationExceptionRecorded](../../src/Compliance.Common/Features/AccessReviews/Events/AccessRemediationExceptionRecorded.cs#L8) — `bdgrz.access_review.remediation.exception_recorded` v1
- [AccessRemediationVerified](../../src/Compliance.Common/Features/AccessReviews/Events/AccessRemediationVerified.cs#L8) — `bdgrz.access_review.remediation.verified` v1
- [AccessReviewCampaignCompleted](../../src/Compliance.Common/Features/AccessReviews/Events/AccessReviewCampaignCompleted.cs#L8) — `bdgrz.access_review.campaign.completed` v1
- [AccessReviewCampaignLaunched](../../src/Compliance.Common/Features/AccessReviews/Events/AccessReviewCampaignLaunched.cs#L8) — `bdgrz.access_review.campaign.launched` v1

#### Applications

- [AccessReviewScopeDecided](../../src/Compliance.Common/Features/Applications/Events/AccessReviewScopeDecided.cs#L9) — `bdgrz.system_instance.access_review_scope.decided` v1
- [ApplicationDeclared](../../src/Compliance.Common/Features/Applications/Events/ApplicationDeclared.cs#L8) — `bdgrz.application.declared` v1
- [ApplicationImportCanceled](../../src/Compliance.Common/Features/Applications/Events/ApplicationImportCanceled.cs#L6) — `bdgrz.application_import.canceled` v1
- [ApplicationImportCommitted](../../src/Compliance.Common/Features/Applications/Events/ApplicationImportCommitted.cs#L6) — `bdgrz.application_import.committed` v1
- [ApplicationImportEffectPending](../../src/Compliance.Common/Features/Applications/Events/ApplicationImportEffectPending.cs#L8) — `bdgrz.application_import.effect_pending` v1
- [ApplicationImportFailed](../../src/Compliance.Common/Features/Applications/Events/ApplicationImportFailed.cs#L6) — `bdgrz.application_import.failed` v1
- [ApplicationImportPlanRowFrozen](../../src/Compliance.Common/Features/Applications/ApplicationImportPlanRowFrozen.cs#L6) — `bdgrz.application_import.plan_row_frozen` v1
- [ApplicationImportPlanSealed](../../src/Compliance.Common/Features/Applications/Events/ApplicationImportPlanSealed.cs#L6) — `bdgrz.application_import.plan_sealed` v1
- [ApplicationImportPlanStarted](../../src/Compliance.Common/Features/Applications/ApplicationImportPlanStarted.cs#L6) — `bdgrz.application_import.plan_started` v1
- [ApplicationImportRetirementProposalSealed](../../src/Compliance.Common/Features/Applications/Events/ApplicationImportRetirementProposalSealed.cs#L6) — `bdgrz.application_import.retirement_proposal_sealed` v1
- [ApplicationImportRetirementProposalStarted](../../src/Compliance.Common/Features/Applications/ApplicationImportRetirementProposalStarted.cs#L6) — `bdgrz.application_import.retirement_proposal_started` v1
- [ApplicationImportRetirementRowFrozen](../../src/Compliance.Common/Features/Applications/ApplicationImportRetirementRowFrozen.cs#L6) — `bdgrz.application_import.retirement_row_frozen` v1
- [ApplicationImportRowCorrelated](../../src/Compliance.Common/Features/Applications/Events/ApplicationImportRowCorrelated.cs#L6) — `bdgrz.application_import.row_correlated` v1
- [ApplicationImportStaged](../../src/Compliance.Common/Features/Applications/Events/ApplicationImportStaged.cs#L6) — `bdgrz.application_import.staged` v1
- [ApplicationRelationshipAdded](../../src/Compliance.Common/Features/Applications/Events/ApplicationRelationshipAdded.cs#L8) — `bdgrz.application.relationship.added` v1
- [ApplicationRelationshipRemoved](../../src/Compliance.Common/Features/Applications/Events/ApplicationRelationshipRemoved.cs#L8) — `bdgrz.application.relationship.removed` v1
- [ApplicationRetired](../../src/Compliance.Common/Features/Applications/Events/ApplicationRetired.cs#L9) — `bdgrz.application.retired` v1
- [ApplicationRevised](../../src/Compliance.Common/Features/Applications/Events/ApplicationRevised.cs#L8) — `bdgrz.application.revised` v1
- [SystemInstanceDeclared](../../src/Compliance.Common/Features/Applications/Events/SystemInstanceDeclared.cs#L8) — `bdgrz.system_instance.declared` v1
- [SystemInstanceRegistered](../../src/Compliance.Common/Features/Applications/Events/SystemInstanceRegistered.cs#L8) — `bdgrz.system_instance.registered` v1
- [SystemInstanceRetired](../../src/Compliance.Common/Features/Applications/Events/SystemInstanceRetired.cs#L9) — `bdgrz.system_instance.retired` v1

#### Boundaries

- [BoundaryApproved](../../src/Compliance.Common/Features/Boundaries/Events/BoundaryApproved.cs#L8) — `bdgrz.boundary.approved` v1
- [BoundaryDraftCreated](../../src/Compliance.Common/Features/Boundaries/Events/BoundaryDraftCreated.cs#L6) — `bdgrz.boundary.draft.created` v1
- [BoundaryDraftDiscarded](../../src/Compliance.Common/Features/Boundaries/Events/BoundaryDraftDiscarded.cs#L8) — `bdgrz.boundary.draft.discarded` v1
- [BoundaryDraftRevised](../../src/Compliance.Common/Features/Boundaries/Events/BoundaryDraftRevised.cs#L6) — `bdgrz.boundary.draft.revised` v1
- [BoundaryReviewed](../../src/Compliance.Common/Features/Boundaries/Events/BoundaryReviewed.cs#L8) — `bdgrz.boundary.reviewed` v1
- [BoundarySuccessorProposed](../../src/Compliance.Common/Features/Boundaries/Events/BoundarySuccessorProposed.cs#L6) — `bdgrz.boundary.successor.proposed` v1

#### Commitments

- [CommitmentApproved](../../src/Compliance.Common/Features/Commitments/Events/CommitmentApproved.cs#L9) — `bdgrz.commitment.approved` v1
- [CommitmentDraftCreated](../../src/Compliance.Common/Features/Commitments/Events/CommitmentDraftCreated.cs#L8) — `bdgrz.commitment.draft.created` v1
- [CommitmentDraftRevised](../../src/Compliance.Common/Features/Commitments/Events/CommitmentDraftRevised.cs#L8) — `bdgrz.commitment.draft.revised` v1
- [CommitmentReviewed](../../src/Compliance.Common/Features/Commitments/Events/CommitmentReviewed.cs#L12) — `bdgrz.commitment.reviewed` v1

#### ControlMappings

- [ControlCriterionMappingProposed](../../src/Compliance.Common/Features/ControlMappings/Events/ControlCriterionMappingProposed.cs#L8) — `bdgrz.control_mapping.proposed` v1
- [ControlCriterionMappingRetired](../../src/Compliance.Common/Features/ControlMappings/Events/ControlCriterionMappingRetired.cs#L8) — `bdgrz.control_mapping.retired` v1
- [ControlCriterionMappingReviewed](../../src/Compliance.Common/Features/ControlMappings/Events/ControlCriterionMappingReviewed.cs#L8) — `bdgrz.control_mapping.reviewed` v1
- [CriterionApplicabilityReviewed](../../src/Compliance.Common/Features/ControlMappings/Events/CriterionApplicabilityReviewed.cs#L7) — `bdgrz.criterion_applicability.reviewed` v1
- [CriterionNotApplicableProposed](../../src/Compliance.Common/Features/ControlMappings/Events/CriterionNotApplicableProposed.cs#L7) — `bdgrz.criterion_applicability.not_applicable_proposed` v1
- [CriterionNotApplicableWithdrawn](../../src/Compliance.Common/Features/ControlMappings/Events/CriterionNotApplicableWithdrawn.cs#L7) — `bdgrz.criterion_applicability.not_applicable_withdrawn` v1

#### Controls

- [ControlApproved](../../src/Compliance.Common/Features/Controls/Events/ControlApproved.cs#L9) — `bdgrz.control.approved` v1
- [ControlDraftCreated](../../src/Compliance.Common/Features/Controls/Events/ControlDraftCreated.cs#L8) — `bdgrz.control.draft.created` v1
- [ControlDraftDiscarded](../../src/Compliance.Common/Features/Controls/Events/ControlDraftDiscarded.cs#L8) — `bdgrz.control.draft.discarded` v1
- [ControlDraftRevised](../../src/Compliance.Common/Features/Controls/Events/ControlDraftRevised.cs#L8) — `bdgrz.control.draft.revised` v1
- [ControlOwnerPersonDesignated](../../src/Compliance.Common/Features/Controls/Events/ControlOwnerPersonDesignated.cs#L13) — `bdgrz.control.owner_person.designated` v1
- [ControlProposalWithdrawn](../../src/Compliance.Common/Features/Controls/Events/ControlProposalWithdrawn.cs#L15) — `bdgrz.control.proposal.withdrawn` v1
- [ControlRetired](../../src/Compliance.Common/Features/Controls/Events/ControlRetired.cs#L12) — `bdgrz.control.retired` v1
- [ControlRetirementProposed](../../src/Compliance.Common/Features/Controls/Events/ControlRetirementProposed.cs#L9) — `bdgrz.control.retirement.proposed` v1
- [ControlReviewed](../../src/Compliance.Common/Features/Controls/Events/ControlReviewed.cs#L8) — `bdgrz.control.reviewed` v1
- [ControlSuccessorProposed](../../src/Compliance.Common/Features/Controls/Events/ControlSuccessorProposed.cs#L12) — `bdgrz.control.successor.proposed` v1

#### Criteria

- [CriteriaTextOverlayEntryRevised](../../src/Compliance.Common/Features/Criteria/Events/CriteriaTextOverlayEntryRevised.cs#L7) — `bdgrz.criteria-overlay.entry-revised` v1

#### Evaluations

- [ControlEvaluationDeviationDisposed](../../src/Compliance.Common/Features/Evaluations/Events/ControlEvaluationDeviationDisposed.cs#L6) — `bdgrz.control.evaluation.deviation_disposed` v1
- [ControlEvaluationPlanVersionDefined](../../src/Compliance.Common/Features/Evaluations/Events/ControlEvaluationPlanVersionDefined.cs#L7) — `bdgrz.control.evaluation.plan.version-defined` v1
- [ControlEvaluationReviewed](../../src/Compliance.Common/Features/Evaluations/Events/ControlEvaluationReviewed.cs#L6) — `bdgrz.control.evaluation.reviewed` v1
- [ControlEvaluationStarted](../../src/Compliance.Common/Features/Evaluations/Events/ControlEvaluationStarted.cs#L7) — `bdgrz.control.evaluation.started` v1
- [ControlEvaluationStepRecorded](../../src/Compliance.Common/Features/Evaluations/Events/ControlEvaluationStepRecorded.cs#L6) — `bdgrz.control.evaluation.step_recorded` v1
- [ControlEvaluationSubmitted](../../src/Compliance.Common/Features/Evaluations/Events/ControlEvaluationSubmitted.cs#L6) — `bdgrz.control.evaluation.submitted` v1

#### Evidence

- [EvidenceArtifactInspected](../../src/Compliance.Common/Features/Evidence/Events/EvidenceArtifactInspected.cs#L7) — `bdgrz.evidence.artifact.inspected` v1
- [EvidenceArtifactQuarantineReleased](../../src/Compliance.Common/Features/Evidence/Events/EvidenceArtifactQuarantineReleased.cs#L8) — `bdgrz.evidence.artifact.quarantine_released` v1
- [EvidenceArtifactRegistered](../../src/Compliance.Common/Features/Evidence/Events/EvidenceArtifactRegistered.cs#L8) — `bdgrz.evidence.artifact.registered` v1
- [EvidenceRedactionApproved](../../src/Compliance.Common/Features/Evidence/Events/EvidenceRedactionApproved.cs#L6) — `bdgrz.evidence.redaction.approved` v1
- [EvidenceRedactionPrepared](../../src/Compliance.Common/Features/Evidence/Events/EvidenceRedactionPrepared.cs#L6) — `bdgrz.evidence.redaction.prepared` v1
- [EvidenceRequestCancelled](../../src/Compliance.Common/Features/Evidence/Events/EvidenceRequestCancelled.cs#L8) — `bdgrz.evidence.request.cancelled` v1
- [EvidenceRequestFulfilled](../../src/Compliance.Common/Features/Evidence/Events/EvidenceRequestFulfilled.cs#L8) — `bdgrz.evidence.request.fulfilled` v1
- [EvidenceRequestOpened](../../src/Compliance.Common/Features/Evidence/Events/EvidenceRequestOpened.cs#L8) — `bdgrz.evidence.request.opened` v1

#### Operations

- [ControlOccurrenceAttested](../../src/Compliance.Common/Features/Operations/Events/ControlOccurrenceAttested.cs#L6) — `bdgrz.control.occurrence.attested` v1
- [ControlOccurrenceOpened](../../src/Compliance.Common/Features/Operations/Events/ControlOccurrenceOpened.cs#L7) — `bdgrz.control.occurrence.opened` v1
- [ControlOccurrenceReviewed](../../src/Compliance.Common/Features/Operations/Events/ControlOccurrenceReviewed.cs#L6) — `bdgrz.control.occurrence.reviewed` v1
- [ControlOperatingPlanApproved](../../src/Compliance.Common/Features/Operations/Events/ControlOperatingPlanApproved.cs#L7) — `bdgrz.control.operating_plan.approved` v1
- [ControlOperatingPlanProposed](../../src/Compliance.Common/Features/Operations/Events/ControlOperatingPlanProposed.cs#L6) — `bdgrz.control.operating_plan.proposed` v1

#### Policies

- [PolicyApproved](../../src/Compliance.Common/Features/Policies/Events/PolicyApproved.cs#L11) — `bdgrz.policy.approved` v1
- [PolicyDraftCreated](../../src/Compliance.Common/Features/Policies/Events/PolicyDraftCreated.cs#L7) — `bdgrz.policy.draft.created` v1
- [PolicyDraftDiscarded](../../src/Compliance.Common/Features/Policies/Events/PolicyDraftDiscarded.cs#L7) — `bdgrz.policy.draft.discarded` v1
- [PolicyDraftRevised](../../src/Compliance.Common/Features/Policies/Events/PolicyDraftRevised.cs#L11) — `bdgrz.policy.draft.revised` v1
- [PolicyPeriodicReviewConfirmed](../../src/Compliance.Common/Features/Policies/Events/PolicyPeriodicReviewConfirmed.cs#L8) — `bdgrz.policy.periodic_review.confirmed` v1
- [PolicyRetired](../../src/Compliance.Common/Features/Policies/Events/PolicyRetired.cs#L7) — `bdgrz.policy.retired` v1
- [PolicyRetirementProposed](../../src/Compliance.Common/Features/Policies/Events/PolicyRetirementProposed.cs#L7) — `bdgrz.policy.retirement.proposed` v1
- [PolicyReviewed](../../src/Compliance.Common/Features/Policies/Events/PolicyReviewed.cs#L7) — `bdgrz.policy.reviewed` v1

#### PolicyDistribution

- [CampaignWaiverApproved](../../src/Compliance.Common/Features/PolicyDistribution/Events/CampaignWaiverApproved.cs#L8) — `bdgrz.policy_campaign.waiver.approved` v1
- [PolicyAcknowledgementRecorded](../../src/Compliance.Common/Features/PolicyDistribution/Events/PolicyAcknowledgementRecorded.cs#L12) — `bdgrz.policy.acknowledgement.recorded` v1
- [PolicyCampaignAudienceAmended](../../src/Compliance.Common/Features/PolicyDistribution/Events/PolicyCampaignAudienceAmended.cs#L11) — `bdgrz.policy_campaign.audience.amended` v1
- [PolicyCampaignAudienceFrozen](../../src/Compliance.Common/Features/PolicyDistribution/Events/PolicyCampaignAudienceFrozen.cs#L6) — `bdgrz.policy_campaign.audience.frozen` v1
- [PolicyCampaignClosed](../../src/Compliance.Common/Features/PolicyDistribution/Events/PolicyCampaignClosed.cs#L7) — `bdgrz.policy_campaign.closed` v1
- [PolicyCampaignLaunched](../../src/Compliance.Common/Features/PolicyDistribution/Events/PolicyCampaignLaunched.cs#L11) — `bdgrz.policy_campaign.launched` v1
- [TrainingCompletionRecorded](../../src/Compliance.Common/Features/PolicyDistribution/Events/TrainingCompletionRecorded.cs#L11) — `bdgrz.training_completion.recorded` v1
- [TrainingRequirementDefined](../../src/Compliance.Common/Features/PolicyDistribution/Events/TrainingRequirementDefined.cs#L8) — `bdgrz.training_requirement.defined` v1

#### Programs

- [ClientServiceCreated](../../src/Compliance.Common/Features/Programs/Events/ClientServiceCreated.cs#L8) — `bdgrz.client-service.created` v1
- [ClientServiceRetired](../../src/Compliance.Common/Features/Programs/Events/ClientServiceRetired.cs#L8) — `bdgrz.client-service.retired` v1
- [ClientServiceRevised](../../src/Compliance.Common/Features/Programs/Events/ClientServiceRevised.cs#L8) — `bdgrz.client-service.revised` v1
- [ProgramCreated](../../src/Compliance.Common/Features/Programs/Events/ProgramCreated.cs#L8) — `bdgrz.program.created` v1
- [ProgramCriteriaEditionSelected](../../src/Compliance.Common/Features/Programs/Events/ProgramCriteriaEditionSelected.cs#L8) — `bdgrz.program.criteria.selected` v1
- [ProgramRevised](../../src/Compliance.Common/Features/Programs/Events/ProgramRevised.cs#L8) — `bdgrz.program.revised` v1

#### Providers

- [AssuranceReportRecorded](../../src/Compliance.Common/Features/Providers/Events/AssuranceReportRecorded.cs#L7) — `bdgrz.provider.assurance_report.recorded` v1
- [AssuranceReportRevised](../../src/Compliance.Common/Features/Providers/Events/AssuranceReportRevised.cs#L7) — `bdgrz.provider.assurance_report.revised` v1
- [ProviderCoverageGapClosed](../../src/Compliance.Common/Features/Providers/Events/ProviderCoverageGapClosed.cs#L7) — `bdgrz.provider.coverage_gap.closed` v1
- [ProviderCoverageGapRecorded](../../src/Compliance.Common/Features/Providers/Events/ProviderCoverageGapRecorded.cs#L7) — `bdgrz.provider.coverage_gap.recorded` v1
- [ProviderCoverageGapRiskAcceptanceLinked](../../src/Compliance.Common/Features/Providers/Events/ProviderCoverageGapRiskAcceptanceLinked.cs#L6) — `bdgrz.provider.coverage_gap.risk_acceptance_linked` v1
- [ProviderRecorded](../../src/Compliance.Common/Features/Providers/Events/ProviderRecorded.cs#L7) — `bdgrz.provider.recorded` v1
- [ProviderReviewRecorded](../../src/Compliance.Common/Features/Providers/Events/ProviderReviewRecorded.cs#L7) — `bdgrz.provider.review.recorded` v1
- [ProviderRevised](../../src/Compliance.Common/Features/Providers/Events/ProviderRevised.cs#L7) — `bdgrz.provider.revised` v1

#### Readiness

- [ReadinessAssessmentRecorded](../../src/Compliance.Common/Features/Readiness/Events/ReadinessAssessmentRecorded.cs#L7) — `bdgrz.readiness.assessment.recorded` v1
- [ReadinessDecisionRecorded](../../src/Compliance.Common/Features/Readiness/Events/ReadinessDecisionRecorded.cs#L6) — `bdgrz.readiness.decision.recorded` v1
- [ReadinessGapAnnotated](../../src/Compliance.Common/Features/Readiness/Events/ReadinessGapAnnotated.cs#L6) — `bdgrz.readiness.gap.annotated` v1
- [ReadinessGapPlanned](../../src/Compliance.Common/Features/Readiness/Events/ReadinessGapPlanned.cs#L6) — `bdgrz.readiness.gap.planned` v1
- [TypeIEntryDecisionRecorded](../../src/Compliance.Common/Features/Readiness/Events/TypeIEntryDecisionRecorded.cs#L6) — `bdgrz.readiness.type_i_entry.decided` v1

#### Remediation

- [CorrectiveActionAdded](../../src/Compliance.Common/Features/Remediation/CorrectiveActionAdded.cs#L6) — `bdgrz.finding.corrective_action.added` v1
- [CorrectiveActionCompleted](../../src/Compliance.Common/Features/Remediation/CorrectiveActionCompleted.cs#L8) — `bdgrz.finding.corrective_action.completed` v1
- [FindingAcceptanceLinked](../../src/Compliance.Common/Features/Remediation/FindingAcceptanceLinked.cs#L6) — `bdgrz.finding.acceptance.linked` v1
- [FindingClosed](../../src/Compliance.Common/Features/Remediation/FindingClosed.cs#L6) — `bdgrz.finding.closed` v1
- [FindingRaised](../../src/Compliance.Common/Features/Remediation/FindingRaised.cs#L7) — `bdgrz.finding.raised` v1
- [FindingReopened](../../src/Compliance.Common/Features/Remediation/FindingReopened.cs#L7) — `bdgrz.finding.reopened` v1
- [FindingRevised](../../src/Compliance.Common/Features/Remediation/FindingRevised.cs#L7) — `bdgrz.finding.revised` v1

#### Responsibilities

- [ResponsibilityAssigned](../../src/Compliance.Common/Features/Responsibilities/Events/ResponsibilityAssigned.cs#L8) — `bdgrz.responsibility.assigned` v1
- [ResponsibilityRevoked](../../src/Compliance.Common/Features/Responsibilities/Events/ResponsibilityRevoked.cs#L8) — `bdgrz.responsibility.revoked` v1

#### Retention

- [ArtifactLegalHoldPlaced](../../src/Compliance.Common/Features/Retention/Events/ArtifactLegalHoldPlaced.cs#L6) — `bdgrz.artifact_retention.legal_hold_placed` v1
- [ArtifactLegalHoldReleased](../../src/Compliance.Common/Features/Retention/Events/ArtifactLegalHoldReleased.cs#L6) — `bdgrz.artifact_retention.legal_hold_released` v1
- [ArtifactRetentionBasisRecorded](../../src/Compliance.Common/Features/Retention/Events/ArtifactRetentionBasisRecorded.cs#L6) — `bdgrz.artifact_retention.basis_recorded` v1
- [ArtifactRetentionBound](../../src/Compliance.Common/Features/Retention/Events/ArtifactRetentionBound.cs#L6) — `bdgrz.artifact_retention.bound` v1

#### Risks

- [RiskAccepted](../../src/Compliance.Common/Features/Risks/Events/RiskAccepted.cs#L6) — `bdgrz.risk.accepted` v1
- [RiskAssessmentRecorded](../../src/Compliance.Common/Features/Risks/Events/RiskAssessmentRecorded.cs#L6) — `bdgrz.risk.assessment.recorded` v1
- [RiskControlTreatmentProposed](../../src/Compliance.Common/Features/Risks/Events/RiskControlTreatmentProposed.cs#L6) — `bdgrz.risk.control_treatment.proposed` v1
- [RiskControlTreatmentRetired](../../src/Compliance.Common/Features/Risks/Events/RiskControlTreatmentRetired.cs#L7) — `bdgrz.risk.control_treatment.retired` v1
- [RiskControlTreatmentReviewed](../../src/Compliance.Common/Features/Risks/Events/RiskControlTreatmentReviewed.cs#L7) — `bdgrz.risk.control_treatment.reviewed` v1
- [RiskDraftCreated](../../src/Compliance.Common/Features/Risks/Events/RiskDraftCreated.cs#L8) — `bdgrz.risk.draft.created` v1
- [RiskDraftRevised](../../src/Compliance.Common/Features/Risks/Events/RiskDraftRevised.cs#L8) — `bdgrz.risk.draft.revised` v1
- [RiskMethodVersionPublished](../../src/Compliance.Common/Features/Risks/Events/RiskMethodVersionPublished.cs#L6) — `bdgrz.risk.method.version_published` v1
- [RiskOwnerAssigned](../../src/Compliance.Common/Features/Risks/Events/RiskOwnerAssigned.cs#L6) — `bdgrz.risk.owner.assigned` v1
- [RiskReassessmentTriggered](../../src/Compliance.Common/Features/Risks/Events/RiskReassessmentTriggered.cs#L6) — `bdgrz.risk.reassessment.triggered` v1
- [RiskTreatmentActionAdded](../../src/Compliance.Common/Features/Risks/Events/RiskTreatmentActionAdded.cs#L6) — `bdgrz.risk.treatment_action.added` v1
- [RiskTreatmentActionCancelled](../../src/Compliance.Common/Features/Risks/Events/RiskTreatmentActionCancelled.cs#L7) — `bdgrz.risk.treatment_action.cancelled` v1
- [RiskTreatmentActionCompletionReviewed](../../src/Compliance.Common/Features/Risks/Events/RiskTreatmentActionCompletionReviewed.cs#L7) — `bdgrz.risk.treatment_action.completion_reviewed` v1
- [RiskTreatmentActionCompletionSubmitted](../../src/Compliance.Common/Features/Risks/Events/RiskTreatmentActionCompletionSubmitted.cs#L7) — `bdgrz.risk.treatment_action.completion_submitted` v1
- [RiskTreatmentActionRevised](../../src/Compliance.Common/Features/Risks/Events/RiskTreatmentActionRevised.cs#L7) — `bdgrz.risk.treatment_action.revised` v1
- [RiskTreatmentChosen](../../src/Compliance.Common/Features/Risks/Events/RiskTreatmentChosen.cs#L6) — `bdgrz.risk.treatment.chosen` v1

#### Snapshots

- [PopulationSnapshotChunkStored](../../src/Compliance.Common/Features/Snapshots/Events/PopulationSnapshotChunkStored.cs#L11) — `bdgrz.snapshot.population.chunk_stored` v1
- [PopulationSnapshotFrozen](../../src/Compliance.Common/Features/Snapshots/Events/PopulationSnapshotFrozen.cs#L14) — `bdgrz.snapshot.population.frozen` v1
- [SnapshotFrozen](../../src/Compliance.Common/Features/Snapshots/Events/SnapshotFrozen.cs#L6) — `bdgrz.snapshot.frozen` v1

#### TechnologyInventory

- [DataFlowRevisionRecorded](../../src/Compliance.Common/Features/TechnologyInventory/Events/DataFlowRevisionRecorded.cs#L8) — `bdgrz.inventory.data_flow.revision_recorded` v1
- [InformationAssetRevisionRecorded](../../src/Compliance.Common/Features/TechnologyInventory/Events/InformationAssetRevisionRecorded.cs#L8) — `bdgrz.inventory.information_asset.revision_recorded` v1
- [LocationRevisionRecorded](../../src/Compliance.Common/Features/TechnologyInventory/Events/LocationRevisionRecorded.cs#L8) — `bdgrz.inventory.location.revision_recorded` v1
- [OperationalProcessRevisionRecorded](../../src/Compliance.Common/Features/TechnologyInventory/Events/OperationalProcessRevisionRecorded.cs#L8) — `bdgrz.inventory.operational_process.revision_recorded` v1
- [TechnologyComponentRevisionRecorded](../../src/Compliance.Common/Features/TechnologyInventory/Events/TechnologyComponentRevisionRecorded.cs#L8) — `bdgrz.inventory.component.revision_recorded` v1

#### Tenants

- [PlatformOperatorGranted](../../src/Compliance.Common/Features/Tenants/Events/PlatformOperatorGranted.cs#L6) — `bdgrz.platform.operator.granted` v1
- [PlatformOperatorRevoked](../../src/Compliance.Common/Features/Tenants/Events/PlatformOperatorRevoked.cs#L6) — `bdgrz.platform.operator.revoked` v1
- [PlatformOperatorRosterSeeded](../../src/Compliance.Common/Features/Tenants/Events/PlatformOperatorRosterSeeded.cs#L6) — `bdgrz.platform.operator-roster.seeded` v1
- [TenantActivated](../../src/Compliance.Common/Features/Tenants/Events/TenantActivated.cs#L6) — `bdgrz.tenant.activated` v1
- [TenantInvitationAccepted](../../src/Compliance.Common/Features/Tenants/Events/TenantInvitationAccepted.cs#L6) — `bdgrz.tenant-invitation.accepted` v1
- [TenantInvitationDeliveryFailed](../../src/Compliance.Common/Features/Tenants/TenantInvitationDeliveryFailed.cs#L6) — `bdgrz.tenant-invitation.delivery.failed` v1
- [TenantInvitationDeliverySent](../../src/Compliance.Common/Features/Tenants/TenantInvitationDeliverySent.cs#L6) — `bdgrz.tenant-invitation.delivery.sent` v1
- [TenantMemberInvited](../../src/Compliance.Common/Features/Tenants/Events/TenantMemberInvited.cs#L6) — `bdgrz.tenant-member.invited` v1
- [TenantOffboardingStarted](../../src/Compliance.Common/Features/Tenants/Events/TenantOffboardingStarted.cs#L6) — `bdgrz.tenant.offboarding.started` v1
- [TenantOwnerRegistered](../../src/Compliance.Common/Features/Tenants/TenantOwnerRegistered.cs#L6) — `bdgrz.tenant-owner.registered` v1
- [TenantReactivated](../../src/Compliance.Common/Features/Tenants/Events/TenantReactivated.cs#L6) — `bdgrz.tenant.reactivated` v1
- [TenantRegistered](../../src/Compliance.Common/Features/Tenants/Events/TenantRegistered.cs#L6) — `bdgrz.tenant.registered` v1
- [TenantSlugChangeRejected](../../src/Compliance.Common/Features/Tenants/Events/TenantSlugChangeRejected.cs#L6) — `bdgrz.tenant-slug.change-rejected` v1
- [TenantSlugChangeRequested](../../src/Compliance.Common/Features/Tenants/Events/TenantSlugChangeRequested.cs#L7) — `bdgrz.tenant-slug.change-requested` v1
- [TenantSlugChanged](../../src/Compliance.Common/Features/Tenants/Events/TenantSlugChanged.cs#L7) — `bdgrz.tenant-slug.changed` v1
- [TenantSlugConfirmed](../../src/Compliance.Common/Features/Tenants/Events/TenantSlugConfirmed.cs#L6) — `bdgrz.tenant-slug.confirmed` v1
- [TenantSlugRegistered](../../src/Compliance.Common/Features/Tenants/Events/TenantSlugRegistered.cs#L6) — `bdgrz.tenant-slug.registered` v1
- [TenantSlugRegistrationRejected](../../src/Compliance.Common/Features/Tenants/Events/TenantSlugRegistrationRejected.cs#L6) — `bdgrz.tenant-slug.registration-rejected` v1
- [TenantSlugRejected](../../src/Compliance.Common/Features/Tenants/Events/TenantSlugRejected.cs#L6) — `bdgrz.tenant-slug.rejected` v1
- [TenantSlugSurrenderConfirmed](../../src/Compliance.Common/Features/Tenants/Events/TenantSlugSurrenderConfirmed.cs#L6) — `bdgrz.tenant-slug-surrender.confirmed` v1
- [TenantSlugSurrenderFailed](../../src/Compliance.Common/Features/Tenants/Events/TenantSlugSurrenderFailed.cs#L6) — `bdgrz.tenant-slug-surrender.failed` v1
- [TenantSlugSurrenderRejected](../../src/Compliance.Common/Features/Tenants/Events/TenantSlugSurrenderRejected.cs#L6) — `bdgrz.tenant-slug.surrender-rejected` v1
- [TenantSlugSurrenderRequested](../../src/Compliance.Common/Features/Tenants/Events/TenantSlugSurrenderRequested.cs#L6) — `bdgrz.tenant-slug.surrender-requested` v1
- [TenantSlugSurrendered](../../src/Compliance.Common/Features/Tenants/Events/TenantSlugSurrendered.cs#L6) — `bdgrz.tenant-slug.surrendered` v1
- [TenantSuspended](../../src/Compliance.Common/Features/Tenants/Events/TenantSuspended.cs#L6) — `bdgrz.tenant.suspended` v1

#### UserIdentities

- [EmailAddressReserved](../../src/Compliance.Common/Features/UserIdentities/Events/EmailAddressReserved.cs#L6) — `bdgrz.email-address.reserved` v1
- [EmailAddressVerified](../../src/Compliance.Common/Features/UserIdentities/Events/EmailAddressVerified.cs#L6) — `bdgrz.email-address.verified` v1
- [EmailChallengeIssued](../../src/Compliance.Common/Features/UserIdentities/Events/EmailChallengeIssued.cs#L6) — `bdgrz.email-address.challenge-issued` v1
- [IdentityRecoveryChallengeClaimed](../../src/Compliance.Common/Features/UserIdentities/Events/IdentityRecoveryChallengeClaimed.cs#L7) — `bdgrz.identity-recovery.challenge-claimed` v1
- [IdentityRecoveryChallengeCompleted](../../src/Compliance.Common/Features/UserIdentities/Events/IdentityRecoveryChallengeCompleted.cs#L7) — `bdgrz.identity-recovery.challenge-completed` v1
- [IdentityRecoveryChallengeIssued](../../src/Compliance.Common/Features/UserIdentities/Events/IdentityRecoveryChallengeIssued.cs#L7) — `bdgrz.identity-recovery.challenge-issued` v1
- [UserIdentityAuthenticated](../../src/Compliance.Common/Features/UserIdentities/Events/UserIdentityAuthenticated.cs#L7) — `bdgrz.user-identity.authenticated` v1
- [UserIdentityProfileObserved](../../src/Compliance.Common/Features/UserIdentities/Events/UserIdentityProfileObserved.cs#L7) — `bdgrz.user-identity.profile-observed` v2
- [UserIdentityRegistered](../../src/Compliance.Common/Features/UserIdentities/Events/UserIdentityRegistered.cs#L7) — `bdgrz.user-identity.registered` v1
- [UserIdentityRevoked](../../src/Compliance.Common/Features/UserIdentities/Events/UserIdentityRevoked.cs#L7) — `bdgrz.user-identity.revoked` v1

#### Work

- [WorkDigestPreferenceChanged](../../src/Compliance.Common/Features/Work/Events/WorkDigestPreferenceChanged.cs#L7) — `bdgrz.work.digest_preference.changed` v1
- [WorkItemAssigned](../../src/Compliance.Common/Features/Work/Events/WorkItemAssigned.cs#L8) — `bdgrz.work.assigned` v1
- [WorkItemEscalated](../../src/Compliance.Common/Features/Work/Events/WorkItemEscalated.cs#L8) — `bdgrz.work.escalated` v1

#### Workforce

- [PersonMembershipCorrelated](../../src/Compliance.Common/Features/Workforce/Events/PersonMembershipCorrelated.cs#L11) — `bdgrz.workforce.person.membership-correlated` v1
- [PersonRecorded](../../src/Compliance.Common/Features/Workforce/Events/PersonRecorded.cs#L7) — `bdgrz.workforce.person.recorded` v1
- [PersonRevised](../../src/Compliance.Common/Features/Workforce/Events/PersonRevised.cs#L7) — `bdgrz.workforce.person.revised` v1
- [ServiceIdentityRecorded](../../src/Compliance.Common/Features/Workforce/Events/ServiceIdentityRecorded.cs#L7) — `bdgrz.workforce.service-identity.recorded` v1
- [ServiceIdentityRevised](../../src/Compliance.Common/Features/Workforce/Events/ServiceIdentityRevised.cs#L7) — `bdgrz.workforce.service-identity.revised` v1
- [WorkRelationshipRecorded](../../src/Compliance.Common/Features/Workforce/Events/WorkRelationshipRecorded.cs#L7) — `bdgrz.workforce.work-relationship.recorded` v1
- [WorkRelationshipRevised](../../src/Compliance.Common/Features/Workforce/Events/WorkRelationshipRevised.cs#L7) — `bdgrz.workforce.work-relationship.revised` v1
- [WorkforceObservationResolved](../../src/Compliance.Common/Features/Workforce/Events/WorkforceObservationResolved.cs#L7) — `bdgrz.workforce.observation.resolved` v1
- [WorkforceSourceObserved](../../src/Compliance.Common/Features/Workforce/Events/WorkforceSourceObserved.cs#L7) — `bdgrz.workforce.source.observed` v1
- [WorkforceSourceReconciled](../../src/Compliance.Common/Features/Workforce/Events/WorkforceSourceReconciled.cs#L6) — `bdgrz.workforce.source.reconciled` v1

### Existing aggregates

#### AccessControl

- [AccessGrant](../../src/Compliance.Core/Features/AccessControl/Aggregates/AccessGrant.cs#L5)
- [FirmStaffRecord](../../src/Compliance.Core/Features/AccessControl/Aggregates/FirmStaffRecord.cs#L12)
- [FirmStaffIdentityClaim](../../src/Compliance.Core/Features/AccessControl/Aggregates/FirmStaffIdentityClaim.cs#L11)
- [FirmProfessionalDutyDesignation](../../src/Compliance.Core/Features/AccessControl/Aggregates/FirmProfessionalDutyDesignation.cs#L16)
- [ProfessionalAssertionIdentityClaim](../../src/Compliance.Core/Features/AccessControl/Aggregates/ProfessionalAssertionIdentityClaim.cs#L15)
- [ClientServiceIndependence](../../src/Compliance.Core/Features/AccessControl/Aggregates/ClientServiceIndependence.cs#L11)
- [ClientIndependenceDecision](../../src/Compliance.Core/Features/AccessControl/Aggregates/ClientIndependenceDecision.cs#L10)
- [ServiceEngagementIndependence](../../src/Compliance.Core/Features/AccessControl/Aggregates/ServiceEngagementIndependence.cs#L11)
- [PracticeIndependenceCommitment](../../src/Compliance.Core/Features/AccessControl/Aggregates/PracticeIndependenceCommitment.cs#L19)
- [IndependenceRuleRatificationCatalog](../../src/Compliance.Core/Features/AccessControl/Aggregates/IndependenceRuleRatificationCatalog.cs#L8)
- [IndependenceRuleCatalog](../../src/Compliance.Core/Features/AccessControl/Aggregates/IndependenceRuleCatalog.cs#L8)
- [Member](../../src/Compliance.Core/Features/AccessControl/Aggregates/Member.cs#L5)
- [Role](../../src/Compliance.Core/Features/AccessControl/Aggregates/Role.cs#L5)
- [RolePermission](../../src/Compliance.Core/Features/AccessControl/Aggregates/RolePermission.cs#L8)
- [SeparationOfDutiesWaiver](../../src/Compliance.Core/Features/AccessControl/Aggregates/SeparationOfDutiesWaiver.cs#L7)
- [Team](../../src/Compliance.Core/Features/AccessControl/Aggregates/Team.cs#L5)
- [TeamMember](../../src/Compliance.Core/Features/AccessControl/Aggregates/TeamMember.cs#L5)
- [TeamRole](../../src/Compliance.Core/Features/AccessControl/Aggregates/TeamRole.cs#L5)
- [TenantManagerGuard](../../src/Compliance.Core/Features/AccessControl/Aggregates/TenantManagerGuard.cs#L11)

#### AccessReviews

- [AccessExpectation](../../src/Compliance.Core/Features/AccessReviews/Aggregates/AccessExpectation.cs#L18)
- [AccessPopulation](../../src/Compliance.Core/Features/AccessReviews/Aggregates/AccessPopulation.cs#L16)
- [AccessPopulationException](../../src/Compliance.Core/Features/AccessReviews/Aggregates/AccessPopulationException.cs#L16)
- [AccessPrincipalClassification](../../src/Compliance.Core/Features/AccessReviews/Aggregates/AccessPrincipalClassification.cs#L15)
- [AccessReviewCampaign](../../src/Compliance.Core/Features/AccessReviews/Aggregates/AccessReviewCampaign.cs#L11)
- [AccessReviewItem](../../src/Compliance.Core/Features/AccessReviews/Aggregates/AccessReviewItem.cs#L17)

#### AdvisorCollaboration

- [AdvisorHandoff](../../src/Compliance.Core/Features/AdvisorCollaboration/Aggregates/AdvisorHandoff.cs#L9)

#### AuditorRequests

- [AuditorRequest](../../src/Compliance.Core/Features/AuditorRequests/Aggregates/AuditorRequest.cs#L13)

#### AuditPackages

- [AuditPackage](../../src/Compliance.Core/Features/AuditPackages/AuditPackage.cs#L17)

#### AuditPopulations

- [AuditPopulation](../../src/Compliance.Core/Features/AuditPopulations/Aggregates/AuditPopulation.cs#L14)

#### Applications

- [ApplicationImportBatch](../../src/Compliance.Core/Features/Applications/Aggregates/ApplicationImportBatch.cs#L19)
- [ApplicationImportSource](../../src/Compliance.Core/Features/Applications/Aggregates/ApplicationImportSource.cs#L16)
- [DeclaredApplication](../../src/Compliance.Core/Features/Applications/Aggregates/DeclaredApplication.cs#L8)
- [DeclaredSystemInstance](../../src/Compliance.Core/Features/Applications/Aggregates/DeclaredSystemInstance.cs#L8)
- [SystemInstanceAccessReviewScope](../../src/Compliance.Core/Features/Applications/Aggregates/SystemInstanceAccessReviewScope.cs#L10)

#### Boundaries

- [SystemBoundary](../../src/Compliance.Core/Features/Boundaries/Aggregates/SystemBoundary.cs#L7)

#### Changes

- [SignificantChange](../../src/Compliance.Core/Features/Changes/Aggregates/SignificantChange.cs#L10)

#### Commitments

- [CommitmentDraft](../../src/Compliance.Core/Features/Commitments/Aggregates/CommitmentDraft.cs#L9)

#### ControlMappings

- [ControlCriterionMapping](../../src/Compliance.Core/Features/ControlMappings/Aggregates/ControlCriterionMapping.cs#L15)
- [CriterionApplicability](../../src/Compliance.Core/Features/ControlMappings/Aggregates/CriterionApplicability.cs#L15)

#### Controls

- [ControlDraft](../../src/Compliance.Core/Features/Controls/Aggregates/ControlDraft.cs#L13)

#### Criteria

- [CriteriaTextOverlay](../../src/Compliance.Core/Features/Criteria/Aggregates/CriteriaTextOverlay.cs#L15)

#### Evaluations

- [ControlEvaluation](../../src/Compliance.Core/Features/Evaluations/Aggregates/ControlEvaluation.cs#L16)
- [ControlEvaluationPlan](../../src/Compliance.Core/Features/Evaluations/Aggregates/ControlEvaluationPlan.cs#L9)

#### Evidence

- [EvidenceArtifact](../../src/Compliance.Core/Features/Evidence/Aggregates/EvidenceArtifact.cs#L12)
- [EvidenceRedaction](../../src/Compliance.Core/Features/Evidence/Aggregates/EvidenceRedaction.cs#L9)
- [EvidenceSupport](../../src/Compliance.Core/Features/Evidence/Aggregates/EvidenceSupport.cs#L9)
- [EvidenceRequestLedger](../../src/Compliance.Core/Features/Evidence/EvidenceRequestLedger.cs#L11)

#### Exports

- [ClientExport](../../src/Compliance.Core/Features/Exports/Aggregates/ClientExport.cs#L14)

#### Examinations

- [ExaminationEngagement](../../src/Compliance.Core/Features/Examinations/Aggregates/ExaminationEngagement.cs#L13)
- [ManagementAssertion](../../src/Compliance.Core/Features/Examinations/Aggregates/ManagementAssertion.cs#L12)
- [ManagementRepresentationLetter](../../src/Compliance.Core/Features/Examinations/Aggregates/ManagementRepresentationLetter.cs#L10)
- [AuditOutcome](../../src/Compliance.Core/Features/Examinations/Aggregates/AuditOutcome.cs#L10)
- [ExaminationSignOff](../../src/Compliance.Core/Features/Examinations/Aggregates/ExaminationSignOff.cs#L11)
- [TypeIIPlan](../../src/Compliance.Core/Features/Examinations/Aggregates/TypeIIPlan.cs#L14)

#### Incidents

- [ComplianceIncident](../../src/Compliance.Core/Features/Incidents/Aggregates/ComplianceIncident.cs#L10)

#### MethodologyTemplates

- [MethodologyTemplate](../../src/Compliance.Core/Features/MethodologyTemplates/Aggregates/MethodologyTemplate.cs#L10)
- [MethodologyTemplateContribution](../../src/Compliance.Core/Features/MethodologyTemplates/Aggregates/MethodologyTemplateContribution.cs#L10)
- [TemplateApplication](../../src/Compliance.Core/Features/MethodologyTemplates/Aggregates/TemplateApplication.cs#L10)

#### SystemDescription

- [SystemDescription](../../src/Compliance.Core/Features/SystemDescription/Aggregates/SystemDescription.cs#L11)

#### Operations

- [ControlOperatingPlan](../../src/Compliance.Core/Features/Operations/Aggregates/ControlOperatingPlan.cs#L14)
- [ControlOccurrence](../../src/Compliance.Core/Features/Operations/Aggregates/ControlOccurrence.cs#L15)
- [TypeIIPeriod](../../src/Compliance.Core/Features/Operations/Aggregates/TypeIIPeriod.cs#L17)
- [TypeIIObservationCalendar](../../src/Compliance.Core/Features/Operations/Aggregates/TypeIIObservationCalendar.cs#L8)

#### Policies

- [Policy](../../src/Compliance.Core/Features/Policies/Aggregates/Policy.cs#L13)

#### PolicyDistribution

- [PolicyDistributionCampaign](../../src/Compliance.Core/Features/PolicyDistribution/Aggregates/PolicyDistributionCampaign.cs#L18)
- [PolicyCampaignParticipant](../../src/Compliance.Core/Features/PolicyDistribution/Aggregates/PolicyCampaignParticipant.cs#L16)
- [TrainingRequirement](../../src/Compliance.Core/Features/PolicyDistribution/Aggregates/TrainingRequirement.cs#L14)

#### Programs

- [ClientService](../../src/Compliance.Core/Features/Programs/Aggregates/ClientService.cs#L7)
- [ComplianceProgram](../../src/Compliance.Core/Features/Programs/Aggregates/ComplianceProgram.cs#L7)
- [ProgramRollover](../../src/Compliance.Core/Features/Programs/Aggregates/ProgramRollover.cs#L15)

#### Providers

- [ProviderAssuranceGap](../../src/Compliance.Core/Features/Providers/Aggregates/ProviderAssuranceGap.cs)
- [ProviderAssuranceReport](../../src/Compliance.Core/Features/Providers/Aggregates/ProviderAssuranceReport.cs)
- [ProviderAssuranceReview](../../src/Compliance.Core/Features/Providers/Aggregates/ProviderAssuranceReview.cs)
- [ProviderRegister](../../src/Compliance.Core/Features/Providers/ProviderRegister.cs#L9)
- [ProviderRiskAcceptance](../../src/Compliance.Core/Features/Providers/Aggregates/ProviderRiskAcceptance.cs)

#### Readiness

- [ReadinessLedger](../../src/Compliance.Core/Features/Readiness/ReadinessLedger.cs#L12)

#### Remediation

- [CorrectiveAction](../../src/Compliance.Core/Features/Remediation/Aggregates/CorrectiveAction.cs)

#### Retention

- [ArtifactDisclosure](../../src/Compliance.Core/Features/Retention/Aggregates/ArtifactDisclosure.cs#L10)
- [ArtifactDisposition](../../src/Compliance.Core/Features/Retention/Aggregates/ArtifactDisposition.cs#L11)
- [ArtifactEngagementHold](../../src/Compliance.Core/Features/Retention/Aggregates/ArtifactEngagementHold.cs#L9)
- [ArtifactRetention](../../src/Compliance.Core/Features/Retention/ArtifactRetention.cs#L7)
- [EvidenceHandlingPolicy](../../src/Compliance.Core/Features/Retention/Aggregates/EvidenceHandlingPolicy.cs#L10)

#### Risks

- [RiskDraft](../../src/Compliance.Core/Features/Risks/Aggregates/RiskDraft.cs#L8)
- [RiskEvaluation](../../src/Compliance.Core/Features/Risks/Aggregates/RiskEvaluation.cs#L11)
- [RiskGovernanceLedger](../../src/Compliance.Core/Features/Risks/RiskGovernanceLedger.cs#L13)
- [RiskMethod](../../src/Compliance.Core/Features/Risks/Aggregates/RiskMethod.cs#L8)

#### Snapshots

- [ImmutableSnapshot](../../src/Compliance.Core/Features/Snapshots/Aggregates/ImmutableSnapshot.cs#L5)
- [PopulationSnapshot](../../src/Compliance.Core/Features/Snapshots/Aggregates/PopulationSnapshot.cs#L14)
- [PopulationSnapshotChunk](../../src/Compliance.Core/Features/Snapshots/Aggregates/PopulationSnapshotChunk.cs#L10)

#### TechnologyInventory

- [DataFlow](../../src/Compliance.Core/Features/TechnologyInventory/Aggregates/DataFlow.cs#L8)
- [InformationAsset](../../src/Compliance.Core/Features/TechnologyInventory/Aggregates/InformationAsset.cs#L8)
- [LocationRegister](../../src/Compliance.Core/Features/TechnologyInventory/LocationRegister.cs#L11)
- [OperationalProcessRegister](../../src/Compliance.Core/Features/TechnologyInventory/OperationalProcessRegister.cs#L11)
- [TechnologyComponent](../../src/Compliance.Core/Features/TechnologyInventory/Aggregates/TechnologyComponent.cs#L8)

#### Tenants

- [PlatformOperatorRoster](../../src/Compliance.Core/Features/Tenants/Aggregates/PlatformOperatorRoster.cs#L9)
- [Tenant](../../src/Compliance.Core/Features/Tenants/Aggregates/Tenant.cs#L6)
- [TenantInvitation](../../src/Compliance.Core/Features/Tenants/Aggregates/TenantInvitation.cs#L9)
- [TenantOwner](../../src/Compliance.Core/Features/Tenants/TenantOwner.cs#L6)
- [TenantSlug](../../src/Compliance.Core/Features/Tenants/Aggregates/TenantSlug.cs#L6)

#### UserIdentities

- [EmailAddress](../../src/Compliance.Core/Features/UserIdentities/Aggregates/EmailAddress.cs#L9)
- [TenantIdentityProvider](../../src/Compliance.Core/Features/UserIdentities/Aggregates/TenantIdentityProvider.cs#L11)
- [UserIdentity](../../src/Compliance.Core/Features/UserIdentities/Aggregates/UserIdentity.cs#L7)

#### Work

- [WorkItemAssignment](../../src/Compliance.Core/Features/Work/Aggregates/WorkItemAssignment.cs#L10)
- [LegacyWorkAssignmentLedger](../../src/Compliance.Core/Features/Work/Aggregates/LegacyWorkAssignmentLedger.cs#L6) (read-only legacy stream adapter)
- [WorkDigestDispatch](../../src/Compliance.Core/Features/Work/Aggregates/WorkDigestDispatch.cs#L6)
- [WorkDigestPreference](../../src/Compliance.Core/Features/Work/Aggregates/WorkDigestPreference.cs#L9)
- [WeeklyWorkDigest](../../src/Compliance.Core/Features/Work/Aggregates/WeeklyWorkDigest.cs#L11)

#### Workforce

- [Person](../../src/Compliance.Core/Features/Workforce/Aggregates/Person.cs#L8)
- [ServiceIdentity](../../src/Compliance.Core/Features/Workforce/Aggregates/ServiceIdentity.cs#L12)
- [WorkRelationship](../../src/Compliance.Core/Features/Workforce/Aggregates/WorkRelationship.cs#L11)
- [WorkforceObservationResolution](../../src/Compliance.Core/Features/Workforce/Aggregates/WorkforceObservationResolution.cs#L11)
- [WorkforceSourceObservation](../../src/Compliance.Core/Features/Workforce/Aggregates/WorkforceSourceObservation.cs#L8)

### Existing models and supporting contracts

#### AccessControl

- [AccessGrantPrincipal](../../src/Compliance.Common/Features/AccessControl/Models/AccessGrantPrincipal.cs#L5)
- [AccessGrantPrincipalKind](../../src/Compliance.Common/Features/AccessControl/AccessGrantPrincipalKind.cs#L6)
- [AccessGrantProposal](../../src/Compliance.Common/Features/AccessControl/Models/AccessGrantProposal.cs#L5)
- [AccessGrantScope](../../src/Compliance.Common/Features/AccessControl/Models/AccessGrantScope.cs#L5)
- [AccessGrantScopeKind](../../src/Compliance.Common/Features/AccessControl/AccessGrantScopeKind.cs#L6)
- [AccessGrantSetView](../../src/Compliance.Common/Features/AccessControl/Models/AccessGrantSetView.cs#L5)
- [AccessGrantSource](../../src/Compliance.Common/Features/AccessControl/Models/AccessGrantSource.cs#L3)
- [AccessGrantTerms](../../src/Compliance.Common/Features/AccessControl/Models/AccessGrantTerms.cs#L5)
- [AccessGrantView](../../src/Compliance.Common/Features/AccessControl/Models/AccessGrantView.cs#L5)
- [ActorReference](../../src/Compliance.Common/Features/AccessControl/Models/ActorReference.cs#L6)
- [AssignmentIndependenceReevaluationView](../../src/Compliance.Common/Features/AccessControl/Models/AssignmentIndependenceReevaluationView.cs#L6)
- [DirectoryIndependenceReevaluationView](../../src/Compliance.Common/Features/AccessControl/Models/DirectoryIndependenceReevaluationView.cs#L6)
- [DirectoryStatusSourceView](../../src/Compliance.Common/Features/AccessControl/Models/DirectoryStatusSourceView.cs#L6)
- [EngagementAcceptanceSourceTimes](../../src/Compliance.Common/Features/AccessControl/Models/EngagementAcceptanceSourceTimes.cs#L3)
- [EngagementActualAssignmentView](../../src/Compliance.Common/Features/AccessControl/Models/EngagementActualAssignmentView.cs#L5)
- [EngagementManagementAcknowledgementView](../../src/Compliance.Common/Features/AccessControl/Models/EngagementManagementAcknowledgementView.cs#L5)
- [FirmStaffDirectoryView](../../src/Compliance.Common/Features/AccessControl/Models/FirmStaffDirectoryView.cs#L5)
- [FirmStaffMemberView](../../src/Compliance.Common/Features/AccessControl/Models/FirmStaffMemberView.cs#L5)
- [HistoricalEngagementAssignmentView](../../src/Compliance.Common/Features/AccessControl/Models/HistoricalEngagementAssignmentView.cs#L5)
- [IClientManagementMutationRequest](../../src/Compliance.Common/Features/AccessControl/IClientManagementMutationRequest.cs#L6)
- [IClientRbacMutationRequest](../../src/Compliance.Common/Features/AccessControl/IClientRbacMutationRequest.cs#L4)
- [IFirmStaffAdministrationRequest](../../src/Compliance.Common/Features/AccessControl/IFirmStaffAdministrationRequest.cs#L5)
- [IIndependenceAdministrationRequest](../../src/Compliance.Common/Features/AccessControl/IIndependenceAdministrationRequest.cs#L6)
- [IIndependenceRuleAdministrationRequest](../../src/Compliance.Common/Features/AccessControl/IIndependenceRuleAdministrationRequest.cs#L6)
- [IRbacManagementRequest](../../src/Compliance.Common/Features/AccessControl/IRbacManagementRequest.cs#L6)
- [ISeparationOfDutiesWaiverAdminRequest](../../src/Compliance.Common/Features/AccessControl/ISeparationOfDutiesWaiverAdminRequest.cs#L6)
- [ITenantAccessRequest](../../src/Compliance.Common/Features/AccessControl/ITenantAccessRequest.cs#L6)
- [IndependenceEvaluationView](../../src/Compliance.Common/Features/AccessControl/Models/IndependenceEvaluationView.cs#L6)
- [IndependenceHistoryView](../../src/Compliance.Common/Features/AccessControl/Models/IndependenceHistoryView.cs#L5)
- [IndependenceRuleContent](../../src/Compliance.Common/Features/AccessControl/Models/IndependenceRuleContent.cs#L3)
- [IndependenceRuleVersionView](../../src/Compliance.Common/Features/AccessControl/Models/IndependenceRuleVersionView.cs#L6)
- [IndependenceServiceRuleContent](../../src/Compliance.Common/Features/AccessControl/Models/IndependenceServiceRuleContent.cs#L3)
- [MemberAccessGrantPath](../../src/Compliance.Common/Features/AccessControl/Models/MemberAccessGrantPath.cs#L3)
- [MemberAccessPath](../../src/Compliance.Common/Features/AccessControl/Models/MemberAccessPath.cs#L5)
- [MemberAccessView](../../src/Compliance.Common/Features/AccessControl/Models/MemberAccessView.cs#L5)
- [NonattestServiceContent](../../src/Compliance.Common/Features/AccessControl/Models/NonattestServiceContent.cs#L5)
- [NonattestServiceView](../../src/Compliance.Common/Features/AccessControl/Models/NonattestServiceView.cs#L5)
- [RolePermissionView](../../src/Compliance.Common/Features/AccessControl/Models/RolePermissionView.cs#L5)
- [RoleTeamView](../../src/Compliance.Common/Features/AccessControl/Models/RoleTeamView.cs#L5)
- [RoleView](../../src/Compliance.Common/Features/AccessControl/Models/RoleView.cs#L5)
- [SeparationOfDutiesActions](../../src/Compliance.Common/Features/AccessControl/Models/SeparationOfDutiesWaiverScope.cs#L72)
- [SeparationOfDutiesRecordTypes](../../src/Compliance.Common/Features/AccessControl/Models/SeparationOfDutiesWaiverScope.cs#L13)
- [SeparationOfDutiesWaiverScope](../../src/Compliance.Common/Features/AccessControl/Models/SeparationOfDutiesWaiverScope.cs#L6)
- [SeparationOfDutiesWaiverView](../../src/Compliance.Common/Features/AccessControl/Models/SeparationOfDutiesWaiverView.cs#L7)
- [ServiceEngagementAcceptanceView](../../src/Compliance.Common/Features/AccessControl/Models/ServiceEngagementAcceptanceView.cs#L5)
- [ServiceEngagementBoundaryReference](../../src/Compliance.Common/Features/AccessControl/Models/ServiceEngagementBoundaryReference.cs#L5)
- [ServiceEngagementDraftContent](../../src/Compliance.Common/Features/AccessControl/Models/ServiceEngagementDraftContent.cs#L5)
- [ServiceEngagementStaffProposalView](../../src/Compliance.Common/Features/AccessControl/Models/ServiceEngagementStaffProposalView.cs#L5)
- [ServiceEngagementView](../../src/Compliance.Common/Features/AccessControl/Models/ServiceEngagementView.cs#L5)
- [ServiceIndependenceReevaluationView](../../src/Compliance.Common/Features/AccessControl/Models/ServiceIndependenceReevaluationView.cs#L6)
- [TeamMemberView](../../src/Compliance.Common/Features/AccessControl/Models/TeamMemberView.cs#L5)
- [TeamRoleView](../../src/Compliance.Common/Features/AccessControl/Models/TeamRoleView.cs#L5)
- [TeamView](../../src/Compliance.Common/Features/AccessControl/Models/TeamView.cs#L5)

#### AccessReviews

- [AccessAssignmentFact](../../src/Compliance.Common/Features/AccessReviews/Models/AccessAssignmentFact.cs#L7)
- [AccessDecisionView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessDecisionView.cs#L10)
- [AccessEntitlementFact](../../src/Compliance.Common/Features/AccessReviews/Models/AccessEntitlementFact.cs#L8)
- [AccessExpectationExceptionView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessExpectationExceptionView.cs#L7)
- [AccessExpectationParameters](../../src/Compliance.Common/Features/AccessReviews/Models/AccessExpectationParameters.cs#L9)
- [AccessExpectationView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessExpectationView.cs#L10)
- [AccessExpectationsView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessExpectationsView.cs#L6)
- [AccessGroupMemberFact](../../src/Compliance.Common/Features/AccessReviews/Models/AccessGroupMemberFact.cs#L7)
- [AccessPopulationAcceptance](../../src/Compliance.Common/Features/AccessReviews/Models/AccessPopulationAcceptance.cs#L6)
- [AccessPopulationExceptionView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessPopulationExceptionView.cs#L10)
- [AccessPopulationFacts](../../src/Compliance.Common/Features/AccessReviews/Models/AccessPopulationFacts.cs#L4)
- [AccessPopulationIssue](../../src/Compliance.Common/Features/AccessReviews/Models/AccessPopulationIssue.cs#L7)
- [AccessPopulationPreview](../../src/Compliance.Common/Features/AccessReviews/Models/AccessPopulationPreview.cs#L9)
- [AccessPopulationRegistration](../../src/Compliance.Common/Features/AccessReviews/Models/AccessPopulationRegistration.cs#L5)
- [AccessPopulationSummaryView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessPopulationSummaryView.cs#L6)
- [AccessPopulationView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessPopulationView.cs#L11)
- [AccessPrincipalClassificationView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessPrincipalClassificationView.cs#L11)
- [AccessPrincipalFact](../../src/Compliance.Common/Features/AccessReviews/Models/AccessPrincipalFact.cs#L8)
- [AccessPrincipalProposalView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessPrincipalProposalView.cs#L6)
- [AccessPrincipalView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessPrincipalView.cs#L9)
- [AccessRemediationChangeView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessRemediationChangeView.cs#L7)
- [AccessRemediationExceptionView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessRemediationExceptionView.cs#L7)
- [AccessRemediationVerificationView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessRemediationVerificationView.cs#L10)
- [AccessReviewAssignment](../../src/Compliance.Common/Features/AccessReviews/Models/AccessReviewAssignment.cs#L9)
- [AccessReviewCampaignCompletionView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessReviewCampaignCompletionView.cs#L7)
- [AccessReviewCampaignRegistration](../../src/Compliance.Common/Features/AccessReviews/Models/AccessReviewCampaignRegistration.cs#L5)
- [AccessReviewCampaignSummaryView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessReviewCampaignSummaryView.cs#L6)
- [AccessReviewCampaignView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessReviewCampaignView.cs#L10)
- [AccessReviewCoverageView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessReviewCoverageView.cs#L6)
- [AccessReviewInstanceCoverageView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessReviewInstanceCoverageView.cs#L10)
- [AccessReviewItemStateView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessReviewItemStateView.cs#L10)
- [AccessReviewItemView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessReviewItemView.cs#L9)
- [AccessReviewerView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessReviewerView.cs#L6)
- [AccessVarianceFinding](../../src/Compliance.Common/Features/AccessReviews/Models/AccessVarianceFinding.cs#L6)
- [AccessVarianceItemView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessVarianceItemView.cs#L10)
- [AccessVarianceView](../../src/Compliance.Common/Features/AccessReviews/Models/AccessVarianceView.cs#L6)
- [BulkAccessDecisionPreview](../../src/Compliance.Common/Features/AccessReviews/Models/BulkAccessDecisionPreview.cs#L9)
- [BulkAccessDecisionRejection](../../src/Compliance.Common/Features/AccessReviews/Models/BulkAccessDecisionRejection.cs#L9)
- [BulkAccessDecisionResult](../../src/Compliance.Common/Features/AccessReviews/Models/BulkAccessDecisionResult.cs#L4)
- [EffectiveAccessHop](../../src/Compliance.Common/Features/AccessReviews/Models/EffectiveAccessHop.cs#L7)
- [EffectiveAccessPathView](../../src/Compliance.Common/Features/AccessReviews/Models/EffectiveAccessPathView.cs#L4)
- [EffectiveAccessView](../../src/Compliance.Common/Features/AccessReviews/Models/EffectiveAccessView.cs#L8)
- [IAccessReviewMutationRequest](../../src/Compliance.Common/Features/AccessReviews/IAccessReviewMutationRequest.cs#L6)
- [IAccessReviewParticipantRequest](../../src/Compliance.Common/Features/AccessReviews/IAccessReviewParticipantRequest.cs#L9)
- [IAccessReviewRequest](../../src/Compliance.Common/Features/AccessReviews/IAccessReviewRequest.cs#L6)

#### Applications

- [AccessReviewScopeDecisionView](../../src/Compliance.Common/Features/Applications/Models/AccessReviewScopeDecisionView.cs#L6)
- [AccessReviewScopeStatusView](../../src/Compliance.Common/Features/Applications/Models/AccessReviewScopeStatusView.cs#L11)
- [AccessReviewScopeView](../../src/Compliance.Common/Features/Applications/Models/AccessReviewScopeView.cs#L9)
- [ApplicationBoundaryReferenceView](../../src/Compliance.Common/Features/Applications/Models/ApplicationBoundaryReferenceView.cs#L9)
- [ApplicationChangePreview](../../src/Compliance.Common/Features/Applications/Models/ApplicationChangePreview.cs#L6)
- [ApplicationIncomingRelationshipView](../../src/Compliance.Common/Features/Applications/Models/ApplicationIncomingRelationshipView.cs#L5)
- [ApplicationControlDraftReferenceView](../../src/Compliance.Common/Features/Applications/Models/ApplicationControlDraftReferenceView.cs#L9)
- [ApplicationFieldChange](../../src/Compliance.Common/Features/Applications/Models/ApplicationFieldChange.cs#L3)
- [ApplicationImportCorrelationView](../../src/Compliance.Common/Features/Applications/Models/ApplicationImportCorrelationView.cs#L5)
- [ApplicationImportEffectProof](../../src/Compliance.Common/Features/Applications/Models/ApplicationImportEffectProof.cs#L5)
- [ApplicationImportInputRow](../../src/Compliance.Common/Features/Applications/Models/ApplicationImportInputRow.cs#L4)
- [ApplicationImportMissingRow](../../src/Compliance.Common/Features/Applications/Models/ApplicationImportMissingRow.cs#L6)
- [ApplicationImportOmissionPreview](../../src/Compliance.Common/Features/Applications/Models/ApplicationImportOmissionPreview.cs#L5)
- [ApplicationImportOmissionProposalView](../../src/Compliance.Common/Features/Applications/Models/ApplicationImportOmissionProposalView.cs#L5)
- [ApplicationImportPlannedRow](../../src/Compliance.Common/Features/Applications/Models/ApplicationImportPlannedRow.cs#L5)
- [ApplicationImportPreviewRow](../../src/Compliance.Common/Features/Applications/Models/ApplicationImportPreviewRow.cs#L6)
- [ApplicationImportProgress](../../src/Compliance.Common/Features/Applications/Models/ApplicationImportProgress.cs#L5)
- [ApplicationImportRegistration](../../src/Compliance.Common/Features/Applications/Models/ApplicationImportRegistration.cs#L5)
- [ApplicationImportRejectedReport](../../src/Compliance.Common/Features/Applications/Models/ApplicationImportRejectedReport.cs#L6)
- [ApplicationImportRejectedRow](../../src/Compliance.Common/Features/Applications/Models/ApplicationImportRejectedRow.cs#L5)
- [ApplicationImportRetirementRow](../../src/Compliance.Common/Features/Applications/Models/ApplicationImportRetirementRow.cs#L5)
- [ApplicationImportRowView](../../src/Compliance.Common/Features/Applications/Models/ApplicationImportRowView.cs#L5)
- [ApplicationImportStagedRow](../../src/Compliance.Common/Features/Applications/Models/ApplicationImportStagedRow.cs#L5)
- [ApplicationImportView](../../src/Compliance.Common/Features/Applications/Models/ApplicationImportView.cs#L5)
- [ApplicationRegistration](../../src/Compliance.Common/Features/Applications/Models/ApplicationRegistration.cs#L5)
- [ApplicationRelationshipChange](../../src/Compliance.Common/Features/Applications/Models/ApplicationRelationshipChange.cs#L5)
- [ApplicationRelationshipPage](../../src/Compliance.Common/Features/Applications/Models/ApplicationRelationshipPage.cs#L5)
- [ApplicationRelationshipView](../../src/Compliance.Common/Features/Applications/Models/ApplicationRelationshipView.cs#L5)
- [ApplicationRevisionField](../../src/Compliance.Common/Features/Applications/Models/ApplicationRevisionField.cs#L4)
- [ApplicationRevisionView](../../src/Compliance.Common/Features/Applications/Models/ApplicationRevisionView.cs#L8)
- [ApplicationView](../../src/Compliance.Common/Features/Applications/Models/ApplicationView.cs#L8)
- [IApplicationInventoryRequest](../../src/Compliance.Common/Features/Applications/IApplicationInventoryRequest.cs#L6)
- [IApplicationInventoryWriteRequest](../../src/Compliance.Common/Features/Applications/IApplicationInventoryRequest.cs#L19)
- [IRestrictedApplicationResourceReadRequest](../../src/Compliance.Common/Features/Applications/IApplicationInventoryRequest.cs#L15)
- [RetirementView](../../src/Compliance.Common/Features/Applications/Models/RetirementView.cs#L6)
- [SystemInstanceRegistration](../../src/Compliance.Common/Features/Applications/Models/SystemInstanceRegistration.cs#L5)
- [SystemInstanceView](../../src/Compliance.Common/Features/Applications/Models/SystemInstanceView.cs#L8)

#### Boundaries

- [BoundaryAffectedRecord](../../src/Compliance.Common/Features/Boundaries/Models/BoundaryAffectedRecord.cs#L5)
- [BoundaryChange](../../src/Compliance.Common/Features/Boundaries/Models/BoundaryChange.cs#L5)
- [BoundaryContent](../../src/Compliance.Common/Features/Boundaries/Models/BoundaryContent.cs#L3)
- [BoundaryDecisionView](../../src/Compliance.Common/Features/Boundaries/Models/BoundaryDecisionView.cs#L7)
- [BoundaryImpactContribution](../../src/Compliance.Common/Features/Boundaries/Models/BoundaryImpactContribution.cs#L3)
- [BoundaryImpactPreview](../../src/Compliance.Common/Features/Boundaries/Models/BoundaryImpactPreview.cs#L5)
- [BoundaryRegistration](../../src/Compliance.Common/Features/Boundaries/Models/BoundaryRegistration.cs#L5)
- [BoundaryScopeEntry](../../src/Compliance.Common/Features/Boundaries/Models/BoundaryScopeEntry.cs#L5)
- [BoundaryVersionView](../../src/Compliance.Common/Features/Boundaries/Models/BoundaryVersionView.cs#L5)
- [BoundaryView](../../src/Compliance.Common/Features/Boundaries/Models/BoundaryView.cs#L5)
- [IBoundaryAuthoringRequest](../../src/Compliance.Common/Features/Boundaries/IBoundaryAuthoringRequest.cs#L5)
- [IBoundaryProgramReadRequest](../../src/Compliance.Common/Features/Boundaries/IBoundaryProgramResourceRequest.cs#L12)
- [IBoundaryProgramResourceRequest](../../src/Compliance.Common/Features/Boundaries/IBoundaryProgramResourceRequest.cs#L5)

#### Commitments

- [CommitmentChange](../../src/Compliance.Common/Features/Commitments/Models/CommitmentChange.cs#L3)
- [CommitmentDecisionView](../../src/Compliance.Common/Features/Commitments/Models/CommitmentDecisionView.cs#L7)
- [CommitmentDependent](../../src/Compliance.Common/Features/Commitments/Models/CommitmentDependent.cs#L5)
- [CommitmentDraftRegistration](../../src/Compliance.Common/Features/Commitments/Models/CommitmentDraftRegistration.cs#L5)
- [CommitmentDraftRevisionView](../../src/Compliance.Common/Features/Commitments/Models/CommitmentDraftRevisionView.cs#L7)
- [CommitmentDraftView](../../src/Compliance.Common/Features/Commitments/Models/CommitmentDraftView.cs#L7)
- [CommitmentImpactPreview](../../src/Compliance.Common/Features/Commitments/Models/CommitmentImpactPreview.cs#L10)
- [CommitmentUnlinkedContext](../../src/Compliance.Common/Features/Commitments/Models/CommitmentUnlinkedContext.cs#L4)
- [CommitmentVersionView](../../src/Compliance.Common/Features/Commitments/Models/CommitmentVersionView.cs#L11)

#### Compliance.Common

- [CommonAssembly](../../src/Compliance.Common/CommonAssembly.cs#L6)

#### ControlMappings

- [ControlCriterionMappingRegistration](../../src/Compliance.Common/Features/ControlMappings/Models/ControlCriterionMappingRegistration.cs#L5)
- [ControlCriterionMappingVersionView](../../src/Compliance.Common/Features/ControlMappings/Models/ControlCriterionMappingVersionView.cs#L10)
- [ControlCriterionMappingView](../../src/Compliance.Common/Features/ControlMappings/Models/ControlCriterionMappingView.cs#L10)
- [CriterionApplicabilityRegistration](../../src/Compliance.Common/Features/ControlMappings/Models/CriterionApplicabilityRegistration.cs#L5)
- [CriterionApplicabilityVersionView](../../src/Compliance.Common/Features/ControlMappings/Models/CriterionApplicabilityVersionView.cs#L10)
- [CriterionApplicabilityView](../../src/Compliance.Common/Features/ControlMappings/Models/CriterionApplicabilityView.cs#L11)
- [CriterionCoverageView](../../src/Compliance.Common/Features/ControlMappings/Models/CriterionCoverageView.cs#L12)
- [MappedControlReference](../../src/Compliance.Common/Features/ControlMappings/Models/MappedControlReference.cs#L10)

#### Controls

- [ControlAffectedRecord](../../src/Compliance.Common/Features/Controls/Models/ControlAffectedRecord.cs#L5)
- [ControlApplicabilityReference](../../src/Compliance.Common/Features/Controls/Models/ControlApplicabilityReference.cs#L5)
- [ControlChange](../../src/Compliance.Common/Features/Controls/Models/ControlChange.cs#L5)
- [ControlContentProvenance](../../src/Compliance.Common/Features/Controls/Models/ControlContentProvenance.cs#L10)
- [ControlDecisionView](../../src/Compliance.Common/Features/Controls/Models/ControlDecisionView.cs#L8)
- [ControlDraftContent](../../src/Compliance.Common/Features/Controls/Models/ControlDraftContent.cs#L3)
- [ControlDraftOwnerPersonView](../../src/Compliance.Common/Features/Controls/Models/ControlDraftOwnerPersonView.cs#L6)
- [ControlDraftRevisionView](../../src/Compliance.Common/Features/Controls/Models/ControlDraftRevisionView.cs#L7)
- [ControlDraftView](../../src/Compliance.Common/Features/Controls/Models/ControlDraftView.cs#L7)
- [ControlImpactContribution](../../src/Compliance.Common/Features/Controls/Models/ControlImpactContribution.cs#L8)
- [ControlImpactPreview](../../src/Compliance.Common/Features/Controls/Models/ControlImpactPreview.cs#L12)
- [ControlPersonOwnerDesignationView](../../src/Compliance.Common/Features/Controls/Models/ControlPersonOwnerDesignationView.cs#L7)
- [ControlRegistration](../../src/Compliance.Common/Features/Controls/Models/ControlRegistration.cs#L5)
- [ControlRetirementRegistration](../../src/Compliance.Common/Features/Controls/Models/ControlRetirementRegistration.cs#L5)
- [ControlSuccessorRegistration](../../src/Compliance.Common/Features/Controls/Models/ControlSuccessorRegistration.cs#L5)
- [ControlVersionIds](../../src/Compliance.Common/Features/Controls/Models/ControlVersionIds.cs#L7)
- [ControlVersionView](../../src/Compliance.Common/Features/Controls/Models/ControlVersionView.cs#L10)

#### Criteria

- [CriteriaCatalogEdition](../../src/Compliance.Common/Features/Criteria/Models/CriteriaCatalogEdition.cs#L11)
- [CriteriaOverlayUsageFlags](../../src/Compliance.Common/Features/Criteria/Models/CriteriaOverlayUsageFlags.cs#L4)
- [CriteriaSupportGap](../../src/Compliance.Common/Features/Criteria/Models/CriteriaSupportGap.cs#L4)
- [CriteriaTextOverlayContent](../../src/Compliance.Common/Features/Criteria/Models/CriteriaTextOverlayContent.cs#L4)
- [CriteriaTextOverlayMetadata](../../src/Compliance.Common/Features/Criteria/Models/CriteriaTextOverlayMetadata.cs#L5)
- [CriteriaTextOverlayRegistration](../../src/Compliance.Common/Features/Criteria/Models/CriteriaTextOverlayRegistration.cs#L5)
- [CriteriaTextOverlayRevision](../../src/Compliance.Common/Features/Criteria/Models/CriteriaTextOverlayRevision.cs#L7)
- [Criterion](../../src/Compliance.Common/Features/Criteria/Models/Criterion.cs#L11)

#### Evaluations

- [ControlEvaluationPlanVersionView](../../src/Compliance.Common/Features/Evaluations/Models/ControlEvaluationPlanVersionView.cs#L6)
- [ControlEvaluationReviewView](../../src/Compliance.Common/Features/Evaluations/Models/ControlEvaluationReviewView.cs#L7)
- [ControlEvaluationStepView](../../src/Compliance.Common/Features/Evaluations/Models/ControlEvaluationStepView.cs#L6)
- [ControlEvaluationView](../../src/Compliance.Common/Features/Evaluations/Models/ControlEvaluationView.cs#L11)
- [EvaluationAssertionConclusion](../../src/Compliance.Common/Features/Evaluations/Models/EvaluationAssertionConclusion.cs#L8)
- [EvaluationDeviationView](../../src/Compliance.Common/Features/Evaluations/Models/EvaluationDeviationView.cs#L11)
- [EvaluationInspectedItem](../../src/Compliance.Common/Features/Evaluations/Models/EvaluationInspectedItem.cs#L8)
- [EvaluationProcedureStep](../../src/Compliance.Common/Features/Evaluations/Models/EvaluationProcedureStep.cs#L7)
- [EvaluationStepResultView](../../src/Compliance.Common/Features/Evaluations/Models/EvaluationStepResultView.cs#L6)
- [EvaluationSubmissionView](../../src/Compliance.Common/Features/Evaluations/Models/EvaluationSubmissionView.cs#L9)

#### Evidence

- [EvidenceArtifactContent](../../src/Compliance.Common/Features/Evidence/Models/EvidenceArtifactContent.cs#L7)
- [EvidenceArtifactMetadataView](../../src/Compliance.Common/Features/Evidence/Models/EvidenceArtifactMetadataView.cs#L7)
- [EvidenceSupportView](../../src/Compliance.Common/Features/Evidence/Models/EvidenceSupportView.cs#L7)
- [EvidenceArtifactRegistration](../../src/Compliance.Common/Features/Evidence/Models/EvidenceArtifactRegistration.cs#L5)
- [EvidenceArtifactResourceTypes](../../src/Compliance.Common/Features/Evidence/Models/EvidenceArtifactResourceTypes.cs#L3)
- [EvidenceArtifactStates](../../src/Compliance.Common/Features/Evidence/Models/EvidenceArtifactStates.cs#L4)
- [EvidenceCapture](../../src/Compliance.Common/Features/Evidence/Models/EvidenceCapture.cs#L10)
- [EvidenceInspectionOutcome](../../src/Compliance.Common/Features/Evidence/EvidenceInspectionOutcome.cs#L4)
- [EvidenceRedactionApprovalFact](../../src/Compliance.Common/Features/Evidence/Models/EvidenceRedactionApprovalFact.cs#L6)
- [EvidenceRedactionApprovalView](../../src/Compliance.Common/Features/Evidence/Models/EvidenceRedactionApprovalView.cs#L6)
- [EvidenceRedactionArtifactView](../../src/Compliance.Common/Features/Evidence/Models/EvidenceRedactionArtifactView.cs#L6)
- [EvidenceRedactionPreparationFact](../../src/Compliance.Common/Features/Evidence/Models/EvidenceRedactionPreparationFact.cs#L6)
- [EvidenceRedactionPreparationView](../../src/Compliance.Common/Features/Evidence/Models/EvidenceRedactionPreparationView.cs#L6)
- [EvidenceRedactionSourceCapsule](../../src/Compliance.Common/Features/Evidence/Models/EvidenceRedactionSourceCapsule.cs#L6)
- [EvidenceRedactionView](../../src/Compliance.Common/Features/Evidence/Models/EvidenceRedactionView.cs#L6)
- [EvidenceRedactionWaiverSnapshot](../../src/Compliance.Common/Features/Evidence/Models/EvidenceRedactionWaiverSnapshot.cs#L7)
- [EvidenceRequestView](../../src/Compliance.Common/Features/Evidence/Models/EvidenceRequestView.cs#L10)
- [IEvidenceRedactionMutationRequest](../../src/Compliance.Common/Features/Evidence/IEvidenceRedactionMutationRequest.cs#L6)

#### Operations

- [ControlAttestationView](../../src/Compliance.Common/Features/Operations/Models/ControlAttestationView.cs#L11)
- [ControlCadence](../../src/Compliance.Common/Features/Operations/Models/ControlCadence.cs#L8)
- [ControlOccurrenceReviewView](../../src/Compliance.Common/Features/Operations/Models/ControlOccurrenceReviewView.cs#L10)
- [ControlOccurrenceView](../../src/Compliance.Common/Features/Operations/Models/ControlOccurrenceView.cs#L10)
- [ControlOperatingBlockerView](../../src/Compliance.Common/Features/Operations/Models/ControlOperatingBlockerView.cs#L10)
- [ControlOperatingPlanPreview](../../src/Compliance.Common/Features/Operations/Models/ControlOperatingPlanPreview.cs#L7)
- [ControlOperatingPlanSetView](../../src/Compliance.Common/Features/Operations/Models/ControlOperatingPlanSetView.cs#L6)
- [ControlOperatingPlanView](../../src/Compliance.Common/Features/Operations/Models/ControlOperatingPlanView.cs#L11)
- [ControlResponsibilityView](../../src/Compliance.Common/Features/Operations/Models/ControlResponsibilityView.cs#L6)
- [EvidenceReference](../../src/Compliance.Common/Features/Operations/Models/EvidenceReference.cs#L8)
- [IControlOperationRequest](../../src/Compliance.Common/Features/Operations/IControlOperationRequest.cs#L9)
- [IOperationsReactionRequest](../../src/Compliance.Common/Features/Operations/IOperationsReactionRequest.cs#L6)
- [MyControlWorkView](../../src/Compliance.Common/Features/Operations/Models/MyControlWorkView.cs#L4)
- [OccurrenceReassignmentView](../../src/Compliance.Common/Features/Operations/Models/OccurrenceReassignmentView.cs#L6)
- [OperatingHolder](../../src/Compliance.Common/Features/Operations/Models/OperatingHolder.cs#L9)
- [WorkItemView](../../src/Compliance.Common/Features/Operations/Models/WorkItemView.cs#L9)

#### Policies

- [PolicyApplicabilityReference](../../src/Compliance.Common/Features/Policies/Models/PolicyApplicabilityReference.cs#L11)
- [PolicyContent](../../src/Compliance.Common/Features/Policies/Models/PolicyContent.cs#L9)
- [PolicyDecisionView](../../src/Compliance.Common/Features/Policies/Models/PolicyDecisionView.cs#L10)
- [PolicyImpactPreview](../../src/Compliance.Common/Features/Policies/Models/PolicyImpactPreview.cs#L11)
- [PolicyRegistration](../../src/Compliance.Common/Features/Policies/Models/PolicyRegistration.cs#L5)
- [PolicyRetirementProposalView](../../src/Compliance.Common/Features/Policies/Models/PolicyRetirementProposalView.cs#L5)
- [PolicySummaryView](../../src/Compliance.Common/Features/Policies/Models/PolicySummaryView.cs#L6)
- [PolicyVersionView](../../src/Compliance.Common/Features/Policies/Models/PolicyVersionView.cs#L11)
- [PolicyView](../../src/Compliance.Common/Features/Policies/Models/PolicyView.cs#L12)

#### PolicyDistribution

- [CampaignAcknowledgementView](../../src/Compliance.Common/Features/PolicyDistribution/Models/CampaignAcknowledgementView.cs#L6)
- [CampaignAmendmentView](../../src/Compliance.Common/Features/PolicyDistribution/Models/CampaignAmendmentView.cs#L6)
- [CampaignAudienceAmendment](../../src/Compliance.Common/Features/PolicyDistribution/Models/CampaignAudienceAmendment.cs#L9)
- [CampaignAudienceEntry](../../src/Compliance.Common/Features/PolicyDistribution/Models/CampaignAudienceEntry.cs#L6)
- [CampaignCompletionView](../../src/Compliance.Common/Features/PolicyDistribution/Models/CampaignCompletionView.cs#L6)
- [CampaignParticipantView](../../src/Compliance.Common/Features/PolicyDistribution/Models/CampaignParticipantView.cs#L11)
- [CampaignReconciliationView](../../src/Compliance.Common/Features/PolicyDistribution/Models/CampaignReconciliationView.cs#L5)
- [CampaignRegistration](../../src/Compliance.Common/Features/PolicyDistribution/Models/CampaignRegistration.cs#L5)
- [CampaignSubject](../../src/Compliance.Common/Features/PolicyDistribution/Models/CampaignSubject.cs#L9)
- [CampaignSummaryView](../../src/Compliance.Common/Features/PolicyDistribution/Models/CampaignSummaryView.cs#L6)
- [CampaignTotalsView](../../src/Compliance.Common/Features/PolicyDistribution/Models/CampaignTotalsView.cs#L8)
- [CampaignView](../../src/Compliance.Common/Features/PolicyDistribution/Models/CampaignView.cs#L7)
- [CampaignWaiverView](../../src/Compliance.Common/Features/PolicyDistribution/Models/CampaignWaiverView.cs#L6)
- [TrainingRequirementContent](../../src/Compliance.Common/Features/PolicyDistribution/Models/TrainingRequirementContent.cs#L8)
- [TrainingRequirementRegistration](../../src/Compliance.Common/Features/PolicyDistribution/Models/TrainingRequirementRegistration.cs#L5)
- [TrainingRequirementView](../../src/Compliance.Common/Features/PolicyDistribution/Models/TrainingRequirementView.cs#L10)

#### Programs

- [ClientServiceRegistration](../../src/Compliance.Common/Features/Programs/Models/ClientServiceRegistration.cs#L5)
- [ClientServiceRevisionView](../../src/Compliance.Common/Features/Programs/Models/ClientServiceRevisionView.cs#L6)
- [ClientServiceView](../../src/Compliance.Common/Features/Programs/Models/ClientServiceView.cs#L6)
- [IClientServiceProgramReadRequest](../../src/Compliance.Common/Features/Programs/IClientServiceProgramResourceRequest.cs#L10)
- [IClientServiceProgramResourceRequest](../../src/Compliance.Common/Features/Programs/IClientServiceProgramResourceRequest.cs#L3)
- [IProgramManagementRequest](../../src/Compliance.Common/Features/Programs/IProgramManagementRequest.cs#L5)
- [IProgramReadRequest](../../src/Compliance.Common/Features/Programs/IProgramScopedRequest.cs#L16)
- [IProgramReadResourceRequest](../../src/Compliance.Common/Features/Programs/IProgramResourceRequest.cs#L15)
- [IProgramResourceRequest](../../src/Compliance.Common/Features/Programs/IProgramResourceRequest.cs#L6)
- [IProgramScopedRequest](../../src/Compliance.Common/Features/Programs/IProgramScopedRequest.cs#L6)
- [ProgramPlan](../../src/Compliance.Common/Features/Programs/Models/ProgramPlan.cs#L3)
- [ProgramRegistration](../../src/Compliance.Common/Features/Programs/Models/ProgramRegistration.cs#L5)
- [ProgramResourceKind](../../src/Compliance.Common/Features/Programs/ProgramResourceKind.cs#L3)
- [ProgramRevisionView](../../src/Compliance.Common/Features/Programs/Models/ProgramRevisionView.cs#L6)
- [ProgramSetupWorkItem](../../src/Compliance.Common/Features/Programs/Models/ProgramSetupWorkItem.cs#L5)
- [ProgramSetupWorkView](../../src/Compliance.Common/Features/Programs/Models/ProgramSetupWorkView.cs#L5)
- [ProgramStageView](../../src/Compliance.Common/Features/Programs/Models/ProgramStageView.cs#L3)
- [ProgramView](../../src/Compliance.Common/Features/Programs/Models/ProgramView.cs#L6)

#### Providers

- [AssuranceBridgeLetter](../../src/Compliance.Common/Features/Providers/Models/AssuranceBridgeLetter.cs#L7)
- [AssuranceExceptionNote](../../src/Compliance.Common/Features/Providers/Models/AssuranceExceptionNote.cs#L4)
- [AssuranceReportContent](../../src/Compliance.Common/Features/Providers/Models/AssuranceReportContent.cs#L9)
- [AssuranceReportCoverageView](../../src/Compliance.Common/Features/Providers/Models/AssuranceReportCoverageView.cs#L10)
- [AssuranceReportRegistration](../../src/Compliance.Common/Features/Providers/Models/AssuranceReportRegistration.cs#L5)
- [AssuranceReportView](../../src/Compliance.Common/Features/Providers/Models/AssuranceReportView.cs#L11)
- [IProviderManagementRequest](../../src/Compliance.Common/Features/Providers/IProviderManagementRequest.cs#L5)
- [IProviderRequest](../../src/Compliance.Common/Features/Providers/IProviderRequest.cs#L5)
- [ProviderAssuranceCoverageView](../../src/Compliance.Common/Features/Providers/Models/ProviderAssuranceCoverageView.cs#L15)
- [ProviderChangeAffectedRecord](../../src/Compliance.Common/Features/Providers/Models/ProviderChangeImpactPreview.cs#L17)
- [ProviderChangeImpactPreview](../../src/Compliance.Common/Features/Providers/Models/ProviderChangeImpactPreview.cs#L9)
- [ProviderChangeImpactSection](../../src/Compliance.Common/Features/Providers/Models/ProviderChangeImpactPreview.cs#L14)
- [ProviderContent](../../src/Compliance.Common/Features/Providers/Models/ProviderContent.cs#L6)
- [ProviderCoverageGapClosureContent](../../src/Compliance.Common/Features/Providers/Models/ProviderCoverageGapView.cs#L16)
- [ProviderCoverageGapClosureView](../../src/Compliance.Common/Features/Providers/Models/ProviderCoverageGapView.cs#L19)
- [ProviderCoverageGapContent](../../src/Compliance.Common/Features/Providers/Models/ProviderCoverageGapContent.cs#L6)
- [ProviderCoverageGapRegistration](../../src/Compliance.Common/Features/Providers/Models/ProviderCoverageGapView.cs#L36)
- [ProviderCoverageGapRiskAcceptanceContent](../../src/Compliance.Common/Features/Providers/Models/ProviderCoverageGapView.cs#L33)
- [ProviderCoverageGapRiskAcceptanceView](../../src/Compliance.Common/Features/Providers/Models/ProviderCoverageGapView.cs#L26)
- [ProviderCoverageGapView](../../src/Compliance.Common/Features/Providers/Models/ProviderCoverageGapView.cs#L7)
- [ProviderDependency](../../src/Compliance.Common/Features/Providers/Models/ProviderDependency.cs#L6)
- [ProviderRegistration](../../src/Compliance.Common/Features/Providers/Models/ProviderRegistration.cs#L5)
- [ProviderReviewContent](../../src/Compliance.Common/Features/Providers/Models/ProviderReviewContent.cs#L10)
- [ProviderReviewRegistration](../../src/Compliance.Common/Features/Providers/Models/ProviderReviewRegistration.cs#L5)
- [ProviderReviewView](../../src/Compliance.Common/Features/Providers/Models/ProviderReviewView.cs#L11)
- [ProviderSourceCitation](../../src/Compliance.Common/Features/Providers/Models/ProviderSourceCitation.cs#L6)
- [ProviderView](../../src/Compliance.Common/Features/Providers/Models/ProviderView.cs#L7)

#### Readiness

- [ReadinessAccessReviewScopeView](../../src/Compliance.Common/Features/Readiness/Models/ReadinessAccessReviewScopeView.cs#L6)
- [ReadinessAnnotationView](../../src/Compliance.Common/Features/Readiness/Models/ReadinessAnnotationView.cs#L10)
- [ReadinessAssessmentRegistration](../../src/Compliance.Common/Features/Readiness/Models/ReadinessAssessmentRegistration.cs#L5)
- [ReadinessAssessmentSummaryView](../../src/Compliance.Common/Features/Readiness/Models/ReadinessAssessmentSummaryView.cs#L6)
- [ReadinessAssessmentView](../../src/Compliance.Common/Features/Readiness/Models/ReadinessAssessmentView.cs#L11)
- [ReadinessDecisionView](../../src/Compliance.Common/Features/Readiness/Models/ReadinessDecisionView.cs#L7)
- [ReadinessFindingView](../../src/Compliance.Common/Features/Readiness/Models/ReadinessFindingView.cs#L9)
- [ReadinessGapPlanView](../../src/Compliance.Common/Features/Readiness/Models/ReadinessGapPlanView.cs#L7)
- [ReadinessGapView](../../src/Compliance.Common/Features/Readiness/Models/ReadinessGapView.cs#L6)
- [ReadinessInputView](../../src/Compliance.Common/Features/Readiness/Models/ReadinessInputView.cs#L8)
- [ReadinessSourceReference](../../src/Compliance.Common/Features/Readiness/Models/ReadinessSourceReference.cs#L6)
- [TypeIEntryDecisionView](../../src/Compliance.Common/Features/Readiness/Models/TypeIEntryDecisionView.cs#L12)
- [TypeIEntryUnresolvedItemView](../../src/Compliance.Common/Features/Readiness/Models/TypeIEntryUnresolvedItemView.cs#L6)

#### Remediation

- [CorrectiveActionView](../../src/Compliance.Common/Features/Remediation/Models/CorrectiveActionView.cs#L8)
- [FindingAcceptanceView](../../src/Compliance.Common/Features/Remediation/Models/FindingAcceptanceView.cs#L11)
- [FindingClosureView](../../src/Compliance.Common/Features/Remediation/Models/FindingClosureView.cs#L8)
- [FindingHistoryEntryView](../../src/Compliance.Common/Features/Remediation/Models/FindingHistoryEntryView.cs#L6)
- [FindingLink](../../src/Compliance.Common/Features/Remediation/Models/FindingLink.cs#L7)
- [FindingRegistration](../../src/Compliance.Common/Features/Remediation/Models/FindingRegistration.cs#L5)
- [FindingSource](../../src/Compliance.Common/Features/Remediation/Models/FindingSource.cs#L10)
- [FindingView](../../src/Compliance.Common/Features/Remediation/Models/FindingView.cs#L10)

#### Responsibilities

- [ResponsibilityAssignmentView](../../src/Compliance.Common/Features/Responsibilities/Models/ResponsibilityAssignmentView.cs#L5)
- [ResponsibilityConflictKind](../../src/Compliance.Common/Features/Responsibilities/ResponsibilityConflictKind.cs#L6)
- [ResponsibilityConflictPreview](../../src/Compliance.Common/Features/Responsibilities/Models/ResponsibilityConflictPreview.cs#L3)
- [ResponsibilityConflictView](../../src/Compliance.Common/Features/Responsibilities/Models/ResponsibilityConflictView.cs#L5)
- [ResponsibilityScope](../../src/Compliance.Common/Features/Responsibilities/Models/ResponsibilityScope.cs#L5)
- [ResponsibilitySetView](../../src/Compliance.Common/Features/Responsibilities/Models/ResponsibilitySetView.cs#L5)
- [ResponsibilityType](../../src/Compliance.Common/Features/Responsibilities/ResponsibilityType.cs#L6)
- [ResponsibilityTypeWireName](../../src/Compliance.Common/Features/Responsibilities/Models/ResponsibilityTypeWireName.cs#L3)

#### Retention

- [ArtifactLegalHoldView](../../src/Compliance.Common/Features/Retention/Models/ArtifactLegalHoldView.cs#L5)
- [ArtifactRetentionSource](../../src/Compliance.Common/Features/Retention/Models/ArtifactRetentionSource.cs#L6)
- [ArtifactRetentionView](../../src/Compliance.Common/Features/Retention/Models/ArtifactRetentionView.cs#L5)
- [IArtifactRetentionAdminRequest](../../src/Compliance.Common/Features/Retention/IArtifactRetentionAdminRequest.cs#L5)

#### Risks

- [IRiskReassessmentReactionRequest](../../src/Compliance.Common/Features/Risks/IRiskReassessmentReactionRequest.cs#L6)
- [RiskAcceptanceView](../../src/Compliance.Common/Features/Risks/Models/RiskAcceptanceView.cs#L7)
- [RiskAssessmentView](../../src/Compliance.Common/Features/Risks/Models/RiskAssessmentView.cs#L7)
- [RiskControlTreatmentRegistration](../../src/Compliance.Common/Features/Risks/Models/RiskControlTreatmentRegistration.cs#L5)
- [RiskControlTreatmentView](../../src/Compliance.Common/Features/Risks/Models/RiskControlTreatmentView.cs#L12)
- [RiskDraftContent](../../src/Compliance.Common/Features/Risks/Models/RiskDraftContent.cs#L3)
- [RiskDraftRevisionView](../../src/Compliance.Common/Features/Risks/Models/RiskDraftRevisionView.cs#L7)
- [RiskDraftView](../../src/Compliance.Common/Features/Risks/Models/RiskDraftView.cs#L7)
- [RiskEvaluationHistoryEntryView](../../src/Compliance.Common/Features/Risks/Models/RiskEvaluationHistoryEntryView.cs#L7)
- [RiskEvaluationView](../../src/Compliance.Common/Features/Risks/Models/RiskEvaluationView.cs#L6)
- [RiskGovernanceView](../../src/Compliance.Common/Features/Risks/Models/RiskGovernanceView.cs#L12)
- [RiskMethodVersionView](../../src/Compliance.Common/Features/Risks/Models/RiskMethodVersionView.cs#L7)
- [RiskOwnerView](../../src/Compliance.Common/Features/Risks/Models/RiskOwnerView.cs#L11)
- [RiskReassessmentTriggerView](../../src/Compliance.Common/Features/Risks/Models/RiskReassessmentTriggerView.cs#L11)
- [RiskRegistration](../../src/Compliance.Common/Features/Risks/Models/RiskRegistration.cs#L5)
- [RiskTreatmentActionCompletionRegistration](../../src/Compliance.Common/Features/Risks/Models/RiskTreatmentActionCompletionRegistration.cs#L5)
- [RiskTreatmentActionCompletionView](../../src/Compliance.Common/Features/Risks/Models/RiskTreatmentActionCompletionView.cs#L11)
- [RiskTreatmentActionRegistration](../../src/Compliance.Common/Features/Risks/Models/RiskTreatmentActionRegistration.cs#L5)
- [RiskTreatmentActionView](../../src/Compliance.Common/Features/Risks/Models/RiskTreatmentActionView.cs#L13)
- [RiskTreatmentView](../../src/Compliance.Common/Features/Risks/Models/RiskTreatmentView.cs#L5)

#### Snapshots

- [ISnapshotProgramReadRequest](../../src/Compliance.Common/Features/Snapshots/ISnapshotProgramResourceRequest.cs#L12)
- [ISnapshotProgramResourceRequest](../../src/Compliance.Common/Features/Snapshots/ISnapshotProgramResourceRequest.cs#L5)
- [PopulationRow](../../src/Compliance.Common/Features/Snapshots/Models/PopulationRow.cs#L5)
- [PopulationSnapshotManifestRegeneration](../../src/Compliance.Common/Features/Snapshots/Models/PopulationSnapshotManifestRegeneration.cs#L9)
- [PopulationSnapshotSummary](../../src/Compliance.Common/Features/Snapshots/Models/PopulationSnapshotSummary.cs#L7)
- [ProgramScopeManifest](../../src/Compliance.Common/Features/Snapshots/Models/ProgramScopeManifest.cs#L5)
- [ProgramScopeSnapshotManifestRegeneration](../../src/Compliance.Common/Features/Snapshots/Models/ProgramScopeSnapshotManifestRegeneration.cs#L5)
- [ProgramScopeSnapshotVerification](../../src/Compliance.Common/Features/Snapshots/Models/ProgramScopeSnapshotVerification.cs#L5)
- [SnapshotRegistration](../../src/Compliance.Common/Features/Snapshots/Models/SnapshotRegistration.cs#L5)
- [SnapshotSourceCheck](../../src/Compliance.Common/Features/Snapshots/Models/SnapshotSourceCheck.cs#L3)
- [SnapshotView](../../src/Compliance.Common/Features/Snapshots/Models/SnapshotView.cs#L5)

#### TechnologyInventory

- [DataFlowContent](../../src/Compliance.Common/Features/TechnologyInventory/Models/DataFlowContent.cs#L10)
- [DataFlowImpactView](../../src/Compliance.Common/Features/TechnologyInventory/Models/DataFlowImpactView.cs#L10)
- [DataFlowRegistration](../../src/Compliance.Common/Features/TechnologyInventory/Models/DataFlowRegistration.cs#L5)
- [DataFlowView](../../src/Compliance.Common/Features/TechnologyInventory/Models/DataFlowView.cs#L7)
- [ITechnologyInventoryReadRequest](../../src/Compliance.Common/Features/TechnologyInventory/ITechnologyInventoryReadRequest.cs#L4)
- [ITechnologyInventoryRequest](../../src/Compliance.Common/Features/TechnologyInventory/ITechnologyInventoryRequest.cs#L5)
- [ITechnologyInventoryWriteRequest](../../src/Compliance.Common/Features/TechnologyInventory/ITechnologyInventoryWriteRequest.cs#L6)
- [InformationAssetChangePreview](../../src/Compliance.Common/Features/TechnologyInventory/Models/InformationAssetChangePreview.cs#L10)
- [InformationAssetContent](../../src/Compliance.Common/Features/TechnologyInventory/Models/InformationAssetContent.cs#L6)
- [InformationAssetRegistration](../../src/Compliance.Common/Features/TechnologyInventory/Models/InformationAssetRegistration.cs#L5)
- [InformationAssetView](../../src/Compliance.Common/Features/TechnologyInventory/Models/InformationAssetView.cs#L7)
- [LocationContent](../../src/Compliance.Common/Features/TechnologyInventory/Models/LocationContent.cs#L9)
- [LocationRegistration](../../src/Compliance.Common/Features/TechnologyInventory/Models/LocationRegistration.cs#L5)
- [LocationView](../../src/Compliance.Common/Features/TechnologyInventory/Models/LocationView.cs#L7)
- [OperationalProcessContent](../../src/Compliance.Common/Features/TechnologyInventory/Models/OperationalProcessContent.cs#L9)
- [OperationalProcessRegistration](../../src/Compliance.Common/Features/TechnologyInventory/Models/OperationalProcessRegistration.cs#L5)
- [OperationalProcessView](../../src/Compliance.Common/Features/TechnologyInventory/Models/OperationalProcessView.cs#L7)
- [TechnologyComponentContent](../../src/Compliance.Common/Features/TechnologyInventory/Models/TechnologyComponentContent.cs#L9)
- [TechnologyComponentRegistration](../../src/Compliance.Common/Features/TechnologyInventory/Models/TechnologyComponentRegistration.cs#L5)
- [TechnologyComponentView](../../src/Compliance.Common/Features/TechnologyInventory/Models/TechnologyComponentView.cs#L7)
- [TechnologyInventoryResourceTypes](../../src/Compliance.Common/Features/TechnologyInventory/Models/TechnologyInventoryResourceTypes.cs#L4)

#### Tenants

- [IPlatformOperatorOrTenantAdminRequest](../../src/Compliance.Common/Features/Tenants/IPlatformOperatorOrTenantAdminRequest.cs#L6)
- [IPlatformOperatorRequest](../../src/Compliance.Common/Features/Tenants/IPlatformOperatorRequest.cs#L5)
- [ITenantLifecycleReactionRequest](../../src/Compliance.Common/Features/Tenants/ITenantLifecycleReactionRequest.cs#L6)
- [PlatformOperatorRosterView](../../src/Compliance.Common/Features/Tenants/Models/PlatformOperatorRosterView.cs#L5)
- [TenantInvitationView](../../src/Compliance.Common/Features/Tenants/Models/TenantInvitationView.cs#L6)
- [TenantMembershipSummary](../../src/Compliance.Common/Features/Tenants/Models/TenantMembershipSummary.cs#L5)
- [TenantMembershipView](../../src/Compliance.Common/Features/Tenants/Models/TenantMembershipView.cs#L9)
- [TenantProvisioningStatusView](../../src/Compliance.Common/Features/Tenants/Models/TenantProvisioningStatusView.cs#L5)
- [TenantRegistration](../../src/Compliance.Common/Features/Tenants/Models/TenantRegistration.cs#L5)
- [TenantSlugResolution](../../src/Compliance.Common/Features/Tenants/Models/TenantSlugResolution.cs#L5)
- [TenantView](../../src/Compliance.Common/Features/Tenants/Models/TenantView.cs#L5)

#### UserIdentities

- [AuthenticatedUserIdentity](../../src/Compliance.Common/Features/UserIdentities/Models/AuthenticatedUserIdentity.cs#L6)
- [EmailAddressView](../../src/Compliance.Common/Features/UserIdentities/Models/EmailAddressView.cs#L5)
- [EmailChallengeStatusView](../../src/Compliance.Common/Features/UserIdentities/Models/EmailChallengeStatusView.cs#L3)
- [IEmailOwnershipRequest](../../src/Compliance.Common/Features/UserIdentities/IEmailOwnershipRequest.cs#L5)
- [IdentityRecoveryOption](../../src/Compliance.Common/Features/UserIdentities/Queries/GetIdentityRecoveryOptions.cs#L19)
- [IdentityRecoveryOptions](../../src/Compliance.Common/Features/UserIdentities/Queries/GetIdentityRecoveryOptions.cs#L15)

#### Work

- [WorkAssignmentEntryView](../../src/Compliance.Common/Features/Work/Models/WorkAssignmentEntryView.cs#L7)
- [WorkCountsView](../../src/Compliance.Common/Features/Work/Models/WorkCountsView.cs#L4)
- [WorkDigestPreferenceView](../../src/Compliance.Common/Features/Work/Models/WorkDigestPreferenceView.cs#L4)
- [WorkDigestView](../../src/Compliance.Common/Features/Work/Models/WorkDigestView.cs#L9)
- [WeeklyWorkDigestView](../../src/Compliance.Common/Features/Work/Models/WeeklyWorkDigestView.cs#L7)
- [WorkDigestDeliveryAttemptView](../../src/Compliance.Common/Features/Work/Models/WeeklyWorkDigestView.cs#L20)
- [WorkDigestSchedulePageView](../../src/Compliance.Common/Features/Work/Models/WorkDigestSchedulePageView.cs#L4)
- [WorkItemDetailView](../../src/Compliance.Common/Features/Work/Models/WorkItemDetailView.cs#L4)
- [WorkQueueItemView](../../src/Compliance.Common/Features/Work/Models/WorkQueueItemView.cs#L15)
- [WorkQueueView](../../src/Compliance.Common/Features/Work/Models/WorkQueueView.cs#L4)
- [WorkReminderView](../../src/Compliance.Common/Features/Work/Models/WorkReminderView.cs#L9)

#### Workforce

- [FrozenPerson](../../src/Compliance.Common/Features/Workforce/Models/FrozenPerson.cs#L6)
- [FrozenWorkRelationship](../../src/Compliance.Common/Features/Workforce/Models/FrozenWorkRelationship.cs#L9)
- [IWorkforceRequest](../../src/Compliance.Common/Features/Workforce/IWorkforceRequest.cs#L5)
- [PersonRegistration](../../src/Compliance.Common/Features/Workforce/Models/PersonRegistration.cs#L5)
- [PersonView](../../src/Compliance.Common/Features/Workforce/Models/PersonView.cs#L11)
- [PersonalContactDetails](../../src/Compliance.Common/Features/Workforce/Models/PersonalContactDetails.cs#L4)
- [ServiceIdentityRegistration](../../src/Compliance.Common/Features/Workforce/Models/ServiceIdentityRegistration.cs#L5)
- [ServiceIdentityTerms](../../src/Compliance.Common/Features/Workforce/Models/ServiceIdentityTerms.cs#L10)
- [ServiceIdentityView](../../src/Compliance.Common/Features/Workforce/Models/ServiceIdentityView.cs#L13)
- [WorkRelationshipRegistration](../../src/Compliance.Common/Features/Workforce/Models/WorkRelationshipRegistration.cs#L5)
- [WorkRelationshipTerms](../../src/Compliance.Common/Features/Workforce/Models/WorkRelationshipTerms.cs#L10)
- [WorkRelationshipView](../../src/Compliance.Common/Features/Workforce/Models/WorkRelationshipView.cs#L10)
- [WorkforceObservationResolutionView](../../src/Compliance.Common/Features/Workforce/Models/WorkforceObservationResolutionView.cs#L10)
- [WorkforceObservationView](../../src/Compliance.Common/Features/Workforce/Models/WorkforceObservationView.cs#L13)
- [WorkforcePersonSourceFacts](../../src/Compliance.Common/Features/Workforce/Models/WorkforcePersonSourceFacts.cs#L4)
- [WorkforceReconciliationObservationView](../../src/Compliance.Common/Features/Workforce/Models/WorkforceReconciliationObservationView.cs#L12)
- [WorkforceRosterSnapshotView](../../src/Compliance.Common/Features/Workforce/Models/WorkforceRosterSnapshotView.cs#L10)
- [WorkforceServiceSourceFacts](../../src/Compliance.Common/Features/Workforce/Models/WorkforceServiceSourceFacts.cs#L4)
- [WorkforceSourceDecision](../../src/Compliance.Common/Features/Workforce/Models/WorkforceSourceDecision.cs#L6)
- [WorkforceSourceFacts](../../src/Compliance.Common/Features/Workforce/Models/WorkforceSourceFacts.cs#L4)
- [WorkforceSourceIdentity](../../src/Compliance.Common/Features/Workforce/Models/WorkforceSourceIdentity.cs#L4)
- [WorkforceSourcePreview](../../src/Compliance.Common/Features/Workforce/Models/WorkforceSourcePreview.cs#L6)
- [WorkforceSourceRegistration](../../src/Compliance.Common/Features/Workforce/Models/WorkforceSourceRegistration.cs#L5)
- [WorkforceSourceView](../../src/Compliance.Common/Features/Workforce/Models/WorkforceSourceView.cs#L10)

### Existing reactors

#### AccessControl

- [AcceptedStaffDirectoryReconciliationReactor](../../src/Compliance.Core/Features/AccessControl/Reactors/AcceptedStaffDirectoryReconciliationReactor.cs#L5)
- [AccessReviewGrantBackfillReactor](../../src/Compliance.Core/Features/AccessControl/AccessReviewGrantBackfillReactor.cs#L8)
- [ApplicationImportGrantBackfillReactor](../../src/Compliance.Core/Features/AccessControl/ApplicationImportGrantBackfillReactor.cs#L6)
- [ApplicationInventoryGrantBackfillReactor](../../src/Compliance.Core/Features/AccessControl/ApplicationInventoryGrantBackfillReactor.cs#L8)
- [BuiltInRoleCatalogMigrationReactor](../../src/Compliance.Core/Features/AccessControl/BuiltInRoleCatalogMigrationReactor.cs#L6)
- [EvidenceArtifactReadPermissionReactor](../../src/Compliance.Core/Features/AccessControl/EvidenceArtifactReadPermissionReactor.cs#L6)
- [EvidenceRedactionPermissionsReactor](../../src/Compliance.Core/Features/AccessControl/EvidenceRedactionPermissionsReactor.cs#L6)
- [FirmStaffStatusReevaluationReactor](../../src/Compliance.Core/Features/AccessControl/Reactors/FirmStaffStatusReevaluationReactor.cs#L5)
- [ProviderInventoryGrantBackfillReactor](../../src/Compliance.Core/Features/AccessControl/ProviderInventoryGrantBackfillReactor.cs#L8)
- [RoleCleanupReactor](../../src/Compliance.Core/Features/AccessControl/Reactors/RoleCleanupReactor.cs#L10)
- [TeamCleanupReactor](../../src/Compliance.Core/Features/AccessControl/Reactors/TeamCleanupReactor.cs#L11)
- [TechnologyInventoryGrantBackfillReactor](../../src/Compliance.Core/Features/AccessControl/TechnologyInventoryGrantBackfillReactor.cs#L6)
- [TenantRbacBootstrapReactor](../../src/Compliance.Core/Features/AccessControl/Reactors/TenantRbacBootstrapReactor.cs#L5)
- [WorkforceGrantBackfillReactor](../../src/Compliance.Core/Features/AccessControl/WorkforceGrantBackfillReactor.cs#L8)
- [WorkforcePersonalDetailsGrantBackfillReactor](../../src/Compliance.Core/Features/AccessControl/WorkforcePersonalDetailsGrantBackfillReactor.cs#L10)
- [WorkforceRestrictedFieldGrantBackfillReactor](../../src/Compliance.Core/Features/AccessControl/WorkforceRestrictedFieldGrantBackfillReactor.cs#L9)
- [ComplianceRecordRestrictedAccessGrantBackfillReactor](../../src/Compliance.Core/Features/AccessControl/ComplianceRecordRestrictedAccessGrantBackfillReactor.cs#L6)

#### Applications

- [ApplicationImportExecutionReactor](../../src/Compliance.Core/Features/Applications/ApplicationImportExecutionReactor.cs#L6)

#### Evaluations

- [ControlEvaluationDeviationFindingReactor](../../src/Compliance.Core/Features/Evaluations/Reactors/ControlEvaluationDeviationFindingReactor.cs#L9)

#### Evidence

- [EvidenceArtifactInspectionEffectsReactor](../../src/Compliance.Core/Features/Evidence/Reactors/EvidenceArtifactInspectionEffectsReactor.cs#L6)

#### Changes

- [LinkImpactReviewWorkReactor](../../src/Compliance.Core/Features/Changes/Reactors/LinkImpactReviewWorkReactor.cs#L5)
- [SystemDescriptionDecisionReactor](../../src/Compliance.Core/Features/Changes/Reactors/SystemDescriptionDecisionReactor.cs#L8)

#### Remediation

- [ControlOccurrenceFindingReactor](../../src/Compliance.Core/Features/Remediation/Reactors/ControlOccurrenceFindingReactor.cs#L11)

#### Risks

- [BoundaryRiskReassessmentReactor](../../src/Compliance.Core/Features/Risks/Reactors/BoundaryRiskReassessmentReactor.cs#L10)
- [RiskMethodReassessmentReactor](../../src/Compliance.Core/Features/Risks/Reactors/RiskMethodReassessmentReactor.cs#L9)

#### Tenants

- [TenantInvitationDeliveryReactor](../../src/Compliance.Core/Features/Tenants/TenantInvitationDeliveryReactor.cs#L8)
- [TenantInvitationReactor](../../src/Compliance.Core/Features/Tenants/Reactors/TenantInvitationReactor.cs#L6)
- [TenantRegistrationReactor](../../src/Compliance.Core/Features/Tenants/Reactors/TenantRegistrationReactor.cs#L5)
- [TenantSelfServiceActivationReactor](../../src/Compliance.Core/Features/Tenants/Reactors/TenantSelfServiceActivationReactor.cs#L6)
- [TenantSlugReactor](../../src/Compliance.Core/Features/Tenants/Reactors/TenantSlugReactor.cs#L5)

#### UserIdentities

- [EmailChallengeDeliveryReactor](../../src/Compliance.Core/Features/UserIdentities/Reactors/EmailChallengeDeliveryReactor.cs#L8)
- [EmailReservationReactor](../../src/Compliance.Core/Features/UserIdentities/Reactors/EmailReservationReactor.cs#L5)
- [IdentityRecoveryNoticeReactor](../../src/Compliance.Core/Features/UserIdentities/IdentityRecoveryNoticeReactor.cs#L6)

### Existing projectors

#### AccessControl

- [AccessGrantProjector](../../src/Compliance.Core/Features/AccessControl/Projectors/AccessGrantProjector.cs#L5)
- [ActualStaffEngagementLocatorProjector](../../src/Compliance.Core/Features/AccessControl/Projectors/ActualStaffEngagementLocatorProjector.cs#L5)
- [PermissionProjector](../../src/Compliance.Core/Features/AccessControl/Projectors/PermissionProjector.cs#L5)
- [RoleDirectoryProjector](../../src/Compliance.Core/Features/AccessControl/Projectors/RoleDirectoryProjector.cs#L5)
- [RolePermissionDirectoryProjector](../../src/Compliance.Core/Features/AccessControl/Projectors/RolePermissionDirectoryProjector.cs#L5)
- [RoleTeamDirectoryProjector](../../src/Compliance.Core/Features/AccessControl/Projectors/RoleTeamDirectoryProjector.cs#L5)
- [TeamDirectoryProjector](../../src/Compliance.Core/Features/AccessControl/Projectors/TeamDirectoryProjector.cs#L5)
- [TeamMemberDirectoryProjector](../../src/Compliance.Core/Features/AccessControl/Projectors/TeamMemberDirectoryProjector.cs#L5)
- [TeamRoleDirectoryProjector](../../src/Compliance.Core/Features/AccessControl/Projectors/TeamRoleDirectoryProjector.cs#L5)

#### AccessReviews

- [AccessPopulationDirectoryProjector](../../src/Compliance.Core/Features/AccessReviews/Projectors/AccessPopulationDirectoryProjector.cs#L5)
- [AccessReviewCampaignDirectoryProjector](../../src/Compliance.Core/Features/AccessReviews/Projectors/AccessReviewCampaignDirectoryProjector.cs#L5)
- [AccessReviewCampaignImpactProjector](../../src/Compliance.Core/Features/AccessReviews/Projectors/AccessReviewCampaignImpactProjector.cs#L5)
- [AccessReviewCampaignWorkItemProjector](../../src/Compliance.Core/Features/Work/Projectors/AccessReviewCampaignWorkItemProjector.cs#L5)

#### AdvisorCollaboration

- [AdvisorHandoffDirectoryProjector](../../src/Compliance.Core/Features/AdvisorCollaboration/Projectors/AdvisorHandoffDirectoryProjector.cs#L5)
- [AdvisorFeedbackWorkItemProjector](../../src/Compliance.Core/Features/AdvisorCollaboration/Projectors/AdvisorFeedbackWorkItemProjector.cs#L5)

#### Applications

- [AccessReviewScopeDirectoryProjector](../../src/Compliance.Core/Features/Applications/Projectors/AccessReviewScopeDirectoryProjector.cs#L5)
- [ApplicationBoundaryReferenceProjector](../../src/Compliance.Core/Features/Applications/Projectors/ApplicationBoundaryReferenceProjector.cs#L6)
- [ApplicationControlDraftReferenceProjector](../../src/Compliance.Core/Features/Applications/Projectors/ApplicationControlDraftReferenceProjector.cs#L7)
- [ApplicationControlVersionReferenceProjector](../../src/Compliance.Core/Features/Applications/Projectors/ApplicationControlVersionReferenceProjector.cs#L5)
- [ApplicationDirectoryProjector](../../src/Compliance.Core/Features/Applications/Projectors/ApplicationDirectoryProjector.cs#L5)
- [ApplicationEvidenceSourceReferenceProjector](../../src/Compliance.Core/Features/Applications/Projectors/ApplicationEvidenceSourceReferenceProjector.cs#L5)
- [ApplicationImportProjector](../../src/Compliance.Core/Features/Applications/Projectors/ApplicationImportProjector.cs#L5)
- [ApplicationIncomingRelationshipProjector](../../src/Compliance.Core/Features/Applications/Projectors/ApplicationIncomingRelationshipProjector.cs#L5)
- [ApplicationPolicyReferenceProjector](../../src/Compliance.Core/Features/Applications/Projectors/ApplicationPolicyReferenceProjector.cs#L5)
- [ApplicationRelationshipProjector](../../src/Compliance.Core/Features/Applications/Projectors/ApplicationRelationshipProjector.cs#L5)

#### Boundaries

- [BoundaryDirectoryProjector](../../src/Compliance.Core/Features/Boundaries/Projectors/BoundaryDirectoryProjector.cs#L5)

#### Commitments

- [CommitmentDraftDirectoryProjector](../../src/Compliance.Core/Features/Commitments/Projectors/CommitmentDraftDirectoryProjector.cs#L5)
- [CommitmentDraftHistoryDirectoryProjectorV1](../../src/Compliance.Core/Features/Commitments/CommitmentDraftHistoryDirectoryProjectorV1.cs#L5)

#### ControlMappings

- [ControlMappingDirectoryV1Projector](../../src/Compliance.Core/Features/ControlMappings/Projectors/ControlMappingDirectoryV1Projector.cs#L5)
- [CriterionApplicabilityDirectoryV1Projector](../../src/Compliance.Core/Features/ControlMappings/Projectors/CriterionApplicabilityDirectoryV1Projector.cs#L5)

#### Controls

- [ControlDraftDirectoryV2Projector](../../src/Compliance.Core/Features/Controls/Projectors/ControlDraftDirectoryV2Projector.cs#L5)
- [ControlDraftHistoryDirectoryV1Projector](../../src/Compliance.Core/Features/Controls/Projectors/ControlDraftHistoryDirectoryV1Projector.cs#L5)

#### Criteria

- [CriteriaTextOverlayDirectoryProjector](../../src/Compliance.Core/Features/Criteria/Projectors/CriteriaTextOverlayDirectoryProjector.cs#L5)

#### Examinations

- [ExaminationEngagementDirectoryProjector](../../src/Compliance.Core/Features/Examinations/Projectors/ExaminationEngagementDirectoryProjector.cs#L5)

#### Changes

- [SignificantChangeDirectoryProjector](../../src/Compliance.Core/Features/Changes/Projectors/SignificantChangeDirectoryProjector.cs#L5)

#### Incidents

- [ComplianceIncidentDirectoryProjector](../../src/Compliance.Core/Features/Incidents/Projectors/ComplianceIncidentDirectoryProjector.cs#L5)

#### Policies

- [PolicyDirectoryProjector](../../src/Compliance.Core/Features/Policies/Projectors/PolicyDirectoryProjector.cs#L5)

#### PolicyDistribution

- [CampaignDirectoryProjector](../../src/Compliance.Core/Features/PolicyDistribution/Projectors/CampaignDirectoryProjector.cs#L5)

#### Programs

- [ClientServiceDirectoryProjector](../../src/Compliance.Core/Features/Programs/Projectors/ClientServiceDirectoryProjector.cs#L5)
- [ProgramDirectoryProjector](../../src/Compliance.Core/Features/Programs/Projectors/ProgramDirectoryProjector.cs#L6)

#### Providers

- [AssuranceProjector](../../src/Compliance.Core/Features/Providers/Projectors/AssuranceProjector.cs#L5)
- [ProviderProjector](../../src/Compliance.Core/Features/Providers/Projectors/ProviderProjector.cs#L5)

#### Readiness

- [ReadinessDirectoryProjector](../../src/Compliance.Core/Features/Readiness/ReadinessDirectoryProjector.cs#L5)

#### Responsibilities

- [MemberResponsibilityProjector](../../src/Compliance.Core/Features/Responsibilities/Projectors/MemberResponsibilityProjector.cs#L5)
- [ResponsibilitySetProjector](../../src/Compliance.Core/Features/Responsibilities/Projectors/ResponsibilitySetProjector.cs#L5)

#### Risks

- [RiskDraftDirectoryProjector](../../src/Compliance.Core/Features/Risks/Projectors/RiskDraftDirectoryProjector.cs#L5)
- [RiskDraftHistoryProjectorV1](../../src/Compliance.Core/Features/Risks/RiskDraftHistoryProjectorV1.cs#L5)
- [RiskEvaluationDirectoryProjectorV1](../../src/Compliance.Core/Features/Risks/RiskEvaluationDirectoryProjectorV1.cs#L5)

#### Snapshots

- [PopulationSnapshotDirectoryProjector](../../src/Compliance.Core/Features/Snapshots/Projectors/PopulationSnapshotDirectoryProjector.cs#L5)
- [SnapshotDirectoryProjector](../../src/Compliance.Core/Features/Snapshots/Projectors/SnapshotDirectoryProjector.cs#L5)

#### TechnologyInventory

- [InventoryRegisterProjector](../../src/Compliance.Core/Features/TechnologyInventory/Projectors/InventoryRegisterProjector.cs#L5)
- [TechnologyInventoryDirectoryProjector](../../src/Compliance.Core/Features/TechnologyInventory/Projectors/TechnologyInventoryDirectoryProjector.cs#L5)

#### Tenants

- [TenantDirectoryProjector](../../src/Compliance.Core/Features/Tenants/Projectors/TenantDirectoryProjector.cs#L5)
- [TenantInvitationDirectoryProjector](../../src/Compliance.Core/Features/Tenants/Projectors/TenantInvitationDirectoryProjector.cs#L5)
- [TenantMembershipProjector](../../src/Compliance.Core/Features/Tenants/Projectors/TenantMembershipProjector.cs#L5)

#### UserIdentities

- [EmailAddressDirectoryProjector](../../src/Compliance.Core/Features/UserIdentities/Projectors/EmailAddressDirectoryProjector.cs#L5)
- [IdentityDirectoryProjector](../../src/Compliance.Core/Features/UserIdentities/Projectors/IdentityDirectoryProjector.cs#L5)
- [PlatformUserDirectoryProjector](../../src/Compliance.Core/Features/UserIdentities/Projectors/PlatformUserDirectoryProjector.cs#L5)

#### Work

- [BoundaryDecisionWorkItemProjector](../../src/Compliance.Core/Features/Work/Projectors/BoundaryDecisionWorkItemProjector.cs#L7)
- [CommitmentDecisionWorkItemProjector](../../src/Compliance.Core/Features/Work/Projectors/CommitmentDecisionWorkItemProjector.cs#L7)
- [ControlCriterionMappingWorkItemProjector](../../src/Compliance.Core/Features/Work/Projectors/ControlCriterionMappingWorkItemProjector.cs#L6)
- [ControlDecisionWorkItemProjector](../../src/Compliance.Core/Features/Work/Projectors/ControlDecisionWorkItemProjector.cs#L7)
- [ControlEvaluationWorkItemProjector](../../src/Compliance.Core/Features/Work/Projectors/ControlEvaluationWorkItemProjector.cs#L6)
- [ControlOccurrenceWorkItemProjector](../../src/Compliance.Core/Features/Work/Projectors/ControlOccurrenceWorkItemProjector.cs#L6)
- [ControlOperatingPlanWorkItemProjector](../../src/Compliance.Core/Features/Work/Projectors/ControlOperatingPlanWorkItemProjector.cs#L6)
- [CorrectiveActionWorkItemProjector](../../src/Compliance.Core/Features/Work/Projectors/CorrectiveActionWorkItemProjector.cs#L6)
- [CriterionApplicabilityWorkItemProjector](../../src/Compliance.Core/Features/Work/Projectors/CriterionApplicabilityWorkItemProjector.cs#L6)
- [EvidenceWorkItemProjector](../../src/Compliance.Core/Features/Work/Projectors/EvidenceWorkItemProjector.cs#L6)
- [FindingClosureWorkItemProjector](../../src/Compliance.Core/Features/Work/Projectors/FindingClosureWorkItemProjector.cs#L6)
- [PolicyCampaignWorkItemProjector](../../src/Compliance.Core/Features/Work/Projectors/PolicyCampaignWorkItemProjector.cs#L6)
- [PolicyDecisionWorkItemProjector](../../src/Compliance.Core/Features/Work/Projectors/PolicyDecisionWorkItemProjector.cs#L6)
- [RiskGovernanceWorkItemProjector](../../src/Compliance.Core/Features/Work/RiskGovernanceWorkItemProjector.cs#L6)

#### Workforce

- [PersonDirectoryProjector](../../src/Compliance.Core/Features/Workforce/Projectors/PersonDirectoryProjector.cs#L5)
- [ServiceIdentityDirectoryProjector](../../src/Compliance.Core/Features/Workforce/Projectors/ServiceIdentityDirectoryProjector.cs#L5)
- [WorkRelationshipDirectoryProjector](../../src/Compliance.Core/Features/Workforce/Projectors/WorkRelationshipDirectoryProjector.cs#L5)
- [WorkforceObservationProjector](../../src/Compliance.Core/Features/Workforce/Projectors/WorkforceObservationProjector.cs#L5)
- [WorkforceObservationResolutionProjector](../../src/Compliance.Core/Features/Workforce/Projectors/WorkforceObservationResolutionProjector.cs#L5)
- [WorkforceSourceProjector](../../src/Compliance.Core/Features/Workforce/Projectors/WorkforceSourceProjector.cs#L5)

#### Requirement-backed response views added 2026-10-17 to 2026-10-18

##### AdvisorCollaboration

- [AdvisorRequestView](../../src/Compliance.Common/Features/AdvisorCollaboration/Models/AdvisorRequestView.cs#L6)
- [AdvisorRequestSummaryView](../../src/Compliance.Common/Features/AdvisorCollaboration/Models/AdvisorRequestSummaryView.cs#L6)
- [AdvisorFeedbackView](../../src/Compliance.Common/Features/AdvisorCollaboration/Models/AdvisorFeedbackView.cs#L6)
- [AdvisorSharedSourceView](../../src/Compliance.Common/Features/AdvisorCollaboration/Models/AdvisorSharedSourceView.cs#L8)
- [AdvisorHandoffPreviewView](../../src/Compliance.Common/Features/AdvisorCollaboration/Models/AdvisorHandoffPreviewView.cs#L6)
- [AdvisorHandoffPreviewItemView](../../src/Compliance.Common/Features/AdvisorCollaboration/Models/AdvisorHandoffPreviewItemView.cs#L4)

##### Examinations

- [ExaminationEngagementView](../../src/Compliance.Common/Features/Examinations/Models/ExaminationEngagementView.cs#L6)
- [TypeIBaselineView](../../src/Compliance.Common/Features/Examinations/Models/TypeIBaselineView.cs#L6)
- [TypeIBaselinePreviewView](../../src/Compliance.Common/Features/Examinations/Models/TypeIBaselinePreviewView.cs#L6)

##### SystemDescription

- [SystemDescriptionView](../../src/Compliance.Common/Features/SystemDescription/Models/SystemDescriptionView.cs#L6)
- [SystemDescriptionVersionView](../../src/Compliance.Common/Features/SystemDescription/Models/SystemDescriptionVersionView.cs#L6)
- [SystemDescriptionImpactPreview](../../src/Compliance.Common/Features/SystemDescription/Models/SystemDescriptionImpactPreview.cs#L6)

##### AuditorRequests

- [AuditorRequestView](../../src/Compliance.Common/Features/AuditorRequests/Models/AuditorRequestView.cs#L6)
- [AuditorRequestSummaryView](../../src/Compliance.Common/Features/AuditorRequests/Models/AuditorRequestSummaryView.cs#L6)
- [SampleSelectionView](../../src/Compliance.Common/Features/AuditorRequests/Models/SampleSelectionView.cs#L6)
- [AuditorRequestResponseView](../../src/Compliance.Common/Features/AuditorRequests/Models/AuditorRequestResponseView.cs#L6)

#### TDD-backed aggregate payload and support models added 2026-10-17

##### AuditorRequests

- [AuditorRequestContent](../../src/Compliance.Core/Features/AuditorRequests/AuditorRequestContent.cs#L6)
- [AuditorRequestTermsVersion](../../src/Compliance.Common/Features/AuditorRequests/Models/AuditorRequestTermsVersion.cs#L6)
- [AuditorRequestCommentView](../../src/Compliance.Common/Features/AuditorRequests/Models/AuditorRequestCommentView.cs#L6)
- [AuditorRequestHistoryEntry](../../src/Compliance.Common/Features/AuditorRequests/Models/AuditorRequestHistoryEntry.cs#L6)
- [AuditorRequestResponseHistoryView](../../src/Compliance.Common/Features/AuditorRequests/Models/AuditorRequestResponseHistoryView.cs#L6)
- [AuditorResponseVersionView](../../src/Compliance.Common/Features/AuditorRequests/Models/AuditorResponseVersionView.cs#L6)
- [AuditorResponseReviewView](../../src/Compliance.Common/Features/AuditorRequests/Models/AuditorResponseReviewView.cs#L6)
- [AuditorResponseDeliveryView](../../src/Compliance.Common/Features/AuditorRequests/Models/AuditorResponseDeliveryView.cs#L6)
- [AuditorResponseDispositionView](../../src/Compliance.Common/Features/AuditorRequests/Models/AuditorResponseDispositionView.cs#L6)
- [AuditorSampleSelectionView](../../src/Compliance.Common/Features/AuditorRequests/Models/AuditorSampleSelectionView.cs#L6)

##### AuditPackages

- [AuditPackageBuildOutput](../../src/Compliance.Common/Features/AuditPackages/Models/AuditPackageBuildOutput.cs#L6)
- [AuditPackageBuildAttemptView](../../src/Compliance.Common/Features/AuditPackages/Models/AuditPackageBuildAttemptView.cs#L6)
- [AuditPackageContent](../../src/Compliance.Common/Features/AuditPackages/Models/AuditPackageContent.cs#L6)
- [AuditPackageDisclosureApprovalView](../../src/Compliance.Common/Features/AuditPackages/Models/AuditPackageDisclosureApprovalView.cs#L6)
- [AuditPackageManifestEntryView](../../src/Compliance.Common/Features/AuditPackages/Models/AuditPackageManifestEntryView.cs#L6)
- [AuditPackageSelectionEntry](../../src/Compliance.Common/Features/AuditPackages/Models/AuditPackageSelectionEntry.cs#L6)
- [AuditPackageValidationFinding](../../src/Compliance.Common/Features/AuditPackages/Models/AuditPackageValidationFinding.cs#L6)
- [AuditPackageValidationOverrideView](../../src/Compliance.Common/Features/AuditPackages/Models/AuditPackageValidationOverrideView.cs#L6)
- [AuditPackageVersionView](../../src/Compliance.Common/Features/AuditPackages/Models/AuditPackageVersionView.cs#L6)
- [AuditPackageView](../../src/Compliance.Common/Features/AuditPackages/Models/AuditPackageView.cs#L6)
- [AuditPackageSummaryView](../../src/Compliance.Common/Features/AuditPackages/Models/AuditPackageSummaryView.cs#L6)
- [AuditPackageManifestView](../../src/Compliance.Common/Features/AuditPackages/Models/AuditPackageManifestView.cs#L6)
- [AuditPackageValidationView](../../src/Compliance.Common/Features/AuditPackages/Models/AuditPackageValidationView.cs#L6)
- [AuditPackageDeliveryView](../../src/Compliance.Common/Features/AuditPackages/Models/AuditPackageDeliveryView.cs#L6)

##### Examinations

- [AuditObservationView](../../src/Compliance.Common/Features/Examinations/Models/AuditObservationView.cs#L6)
- [AuditOutcomeView](../../src/Compliance.Common/Features/Examinations/Models/AuditOutcomeView.cs#L6)
- [AuditOutcomeContent](../../src/Compliance.Common/Features/Examinations/Models/AuditOutcomeContent.cs#L6)
- [ExaminationContentReference](../../src/Compliance.Common/Features/Examinations/Models/ExaminationContentReference.cs#L6)
- [ExaminationSignOffContent](../../src/Compliance.Common/Features/Examinations/Models/ExaminationSignOffContent.cs#L6)
- [ManagementAssertionContent](../../src/Compliance.Common/Features/Examinations/Models/ManagementAssertionContent.cs#L6)
- [ManagementAssertionVersionView](../../src/Compliance.Common/Features/Examinations/Models/ManagementAssertionVersionView.cs#L6)
- [ManagementAssertionApprovalView](../../src/Compliance.Common/Features/Examinations/Models/ManagementAssertionApprovalView.cs#L6)
- [TypeIIPlanView](../../src/Compliance.Common/Features/Examinations/Models/TypeIIPlanView.cs#L6)
- [ManagementAssertionView](../../src/Compliance.Common/Features/Examinations/Models/ManagementAssertionView.cs#L6)
- [ManagementAssertionSigner](../../src/Compliance.Common/Features/Examinations/Models/ManagementAssertionSigner.cs#L6)
- [ManagementRepresentationLetterContent](../../src/Compliance.Common/Features/Examinations/Models/ManagementRepresentationLetterContent.cs#L6)
- [ManagementRepresentationLetterView](../../src/Compliance.Common/Features/Examinations/Models/ManagementRepresentationLetterView.cs#L6)
- [ManagementRepresentationLetterSignatureView](../../src/Compliance.Common/Features/Examinations/Models/ManagementRepresentationLetterSignatureView.cs#L6)
- [ExaminationSignOffView](../../src/Compliance.Common/Features/Examinations/Models/ExaminationSignOffView.cs#L6)
- [TypeIBaselineSourceReference](../../src/Compliance.Common/Features/Examinations/Models/TypeIBaselineSourceReference.cs#L6)
- [TypeIBaselineSourceCoverage](../../src/Compliance.Common/Features/Examinations/Models/TypeIBaselineSourceCoverage.cs#L3)
- [TypeIIPlanContent](../../src/Compliance.Common/Features/Examinations/Models/TypeIIPlanContent.cs#L6)
- [TypeIIPlanVersionView](../../src/Compliance.Common/Features/Examinations/Models/TypeIIPlanVersionView.cs#L6)
- [TypeIIPlanReviewView](../../src/Compliance.Common/Features/Examinations/Models/TypeIIPlanReviewView.cs#L6)
- [TypeIIPlanApprovalView](../../src/Compliance.Common/Features/Examinations/Models/TypeIIPlanApprovalView.cs#L6)
- [TypeIIControlPlanEntry](../../src/Compliance.Common/Features/Examinations/Models/TypeIIControlPlanEntry.cs#L6)
- [TypeIICarriedForwardGap](../../src/Compliance.Common/Features/Examinations/Models/TypeIICarriedForwardGap.cs#L6)
- [TypeIIPlanGapAcknowledgement](../../src/Compliance.Common/Features/Examinations/Models/TypeIIPlanGapAcknowledgement.cs#L6)
- [TypeIIPlannedChange](../../src/Compliance.Common/Features/Examinations/Models/TypeIIPlannedChange.cs#L6)

##### Retention

- [ArtifactDisclosureDeliveryView](../../src/Compliance.Common/Features/Retention/Models/ArtifactDisclosureDeliveryView.cs#L6)
- [ArtifactDisclosureView](../../src/Compliance.Common/Features/Retention/Models/ArtifactDisclosureView.cs#L6)
- [ArtifactDispositionAttemptView](../../src/Compliance.Common/Features/Retention/Models/ArtifactDispositionAttemptView.cs#L6)
- [ArtifactDispositionTombstoneView](../../src/Compliance.Common/Features/Retention/Models/ArtifactDispositionTombstoneView.cs#L6)
- [ArtifactDispositionView](../../src/Compliance.Common/Features/Retention/Models/ArtifactDispositionView.cs#L6)
- [ArtifactEngagementHoldView](../../src/Compliance.Common/Features/Retention/Models/ArtifactEngagementHoldView.cs#L6)
- [EvidenceHandlingPolicyHistoryEntry](../../src/Compliance.Common/Features/Retention/Models/EvidenceHandlingPolicyHistoryEntry.cs#L6)
- [EvidenceHandlingPolicyView](../../src/Compliance.Common/Features/Retention/Models/EvidenceHandlingPolicyView.cs#L6)
- [ArtifactDisclosureSourceSnapshot](../../src/Compliance.Core/Features/Retention/ArtifactDisclosureSourceSnapshot.cs#L6)
- [ArtifactDispositionEligibilitySnapshot](../../src/Compliance.Core/Features/Retention/ArtifactDispositionEligibilitySnapshot.cs#L6)
- [EvidenceHandlingPolicySourceSnapshot](../../src/Compliance.Core/Features/Retention/EvidenceHandlingPolicySourceSnapshot.cs#L6)

##### Operations

- [ObservationPeriodView](../../src/Compliance.Common/Features/Operations/Models/ObservationPeriodView.cs#L6)
- [ObservationPeriodSummaryView](../../src/Compliance.Common/Features/Operations/Models/ObservationPeriodSummaryView.cs#L6)
- [TypeIIPeriodView](../../src/Compliance.Common/Features/Operations/Models/TypeIIPeriodView.cs#L8)
- [TypeIIPeriodOpeningContent](../../src/Compliance.Common/Features/Operations/Models/TypeIIPeriodOpeningContent.cs#L7)
- [TypeIIPeriodOccurrenceView](../../src/Compliance.Common/Features/Operations/Models/TypeIIPeriodOccurrenceView.cs#L7)
- [TypeIIPeriodAmendmentView](../../src/Compliance.Common/Features/Operations/Models/TypeIIPeriodAmendmentView.cs#L7)
- [TypeIIPeriodCloseValidationContent](../../src/Compliance.Common/Features/Operations/Models/TypeIIPeriodCloseValidationContent.cs#L7)
- [TypeIIPeriodCloseValidationView](../../src/Compliance.Common/Features/Operations/Models/TypeIIPeriodCloseValidationView.cs#L7)
- [TypeIIPeriodCloseSnapshotContent](../../src/Compliance.Common/Features/Operations/Models/TypeIIPeriodCloseSnapshotContent.cs#L7)
- [TypeIIPeriodCloseSnapshotView](../../src/Compliance.Common/Features/Operations/Models/TypeIIPeriodCloseSnapshotView.cs#L7)
- [TypeIIPeriodCloseApprovalView](../../src/Compliance.Common/Features/Operations/Models/TypeIIPeriodCloseApprovalView.cs#L7)
- [ControlCalendarView](../../src/Compliance.Common/Features/Operations/Models/ControlCalendarView.cs#L6)
- [MissingOccurrenceView](../../src/Compliance.Common/Features/Operations/Models/MissingOccurrenceView.cs#L6)
- [GovernanceReviewDueView](../../src/Compliance.Common/Features/Operations/Models/GovernanceReviewDueView.cs#L6)
- [ManagementReviewView](../../src/Compliance.Common/Features/Operations/Models/ManagementReviewView.cs#L6)
- [ManagementReviewActionView](../../src/Compliance.Common/Features/Operations/Models/ManagementReviewActionView.cs#L6)

##### Changes

- [SignificantChangeView](../../src/Compliance.Common/Features/Changes/Models/SignificantChangeView.cs#L6)
- [SignificantChangeSummaryView](../../src/Compliance.Common/Features/Changes/Models/SignificantChangeSummaryView.cs#L6)

##### Incidents

- [ComplianceIncidentView](../../src/Compliance.Common/Features/Incidents/Models/ComplianceIncidentView.cs#L6)
- [ComplianceIncidentSummaryView](../../src/Compliance.Common/Features/Incidents/Models/ComplianceIncidentSummaryView.cs#L6)
- [ComplianceIncidentImpactAssessmentView](../../src/Compliance.Common/Features/Incidents/Models/ComplianceIncidentImpactAssessmentView.cs#L6)

##### AuditPopulations

- [ObservationPeriodClosePreview](../../src/Compliance.Common/Features/AuditPopulations/Models/ObservationPeriodClosePreview.cs#L6)
- [ObservationPeriodCloseView](../../src/Compliance.Common/Features/AuditPopulations/Models/ObservationPeriodCloseView.cs#L6)
- [AuditPopulationDefinitionView](../../src/Compliance.Common/Features/AuditPopulations/Models/AuditPopulationDefinitionView.cs#L6)
- [AuditPopulationExportView](../../src/Compliance.Common/Features/AuditPopulations/Models/AuditPopulationExportView.cs#L6)
- [AuditPopulationExportReceipt](../../src/Compliance.Common/Features/AuditPopulations/Models/AuditPopulationExportReceipt.cs#L4)
- [AuditPopulationView](../../src/Compliance.Common/Features/AuditPopulations/Models/AuditPopulationView.cs#L6)
- [AuditPopulationRowView](../../src/Compliance.Common/Features/AuditPopulations/Models/AuditPopulationRowView.cs#L6)
- [PopulationReconciliationView](../../src/Compliance.Common/Features/AuditPopulations/Models/PopulationReconciliationView.cs#L6)

##### Programs

- [ProgramRolloverPreview](../../src/Compliance.Common/Features/Programs/Models/ProgramRolloverPreview.cs#L6)
- [ProgramRolloverView](../../src/Compliance.Common/Features/Programs/Models/ProgramRolloverView.cs#L6)
- [NextPeriodPlanView](../../src/Compliance.Common/Features/Programs/Models/NextPeriodPlanView.cs#L6)
- [CarryForwardReferenceView](../../src/Compliance.Common/Features/Programs/Models/CarryForwardReferenceView.cs#L6)

##### FirmPortfolio

- [AuthorizedClientSummaryView](../../src/Compliance.Common/Features/FirmPortfolio/Models/AuthorizedClientSummaryView.cs#L6)
- [CrossClientWorkItemView](../../src/Compliance.Common/Features/FirmPortfolio/Models/CrossClientWorkItemView.cs#L6)

##### MethodologyTemplates

- [MethodologyTemplateSummaryView](../../src/Compliance.Common/Features/MethodologyTemplates/Models/MethodologyTemplateSummaryView.cs#L6)
- [MethodologyTemplateVersionView](../../src/Compliance.Common/Features/MethodologyTemplates/Models/MethodologyTemplateVersionView.cs#L6)
- [MethodologyTemplateApplicationPreview](../../src/Compliance.Common/Features/MethodologyTemplates/Models/MethodologyTemplateApplicationPreview.cs#L6)
- [TemplateApplicationView](../../src/Compliance.Common/Features/MethodologyTemplates/Models/TemplateApplicationView.cs#L6)

##### AttestRecords

- [OwnAttestRecordPackageView](../../src/Compliance.Common/Features/AttestRecords/Models/OwnAttestRecordPackageView.cs#L6)
- [OwnAttestRecordDeliveryView](../../src/Compliance.Common/Features/AttestRecords/Models/OwnAttestRecordDeliveryView.cs#L6)
- [AttestDeliveryHoldView](../../src/Compliance.Common/Features/AttestRecords/Models/AttestDeliveryHoldView.cs#L6)

### Existing HTTP APIs

These are literal MapPortia registrations in the host. Host-mode/authentication conditionals can limit availability. Middleware, health checks, raw authentication routes and MCP mappings are listed separately below. This is not a generated OpenAPI snapshot.

| Method | Route | Request | Source |
| --- | --- | --- | --- |
| POST | `/api/v1/developer-user-sessions` | `ContinueWithDeveloperIdentity` | [registration](../../src/Compliance.App/Program.cs#L480) |
| POST | `/api/v1/oidc-user-sessions` | `ContinueWithOidcProvider` | [registration](../../src/Compliance.App/Program.cs#L488) |
| POST | `/api/v1/my/oidc-identity-links` | `LinkOidcProviderIdentity` | [registration](../../src/Compliance.App/Program.cs#L493) |
| POST | `/api/v1/identity-recovery/challenges` | `StartIdentityRecovery` | [registration](../../src/Compliance.App/Program.cs#L497) |
| POST | `/api/v1/identity-recovery/options` | `GetIdentityRecoveryOptions` | [registration](../../src/Compliance.App/Program.cs#L500) |
| POST | `/api/v1/identity-recovery/completions` | `CompleteIdentityRecovery` | [registration](../../src/Compliance.App/Program.cs#L504) |
| POST | `/api/v1/tenants` | `RegisterTenant` | [registration](../../src/Compliance.App/Program.cs#L508) |
| POST | `/api/v1/tenants/{tenant_id}/suspensions` | `SuspendTenant` | [registration](../../src/Compliance.App/Program.cs#L516) |
| DELETE | `/api/v1/tenants/{tenant_id}/suspensions` | `ReactivateTenant` | [registration](../../src/Compliance.App/Program.cs#L519) |
| POST | `/api/v1/tenants/{tenant_id}/offboarding` | `OffboardTenant` | [registration](../../src/Compliance.App/Program.cs#L522) |
| POST | `/api/v1/platform/operator-grants` | `GrantPlatformOperator` | [registration](../../src/Compliance.App/Program.cs#L525) |
| POST | `/api/v1/platform/operator-revocations` | `RevokePlatformOperator` | [registration](../../src/Compliance.App/Program.cs#L528) |
| GET | `/api/v1/platform/operators` | `ListPlatformOperators` | [registration](../../src/Compliance.App/Program.cs#L531) |
| GET | `/api/v1/platform/tenants/{tenant_id}/members/{member_id}/work-digest-dispatches/{week_of}` | `GetWorkDigestDispatchStatus` | [registration](../../src/Compliance.App/Program.cs#L534) |
| POST | `/api/v1/platform/tenants/{tenant_id}/members/{member_id}/work-digest-dispatches/{week_of}/retry-authorizations` | `AuthorizeWorkDigestUnknownRetry` | [registration](../../src/Compliance.App/Program.cs#L538) |
| POST | `/api/v1/tenants/{tenant_id}/invitations` | `InviteTenantMember` | [registration](../../src/Compliance.App/Program.cs#L542) |
| POST | `/api/v1/tenants/{tenant_id}/member-invitations` | `InviteOrganizationMember` | [registration](../../src/Compliance.App/Program.cs#L545) |
| POST | `/api/v1/tenants/{tenant_id}/guest-invitations` | `InviteGuestConsultant` | [registration](../../src/Compliance.App/Program.cs#L548) |
| GET | `/api/v1/tenants/{tenant_id}/member-invitations` | `ListTenantInvitations` | [registration](../../src/Compliance.App/Program.cs#L551) |
| GET | `/api/v1/tenants/{tenant_id}/members/{user_id}/access` | `GetMemberAccess` | [registration](../../src/Compliance.App/Program.cs#L555) |
| POST | `/api/v1/tenants/{tenant_id}/members/{user_id}/suspensions` | `SuspendMember` | [registration](../../src/Compliance.App/Program.cs#L559) |
| DELETE | `/api/v1/tenants/{tenant_id}/members/{user_id}/suspensions` | `ReinstateMember` | [registration](../../src/Compliance.App/Program.cs#L563) |
| POST | `/api/v1/tenants/{tenant_id}/members/{user_id}/deprovisioning` | `DeprovisionMember` | [registration](../../src/Compliance.App/Program.cs#L567) |
| GET | `/api/v1/tenants/{tenant_id}/members/{user_id}` | `GetTenantMember` | [registration](../../src/Compliance.App/Program.cs#L571) |
| GET | `/api/v1/tenants/{tenant_id}/members/{user_id}/responsibilities` | `ListMemberResponsibilities` | [registration](../../src/Compliance.App/Program.cs#L575) |
| POST | `/api/v1/tenants/{tenant_id}/invitations/acceptance` | `AcceptTenantInvitation` | [registration](../../src/Compliance.App/Program.cs#L579) |
| GET | `/api/v1/tenants/{tenant_id}` | `GetTenant` | [registration](../../src/Compliance.App/Program.cs#L582) |
| GET | `/api/v1/tenants/{tenant_id}/provisioning-status` | `GetTenantProvisioningStatus` | [registration](../../src/Compliance.App/Program.cs#L585) |
| GET | `/api/v1/platform/tenants` | `ListTenants` | [registration](../../src/Compliance.App/Program.cs#L589) |
| GET | `/api/v1/tenants/{tenant_id}/members` | `ListTenantMembers` | [registration](../../src/Compliance.App/Program.cs#L592) |
| POST | `/api/v1/tenants/{tenant_id}/slug-changes` | `ChangeTenantSlug` | [registration](../../src/Compliance.App/Program.cs#L595) |
| GET | `/api/v1/tenant-slugs/{slug}/mine` | `ResolveMyTenantSlug` | [registration](../../src/Compliance.App/Program.cs#L598) |
| POST | `/api/v1/tenants/{tenant_id}/programs` | `CreateProgram` | [registration](../../src/Compliance.App/Program.cs#L601) |
| GET | `/api/v1/tenants/{tenant_id}/criteria-editions` | `ListCriteriaCatalogEditions` | [registration](../../src/Compliance.App/Program.cs#L604) |
| GET | `/api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}` | `GetCriteriaCatalogEdition` | [registration](../../src/Compliance.App/Program.cs#L608) |
| GET | `/api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}/entries` | `ListCriteriaCatalogEntries` | [registration](../../src/Compliance.App/Program.cs#L612) |
| GET | `/api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}/entries/export` | `ExportCriteriaCatalogEntries` | [registration](../../src/Compliance.App/Program.cs#L616) |
| GET | `/api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}/entries/{identifier}` | `GetCriteriaCatalogEntry` | [registration](../../src/Compliance.App/Program.cs#L620) |
| PUT | `/api/v1/platform/firm-staff/{staff_member_id}/register-firm-staff` | `RegisterFirmStaff` | [registration](../../src/Compliance.App/Program.cs#L624) |
| PUT | `/api/v1/platform/firm-staff/{staff_member_id}/set-firm-staff-status/{command_id}` | `SetFirmStaffStatus` | [registration](../../src/Compliance.App/Program.cs#L628) |
| PUT | `/api/v1/platform/firm-professional-duties/{designation_id}/assertions/{assertion_id}` | `RecordFirmProfessionalDutyDesignation` | [registration](../../src/Compliance.App/Program.cs#L632) |
| PUT | `/api/v1/platform/firm-professional-duties/{designation_id}/assertion-revocations/{assertion_id}` | `RevokeFirmProfessionalDutyDesignation` | [registration](../../src/Compliance.App/Program.cs#L636) |
| GET | `/api/v1/platform/firm-professional-duties` | `GetFirmProfessionalDutyCatalog` | [registration](../../src/Compliance.App/Program.cs#L640) |
| GET | `/api/v1/platform/firm-staff` | `GetFirmStaffDirectory` | [registration](../../src/Compliance.App/Program.cs#L652) |
| GET | `/api/v1/tenants/{tenant_id}/firm-staff` | `ListAssignableFirmStaff` | [registration](../../src/Compliance.App/Program.cs#L656) |
| PUT | `/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/create-service-engagement` | `CreateServiceEngagement` | [registration](../../src/Compliance.App/Program.cs#L660) |
| PUT | `/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/amend-service-engagement/{command_id}` | `AmendServiceEngagement` | [registration](../../src/Compliance.App/Program.cs#L664) |
| PUT | `/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/propose-service-engagement-staff/{command_id}` | `ProposeServiceEngagementStaff` | [registration](../../src/Compliance.App/Program.cs#L668) |
| PUT | `/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/staff-proposals/{staff_member_id}/withdrawals/{command_id}` | `WithdrawServiceEngagementStaffProposal` | [registration](../../src/Compliance.App/Program.cs#L672) |
| PUT | `/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/actual-assignments/{staff_member_id}/revocations/{command_id}` | `RevokeServiceEngagementActualStaff` | [registration](../../src/Compliance.App/Program.cs#L676) |
| GET | `/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/acceptance` | `GetServiceEngagementAcceptance` | [registration](../../src/Compliance.App/Program.cs#L684) |
| GET | `/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/acceptance-history` | `GetServiceEngagementAcceptanceHistory` | [registration](../../src/Compliance.App/Program.cs#L692) |
| GET | `/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/management-acknowledgements` | `GetEngagementManagementAcknowledgements` | [registration](../../src/Compliance.App/Program.cs#L700) |
| PUT | `/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/closure/{command_id}` | `CloseServiceEngagement` | [registration](../../src/Compliance.App/Program.cs#L704) |
| GET | `/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}` | `GetServiceEngagement` | [registration](../../src/Compliance.App/Program.cs#L708) |
| GET | `/api/v1/tenants/{tenant_id}/service-engagements` | `ListServiceEngagements` | [registration](../../src/Compliance.App/Program.cs#L712) |
| GET | `/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/history` | `GetServiceEngagementHistory` | [registration](../../src/Compliance.App/Program.cs#L716) |
| GET | `/api/v1/tenants/{tenant_id}/independence/rules` | `GetClientIndependenceRules` | [registration](../../src/Compliance.App/Program.cs#L720) |
| GET | `/api/v1/platform/independence/rules` | `GetIndependenceRuleVersions` | [registration](../../src/Compliance.App/Program.cs#L724) |
| PUT | `/api/v1/platform/independence/rules` | `ReviseIndependenceRules` | [registration](../../src/Compliance.App/Program.cs#L728) |
| POST | `/api/v1/platform/independence/rules/{rule_version}/ratification` | `RatifyIndependenceRuleVersion` | [registration](../../src/Compliance.App/Program.cs#L732) |
| PUT | `/api/v1/tenants/{tenant_id}/nonattest-services/record-nonattest-service/{service_record_id}` | `RecordNonattestService` | [registration](../../src/Compliance.App/Program.cs#L736) |
| PUT | `/api/v1/tenants/{tenant_id}/client-independence-evaluations/evaluate-client-independence/{evaluation_id}` | `EvaluateClientIndependence` | [registration](../../src/Compliance.App/Program.cs#L740) |
| GET | `/api/v1/tenants/{tenant_id}/independence/history` | `GetClientIndependenceHistory` | [registration](../../src/Compliance.App/Program.cs#L744) |
| PUT | `/api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}/overlays/set-criteria-text-overlay/{command_id}` | `SetCriteriaTextOverlay` | [registration](../../src/Compliance.App/Program.cs#L748) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/criteria-edition` | `SelectProgramCriteriaEdition` | [registration](../../src/Compliance.App/Program.cs#L752) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}` | `ReviseProgram` | [registration](../../src/Compliance.App/Program.cs#L756) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}` | `GetProgram` | [registration](../../src/Compliance.App/Program.cs#L759) |
| GET | `/api/v1/tenants/{tenant_id}/programs` | `ListPrograms` | [registration](../../src/Compliance.App/Program.cs#L762) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/revisions` | `ListProgramRevisions` | [registration](../../src/Compliance.App/Program.cs#L765) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/revisions/{revision}` | `GetProgramRevision` | [registration](../../src/Compliance.App/Program.cs#L769) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/setup-work` | `GetProgramSetupWork` | [registration](../../src/Compliance.App/Program.cs#L773) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls` | `CreateControlDraft` | [registration](../../src/Compliance.App/Program.cs#L777) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft` | `ReviseControlDraft` | [registration](../../src/Compliance.App/Program.cs#L781) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/discards` | `DiscardControlDraft` | [registration](../../src/Compliance.App/Program.cs#L785) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft` | `GetControlDraft` | [registration](../../src/Compliance.App/Program.cs#L789) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls` | `ListControlDrafts` | [registration](../../src/Compliance.App/Program.cs#L793) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/revisions` | `ListControlDraftRevisions` | [registration](../../src/Compliance.App/Program.cs#L797) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/revisions/{revision}` | `GetControlDraftRevision` | [registration](../../src/Compliance.App/Program.cs#L801) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/reviews` | `ReviewControl` | [registration](../../src/Compliance.App/Program.cs#L806) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/approvals` | `ApproveControl` | [registration](../../src/Compliance.App/Program.cs#L810) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/current-version` | `GetCurrentControlVersion` | [registration](../../src/Compliance.App/Program.cs#L814) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/effective-version` | `GetEffectiveControlVersion` | [registration](../../src/Compliance.App/Program.cs#L818) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/versions` | `ListControlVersions` | [registration](../../src/Compliance.App/Program.cs#L822) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/versions/{version_id}` | `GetControlVersion` | [registration](../../src/Compliance.App/Program.cs#L826) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/decisions` | `ListControlDecisions` | [registration](../../src/Compliance.App/Program.cs#L830) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/decisions/{decision_id}` | `GetControlDecision` | [registration](../../src/Compliance.App/Program.cs#L834) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}` | `ProposeControlCriterionMapping` | [registration](../../src/Compliance.App/Program.cs#L839) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}/reviews/{command_id}` | `ReviewControlCriterionMapping` | [registration](../../src/Compliance.App/Program.cs#L843) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}/retirements/{command_id}` | `RetireControlCriterionMapping` | [registration](../../src/Compliance.App/Program.cs#L847) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}` | `GetControlCriterionMapping` | [registration](../../src/Compliance.App/Program.cs#L851) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings` | `ListControlCriterionMappings` | [registration](../../src/Compliance.App/Program.cs#L855) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/criteria-coverage` | `ListCriteriaCoverage` | [registration](../../src/Compliance.App/Program.cs#L859) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/successors` | `ProposeControlSuccessor` | [registration](../../src/Compliance.App/Program.cs#L863) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/retirement-proposals` | `ProposeControlRetirement` | [registration](../../src/Compliance.App/Program.cs#L867) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/impact-preview` | `PreviewControlImpact` | [registration](../../src/Compliance.App/Program.cs#L871) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/retirements` | `RetireControl` | [registration](../../src/Compliance.App/Program.cs#L876) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/proposal-withdrawals` | `WithdrawControlProposal` | [registration](../../src/Compliance.App/Program.cs#L881) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/owner-person` | `DesignateControlOwnerPerson` | [registration](../../src/Compliance.App/Program.cs#L885) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/owner-person` | `GetControlDraftOwnerPerson` | [registration](../../src/Compliance.App/Program.cs#L889) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}` | `ProposeCriterionNotApplicable` | [registration](../../src/Compliance.App/Program.cs#L894) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}/reviews/{command_id}` | `ReviewCriterionApplicability` | [registration](../../src/Compliance.App/Program.cs#L898) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}/withdrawals/{command_id}` | `WithdrawCriterionNotApplicable` | [registration](../../src/Compliance.App/Program.cs#L902) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}` | `GetCriterionApplicability` | [registration](../../src/Compliance.App/Program.cs#L906) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability` | `ListCriterionApplicability` | [registration](../../src/Compliance.App/Program.cs#L910) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts` | `CreateCommitmentDraft` | [registration](../../src/Compliance.App/Program.cs#L914) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}` | `ReviseCommitmentDraft` | [registration](../../src/Compliance.App/Program.cs#L918) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}` | `GetCommitmentDraft` | [registration](../../src/Compliance.App/Program.cs#L922) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts` | `ListCommitmentDrafts` | [registration](../../src/Compliance.App/Program.cs#L926) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/revisions` | `ListCommitmentDraftRevisions` | [registration](../../src/Compliance.App/Program.cs#L930) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/revisions/{revision}` | `GetCommitmentDraftRevision` | [registration](../../src/Compliance.App/Program.cs#L934) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/impact-preview` | `PreviewCommitmentImpact` | [registration](../../src/Compliance.App/Program.cs#L938) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/reviews` | `ReviewCommitmentDraft` | [registration](../../src/Compliance.App/Program.cs#L942) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/approvals` | `ApproveCommitmentDraft` | [registration](../../src/Compliance.App/Program.cs#L947) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/decisions` | `ListCommitmentDecisions` | [registration](../../src/Compliance.App/Program.cs#L951) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/versions` | `ListCommitmentVersions` | [registration](../../src/Compliance.App/Program.cs#L955) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/versions/{version}` | `GetCommitmentVersion` | [registration](../../src/Compliance.App/Program.cs#L959) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/effective-version` | `GetEffectiveCommitmentVersion` | [registration](../../src/Compliance.App/Program.cs#L963) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies` | `CreatePolicyDraft` | [registration](../../src/Compliance.App/Program.cs#L967) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies` | `ListPolicies` | [registration](../../src/Compliance.App/Program.cs#L971) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}` | `GetPolicy` | [registration](../../src/Compliance.App/Program.cs#L975) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/draft` | `RevisePolicyDraft` | [registration](../../src/Compliance.App/Program.cs#L979) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/draft/discards` | `DiscardPolicyDraft` | [registration](../../src/Compliance.App/Program.cs#L983) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/successors` | `ProposePolicySuccessor` | [registration](../../src/Compliance.App/Program.cs#L987) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/impact-preview` | `PreviewPolicyImpact` | [registration](../../src/Compliance.App/Program.cs#L991) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/reviews` | `ReviewPolicyDraft` | [registration](../../src/Compliance.App/Program.cs#L996) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/approvals` | `ApprovePolicy` | [registration](../../src/Compliance.App/Program.cs#L1000) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/periodic-reviews` | `ConfirmPolicyReview` | [registration](../../src/Compliance.App/Program.cs#L1004) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/retirement-proposals` | `ProposePolicyRetirement` | [registration](../../src/Compliance.App/Program.cs#L1008) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/retirements` | `ApprovePolicyRetirement` | [registration](../../src/Compliance.App/Program.cs#L1012) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/versions` | `ListPolicyVersions` | [registration](../../src/Compliance.App/Program.cs#L1016) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/versions/{version}` | `GetPolicyVersion` | [registration](../../src/Compliance.App/Program.cs#L1020) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/effective-version` | `GetEffectivePolicyVersion` | [registration](../../src/Compliance.App/Program.cs#L1024) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/decisions` | `ListPolicyDecisions` | [registration](../../src/Compliance.App/Program.cs#L1028) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements/{requirement_id}` | `DefineTrainingRequirement` | [registration](../../src/Compliance.App/Program.cs#L1032) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements` | `ListTrainingRequirements` | [registration](../../src/Compliance.App/Program.cs#L1036) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements/{requirement_id}` | `GetTrainingRequirement` | [registration](../../src/Compliance.App/Program.cs#L1040) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements/revise-training-requirement/{command_id}` | `ReviseTrainingRequirement` | [registration](../../src/Compliance.App/Program.cs#L1044) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policy-campaigns/{campaign_id}` | `LaunchPolicyCampaign` | [registration](../../src/Compliance.App/Program.cs#L1048) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/training-campaigns/{campaign_id}` | `LaunchTrainingCampaign` | [registration](../../src/Compliance.App/Program.cs#L1052) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns` | `ListCampaigns` | [registration](../../src/Compliance.App/Program.cs#L1056) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}` | `GetCampaign` | [registration](../../src/Compliance.App/Program.cs#L1060) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/participants` | `ListCampaignParticipants` | [registration](../../src/Compliance.App/Program.cs#L1064) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/amendments` | `ListCampaignAmendments` | [registration](../../src/Compliance.App/Program.cs#L1068) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/reconciliations/{command_id}` | `ReconcileCampaignAudience` | [registration](../../src/Compliance.App/Program.cs#L1072) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/completions/{command_id}` | `RecordTrainingCompletion` | [registration](../../src/Compliance.App/Program.cs#L1076) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/closure/{command_id}` | `CloseCampaign` | [registration](../../src/Compliance.App/Program.cs#L1080) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/acknowledgements/{command_id}` | `AcknowledgePolicy` | [registration](../../src/Compliance.App/Program.cs#L1085) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/waivers/{command_id}` | `ApproveCampaignWaiver` | [registration](../../src/Compliance.App/Program.cs#L1089) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks` | `CreateRiskDraft` | [registration](../../src/Compliance.App/Program.cs#L1093) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/draft` | `ReviseRiskDraft` | [registration](../../src/Compliance.App/Program.cs#L1097) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/draft` | `GetRiskDraft` | [registration](../../src/Compliance.App/Program.cs#L1101) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks` | `ListRiskDrafts` | [registration](../../src/Compliance.App/Program.cs#L1105) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/draft/revisions` | `ListRiskDraftRevisions` | [registration](../../src/Compliance.App/Program.cs#L1109) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/draft/revisions/{revision}` | `GetRiskDraftRevision` | [registration](../../src/Compliance.App/Program.cs#L1113) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risk-method/versions` | `PublishRiskMethodVersion` | [registration](../../src/Compliance.App/Program.cs#L1117) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risk-method` | `GetRiskMethod` | [registration](../../src/Compliance.App/Program.cs#L1121) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risk-method/versions/{version}` | `GetRiskMethodVersion` | [registration](../../src/Compliance.App/Program.cs#L1125) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/assessments` | `RecordRiskAssessment` | [registration](../../src/Compliance.App/Program.cs#L1129) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment` | `ChooseRiskTreatment` | [registration](../../src/Compliance.App/Program.cs#L1133) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/acceptances` | `AcceptRisk` | [registration](../../src/Compliance.App/Program.cs#L1137) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/evaluation` | `GetRiskEvaluation` | [registration](../../src/Compliance.App/Program.cs#L1141) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/evaluation/history` | `ListRiskEvaluationHistory` | [registration](../../src/Compliance.App/Program.cs#L1145) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/owner` | `AssignRiskOwner` | [registration](../../src/Compliance.App/Program.cs#L1150) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/control-treatments` | `ProposeRiskControlTreatment` | [registration](../../src/Compliance.App/Program.cs#L1154) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/control-treatments/{treatment_id}/reviews` | `ReviewRiskControlTreatment` | [registration](../../src/Compliance.App/Program.cs#L1158) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/control-treatments/{treatment_id}/retirements` | `RetireRiskControlTreatment` | [registration](../../src/Compliance.App/Program.cs#L1162) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions` | `AddRiskTreatmentAction` | [registration](../../src/Compliance.App/Program.cs#L1166) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}` | `ReviseRiskTreatmentAction` | [registration](../../src/Compliance.App/Program.cs#L1170) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}/cancellations` | `CancelRiskTreatmentAction` | [registration](../../src/Compliance.App/Program.cs#L1174) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}/completions` | `SubmitRiskTreatmentActionCompletion` | [registration](../../src/Compliance.App/Program.cs#L1178) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}/completion-reviews` | `ReviewRiskTreatmentActionCompletion` | [registration](../../src/Compliance.App/Program.cs#L1183) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/governance` | `GetRiskGovernance` | [registration](../../src/Compliance.App/Program.cs#L1187) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments` | `RunReadinessAssessment` | [registration](../../src/Compliance.App/Program.cs#L1191) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments` | `ListReadinessAssessments` | [registration](../../src/Compliance.App/Program.cs#L1195) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}` | `GetReadinessAssessment` | [registration](../../src/Compliance.App/Program.cs#L1199) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/gaps` | `ListReadinessGaps` | [registration](../../src/Compliance.App/Program.cs#L1203) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/decision` | `DecideReadiness` | [registration](../../src/Compliance.App/Program.cs#L1207) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/gaps/{gap_id}/plan` | `PlanReadinessGap` | [registration](../../src/Compliance.App/Program.cs#L1211) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/type-i-entry-decision` | `DecideTypeIEntry` | [registration](../../src/Compliance.App/Program.cs#L1217) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/type-i-entry-decisions` | `ListTypeIEntryDecisions` | [registration](../../src/Compliance.App/Program.cs#L1221) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/gaps/{gap_id}/annotations` | `AnnotateReadinessGap` | [registration](../../src/Compliance.App/Program.cs#L1225) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/annotations` | `ListReadinessAnnotations` | [registration](../../src/Compliance.App/Program.cs#L1229) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan/proposals/{plan_version_id}` | `ProposeControlOperatingPlan` | [registration](../../src/Compliance.App/Program.cs#L1233) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan/preview` | `PreviewControlOperatingPlan` | [registration](../../src/Compliance.App/Program.cs#L1237) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan/proposals/{plan_version_id}/approvals/{command_id}` | `ApproveControlOperatingPlan` | [registration](../../src/Compliance.App/Program.cs#L1241) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan` | `GetControlOperatingPlan` | [registration](../../src/Compliance.App/Program.cs#L1245) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/operating-blockers` | `ListControlOperatingBlockers` | [registration](../../src/Compliance.App/Program.cs#L1249) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/my-work` | `ListMyControlWork` | [registration](../../src/Compliance.App/Program.cs#L1253) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{command_id}` | `OpenControlOccurrence` | [registration](../../src/Compliance.App/Program.cs#L1257) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences` | `ListControlOccurrences` | [registration](../../src/Compliance.App/Program.cs#L1261) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}` | `GetControlOccurrence` | [registration](../../src/Compliance.App/Program.cs#L1265) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/attestations/{command_id}` | `AttestControlOccurrence` | [registration](../../src/Compliance.App/Program.cs#L1269) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/corrections/{command_id}` | `CorrectControlAttestation` | [registration](../../src/Compliance.App/Program.cs#L1273) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/reviews/{command_id}` | `ReviewControlOccurrence` | [registration](../../src/Compliance.App/Program.cs#L1277) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}` | `StartControlEvaluation` | [registration](../../src/Compliance.App/Program.cs#L1281) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluation-plan/versions` | `DefineControlEvaluationPlan` | [registration](../../src/Compliance.App/Program.cs#L1285) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluation-plan/versions` | `ListControlEvaluationPlanVersions` | [registration](../../src/Compliance.App/Program.cs#L1289) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluation-plan/versions/{plan_version_id}` | `GetControlEvaluationPlanVersion` | [registration](../../src/Compliance.App/Program.cs#L1294) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations` | `ListControlEvaluations` | [registration](../../src/Compliance.App/Program.cs#L1298) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}` | `GetControlEvaluation` | [registration](../../src/Compliance.App/Program.cs#L1302) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/steps/{step_id}/results/{command_id}` | `RecordControlEvaluationStep` | [registration](../../src/Compliance.App/Program.cs#L1306) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/deviations/{deviation_id}/dispositions/{command_id}` | `DisposeControlEvaluationDeviation` | [registration](../../src/Compliance.App/Program.cs#L1310) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/submissions/{command_id}` | `SubmitControlEvaluation` | [registration](../../src/Compliance.App/Program.cs#L1314) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/reviews/{command_id}` | `ReviewControlEvaluation` | [registration](../../src/Compliance.App/Program.cs#L1318) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests` | `OpenEvidenceRequest` | [registration](../../src/Compliance.App/Program.cs#L1322) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests` | `ListEvidenceRequests` | [registration](../../src/Compliance.App/Program.cs#L1326) |
| POST | `/api/v1/tenants/{tenant_id}/evidence-redactions/{redaction_id}/preparations` | `PrepareEvidenceRedaction` | [registration](../../src/Compliance.App/Program.cs#L1330) |
| POST | `/api/v1/tenants/{tenant_id}/evidence-redactions/{redaction_id}/approvals` | `ApproveEvidenceRedaction` | [registration](../../src/Compliance.App/Program.cs#L1334) |
| GET | `/api/v1/tenants/{tenant_id}/evidence-redactions/{redaction_id}` | `GetEvidenceRedaction` | [registration](../../src/Compliance.App/Program.cs#L1338) |
| GET | `/api/v1/tenants/{tenant_id}/evidence-artifacts/{artifact_id}/metadata` | `GetEvidenceArtifactMetadata` | [registration](../../src/Compliance.App/Program.cs#L1342) |
| POST | `/api/v1/tenants/{tenant_id}/evidence-artifacts/{artifact_id}/downloads` | `IssueArtifactDownload` | [registration](../../src/Compliance.App/Program.cs#L1346) |
| POST | `/api/v1/tenants/{tenant_id}/evidence-artifacts/{artifact_id}/quarantine-releases` | `ReleaseEvidenceArtifactQuarantine` | [registration](../../src/Compliance.App/Program.cs#L1350) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-artifacts/{artifact_id}/supports` | `LinkEvidenceSupport` | [registration](../../src/Compliance.App/Program.cs#L1355) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests/{evidence_request_id}` | `GetEvidenceRequest` | [registration](../../src/Compliance.App/Program.cs#L1359) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests/{evidence_request_id}/fulfilments` | `FulfilEvidenceRequest` | [registration](../../src/Compliance.App/Program.cs#L1363) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests/{evidence_request_id}/cancellations` | `CancelEvidenceRequest` | [registration](../../src/Compliance.App/Program.cs#L1367) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}` | `RaiseFinding` | [registration](../../src/Compliance.App/Program.cs#L1371) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/findings` | `ListFindings` | [registration](../../src/Compliance.App/Program.cs#L1375) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}` | `GetFinding` | [registration](../../src/Compliance.App/Program.cs#L1379) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/revisions/{command_id}` | `ReviseFinding` | [registration](../../src/Compliance.App/Program.cs#L1383) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/corrective-actions/{action_id}` | `AddCorrectiveAction` | [registration](../../src/Compliance.App/Program.cs#L1387) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/corrective-actions/{action_id}/completions/{command_id}` | `CompleteCorrectiveAction` | [registration](../../src/Compliance.App/Program.cs#L1391) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/acceptances/{command_id}` | `LinkFindingAcceptance` | [registration](../../src/Compliance.App/Program.cs#L1395) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/closures/{command_id}` | `CloseFinding` | [registration](../../src/Compliance.App/Program.cs#L1399) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/reopenings/{command_id}` | `ReopenFinding` | [registration](../../src/Compliance.App/Program.cs#L1403) |
| PUT | `/api/v1/tenants/{tenant_id}/access-populations/{population_id}` | `OpenAccessPopulation` | [registration](../../src/Compliance.App/Program.cs#L1407) |
| PUT | `/api/v1/tenants/{tenant_id}/access-populations/record-access-population-facts/{command_id}` | `RecordAccessPopulationFacts` | [registration](../../src/Compliance.App/Program.cs#L1411) |
| GET | `/api/v1/tenants/{tenant_id}/access-populations/{population_id}/preview` | `PreviewAccessPopulation` | [registration](../../src/Compliance.App/Program.cs#L1415) |
| PUT | `/api/v1/tenants/{tenant_id}/access-populations/{population_id}/acceptance/{command_id}` | `AcceptAccessPopulation` | [registration](../../src/Compliance.App/Program.cs#L1419) |
| GET | `/api/v1/tenants/{tenant_id}/access-populations/{population_id}` | `GetAccessPopulation` | [registration](../../src/Compliance.App/Program.cs#L1423) |
| GET | `/api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-populations` | `ListAccessPopulations` | [registration](../../src/Compliance.App/Program.cs#L1427) |
| PUT | `/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-population-exceptions/{exception_id}` | `ExemptMissingAccessPopulation` | [registration](../../src/Compliance.App/Program.cs#L1431) |
| GET | `/api/v1/tenants/{tenant_id}/applications/{application_id}/access-review-coverage` | `GetAccessReviewCoverage` | [registration](../../src/Compliance.App/Program.cs#L1435) |
| PUT | `/api/v1/tenants/{tenant_id}/access-populations/{population_id}/classifications/{command_id}` | `ClassifyAccessPrincipal` | [registration](../../src/Compliance.App/Program.cs#L1439) |
| GET | `/api/v1/tenants/{tenant_id}/access-populations/{population_id}/principals` | `ListAccessPrincipals` | [registration](../../src/Compliance.App/Program.cs#L1443) |
| GET | `/api/v1/tenants/{tenant_id}/access-populations/{population_id}/variance` | `GetAccessVariance` | [registration](../../src/Compliance.App/Program.cs#L1447) |
| PUT | `/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id}` | `ProposeAccessExpectation` | [registration](../../src/Compliance.App/Program.cs#L1451) |
| PUT | `/api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id}/approval/{command_id}` | `ApproveAccessExpectation` | [registration](../../src/Compliance.App/Program.cs#L1455) |
| PUT | `/api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id}/exceptions/{command_id}` | `ExemptAccessExpectation` | [registration](../../src/Compliance.App/Program.cs#L1459) |
| GET | `/api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-expectations` | `ListAccessExpectations` | [registration](../../src/Compliance.App/Program.cs#L1463) |
| PUT | `/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}` | `LaunchAccessReviewCampaign` | [registration](../../src/Compliance.App/Program.cs#L1467) |
| GET | `/api/v1/tenants/{tenant_id}/access-review-campaigns` | `ListAccessReviewCampaigns` | [registration](../../src/Compliance.App/Program.cs#L1471) |
| GET | `/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}` | `GetAccessReviewCampaign` | [registration](../../src/Compliance.App/Program.cs#L1475) |
| PUT | `/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/decisions/{command_id}` | `RecordAccessDecision` | [registration](../../src/Compliance.App/Program.cs#L1479) |
| POST | `/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/bulk-decision-previews` | `PreviewBulkAccessDecision` | [registration](../../src/Compliance.App/Program.cs#L1483) |
| PUT | `/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-changes/{command_id}` | `RecordAccessRemediationChange` | [registration](../../src/Compliance.App/Program.cs#L1491) |
| PUT | `/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/responsibility-reassignments/{command_id}` | `ReassignAccessReviewResponsibility` | [registration](../../src/Compliance.App/Program.cs#L1495) |
| PUT | `/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-verifications/{command_id}` | `VerifyAccessRemediation` | [registration](../../src/Compliance.App/Program.cs#L1499) |
| PUT | `/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-exceptions/{command_id}` | `ExemptAccessRemediation` | [registration](../../src/Compliance.App/Program.cs#L1503) |
| PUT | `/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/completion/{command_id}` | `CompleteAccessReviewCampaign` | [registration](../../src/Compliance.App/Program.cs#L1507) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/work` | `ListWork` | [registration](../../src/Compliance.App/Program.cs#L1511) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/work/reminders` | `ListWorkReminders` | [registration](../../src/Compliance.App/Program.cs#L1515) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/work/digest` | `GetWorkDigest` | [registration](../../src/Compliance.App/Program.cs#L1519) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}` | `GetWorkItem` | [registration](../../src/Compliance.App/Program.cs#L1523) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/claims` | `ClaimWorkItem` | [registration](../../src/Compliance.App/Program.cs#L1527) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/assignments` | `AssignWorkItem` | [registration](../../src/Compliance.App/Program.cs#L1531) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/delegations` | `DelegateWorkItem` | [registration](../../src/Compliance.App/Program.cs#L1535) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/escalations` | `EscalateWorkItem` | [registration](../../src/Compliance.App/Program.cs#L1539) |
| GET | `/api/v1/tenants/{tenant_id}/work-digest-preference` | `GetWorkDigestPreference` | [registration](../../src/Compliance.App/Program.cs#L1543) |
| PUT | `/api/v1/tenants/{tenant_id}/work-digest-preference` | `SetWorkDigestPreference` | [registration](../../src/Compliance.App/Program.cs#L1547) |
| POST | `/api/v1/tenants/{tenant_id}/people` | `RecordPerson` | [registration](../../src/Compliance.App/Program.cs#L1551) |
| PUT | `/api/v1/tenants/{tenant_id}/people/{person_id}` | `RevisePerson` | [registration](../../src/Compliance.App/Program.cs#L1555) |
| PUT | `/api/v1/tenants/{tenant_id}/people/{person_id}/membership-correlation` | `CorrelatePersonMembership` | [registration](../../src/Compliance.App/Program.cs#L1559) |
| GET | `/api/v1/tenants/{tenant_id}/people/{person_id}` | `GetPerson` | [registration](../../src/Compliance.App/Program.cs#L1563) |
| GET | `/api/v1/tenants/{tenant_id}/people` | `ListPeople` | [registration](../../src/Compliance.App/Program.cs#L1567) |
| POST | `/api/v1/tenants/{tenant_id}/work-relationships` | `RecordWorkRelationship` | [registration](../../src/Compliance.App/Program.cs#L1571) |
| PUT | `/api/v1/tenants/{tenant_id}/work-relationships/{relationship_id}` | `ReviseWorkRelationship` | [registration](../../src/Compliance.App/Program.cs#L1575) |
| GET | `/api/v1/tenants/{tenant_id}/work-relationships/{relationship_id}` | `GetWorkRelationship` | [registration](../../src/Compliance.App/Program.cs#L1579) |
| GET | `/api/v1/tenants/{tenant_id}/work-relationships` | `ListWorkRelationships` | [registration](../../src/Compliance.App/Program.cs#L1583) |
| GET | `/api/v1/tenants/{tenant_id}/workforce-observations` | `ListWorkforceObservations` | [registration](../../src/Compliance.App/Program.cs#L1587) |
| PUT | `/api/v1/tenants/{tenant_id}/workforce-observations/{observation_id}/resolution` | `ResolveWorkforceObservation` | [registration](../../src/Compliance.App/Program.cs#L1591) |
| POST | `/api/v1/tenants/{tenant_id}/workforce-source-observations` | `RecordWorkforceSourceObservation` | [registration](../../src/Compliance.App/Program.cs#L1595) |
| GET | `/api/v1/tenants/{tenant_id}/workforce-source-observations/{observation_id}` | `GetWorkforceSourceObservation` | [registration](../../src/Compliance.App/Program.cs#L1599) |
| GET | `/api/v1/tenants/{tenant_id}/workforce-source-observations` | `ListWorkforceSourceObservations` | [registration](../../src/Compliance.App/Program.cs#L1603) |
| GET | `/api/v1/tenants/{tenant_id}/workforce-source-observations/{observation_id}/preview` | `PreviewWorkforceSourceObservation` | [registration](../../src/Compliance.App/Program.cs#L1607) |
| PUT | `/api/v1/tenants/{tenant_id}/workforce-source-observations/{observation_id}/decision` | `ReconcileWorkforceSourceObservation` | [registration](../../src/Compliance.App/Program.cs#L1611) |
| GET | `/api/v1/tenants/{tenant_id}/workforce-reconciliation-observations` | `ListWorkforceReconciliationObservations` | [registration](../../src/Compliance.App/Program.cs#L1615) |
| POST | `/api/v1/tenants/{tenant_id}/service-identities` | `RecordServiceIdentity` | [registration](../../src/Compliance.App/Program.cs#L1620) |
| PUT | `/api/v1/tenants/{tenant_id}/service-identities/{service_identity_id}` | `ReviseServiceIdentity` | [registration](../../src/Compliance.App/Program.cs#L1624) |
| GET | `/api/v1/tenants/{tenant_id}/service-identities/{service_identity_id}` | `GetServiceIdentity` | [registration](../../src/Compliance.App/Program.cs#L1628) |
| GET | `/api/v1/tenants/{tenant_id}/service-identities` | `ListServiceIdentities` | [registration](../../src/Compliance.App/Program.cs#L1632) |
| POST | `/api/v1/tenants/{tenant_id}/providers` | `RecordProvider` | [registration](../../src/Compliance.App/Program.cs#L1636) |
| PUT | `/api/v1/tenants/{tenant_id}/providers/{provider_id}` | `ReviseProvider` | [registration](../../src/Compliance.App/Program.cs#L1639) |
| GET | `/api/v1/tenants/{tenant_id}/providers/{provider_id}` | `GetProvider` | [registration](../../src/Compliance.App/Program.cs#L1642) |
| GET | `/api/v1/tenants/{tenant_id}/providers` | `ListProviders` | [registration](../../src/Compliance.App/Program.cs#L1645) |
| GET | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/revisions/{revision}` | `GetProviderRevision` | [registration](../../src/Compliance.App/Program.cs#L1648) |
| GET | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/revisions` | `ListProviderRevisions` | [registration](../../src/Compliance.App/Program.cs#L1651) |
| POST | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/assurance-reports` | `RecordAssuranceReport` | [registration](../../src/Compliance.App/Program.cs#L1654) |
| PUT | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/assurance-reports/{report_id}` | `ReviseAssuranceReport` | [registration](../../src/Compliance.App/Program.cs#L1658) |
| GET | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/assurance-reports` | `ListProviderAssuranceReports` | [registration](../../src/Compliance.App/Program.cs#L1662) |
| POST | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/reviews` | `RecordProviderReview` | [registration](../../src/Compliance.App/Program.cs#L1667) |
| GET | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/reviews` | `ListProviderReviews` | [registration](../../src/Compliance.App/Program.cs#L1671) |
| POST | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps` | `RecordProviderCoverageGap` | [registration](../../src/Compliance.App/Program.cs#L1675) |
| PUT | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps/{gap_id}` | `CloseProviderCoverageGap` | [registration](../../src/Compliance.App/Program.cs#L1680) |
| PUT | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps/{gap_id}/risk-acceptances` | `LinkProviderCoverageGapRiskAcceptance` | [registration](../../src/Compliance.App/Program.cs#L1685) |
| GET | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps/{gap_id}` | `GetProviderCoverageGap` | [registration](../../src/Compliance.App/Program.cs#L1689) |
| GET | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps` | `ListProviderCoverageGaps` | [registration](../../src/Compliance.App/Program.cs#L1693) |
| GET | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/assurance-coverage` | `GetProviderAssuranceCoverage` | [registration](../../src/Compliance.App/Program.cs#L1697) |
| POST | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/change-impact-previews` | `PreviewProviderChange` | [registration](../../src/Compliance.App/Program.cs#L1701) |
| POST | `/api/v1/tenants/{tenant_id}/technology-components` | `RecordTechnologyComponent` | [registration](../../src/Compliance.App/Program.cs#L1706) |
| PUT | `/api/v1/tenants/{tenant_id}/technology-components/{component_id}` | `ReviseTechnologyComponent` | [registration](../../src/Compliance.App/Program.cs#L1710) |
| GET | `/api/v1/tenants/{tenant_id}/technology-components/{component_id}/boundary-references` | `ListTechnologyComponentBoundaryReferences` | [registration](../../src/Compliance.App/Program.cs#L1714) |
| GET | `/api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}/boundary-references` | `ListInformationAssetBoundaryReferences` | [registration](../../src/Compliance.App/Program.cs#L1720) |
| GET | `/api/v1/tenants/{tenant_id}/technology-components/{component_id}` | `GetTechnologyComponent` | [registration](../../src/Compliance.App/Program.cs#L1726) |
| GET | `/api/v1/tenants/{tenant_id}/technology-components` | `ListTechnologyComponents` | [registration](../../src/Compliance.App/Program.cs#L1730) |
| GET | `/api/v1/tenants/{tenant_id}/technology-components/{component_id}/revisions` | `ListTechnologyComponentRevisions` | [registration](../../src/Compliance.App/Program.cs#L1734) |
| POST | `/api/v1/tenants/{tenant_id}/information-assets` | `RecordInformationAsset` | [registration](../../src/Compliance.App/Program.cs#L1738) |
| PUT | `/api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}` | `ReviseInformationAsset` | [registration](../../src/Compliance.App/Program.cs#L1742) |
| GET | `/api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}` | `GetInformationAsset` | [registration](../../src/Compliance.App/Program.cs#L1746) |
| GET | `/api/v1/tenants/{tenant_id}/information-assets` | `ListInformationAssets` | [registration](../../src/Compliance.App/Program.cs#L1750) |
| GET | `/api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}/revisions` | `ListInformationAssetRevisions` | [registration](../../src/Compliance.App/Program.cs#L1754) |
| POST | `/api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}/change-previews` | `PreviewInformationAssetChange` | [registration](../../src/Compliance.App/Program.cs#L1758) |
| POST | `/api/v1/tenants/{tenant_id}/locations` | `RecordLocation` | [registration](../../src/Compliance.App/Program.cs#L1763) |
| PUT | `/api/v1/tenants/{tenant_id}/locations/{location_id}` | `ReviseLocation` | [registration](../../src/Compliance.App/Program.cs#L1767) |
| GET | `/api/v1/tenants/{tenant_id}/locations/{location_id}` | `GetLocation` | [registration](../../src/Compliance.App/Program.cs#L1771) |
| GET | `/api/v1/tenants/{tenant_id}/locations` | `ListLocations` | [registration](../../src/Compliance.App/Program.cs#L1775) |
| GET | `/api/v1/tenants/{tenant_id}/locations/{location_id}/revisions` | `ListLocationRevisions` | [registration](../../src/Compliance.App/Program.cs#L1779) |
| POST | `/api/v1/tenants/{tenant_id}/operational-processes` | `RecordOperationalProcess` | [registration](../../src/Compliance.App/Program.cs#L1783) |
| PUT | `/api/v1/tenants/{tenant_id}/operational-processes/{operational_process_id}` | `ReviseOperationalProcess` | [registration](../../src/Compliance.App/Program.cs#L1787) |
| GET | `/api/v1/tenants/{tenant_id}/operational-processes/{operational_process_id}` | `GetOperationalProcess` | [registration](../../src/Compliance.App/Program.cs#L1791) |
| GET | `/api/v1/tenants/{tenant_id}/operational-processes` | `ListOperationalProcesses` | [registration](../../src/Compliance.App/Program.cs#L1795) |
| GET | `/api/v1/tenants/{tenant_id}/operational-processes/{operational_process_id}/revisions` | `ListOperationalProcessRevisions` | [registration](../../src/Compliance.App/Program.cs#L1799) |
| POST | `/api/v1/tenants/{tenant_id}/data-flows` | `RecordDataFlow` | [registration](../../src/Compliance.App/Program.cs#L1803) |
| PUT | `/api/v1/tenants/{tenant_id}/data-flows/{data_flow_id}` | `ReviseDataFlow` | [registration](../../src/Compliance.App/Program.cs#L1807) |
| GET | `/api/v1/tenants/{tenant_id}/data-flows/{data_flow_id}` | `GetDataFlow` | [registration](../../src/Compliance.App/Program.cs#L1811) |
| GET | `/api/v1/tenants/{tenant_id}/data-flows` | `ListDataFlows` | [registration](../../src/Compliance.App/Program.cs#L1815) |
| GET | `/api/v1/tenants/{tenant_id}/data-flows/{data_flow_id}/revisions` | `ListDataFlowRevisions` | [registration](../../src/Compliance.App/Program.cs#L1819) |
| PUT | `/api/v1/tenants/{tenant_id}/applications/{application_id}` | `DeclareApplication` | [registration](../../src/Compliance.App/Program.cs#L1823) |
| PUT | `/api/v1/tenants/{tenant_id}/applications/revise-application/{command_id}` | `ReviseApplication` | [registration](../../src/Compliance.App/Program.cs#L1827) |
| GET | `/api/v1/tenants/{tenant_id}/applications/{application_id}` | `GetApplication` | [registration](../../src/Compliance.App/Program.cs#L1836) |
| GET | `/api/v1/tenants/{tenant_id}/applications` | `ListApplications` | [registration](../../src/Compliance.App/Program.cs#L1840) |
| POST | `/api/v1/tenants/{tenant_id}/applications/{application_id}/relationships/{relationship_id}` | `AddApplicationRelationship` | [registration](../../src/Compliance.App/Program.cs#L1844) |
| DELETE | `/api/v1/tenants/{tenant_id}/applications/{application_id}/relationships/{relationship_id}` | `RemoveApplicationRelationship` | [registration](../../src/Compliance.App/Program.cs#L1852) |
| GET | `/api/v1/tenants/{tenant_id}/applications/{application_id}/relationships` | `ListApplicationRelationships` | [registration](../../src/Compliance.App/Program.cs#L1860) |
| GET | `/api/v1/tenants/{tenant_id}/applications/{application_id}/revisions/{revision}` | `GetApplicationRevision` | [registration](../../src/Compliance.App/Program.cs#L1875) |
| GET | `/api/v1/tenants/{tenant_id}/applications/{application_id}/revisions` | `ListApplicationRevisions` | [registration](../../src/Compliance.App/Program.cs#L1879) |
| POST | `/api/v1/tenants/{tenant_id}/applications/{source_application_id}/relationships` | `RecordApplicationRelationship` | [registration](../../src/Compliance.App/Program.cs#L1883) |
| DELETE | `/api/v1/tenants/{tenant_id}/applications/{source_application_id}/relationships/{relationship_type}/{target_application_id}` | `RemoveGovernedApplicationRelationship` | [registration](../../src/Compliance.App/Program.cs#L1888) |
| GET | `/api/v1/tenants/{tenant_id}/applications/{application_id}/relationships/directed` | `ListDirectedApplicationRelationships` | [registration](../../src/Compliance.App/Program.cs#L1893) |
| POST | `/api/v1/tenants/{tenant_id}/applications/{predecessor_application_id}/successors/{successor_application_id}/approval` | `ApproveApplicationSuccessor` | [registration](../../src/Compliance.App/Program.cs#L1898) |
| PUT | `/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}` | `DeclareSystemInstance` | [registration](../../src/Compliance.App/Program.cs#L1903) |
| GET | `/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}` | `GetSystemInstance` | [registration](../../src/Compliance.App/Program.cs#L1907) |
| GET | `/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances` | `ListSystemInstances` | [registration](../../src/Compliance.App/Program.cs#L1911) |
| GET | `/api/v1/tenants/{tenant_id}/applications/{application_id}/boundary-references` | `ListApplicationBoundaryReferences` | [registration](../../src/Compliance.App/Program.cs#L1915) |
| GET | `/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/boundary-references` | `ListSystemInstanceBoundaryReferences` | [registration](../../src/Compliance.App/Program.cs#L1921) |
| POST | `/api/v1/tenants/{tenant_id}/applications/{application_id}/change-previews` | `PreviewApplicationChange` | [registration](../../src/Compliance.App/Program.cs#L1927) |
| PUT | `/api/v1/tenants/{tenant_id}/applications/{application_id}/retirements/{command_id}` | `RetireApplication` | [registration](../../src/Compliance.App/Program.cs#L1937) |
| PUT | `/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/retirements/{command_id}` | `RetireSystemInstance` | [registration](../../src/Compliance.App/Program.cs#L1942) |
| POST | `/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-review-scope-decisions` | `DecideAccessReviewScope` | [registration](../../src/Compliance.App/Program.cs#L1947) |
| GET | `/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-review-scope` | `GetAccessReviewScope` | [registration](../../src/Compliance.App/Program.cs#L1952) |
| GET | `/api/v1/tenants/{tenant_id}/applications/{application_id}/access-review-scopes` | `ListAccessReviewScopes` | [registration](../../src/Compliance.App/Program.cs#L1957) |
| GET | `/api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}` | `GetArtifactRetention` | [registration](../../src/Compliance.App/Program.cs#L1962) |
| GET | `/api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}/legal-holds` | `ListArtifactLegalHolds` | [registration](../../src/Compliance.App/Program.cs#L1966) |
| GET | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/retention` | `GetApplicationImportRetention` | [registration](../../src/Compliance.App/Program.cs#L1970) |
| POST | `/api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}/basis` | `RecordArtifactRetentionBasis` | [registration](../../src/Compliance.App/Program.cs#L1974) |
| POST | `/api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}/legal-holds` | `PlaceArtifactLegalHold` | [registration](../../src/Compliance.App/Program.cs#L1978) |
| POST | `/api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}/legal-holds/{hold_id}/releases` | `ReleaseArtifactLegalHold` | [registration](../../src/Compliance.App/Program.cs#L1982) |
| PUT | `/api/v1/tenants/{tenant_id}/application-imports/{submission_id}` | `StageApplicationImport` | [registration](../../src/Compliance.App/Program.cs#L1986) |
| GET | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/progress` | `GetApplicationImportProgress` | [registration](../../src/Compliance.App/Program.cs#L1990) |
| GET | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/rejected-report` | `GetApplicationImportRejectedReport` | [registration](../../src/Compliance.App/Program.cs#L1995) |
| PUT | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/acceptances/{command_id}` | `AcceptApplicationImport` | [registration](../../src/Compliance.App/Program.cs#L2000) |
| PUT | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/cancellations/{command_id}` | `CancelApplicationImport` | [registration](../../src/Compliance.App/Program.cs#L2005) |
| PUT | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/rows/{row_id}/correlations/{command_id}` | `CorrelateApplicationImportRow` | [registration](../../src/Compliance.App/Program.cs#L2009) |
| GET | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}` | `GetApplicationImport` | [registration](../../src/Compliance.App/Program.cs#L2014) |
| GET | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/rows` | `ListApplicationImportRows` | [registration](../../src/Compliance.App/Program.cs#L2018) |
| GET | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/preview` | `PreviewApplicationImport` | [registration](../../src/Compliance.App/Program.cs#L2022) |
| GET | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/preview/missing` | `PreviewMissingApplicationImportRows` | [registration](../../src/Compliance.App/Program.cs#L2026) |
| GET | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/omission-proposal/preview` | `PreviewApplicationImportOmissionProposal` | [registration](../../src/Compliance.App/Program.cs#L2031) |
| PUT | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/omission-proposal/{command_id}` | `FreezeApplicationImportOmissionProposal` | [registration](../../src/Compliance.App/Program.cs#L2035) |
| GET | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/omission-proposal` | `GetApplicationImportOmissionProposal` | [registration](../../src/Compliance.App/Program.cs#L2040) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/client-services` | `CreateClientService` | [registration](../../src/Compliance.App/Program.cs#L2044) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/client-services` | `ListProgramClientServices` | [registration](../../src/Compliance.App/Program.cs#L2048) |
| PUT | `/api/v1/tenants/{tenant_id}/client-services/{service_id}` | `ReviseClientService` | [registration](../../src/Compliance.App/Program.cs#L2052) |
| POST | `/api/v1/tenants/{tenant_id}/client-services/{service_id}/retirements` | `RetireClientService` | [registration](../../src/Compliance.App/Program.cs#L2056) |
| GET | `/api/v1/tenants/{tenant_id}/client-services/{service_id}` | `GetClientService` | [registration](../../src/Compliance.App/Program.cs#L2060) |
| GET | `/api/v1/tenants/{tenant_id}/client-services` | `ListClientServices` | [registration](../../src/Compliance.App/Program.cs#L2064) |
| GET | `/api/v1/tenants/{tenant_id}/client-services/{service_id}/revisions` | `ListClientServiceRevisions` | [registration](../../src/Compliance.App/Program.cs#L2068) |
| GET | `/api/v1/tenants/{tenant_id}/client-services/{service_id}/revisions/{revision}` | `GetClientServiceRevision` | [registration](../../src/Compliance.App/Program.cs#L2072) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/boundaries` | `CreateBoundary` | [registration](../../src/Compliance.App/Program.cs#L2076) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/boundaries` | `ListProgramBoundaries` | [registration](../../src/Compliance.App/Program.cs#L2080) |
| PUT | `/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}` | `ReviseBoundaryDraft` | [registration](../../src/Compliance.App/Program.cs#L2084) |
| POST | `/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/discards` | `DiscardBoundaryDraft` | [registration](../../src/Compliance.App/Program.cs#L2088) |
| GET | `/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}` | `GetBoundary` | [registration](../../src/Compliance.App/Program.cs#L2092) |
| POST | `/api/v1/tenants/{tenant_id}/responsibilities` | `AssignResponsibility` | [registration](../../src/Compliance.App/Program.cs#L2096) |
| POST | `/api/v1/tenants/{tenant_id}/responsibilities/{assignment_id}/revocation` | `RevokeResponsibility` | [registration](../../src/Compliance.App/Program.cs#L2100) |
| GET | `/api/v1/tenants/{tenant_id}/responsibilities` | `ListResponsibilities` | [registration](../../src/Compliance.App/Program.cs#L2104) |
| GET | `/api/v1/tenants/{tenant_id}/responsibilities/conflict-preview` | `PreviewResponsibilityConflicts` | [registration](../../src/Compliance.App/Program.cs#L2108) |
| GET | `/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/versions/{version_id}` | `GetBoundaryVersion` | [registration](../../src/Compliance.App/Program.cs#L2113) |
| GET | `/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/versions` | `ListBoundaryVersions` | [registration](../../src/Compliance.App/Program.cs#L2117) |
| GET | `/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/effective-version` | `GetEffectiveBoundaryVersion` | [registration](../../src/Compliance.App/Program.cs#L2121) |
| GET | `/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/decisions/{decision_id}` | `GetBoundaryDecision` | [registration](../../src/Compliance.App/Program.cs#L2125) |
| GET | `/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/decisions` | `ListBoundaryDecisions` | [registration](../../src/Compliance.App/Program.cs#L2129) |
| GET | `/api/v1/tenants/{tenant_id}/separation-of-duties-waivers/{waiver_id}` | `GetSeparationOfDutiesWaiver` | [registration](../../src/Compliance.App/Program.cs#L2133) |
| POST | `/api/v1/tenants/{tenant_id}/scope-snapshots` | `FreezeProgramScopeSnapshot` | [registration](../../src/Compliance.App/Program.cs#L2137) |
| POST | `/api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}/amendments` | `AmendProgramScopeSnapshot` | [registration](../../src/Compliance.App/Program.cs#L2141) |
| GET | `/api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}` | `GetSnapshot` | [registration](../../src/Compliance.App/Program.cs#L2145) |
| GET | `/api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}/verification` | `VerifyProgramScopeSnapshot` | [registration](../../src/Compliance.App/Program.cs#L2149) |
| GET | `/api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}/manifest-regeneration` | `RegenerateProgramScopeSnapshotManifest` | [registration](../../src/Compliance.App/Program.cs#L2153) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/scope-snapshots` | `ListProgramSnapshots` | [registration](../../src/Compliance.App/Program.cs#L2158) |
| POST | `/api/v1/tenants/{tenant_id}/workforce-roster-snapshots` | `FreezeWorkforceRosterSnapshot` | [registration](../../src/Compliance.App/Program.cs#L2162) |
| POST | `/api/v1/tenants/{tenant_id}/workforce-roster-snapshots/{snapshot_id}/amendments` | `AmendWorkforceRosterSnapshot` | [registration](../../src/Compliance.App/Program.cs#L2166) |
| GET | `/api/v1/tenants/{tenant_id}/workforce-roster-snapshots/{snapshot_id}` | `GetWorkforceRosterSnapshot` | [registration](../../src/Compliance.App/Program.cs#L2170) |
| GET | `/api/v1/tenants/{tenant_id}/workforce-roster-snapshots` | `ListWorkforceRosterSnapshots` | [registration](../../src/Compliance.App/Program.cs#L2174) |
| GET | `/api/v1/tenants/{tenant_id}/workforce-roster-snapshots/{snapshot_id}/manifest-regeneration` | `RegenerateWorkforceRosterSnapshotManifest` | [registration](../../src/Compliance.App/Program.cs#L2178) |
| GET | `/api/v1/tenants/{tenant_id}/workforce-roster-snapshot-as-of` | `GetWorkforceRosterSnapshotAsOf` | [registration](../../src/Compliance.App/Program.cs#L2182) |
| GET | `/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/impact-preview` | `PreviewBoundaryImpact` | [registration](../../src/Compliance.App/Program.cs#L2186) |
| POST | `/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/reviews` | `ReviewBoundary` | [registration](../../src/Compliance.App/Program.cs#L2190) |
| POST | `/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/approvals` | `ApproveBoundary` | [registration](../../src/Compliance.App/Program.cs#L2194) |
| POST | `/api/v1/tenants/{tenant_id}/separation-of-duties-waivers` | `RecordSeparationOfDutiesWaiver` | [registration](../../src/Compliance.App/Program.cs#L2198) |
| POST | `/api/v1/tenants/{tenant_id}/separation-of-duties-waivers/{waiver_id}/approvals` | `ApproveSeparationOfDutiesWaiver` | [registration](../../src/Compliance.App/Program.cs#L2202) |
| POST | `/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/successors` | `ProposeBoundarySuccessor` | [registration](../../src/Compliance.App/Program.cs#L2206) |
| POST | `/api/v1/users/{user_id}/email-addresses/{email_address}` | `ReserveEmail` | [registration](../../src/Compliance.App/Program.cs#L2210) |
| POST | `/api/v1/users/{user_id}/email-addresses/{email_address}/challenges` | `IssueEmailChallenge` | [registration](../../src/Compliance.App/Program.cs#L2213) |
| GET | `/api/v1/users/{user_id}/email-addresses/{email_address}/challenges/status` | `GetEmailChallengeStatus` | [registration](../../src/Compliance.App/Program.cs#L2216) |
| POST | `/api/v1/users/{user_id}/email-addresses/{email_address}/verifications` | `CompleteEmailChallenge` | [registration](../../src/Compliance.App/Program.cs#L2220) |
| GET | `/api/v1/users/{user_id}/email-addresses/{email_address}` | `GetEmailAddress` | [registration](../../src/Compliance.App/Program.cs#L2223) |
| GET | `/api/v1/users/{user_id}/email-addresses` | `ListEmailAddresses` | [registration](../../src/Compliance.App/Program.cs#L2226) |
| GET | `/api/v1/tenants/mine` | `ListMyTenants` | [registration](../../src/Compliance.App/Program.cs#L2229) |
| POST | `/api/v1/tenants/{tenant_id}/identity-providers/{provider_connection_id}/disable` | `DisableTenantIdentityProvider` | [registration](../../src/Compliance.App/Program.cs#L2232) |
| POST | `/api/v1/tenants/{tenant_id}/teams/{team_id}` | `DefineTeam` | [registration](../../src/Compliance.App/Program.cs#L2236) |
| DELETE | `/api/v1/tenants/{tenant_id}/teams/{team_id}` | `DeleteTeam` | [registration](../../src/Compliance.App/Program.cs#L2239) |
| GET | `/api/v1/tenants/{tenant_id}/teams/{team_id}` | `GetTeam` | [registration](../../src/Compliance.App/Program.cs#L2242) |
| GET | `/api/v1/tenants/{tenant_id}/teams` | `ListTeams` | [registration](../../src/Compliance.App/Program.cs#L2245) |
| POST | `/api/v1/tenants/{tenant_id}/teams/{team_id}/members/{member_id}` | `AssignTeamMember` | [registration](../../src/Compliance.App/Program.cs#L2248) |
| DELETE | `/api/v1/tenants/{tenant_id}/teams/{team_id}/members/{member_id}` | `RemoveTeamMember` | [registration](../../src/Compliance.App/Program.cs#L2251) |
| GET | `/api/v1/tenants/{tenant_id}/teams/{team_id}/members` | `ListTeamMembers` | [registration](../../src/Compliance.App/Program.cs#L2254) |
| POST | `/api/v1/tenants/{tenant_id}/teams/{team_id}/roles/{role_id}` | `AssignTeamRole` | [registration](../../src/Compliance.App/Program.cs#L2257) |
| DELETE | `/api/v1/tenants/{tenant_id}/teams/{team_id}/roles/{role_id}` | `RemoveTeamRole` | [registration](../../src/Compliance.App/Program.cs#L2260) |
| POST | `/api/v1/tenants/{tenant_id}/roles/{role_id}` | `DefineRole` | [registration](../../src/Compliance.App/Program.cs#L2263) |
| DELETE | `/api/v1/tenants/{tenant_id}/roles/{role_id}` | `DeleteRole` | [registration](../../src/Compliance.App/Program.cs#L2266) |
| GET | `/api/v1/tenants/{tenant_id}/roles/{role_id}` | `GetRole` | [registration](../../src/Compliance.App/Program.cs#L2269) |
| GET | `/api/v1/tenants/{tenant_id}/roles` | `ListRoles` | [registration](../../src/Compliance.App/Program.cs#L2272) |
| POST | `/api/v1/tenants/{tenant_id}/roles/{role_id}/permissions/{permission}` | `AssignRolePermission` | [registration](../../src/Compliance.App/Program.cs#L2275) |
| DELETE | `/api/v1/tenants/{tenant_id}/roles/{role_id}/permissions/{permission}` | `RemoveRolePermission` | [registration](../../src/Compliance.App/Program.cs#L2278) |
| GET | `/api/v1/tenants/{tenant_id}/roles/{role_id}/permissions` | `ListRolePermissions` | [registration](../../src/Compliance.App/Program.cs#L2281) |
| GET | `/api/v1/tenants/{tenant_id}/roles/{role_id}/teams` | `ListRoleTeams` | [registration](../../src/Compliance.App/Program.cs#L2285) |
| GET | `/api/v1/tenants/{tenant_id}/teams/{team_id}/roles` | `ListTeamRoles` | [registration](../../src/Compliance.App/Program.cs#L2288) |
| POST | `/api/v1/tenants/{tenant_id}/access-grants/{grant_id}` | `GrantAccess` | [registration](../../src/Compliance.App/Program.cs#L2291) |
| DELETE | `/api/v1/tenants/{tenant_id}/access-grants/{grant_id}` | `RevokeAccessGrant` | [registration](../../src/Compliance.App/Program.cs#L2294) |
| GET | `/api/v1/tenants/{tenant_id}/access-grants` | `ListAccessGrants` | [registration](../../src/Compliance.App/Program.cs#L2297) |
| POST | `/api/v1/tenants/{tenant_id}/identity-providers/{provider_connection_id}` | `ConfigureTenantIdentityProvider` | [registration](../../src/Compliance.App/Program.cs#L2300) |
| POST | `/api/v1/tenants/{tenant_id}/identity-providers/{provider_connection_id}/tests` | `TestTenantIdentityProvider` | [registration](../../src/Compliance.App/Program.cs#L2304) |
| POST | `/api/v1/tenants/{tenant_id}/identity-providers/{provider_connection_id}/credential-rotations` | `RotateTenantIdentityProviderCredentials` | [registration](../../src/Compliance.App/Program.cs#L2308) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}` | `CreateAuditPackage` | [registration](../../src/Compliance.App/Program.cs#L2312) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/amendments` | `AmendAuditPackage` | [registration](../../src/Compliance.App/Program.cs#L2316) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/validations` | `ValidateAuditPackage` | [registration](../../src/Compliance.App/Program.cs#L2320) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}` | `CreateExaminationEngagement` | [registration](../../src/Compliance.App/Program.cs#L2324) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/revisions` | `ReviseExaminationEngagement` | [registration](../../src/Compliance.App/Program.cs#L2328) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}` | `GetExaminationEngagement` | [registration](../../src/Compliance.App/Program.cs#L2332) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/type-i-baselines/{type_i_baseline_id}` | `GetTypeIBaseline` | [registration](../../src/Compliance.App/Program.cs#L2336) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/type-i-baseline-preview` | `PreviewTypeIBaseline` | [registration](../../src/Compliance.App/Program.cs#L2340) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/type-i-baselines/{baseline_id}/freeze` | `FreezeTypeIBaseline` | [registration](../../src/Compliance.App/Program.cs#L2344) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/type-i-baselines/{baseline_id}/amendments` | `AmendTypeIBaseline` | [registration](../../src/Compliance.App/Program.cs#L2348) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/assertions/{assertion_id}` | `RecordManagementAssertion` | [registration](../../src/Compliance.App/Program.cs#L2352) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/representation-letters/{letter_id}` | `RecordManagementRepresentationLetter` | [registration](../../src/Compliance.App/Program.cs#L2356) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/outcomes/{outcome_id}` | `RecordExaminationOutcome` | [registration](../../src/Compliance.App/Program.cs#L2360) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/management-reviews/{sign_off_id}` | `RecordManagementExaminationReview` | [registration](../../src/Compliance.App/Program.cs#L2364) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/type-ii-plans/{plan_id}/proposals` | `ProposeTypeIIOperatingPlan` | [registration](../../src/Compliance.App/Program.cs#L2368) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/type-ii-periods/{period_id}` | `StartTypeIIObservationPeriod` | [registration](../../src/Compliance.App/Program.cs#L2372) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/type-ii-periods/{period_id}/amendments/{command_id}` | `AmendTypeIIObservationPeriod` | [registration](../../src/Compliance.App/Program.cs#L2376) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/type-ii-periods/{period_id}/amendments/{amendment_id}/cancellations/{command_id}` | `CancelTypeIIPeriodAmendment` | [registration](../../src/Compliance.App/Program.cs#L2376) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/observation-periods/{period_id}/close-approvals` | `ApproveTypeIIPeriodClose` | [registration](../../src/Compliance.App/Program.cs#L2380) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/observation-periods/{period_id}/close-snapshots/{close_snapshot_id}/freezes` | `FreezeTypeIIPeriodClose` | [registration](../../src/Compliance.App/Program.cs#L2384) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/rollovers/{rollover_id}/proposals` | `ProposeProgramRollover` | [registration](../../src/Compliance.App/Program.cs#L2388) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/rollovers/{rollover_id}/decisions` | `ApproveProgramRollover` | [registration](../../src/Compliance.App/Program.cs#L2392) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs` | `CreateAdvisorHandoff` | [registration](../../src/Compliance.App/Program.cs#L2396) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/selection-revisions` | `ReviseAdvisorHandoffSelection` | [registration](../../src/Compliance.App/Program.cs#L2400) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/ready-for-review` | `MarkAdvisorHandoffReadyForReview` | [registration](../../src/Compliance.App/Program.cs#L2404) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/closures` | `CloseAdvisorHandoff` | [registration](../../src/Compliance.App/Program.cs#L2408) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/revocations` | `RevokeAdvisorHandoff` | [registration](../../src/Compliance.App/Program.cs#L2412) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/feedback/{feedback_id}/responses` | `RespondToAdvisorFeedback` | [registration](../../src/Compliance.App/Program.cs#L2416) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/feedback/{feedback_id}/resolutions` | `ResolveAdvisorFeedback` | [registration](../../src/Compliance.App/Program.cs#L2420) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/feedback` | `RecordAdvisorFeedback` | [registration](../../src/Compliance.App/Program.cs#L2424) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/feedback/{feedback_id}/corrections` | `CorrectAdvisorFeedback` | [registration](../../src/Compliance.App/Program.cs#L2428) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{advisor_request_id}` | `GetAdvisorRequest` | [registration](../../src/Compliance.App/Program.cs#L2432) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs` | `ListAdvisorRequests` | [registration](../../src/Compliance.App/Program.cs#L2436) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/sources/{source_family}/{record_id}` | `GetAdvisorSharedSource` | [registration](../../src/Compliance.App/Program.cs#L2440) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/preview` | `PreviewAdvisorHandoff` | [registration](../../src/Compliance.App/Program.cs#L2444) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/feedback` | `ListAdvisorFeedback` | [registration](../../src/Compliance.App/Program.cs#L2448) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/significant-changes/{significant_change_id}` | `GetSignificantChange` | [registration](../../src/Compliance.App/Program.cs#L2452) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/significant-changes` | `ListSignificantChanges` | [registration](../../src/Compliance.App/Program.cs#L2456) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/incidents/{incident_id}/impact-assessment` | `GetComplianceImpactAssessment` | [registration](../../src/Compliance.App/Program.cs#L2460) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/incidents/{incident_id}` | `GetComplianceIncident` | [registration](../../src/Compliance.App/Program.cs#L2464) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/incidents` | `ListComplianceIncidents` | [registration](../../src/Compliance.App/Program.cs#L2468) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/significant-changes/{change_id}` | `RecordSignificantChange` | [registration](../../src/Compliance.App/Program.cs#L2472) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/significant-changes/{change_id}/revisions` | `ReviseSignificantChange` | [registration](../../src/Compliance.App/Program.cs#L2476) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/significant-changes/{change_id}/assessments` | `AssessSignificantChange` | [registration](../../src/Compliance.App/Program.cs#L2480) |

### Additional transport and MCP registrations

These source lines identify raw routes, health routes and MCP configuration. Follow their surrounding code for prefixes, handlers, allowlists and authentication. ICallable markers and discriminators in the contract inventory alone do not prove a tool is exposed.

- [Program.cs:45](../../src/Compliance.App/Program.cs#L45): `.AddMcpTool<DefineTeam>(tool => tool.Idempotent())`
- [Program.cs:46](../../src/Compliance.App/Program.cs#L46): `.AddMcpTool<DeleteTeam>(tool => tool.Destructive())`
- [Program.cs:47](../../src/Compliance.App/Program.cs#L47): `.AddMcpTool<GetTeam>(tool => tool.ReadOnly())`
- [Program.cs:48](../../src/Compliance.App/Program.cs#L48): `.AddMcpTool<ListTeams>(tool => tool.ReadOnly())`
- [Program.cs:49](../../src/Compliance.App/Program.cs#L49): `.AddMcpTool<AssignTeamMember>(tool => tool.Idempotent())`
- [Program.cs:50](../../src/Compliance.App/Program.cs#L50): `.AddMcpTool<RemoveTeamMember>(tool => tool.Destructive())`
- [Program.cs:51](../../src/Compliance.App/Program.cs#L51): `.AddMcpTool<SuspendMember>(tool => tool.Destructive())`
- [Program.cs:52](../../src/Compliance.App/Program.cs#L52): `.AddMcpTool<ReinstateMember>(tool => tool.Idempotent())`
- [Program.cs:53](../../src/Compliance.App/Program.cs#L53): `.AddMcpTool<GetTenantMember>(tool => tool.ReadOnly())`
- [Program.cs:54](../../src/Compliance.App/Program.cs#L54): `.AddMcpTool<ListMemberResponsibilities>(tool => tool.ReadOnly())`
- [Program.cs:55](../../src/Compliance.App/Program.cs#L55): `.AddMcpTool<ListTeamMembers>(tool => tool.ReadOnly())`
- [Program.cs:56](../../src/Compliance.App/Program.cs#L56): `.AddMcpTool<DefineRole>(tool => tool.Idempotent())`
- [Program.cs:57](../../src/Compliance.App/Program.cs#L57): `.AddMcpTool<DeleteRole>(tool => tool.Destructive())`
- [Program.cs:58](../../src/Compliance.App/Program.cs#L58): `.AddMcpTool<GetRole>(tool => tool.ReadOnly())`
- [Program.cs:59](../../src/Compliance.App/Program.cs#L59): `.AddMcpTool<ListRoles>(tool => tool.ReadOnly())`
- [Program.cs:60](../../src/Compliance.App/Program.cs#L60): `.AddMcpTool<AssignRolePermission>(tool => tool.Idempotent())`
- [Program.cs:61](../../src/Compliance.App/Program.cs#L61): `.AddMcpTool<RemoveRolePermission>(tool => tool.Destructive())`
- [Program.cs:62](../../src/Compliance.App/Program.cs#L62): `.AddMcpTool<ListRolePermissions>(tool => tool.ReadOnly())`
- [Program.cs:63](../../src/Compliance.App/Program.cs#L63): `.AddMcpTool<AssignTeamRole>(tool => tool.Idempotent())`
- [Program.cs:64](../../src/Compliance.App/Program.cs#L64): `.AddMcpTool<RemoveTeamRole>(tool => tool.Destructive())`
- [Program.cs:65](../../src/Compliance.App/Program.cs#L65): `.AddMcpTool<ListRoleTeams>(tool => tool.ReadOnly())`
- [Program.cs:66](../../src/Compliance.App/Program.cs#L66): `.AddMcpTool<ListTeamRoles>(tool => tool.ReadOnly())`
- [Program.cs:67](../../src/Compliance.App/Program.cs#L67): `.AddMcpTool<GrantAccess>(tool => tool.Idempotent())`
- [Program.cs:68](../../src/Compliance.App/Program.cs#L68): `.AddMcpTool<RevokeAccessGrant>(tool => tool.Destructive())`
- [Program.cs:69](../../src/Compliance.App/Program.cs#L69): `.AddMcpTool<ListAccessGrants>(tool => tool.ReadOnly())`
- [Program.cs:70](../../src/Compliance.App/Program.cs#L70): `.AddMcpTool<ListMyTenants>(tool => tool.ReadOnly())`
- [Program.cs:71](../../src/Compliance.App/Program.cs#L71): `.AddMcpTool<RegisterTenant>()`
- [Program.cs:72](../../src/Compliance.App/Program.cs#L72): `.AddMcpTool<SuspendTenant>(tool => tool.Destructive())`
- [Program.cs:73](../../src/Compliance.App/Program.cs#L73): `.AddMcpTool<ReactivateTenant>(tool => tool.Idempotent())`
- [Program.cs:74](../../src/Compliance.App/Program.cs#L74): `.AddMcpTool<OffboardTenant>(tool => tool.Destructive())`
- [Program.cs:75](../../src/Compliance.App/Program.cs#L75): `.AddMcpTool<GrantPlatformOperator>()`
- [Program.cs:76](../../src/Compliance.App/Program.cs#L76): `.AddMcpTool<RevokePlatformOperator>(tool => tool.Destructive())`
- [Program.cs:77](../../src/Compliance.App/Program.cs#L77): `.AddMcpTool<ListPlatformOperators>(tool => tool.ReadOnly())`
- [Program.cs:78](../../src/Compliance.App/Program.cs#L78): `.AddMcpTool<InviteTenantMember>()`
- [Program.cs:79](../../src/Compliance.App/Program.cs#L79): `.AddMcpTool<InviteOrganizationMember>()`
- [Program.cs:80](../../src/Compliance.App/Program.cs#L80): `.AddMcpTool<ListTenantInvitations>(tool => tool.ReadOnly())`
- [Program.cs:81](../../src/Compliance.App/Program.cs#L81): `.AddMcpTool<GetMemberAccess>(tool => tool.ReadOnly())`
- [Program.cs:82](../../src/Compliance.App/Program.cs#L82): `.AddMcpTool<GetTenant>(tool => tool.ReadOnly())`
- [Program.cs:83](../../src/Compliance.App/Program.cs#L83): `.AddMcpTool<GetTenantProvisioningStatus>(tool => tool.ReadOnly())`
- [Program.cs:84](../../src/Compliance.App/Program.cs#L84): `.AddMcpTool<ListTenants>(tool => tool.ReadOnly())`
- [Program.cs:85](../../src/Compliance.App/Program.cs#L85): `.AddMcpTool<ListTenantMembers>(tool => tool.ReadOnly())`
- [Program.cs:86](../../src/Compliance.App/Program.cs#L86): `.AddMcpTool<ChangeTenantSlug>()`
- [Program.cs:87](../../src/Compliance.App/Program.cs#L87): `.AddMcpTool<ResolveMyTenantSlug>(tool => tool.ReadOnly())`
- [Program.cs:88](../../src/Compliance.App/Program.cs#L88): `.AddMcpTool<CreateProgram>()`
- [Program.cs:89](../../src/Compliance.App/Program.cs#L89): `.AddMcpTool<ListCriteriaCatalogEditions>(tool => tool.ReadOnly())`
- [Program.cs:90](../../src/Compliance.App/Program.cs#L90): `.AddMcpTool<GetCriteriaCatalogEdition>(tool => tool.ReadOnly())`
- [Program.cs:91](../../src/Compliance.App/Program.cs#L91): `.AddMcpTool<ListCriteriaCatalogEntries>(tool => tool.ReadOnly())`
- [Program.cs:92](../../src/Compliance.App/Program.cs#L92): `.AddMcpTool<GetCriteriaCatalogEntry>(tool => tool.ReadOnly())`
- [Program.cs:93](../../src/Compliance.App/Program.cs#L93): `.AddMcpTool<ExportCriteriaCatalogEntries>(tool => tool.ReadOnly())`
- [Program.cs:97](../../src/Compliance.App/Program.cs#L97): `.AddMcpTool<RegisterFirmStaff>(tool => tool.Idempotent())`
- [Program.cs:98](../../src/Compliance.App/Program.cs#L98): `.AddMcpTool<SetFirmStaffStatus>(tool => tool.Idempotent())`
- [Program.cs:99](../../src/Compliance.App/Program.cs#L99): `.AddMcpTool<GetFirmStaffDirectory>(tool => tool.ReadOnly())`
- [Program.cs:100](../../src/Compliance.App/Program.cs#L100): `.AddMcpTool<ListAssignableFirmStaff>(tool => tool.ReadOnly())`
- [Program.cs:101](../../src/Compliance.App/Program.cs#L101): `.AddMcpTool<GetServiceEngagementAcceptance>(tool => tool.ReadOnly())`
- [Program.cs:102](../../src/Compliance.App/Program.cs#L102): `.AddMcpTool<GetServiceEngagementAcceptanceHistory>(tool => tool.ReadOnly())`
- [Program.cs:103](../../src/Compliance.App/Program.cs#L103): `.AddMcpTool<GetEngagementManagementAcknowledgements>(tool => tool.ReadOnly())`
- [Program.cs:104](../../src/Compliance.App/Program.cs#L104): `.AddMcpTool<CreateServiceEngagement>(tool => tool.Idempotent())`
- [Program.cs:105](../../src/Compliance.App/Program.cs#L105): `.AddMcpTool<AmendServiceEngagement>(tool => tool.Idempotent())`
- [Program.cs:106](../../src/Compliance.App/Program.cs#L106): `.AddMcpTool<ProposeServiceEngagementStaff>(tool => tool.Idempotent())`
- [Program.cs:107](../../src/Compliance.App/Program.cs#L107): `.AddMcpTool<WithdrawServiceEngagementStaffProposal>(tool => tool.Idempotent())`
- [Program.cs:108](../../src/Compliance.App/Program.cs#L108): `.AddMcpTool<RevokeServiceEngagementActualStaff>(tool => tool.Idempotent())`
- [Program.cs:109](../../src/Compliance.App/Program.cs#L109): `.AddMcpTool<CloseServiceEngagement>(tool => tool.Idempotent())`
- [Program.cs:110](../../src/Compliance.App/Program.cs#L110): `.AddMcpTool<GetServiceEngagement>(tool => tool.ReadOnly())`
- [Program.cs:111](../../src/Compliance.App/Program.cs#L111): `.AddMcpTool<ListServiceEngagements>(tool => tool.ReadOnly())`
- [Program.cs:112](../../src/Compliance.App/Program.cs#L112): `.AddMcpTool<GetServiceEngagementHistory>(tool => tool.ReadOnly())`
- [Program.cs:113](../../src/Compliance.App/Program.cs#L113): `.AddMcpTool<GetExaminationEngagement>(tool => tool.ReadOnly())`
- [Program.cs:114](../../src/Compliance.App/Program.cs#L114): `.AddMcpTool<GetTypeIBaseline>(tool => tool.ReadOnly())`
- [Program.cs:115](../../src/Compliance.App/Program.cs#L115): `.AddMcpTool<PreviewTypeIBaseline>(tool => tool.ReadOnly())`
- [Program.cs:116](../../src/Compliance.App/Program.cs#L116): `.AddMcpTool<CreateExaminationEngagement>(tool => tool.Idempotent())`
- [Program.cs:117](../../src/Compliance.App/Program.cs#L117): `.AddMcpTool<ReviseExaminationEngagement>(tool => tool.Idempotent())`
- [Program.cs:118](../../src/Compliance.App/Program.cs#L118): `.AddMcpTool<FreezeTypeIBaseline>(tool => tool.Idempotent())`
- [Program.cs:119](../../src/Compliance.App/Program.cs#L119): `.AddMcpTool<AmendTypeIBaseline>(tool => tool.Idempotent())`
- [Program.cs:120](../../src/Compliance.App/Program.cs#L120): `.AddMcpTool<GetClientIndependenceRules>(tool => tool.ReadOnly())`
- [Program.cs:121](../../src/Compliance.App/Program.cs#L121): `.AddMcpTool<GetIndependenceRuleVersions>(tool => tool.ReadOnly())`
- [Program.cs:122](../../src/Compliance.App/Program.cs#L122): `.AddMcpTool<ReviseIndependenceRules>(tool => tool.Idempotent())`
- [Program.cs:123](../../src/Compliance.App/Program.cs#L123): `.AddMcpTool<RecordNonattestService>(tool => tool.Idempotent())`
- [Program.cs:124](../../src/Compliance.App/Program.cs#L124): `.AddMcpTool<EvaluateClientIndependence>(tool => tool.Idempotent())`
- [Program.cs:125](../../src/Compliance.App/Program.cs#L125): `.AddMcpTool<GetClientIndependenceHistory>(tool => tool.ReadOnly())`
- [Program.cs:126](../../src/Compliance.App/Program.cs#L126): `.AddMcpTool<SetCriteriaTextOverlay>(tool => tool.Idempotent())
- [Program.cs:116](../../src/Compliance.App/Program.cs#L116): `.AddMcpTool<SelectProgramCriteriaEdition>(tool => tool.Idempotent())`
- [Program.cs:117](../../src/Compliance.App/Program.cs#L117): `.AddMcpTool<CreateControlDraft>()`
- [Program.cs:118](../../src/Compliance.App/Program.cs#L118): `.AddMcpTool<ReviseControlDraft>(tool => tool.Idempotent())`
- [Program.cs:119](../../src/Compliance.App/Program.cs#L119): `.AddMcpTool<DiscardControlDraft>(tool => tool.Destructive())`
- [Program.cs:120](../../src/Compliance.App/Program.cs#L120): `.AddMcpTool<GetControlDraft>(tool => tool.ReadOnly())`
- [Program.cs:121](../../src/Compliance.App/Program.cs#L121): `.AddMcpTool<ListControlDrafts>(tool => tool.ReadOnly())`
- [Program.cs:122](../../src/Compliance.App/Program.cs#L122): `.AddMcpTool<ListControlDraftRevisions>(tool => tool.ReadOnly())`
- [Program.cs:123](../../src/Compliance.App/Program.cs#L123): `.AddMcpTool<GetControlDraftRevision>(tool => tool.ReadOnly())`
- [Program.cs:124](../../src/Compliance.App/Program.cs#L124): `.AddMcpTool<GetControlVersion>(tool => tool.ReadOnly())`
- [Program.cs:125](../../src/Compliance.App/Program.cs#L125): `.AddMcpTool<GetCurrentControlVersion>(tool => tool.ReadOnly())`
- [Program.cs:126](../../src/Compliance.App/Program.cs#L126): `.AddMcpTool<GetEffectiveControlVersion>(tool => tool.ReadOnly())`
- [Program.cs:127](../../src/Compliance.App/Program.cs#L127): `.AddMcpTool<ListControlVersions>(tool => tool.ReadOnly())`
- [Program.cs:128](../../src/Compliance.App/Program.cs#L128): `.AddMcpTool<GetControlDecision>(tool => tool.ReadOnly())`
- [Program.cs:129](../../src/Compliance.App/Program.cs#L129): `.AddMcpTool<ListControlDecisions>(tool => tool.ReadOnly())`
- [Program.cs:130](../../src/Compliance.App/Program.cs#L130): `.AddMcpTool<GetControlCriterionMapping>(tool => tool.ReadOnly())`
- [Program.cs:131](../../src/Compliance.App/Program.cs#L131): `.AddMcpTool<ListControlCriterionMappings>(tool => tool.ReadOnly())`
- [Program.cs:132](../../src/Compliance.App/Program.cs#L132): `.AddMcpTool<ListCriteriaCoverage>(tool => tool.ReadOnly())`
- [Program.cs:133](../../src/Compliance.App/Program.cs#L133): `.AddMcpTool<ProposeControlSuccessor>()`
- [Program.cs:134](../../src/Compliance.App/Program.cs#L134): `.AddMcpTool<ProposeControlRetirement>()`
- [Program.cs:135](../../src/Compliance.App/Program.cs#L135): `.AddMcpTool<PreviewControlImpact>(tool => tool.ReadOnly())`
- [Program.cs:136](../../src/Compliance.App/Program.cs#L136): `.AddMcpTool<GetCriterionApplicability>(tool => tool.ReadOnly())`
- [Program.cs:137](../../src/Compliance.App/Program.cs#L137): `.AddMcpTool<ListCriterionApplicability>(tool => tool.ReadOnly())`
- [Program.cs:138](../../src/Compliance.App/Program.cs#L138): `.AddMcpTool<CreateCommitmentDraft>()`
- [Program.cs:139](../../src/Compliance.App/Program.cs#L139): `.AddMcpTool<ReviseCommitmentDraft>()`
- [Program.cs:140](../../src/Compliance.App/Program.cs#L140): `.AddMcpTool<GetCommitmentDraft>(tool => tool.ReadOnly())`
- [Program.cs:141](../../src/Compliance.App/Program.cs#L141): `.AddMcpTool<ListCommitmentDrafts>(tool => tool.ReadOnly())`
- [Program.cs:142](../../src/Compliance.App/Program.cs#L142): `.AddMcpTool<ListCommitmentDraftRevisions>(tool => tool.ReadOnly())`
- [Program.cs:143](../../src/Compliance.App/Program.cs#L143): `.AddMcpTool<GetCommitmentDraftRevision>(tool => tool.ReadOnly())`
- [Program.cs:144](../../src/Compliance.App/Program.cs#L144): `.AddMcpTool<PreviewCommitmentImpact>(tool => tool.ReadOnly())`
- [Program.cs:145](../../src/Compliance.App/Program.cs#L145): `.AddMcpTool<GetCommitmentVersion>(tool => tool.ReadOnly())`
- [Program.cs:146](../../src/Compliance.App/Program.cs#L146): `.AddMcpTool<GetEffectiveCommitmentVersion>(tool => tool.ReadOnly())`
- [Program.cs:147](../../src/Compliance.App/Program.cs#L147): `.AddMcpTool<ListCommitmentVersions>(tool => tool.ReadOnly())`
- [Program.cs:148](../../src/Compliance.App/Program.cs#L148): `.AddMcpTool<ListCommitmentDecisions>(tool => tool.ReadOnly())`
- [Program.cs:149](../../src/Compliance.App/Program.cs#L149): `.AddMcpTool<CreatePolicyDraft>()`
- [Program.cs:150](../../src/Compliance.App/Program.cs#L150): `.AddMcpTool<RevisePolicyDraft>()`
- [Program.cs:151](../../src/Compliance.App/Program.cs#L151): `.AddMcpTool<ProposePolicySuccessor>()`
- [Program.cs:152](../../src/Compliance.App/Program.cs#L152): `.AddMcpTool<DiscardPolicyDraft>(tool => tool.Destructive())`
- [Program.cs:153](../../src/Compliance.App/Program.cs#L153): `.AddMcpTool<ProposePolicyRetirement>()`
- [Program.cs:154](../../src/Compliance.App/Program.cs#L154): `.AddMcpTool<GetPolicy>(tool => tool.ReadOnly())`
- [Program.cs:155](../../src/Compliance.App/Program.cs#L155): `.AddMcpTool<ListPolicies>(tool => tool.ReadOnly())`
- [Program.cs:156](../../src/Compliance.App/Program.cs#L156): `.AddMcpTool<GetPolicyVersion>(tool => tool.ReadOnly())`
- [Program.cs:157](../../src/Compliance.App/Program.cs#L157): `.AddMcpTool<GetEffectivePolicyVersion>(tool => tool.ReadOnly())`
- [Program.cs:158](../../src/Compliance.App/Program.cs#L158): `.AddMcpTool<ListPolicyVersions>(tool => tool.ReadOnly())`
- [Program.cs:159](../../src/Compliance.App/Program.cs#L159): `.AddMcpTool<ListPolicyDecisions>(tool => tool.ReadOnly())`
- [Program.cs:160](../../src/Compliance.App/Program.cs#L160): `.AddMcpTool<PreviewPolicyImpact>(tool => tool.ReadOnly())`
- [Program.cs:161](../../src/Compliance.App/Program.cs#L161): `.AddMcpTool<DefineTrainingRequirement>()`
- [Program.cs:162](../../src/Compliance.App/Program.cs#L162): `.AddMcpTool<ReviseTrainingRequirement>()`
- [Program.cs:163](../../src/Compliance.App/Program.cs#L163): `.AddMcpTool<GetTrainingRequirement>(tool => tool.ReadOnly())`
- [Program.cs:164](../../src/Compliance.App/Program.cs#L164): `.AddMcpTool<ListTrainingRequirements>(tool => tool.ReadOnly())`
- [Program.cs:165](../../src/Compliance.App/Program.cs#L165): `.AddMcpTool<LaunchPolicyCampaign>()`
- [Program.cs:166](../../src/Compliance.App/Program.cs#L166): `.AddMcpTool<LaunchTrainingCampaign>()`
- [Program.cs:167](../../src/Compliance.App/Program.cs#L167): `.AddMcpTool<ReconcileCampaignAudience>()`
- [Program.cs:168](../../src/Compliance.App/Program.cs#L168): `.AddMcpTool<RecordTrainingCompletion>()`
- [Program.cs:169](../../src/Compliance.App/Program.cs#L169): `.AddMcpTool<CloseCampaign>()`
- [Program.cs:170](../../src/Compliance.App/Program.cs#L170): `.AddMcpTool<GetCampaign>(tool => tool.ReadOnly())`
- [Program.cs:171](../../src/Compliance.App/Program.cs#L171): `.AddMcpTool<ListCampaigns>(tool => tool.ReadOnly())`
- [Program.cs:172](../../src/Compliance.App/Program.cs#L172): `.AddMcpTool<ListCampaignParticipants>(tool => tool.ReadOnly())`
- [Program.cs:173](../../src/Compliance.App/Program.cs#L173): `.AddMcpTool<ListCampaignAmendments>(tool => tool.ReadOnly())`
- [Program.cs:174](../../src/Compliance.App/Program.cs#L174): `.AddMcpTool<CreateRiskDraft>()`
- [Program.cs:175](../../src/Compliance.App/Program.cs#L175): `.AddMcpTool<ReviseRiskDraft>(tool => tool.Idempotent())`
- [Program.cs:176](../../src/Compliance.App/Program.cs#L176): `.AddMcpTool<GetRiskDraft>(tool => tool.ReadOnly())`
- [Program.cs:177](../../src/Compliance.App/Program.cs#L177): `.AddMcpTool<ListRiskDrafts>(tool => tool.ReadOnly())`
- [Program.cs:178](../../src/Compliance.App/Program.cs#L178): `.AddMcpTool<ListRiskDraftRevisions>(tool => tool.ReadOnly())`
- [Program.cs:179](../../src/Compliance.App/Program.cs#L179): `.AddMcpTool<GetRiskDraftRevision>(tool => tool.ReadOnly())`
- [Program.cs:180](../../src/Compliance.App/Program.cs#L180): `.AddMcpTool<GetRiskMethod>(tool => tool.ReadOnly())`
- [Program.cs:181](../../src/Compliance.App/Program.cs#L181): `.AddMcpTool<GetRiskMethodVersion>(tool => tool.ReadOnly())`
- [Program.cs:182](../../src/Compliance.App/Program.cs#L182): `.AddMcpTool<GetRiskEvaluation>(tool => tool.ReadOnly())`
- [Program.cs:183](../../src/Compliance.App/Program.cs#L183): `.AddMcpTool<ListRiskEvaluationHistory>(tool => tool.ReadOnly())`
- [Program.cs:184](../../src/Compliance.App/Program.cs#L184): `.AddMcpTool<GetRiskGovernance>(tool => tool.ReadOnly())`
- [Program.cs:185](../../src/Compliance.App/Program.cs#L185): `.AddMcpTool<ReviseProgram>(tool => tool.Idempotent())`
- [Program.cs:186](../../src/Compliance.App/Program.cs#L186): `.AddMcpTool<GetProgram>(tool => tool.ReadOnly())`
- [Program.cs:187](../../src/Compliance.App/Program.cs#L187): `.AddMcpTool<ListPrograms>(tool => tool.ReadOnly())`
- [Program.cs:188](../../src/Compliance.App/Program.cs#L188): `.AddMcpTool<ListProgramRevisions>(tool => tool.ReadOnly())`
- [Program.cs:189](../../src/Compliance.App/Program.cs#L189): `.AddMcpTool<GetProgramRevision>(tool => tool.ReadOnly())`
- [Program.cs:190](../../src/Compliance.App/Program.cs#L190): `.AddMcpTool<GetProgramSetupWork>(tool => tool.ReadOnly())`
- [Program.cs:191](../../src/Compliance.App/Program.cs#L191): `.AddMcpTool<DeclareApplication>()`
- [Program.cs:192](../../src/Compliance.App/Program.cs#L192): `.AddMcpTool<ReviseApplicationMcpInput>(tool => tool.Named("bdgrz.application.revise")`
- [Program.cs:194](../../src/Compliance.App/Program.cs#L194): `.AddMcpTool<DeclareSystemInstance>()`
- [Program.cs:195](../../src/Compliance.App/Program.cs#L195): `.AddMcpTool<GetApplication>(tool => tool.ReadOnly())`
- [Program.cs:196](../../src/Compliance.App/Program.cs#L196): `.AddMcpTool<ListApplications>(tool => tool.ReadOnly())`
- [Program.cs:197](../../src/Compliance.App/Program.cs#L197): `.AddMcpTool<GetApplicationRevision>(tool => tool.ReadOnly())`
- [Program.cs:198](../../src/Compliance.App/Program.cs#L198): `.AddMcpTool<ListApplicationRevisions>(tool => tool.ReadOnly())`
- [Program.cs:199](../../src/Compliance.App/Program.cs#L199): `.AddMcpTool<GetSystemInstance>(tool => tool.ReadOnly())`
- [Program.cs:200](../../src/Compliance.App/Program.cs#L200): `.AddMcpTool<ListSystemInstances>(tool => tool.ReadOnly())`
- [Program.cs:201](../../src/Compliance.App/Program.cs#L201): `.AddMcpTool<ListApplicationBoundaryReferences>(tool => tool.ReadOnly())`
- [Program.cs:202](../../src/Compliance.App/Program.cs#L202): `.AddMcpTool<ListSystemInstanceBoundaryReferences>(tool => tool.ReadOnly())`
- [Program.cs:203](../../src/Compliance.App/Program.cs#L203): `.AddMcpTool<PreviewApplicationChangeMcpInput>(tool => tool.Named("bdgrz.application.change.preview")`
- [Program.cs:205](../../src/Compliance.App/Program.cs#L205): `.AddMcpTool<RetireApplication>(tool => tool.Idempotent())`
- [Program.cs:206](../../src/Compliance.App/Program.cs#L206): `.AddMcpTool<RetireSystemInstance>(tool => tool.Idempotent())`
- [Program.cs:207](../../src/Compliance.App/Program.cs#L207): `.AddMcpTool<GetAccessReviewScope>(tool => tool.ReadOnly())`
- [Program.cs:208](../../src/Compliance.App/Program.cs#L208): `.AddMcpTool<ListAccessReviewScopes>(tool => tool.ReadOnly())`
- [Program.cs:209](../../src/Compliance.App/Program.cs#L209): `.AddMcpTool<StageApplicationImport>(tool => tool.Idempotent())`
- [Program.cs:210](../../src/Compliance.App/Program.cs#L210): `.AddMcpTool<GetApplicationImport>(tool => tool.ReadOnly())`
- [Program.cs:211](../../src/Compliance.App/Program.cs#L211): `.AddMcpTool<ListApplicationImportRows>(tool => tool.ReadOnly())`
- [Program.cs:212](../../src/Compliance.App/Program.cs#L212): `.AddMcpTool<PreviewApplicationImport>(tool => tool.ReadOnly())`
- [Program.cs:213](../../src/Compliance.App/Program.cs#L213): `.AddMcpTool<PreviewMissingApplicationImportRows>(tool => tool.ReadOnly())`
- [Program.cs:214](../../src/Compliance.App/Program.cs#L214): `.AddMcpTool<PreviewApplicationImportOmissionProposal>(tool => tool.ReadOnly())`
- [Program.cs:215](../../src/Compliance.App/Program.cs#L215): `.AddMcpTool<GetApplicationImportOmissionProposal>(tool => tool.ReadOnly())`
- [Program.cs:216](../../src/Compliance.App/Program.cs#L216): `.AddMcpTool<GetArtifactRetention>(tool => tool.ReadOnly())`
- [Program.cs:217](../../src/Compliance.App/Program.cs#L217): `.AddMcpTool<ListArtifactLegalHolds>(tool => tool.ReadOnly())`
- [Program.cs:218](../../src/Compliance.App/Program.cs#L218): `.AddMcpTool<GetApplicationImportRetention>(tool => tool.ReadOnly())`
- [Program.cs:219](../../src/Compliance.App/Program.cs#L219): `.AddMcpTool<GetApplicationImportProgress>(tool => tool.ReadOnly())`
- [Program.cs:220](../../src/Compliance.App/Program.cs#L220): `.AddMcpTool<GetApplicationImportRejectedReport>(tool => tool.ReadOnly())`
- [Program.cs:221](../../src/Compliance.App/Program.cs#L221): `.AddMcpTool<RecordPerson>()`
- [Program.cs:222](../../src/Compliance.App/Program.cs#L222): `.AddMcpTool<RevisePerson>(tool => tool.Idempotent())`
- [Program.cs:223](../../src/Compliance.App/Program.cs#L223): `.AddMcpTool<CorrelatePersonMembership>(tool => tool.Idempotent())`
- [Program.cs:224](../../src/Compliance.App/Program.cs#L224): `.AddMcpTool<GetPerson>(tool => tool.ReadOnly())`
- [Program.cs:225](../../src/Compliance.App/Program.cs#L225): `.AddMcpTool<ListPeople>(tool => tool.ReadOnly())`
- [Program.cs:226](../../src/Compliance.App/Program.cs#L226): `.AddMcpTool<RecordWorkRelationship>()`
- [Program.cs:227](../../src/Compliance.App/Program.cs#L227): `.AddMcpTool<ReviseWorkRelationship>(tool => tool.Idempotent())`
- [Program.cs:228](../../src/Compliance.App/Program.cs#L228): `.AddMcpTool<GetWorkRelationship>(tool => tool.ReadOnly())`
- [Program.cs:229](../../src/Compliance.App/Program.cs#L229): `.AddMcpTool<ListWorkRelationships>(tool => tool.ReadOnly())`
- [Program.cs:230](../../src/Compliance.App/Program.cs#L230): `.AddMcpTool<ListWorkforceObservations>(tool => tool.ReadOnly())`
- [Program.cs:231](../../src/Compliance.App/Program.cs#L231): `.AddMcpTool<ListWorkforceReconciliationObservations>(tool => tool.ReadOnly())`
- [Program.cs:232](../../src/Compliance.App/Program.cs#L232): `.AddMcpTool<RecordWorkforceSourceObservation>(tool => tool.Idempotent())`
- [Program.cs:233](../../src/Compliance.App/Program.cs#L233): `.AddMcpTool<GetWorkforceSourceObservation>(tool => tool.ReadOnly())`
- [Program.cs:234](../../src/Compliance.App/Program.cs#L234): `.AddMcpTool<ListWorkforceSourceObservations>(tool => tool.ReadOnly())`
- [Program.cs:235](../../src/Compliance.App/Program.cs#L235): `.AddMcpTool<PreviewWorkforceSourceObservation>(tool => tool.ReadOnly())`
- [Program.cs:236](../../src/Compliance.App/Program.cs#L236): `.AddMcpTool<RecordServiceIdentity>()`
- [Program.cs:237](../../src/Compliance.App/Program.cs#L237): `.AddMcpTool<ReviseServiceIdentity>(tool => tool.Idempotent())`
- [Program.cs:238](../../src/Compliance.App/Program.cs#L238): `.AddMcpTool<GetServiceIdentity>(tool => tool.ReadOnly())`
- [Program.cs:239](../../src/Compliance.App/Program.cs#L239): `.AddMcpTool<ListServiceIdentities>(tool => tool.ReadOnly())`
- [Program.cs:240](../../src/Compliance.App/Program.cs#L240): `.AddMcpTool<RecordProvider>()`
- [Program.cs:241](../../src/Compliance.App/Program.cs#L241): `.AddMcpTool<ReviseProvider>(tool => tool.Idempotent())`
- [Program.cs:242](../../src/Compliance.App/Program.cs#L242): `.AddMcpTool<GetProvider>(tool => tool.ReadOnly().Idempotent())`
- [Program.cs:243](../../src/Compliance.App/Program.cs#L243): `.AddMcpTool<ListProviders>(tool => tool.ReadOnly().Idempotent())`
- [Program.cs:244](../../src/Compliance.App/Program.cs#L244): `.AddMcpTool<GetProviderRevision>(tool => tool.ReadOnly().Idempotent())`
- [Program.cs:245](../../src/Compliance.App/Program.cs#L245): `.AddMcpTool<ListProviderRevisions>(tool => tool.ReadOnly().Idempotent())`
- [Program.cs:246](../../src/Compliance.App/Program.cs#L246): `.AddMcpTool<RecordAssuranceReport>()`
- [Program.cs:247](../../src/Compliance.App/Program.cs#L247): `.AddMcpTool<ReviseAssuranceReport>(tool => tool.Idempotent())`
- [Program.cs:248](../../src/Compliance.App/Program.cs#L248): `.AddMcpTool<ListProviderAssuranceReports>(tool => tool.ReadOnly().Idempotent())`
- [Program.cs:249](../../src/Compliance.App/Program.cs#L249): `.AddMcpTool<ListProviderReviews>(tool => tool.ReadOnly().Idempotent())`
- [Program.cs:250](../../src/Compliance.App/Program.cs#L250): `.AddMcpTool<RecordProviderCoverageGap>()`
- [Program.cs:251](../../src/Compliance.App/Program.cs#L251): `.AddMcpTool<GetProviderCoverageGap>(tool => tool.ReadOnly().Idempotent())`
- [Program.cs:252](../../src/Compliance.App/Program.cs#L252): `.AddMcpTool<ListProviderCoverageGaps>(tool => tool.ReadOnly().Idempotent())`
- [Program.cs:253](../../src/Compliance.App/Program.cs#L253): `.AddMcpTool<GetProviderAssuranceCoverage>(tool => tool.ReadOnly().Idempotent())`
- [Program.cs:254](../../src/Compliance.App/Program.cs#L254): `.AddMcpTool<PreviewProviderChange>(tool => tool.ReadOnly().Idempotent())`
- [Program.cs:255](../../src/Compliance.App/Program.cs#L255): `.AddMcpTool<RecordTechnologyComponent>()`
- [Program.cs:256](../../src/Compliance.App/Program.cs#L256): `.AddMcpTool<ReviseTechnologyComponent>(tool => tool.Idempotent())`
- [Program.cs:257](../../src/Compliance.App/Program.cs#L257): `.AddMcpTool<GetTechnologyComponent>(tool => tool.ReadOnly())`
- [Program.cs:258](../../src/Compliance.App/Program.cs#L258): `.AddMcpTool<ListTechnologyComponentBoundaryReferences>(tool => tool.ReadOnly())`
- [Program.cs:259](../../src/Compliance.App/Program.cs#L259): `.AddMcpTool<ListInformationAssetBoundaryReferences>(tool => tool.ReadOnly())`
- [Program.cs:260](../../src/Compliance.App/Program.cs#L260): `.AddMcpTool<ListTechnologyComponents>(tool => tool.ReadOnly())`
- [Program.cs:261](../../src/Compliance.App/Program.cs#L261): `.AddMcpTool<ListTechnologyComponentRevisions>(tool => tool.ReadOnly())`
- [Program.cs:262](../../src/Compliance.App/Program.cs#L262): `.AddMcpTool<RecordInformationAsset>()`
- [Program.cs:263](../../src/Compliance.App/Program.cs#L263): `.AddMcpTool<ReviseInformationAsset>(tool => tool.Idempotent())`
- [Program.cs:264](../../src/Compliance.App/Program.cs#L264): `.AddMcpTool<GetInformationAsset>(tool => tool.ReadOnly())`
- [Program.cs:265](../../src/Compliance.App/Program.cs#L265): `.AddMcpTool<ListInformationAssets>(tool => tool.ReadOnly())`
- [Program.cs:266](../../src/Compliance.App/Program.cs#L266): `.AddMcpTool<ListInformationAssetRevisions>(tool => tool.ReadOnly())`
- [Program.cs:267](../../src/Compliance.App/Program.cs#L267): `.AddMcpTool<PreviewInformationAssetChange>(tool => tool.ReadOnly())`
- [Program.cs:268](../../src/Compliance.App/Program.cs#L268): `.AddMcpTool<RecordLocation>()`
- [Program.cs:269](../../src/Compliance.App/Program.cs#L269): `.AddMcpTool<ReviseLocation>(tool => tool.Idempotent())`
- [Program.cs:270](../../src/Compliance.App/Program.cs#L270): `.AddMcpTool<GetLocation>(tool => tool.ReadOnly())`
- [Program.cs:271](../../src/Compliance.App/Program.cs#L271): `.AddMcpTool<ListLocations>(tool => tool.ReadOnly())`
- [Program.cs:272](../../src/Compliance.App/Program.cs#L272): `.AddMcpTool<ListLocationRevisions>(tool => tool.ReadOnly())`
- [Program.cs:273](../../src/Compliance.App/Program.cs#L273): `.AddMcpTool<RecordOperationalProcess>()`
- [Program.cs:274](../../src/Compliance.App/Program.cs#L274): `.AddMcpTool<ReviseOperationalProcess>(tool => tool.Idempotent())`
- [Program.cs:275](../../src/Compliance.App/Program.cs#L275): `.AddMcpTool<GetOperationalProcess>(tool => tool.ReadOnly())`
- [Program.cs:276](../../src/Compliance.App/Program.cs#L276): `.AddMcpTool<ListOperationalProcesses>(tool => tool.ReadOnly())`
- [Program.cs:277](../../src/Compliance.App/Program.cs#L277): `.AddMcpTool<ListOperationalProcessRevisions>(tool => tool.ReadOnly())`
- [Program.cs:278](../../src/Compliance.App/Program.cs#L278): `.AddMcpTool<RecordDataFlow>()`
- [Program.cs:279](../../src/Compliance.App/Program.cs#L279): `.AddMcpTool<ReviseDataFlow>(tool => tool.Idempotent())`
- [Program.cs:280](../../src/Compliance.App/Program.cs#L280): `.AddMcpTool<GetDataFlow>(tool => tool.ReadOnly())`
- [Program.cs:281](../../src/Compliance.App/Program.cs#L281): `.AddMcpTool<ListDataFlows>(tool => tool.ReadOnly())`
- [Program.cs:282](../../src/Compliance.App/Program.cs#L282): `.AddMcpTool<ListDataFlowRevisions>(tool => tool.ReadOnly())`
- [Program.cs:283](../../src/Compliance.App/Program.cs#L283): `.AddMcpTool<CreateClientService>()`
- [Program.cs:284](../../src/Compliance.App/Program.cs#L284): `.AddMcpTool<ReviseClientService>(tool => tool.Idempotent())`
- [Program.cs:285](../../src/Compliance.App/Program.cs#L285): `.AddMcpTool<RetireClientService>(tool => tool.Destructive())`
- [Program.cs:286](../../src/Compliance.App/Program.cs#L286): `.AddMcpTool<GetClientService>(tool => tool.ReadOnly())`
- [Program.cs:287](../../src/Compliance.App/Program.cs#L287): `.AddMcpTool<ListClientServices>(tool => tool.ReadOnly())`
- [Program.cs:288](../../src/Compliance.App/Program.cs#L288): `.AddMcpTool<ListProgramClientServices>(tool => tool.ReadOnly())`
- [Program.cs:289](../../src/Compliance.App/Program.cs#L289): `.AddMcpTool<ListClientServiceRevisions>(tool => tool.ReadOnly())`
- [Program.cs:290](../../src/Compliance.App/Program.cs#L290): `.AddMcpTool<GetClientServiceRevision>(tool => tool.ReadOnly())`
- [Program.cs:291](../../src/Compliance.App/Program.cs#L291): `.AddMcpTool<CreateBoundary>()`
- [Program.cs:292](../../src/Compliance.App/Program.cs#L292): `.AddMcpTool<ReviseBoundaryDraft>(tool => tool.Idempotent())`
- [Program.cs:293](../../src/Compliance.App/Program.cs#L293): `.AddMcpTool<DiscardBoundaryDraft>(tool => tool.Destructive())`
- [Program.cs:294](../../src/Compliance.App/Program.cs#L294): `.AddMcpTool<GetBoundary>(tool => tool.ReadOnly())`
- [Program.cs:295](../../src/Compliance.App/Program.cs#L295): `.AddMcpTool<ListResponsibilities>(tool => tool.ReadOnly())`
- [Program.cs:296](../../src/Compliance.App/Program.cs#L296): `.AddMcpTool<PreviewResponsibilityConflicts>(tool => tool.ReadOnly())`
- [Program.cs:297](../../src/Compliance.App/Program.cs#L297): `.AddMcpTool<ListProgramBoundaries>(tool => tool.ReadOnly())`
- [Program.cs:298](../../src/Compliance.App/Program.cs#L298): `.AddMcpTool<GetBoundaryVersion>(tool => tool.ReadOnly())`
- [Program.cs:299](../../src/Compliance.App/Program.cs#L299): `.AddMcpTool<ListBoundaryVersions>(tool => tool.ReadOnly())`
- [Program.cs:300](../../src/Compliance.App/Program.cs#L300): `.AddMcpTool<GetEffectiveBoundaryVersion>(tool => tool.ReadOnly())`
- [Program.cs:301](../../src/Compliance.App/Program.cs#L301): `.AddMcpTool<GetBoundaryDecision>(tool => tool.ReadOnly())`
- [Program.cs:302](../../src/Compliance.App/Program.cs#L302): `.AddMcpTool<ListBoundaryDecisions>(tool => tool.ReadOnly())`
- [Program.cs:303](../../src/Compliance.App/Program.cs#L303): `.AddMcpTool<PreviewBoundaryImpact>(tool => tool.ReadOnly())`
- [Program.cs:304](../../src/Compliance.App/Program.cs#L304): `.AddMcpTool<ProposeBoundarySuccessor>()`
- [Program.cs:305](../../src/Compliance.App/Program.cs#L305): `.AddMcpTool<FreezeProgramScopeSnapshot>()`
- [Program.cs:306](../../src/Compliance.App/Program.cs#L306): `.AddMcpTool<AmendProgramScopeSnapshot>()`
- [Program.cs:307](../../src/Compliance.App/Program.cs#L307): `.AddMcpTool<GetSnapshot>(tool => tool.ReadOnly())`
- [Program.cs:308](../../src/Compliance.App/Program.cs#L308): `.AddMcpTool<VerifyProgramScopeSnapshot>(tool => tool.ReadOnly())`
- [Program.cs:309](../../src/Compliance.App/Program.cs#L309): `.AddMcpTool<RegenerateProgramScopeSnapshotManifest>(tool => tool.ReadOnly())`
- [Program.cs:310](../../src/Compliance.App/Program.cs#L310): `.AddMcpTool<ListProgramSnapshots>(tool => tool.ReadOnly())`
- [Program.cs:311](../../src/Compliance.App/Program.cs#L311): `.AddMcpTool<FreezeWorkforceRosterSnapshot>()`
- [Program.cs:312](../../src/Compliance.App/Program.cs#L312): `.AddMcpTool<AmendWorkforceRosterSnapshot>()`
- [Program.cs:313](../../src/Compliance.App/Program.cs#L313): `.AddMcpTool<GetWorkforceRosterSnapshot>(tool => tool.ReadOnly())`
- [Program.cs:314](../../src/Compliance.App/Program.cs#L314): `.AddMcpTool<GetWorkforceRosterSnapshotAsOf>(tool => tool.ReadOnly())`
- [Program.cs:315](../../src/Compliance.App/Program.cs#L315): `.AddMcpTool<ListWorkforceRosterSnapshots>(tool => tool.ReadOnly())`
- [Program.cs:316](../../src/Compliance.App/Program.cs#L316): `.AddMcpTool<RegenerateWorkforceRosterSnapshotManifest>(tool => tool.ReadOnly())`
- [Program.cs:317](../../src/Compliance.App/Program.cs#L317): `.AddMcpTool<GetReadinessAssessment>(tool => tool.ReadOnly())`
- [Program.cs:330](../../src/Compliance.App/Program.cs#L330): `.AddMcpTool<GetAdvisorRequest>(tool => tool.ReadOnly())`
- [Program.cs:331](../../src/Compliance.App/Program.cs#L331): `.AddMcpTool<PreviewAdvisorHandoff>(tool => tool.ReadOnly())`
- [Program.cs:332](../../src/Compliance.App/Program.cs#L332): `.AddMcpTool<GetAdvisorSharedSource>(tool => tool.ReadOnly())`
- [Program.cs:333](../../src/Compliance.App/Program.cs#L333): `.AddMcpTool<ListAdvisorRequests>(tool => tool.ReadOnly())`
- [Program.cs:334](../../src/Compliance.App/Program.cs#L334): `.AddMcpTool<ListAdvisorFeedback>(tool => tool.ReadOnly())`
- [Program.cs:318](../../src/Compliance.App/Program.cs#L318): `.AddMcpTool<ListReadinessAssessments>(tool => tool.ReadOnly())`
- [Program.cs:319](../../src/Compliance.App/Program.cs#L319): `.AddMcpTool<ListReadinessGaps>(tool => tool.ReadOnly())`
- [Program.cs:320](../../src/Compliance.App/Program.cs#L320): `.AddMcpTool<ListTypeIEntryDecisions>(tool => tool.ReadOnly())`
- [Program.cs:321](../../src/Compliance.App/Program.cs#L321): `.AddMcpTool<ListReadinessAnnotations>(tool => tool.ReadOnly())`
- [Program.cs:322](../../src/Compliance.App/Program.cs#L322): `.AddMcpTool<ProposeControlOperatingPlan>()`
- [Program.cs:323](../../src/Compliance.App/Program.cs#L323): `.AddMcpTool<PreviewControlOperatingPlan>(tool => tool.ReadOnly())`
- [Program.cs:324](../../src/Compliance.App/Program.cs#L324): `.AddMcpTool<GetControlOperatingPlan>(tool => tool.ReadOnly())`
- [Program.cs:325](../../src/Compliance.App/Program.cs#L325): `.AddMcpTool<ListControlOperatingBlockers>(tool => tool.ReadOnly())`
- [Program.cs:326](../../src/Compliance.App/Program.cs#L326): `.AddMcpTool<ListMyControlWork>(tool => tool.ReadOnly())`
- [Program.cs:327](../../src/Compliance.App/Program.cs#L327): `.AddMcpTool<GetControlOccurrence>(tool => tool.ReadOnly())`
- [Program.cs:328](../../src/Compliance.App/Program.cs#L328): `.AddMcpTool<ListControlOccurrences>(tool => tool.ReadOnly())`
- [Program.cs:329](../../src/Compliance.App/Program.cs#L329): `.AddMcpTool<DefineControlEvaluationPlan>()`
- [Program.cs:330](../../src/Compliance.App/Program.cs#L330): `.AddMcpTool<GetControlEvaluationPlanVersion>(tool => tool.ReadOnly())`
- [Program.cs:331](../../src/Compliance.App/Program.cs#L331): `.AddMcpTool<ListControlEvaluationPlanVersions>(tool => tool.ReadOnly())`
- [Program.cs:332](../../src/Compliance.App/Program.cs#L332): `.AddMcpTool<StartControlEvaluation>()`
- [Program.cs:333](../../src/Compliance.App/Program.cs#L333): `.AddMcpTool<RecordControlEvaluationStep>()`
- [Program.cs:334](../../src/Compliance.App/Program.cs#L334): `.AddMcpTool<DisposeControlEvaluationDeviation>()`
- [Program.cs:335](../../src/Compliance.App/Program.cs#L335): `.AddMcpTool<GetControlEvaluation>(tool => tool.ReadOnly())`
- [Program.cs:336](../../src/Compliance.App/Program.cs#L336): `.AddMcpTool<ListControlEvaluations>(tool => tool.ReadOnly())`
- [Program.cs:337](../../src/Compliance.App/Program.cs#L337): `.AddMcpTool<RaiseFinding>()`
- [Program.cs:338](../../src/Compliance.App/Program.cs#L338): `.AddMcpTool<GetFinding>(tool => tool.ReadOnly())`
- [Program.cs:339](../../src/Compliance.App/Program.cs#L339): `.AddMcpTool<ListFindings>(tool => tool.ReadOnly())`
- [Program.cs:340](../../src/Compliance.App/Program.cs#L340): `.AddMcpTool<OpenEvidenceRequest>()`
- [Program.cs:341](../../src/Compliance.App/Program.cs#L341): `.AddMcpTool<CancelEvidenceRequest>()`
- [Program.cs:342](../../src/Compliance.App/Program.cs#L342): `.AddMcpTool<PrepareEvidenceRedaction>()`
- [Program.cs:343](../../src/Compliance.App/Program.cs#L343): `.AddMcpTool<GetEvidenceRedaction>(tool => tool.ReadOnly())`
- [Program.cs:344](../../src/Compliance.App/Program.cs#L344): `.AddMcpTool<GetEvidenceArtifactMetadata>(tool => tool.ReadOnly())`
- [Program.cs:345](../../src/Compliance.App/Program.cs#L345): `.AddMcpTool<GetEvidenceRequest>(tool => tool.ReadOnly())`
- [Program.cs:346](../../src/Compliance.App/Program.cs#L346): `.AddMcpTool<ListEvidenceRequests>(tool => tool.ReadOnly())`
- [Program.cs:347](../../src/Compliance.App/Program.cs#L347): `.AddMcpTool<OpenAccessPopulation>()`
- [Program.cs:348](../../src/Compliance.App/Program.cs#L348): `.AddMcpTool<RecordAccessPopulationFacts>(tool => tool.Idempotent())`
- [Program.cs:349](../../src/Compliance.App/Program.cs#L349): `.AddMcpTool<PreviewAccessPopulation>(tool => tool.ReadOnly())`
- [Program.cs:350](../../src/Compliance.App/Program.cs#L350): `.AddMcpTool<GetAccessPopulation>(tool => tool.ReadOnly())`
- [Program.cs:351](../../src/Compliance.App/Program.cs#L351): `.AddMcpTool<ListAccessPopulations>(tool => tool.ReadOnly())`
- [Program.cs:352](../../src/Compliance.App/Program.cs#L352): `.AddMcpTool<GetAccessReviewCoverage>(tool => tool.ReadOnly())`
- [Program.cs:353](../../src/Compliance.App/Program.cs#L353): `.AddMcpTool<ClassifyAccessPrincipal>()`
- [Program.cs:354](../../src/Compliance.App/Program.cs#L354): `.AddMcpTool<ListAccessPrincipals>(tool => tool.ReadOnly())`
- [Program.cs:355](../../src/Compliance.App/Program.cs#L355): `.AddMcpTool<GetAccessVariance>(tool => tool.ReadOnly())`
- [Program.cs:356](../../src/Compliance.App/Program.cs#L356): `.AddMcpTool<ProposeAccessExpectation>()`
- [Program.cs:357](../../src/Compliance.App/Program.cs#L357): `.AddMcpTool<ListAccessExpectations>(tool => tool.ReadOnly())`
- [Program.cs:358](../../src/Compliance.App/Program.cs#L358): `.AddMcpTool<LaunchAccessReviewCampaign>()`
- [Program.cs:359](../../src/Compliance.App/Program.cs#L359): `.AddMcpTool<GetAccessReviewCampaign>(tool => tool.ReadOnly())`
- [Program.cs:360](../../src/Compliance.App/Program.cs#L360): `.AddMcpTool<ListAccessReviewCampaigns>(tool => tool.ReadOnly())`
- [Program.cs:361](../../src/Compliance.App/Program.cs#L361): `.AddMcpTool<PreviewBulkAccessDecision>(tool => tool.ReadOnly())`
- [Program.cs:362](../../src/Compliance.App/Program.cs#L362): `.AddMcpTool<RecordAccessRemediationChange>()`
- [Program.cs:363](../../src/Compliance.App/Program.cs#L363): `.AddMcpTool<VerifyAccessRemediation>()`
- [Program.cs:364](../../src/Compliance.App/Program.cs#L364): `.AddMcpTool<ListWork>(tool => tool.ReadOnly())`
- [Program.cs:365](../../src/Compliance.App/Program.cs#L365): `.AddMcpTool<GetWorkItem>(tool => tool.ReadOnly())`
- [Program.cs:366](../../src/Compliance.App/Program.cs#L366): `.AddMcpTool<ClaimWorkItem>()`
- [Program.cs:367](../../src/Compliance.App/Program.cs#L367): `.AddMcpTool<AssignWorkItem>()`
- [Program.cs:368](../../src/Compliance.App/Program.cs#L368): `.AddMcpTool<DelegateWorkItem>()`
- [Program.cs:369](../../src/Compliance.App/Program.cs#L369): `.AddMcpTool<EscalateWorkItem>()`
- [Program.cs:370](../../src/Compliance.App/Program.cs#L370): `.AddMcpTool<ListWorkReminders>(tool => tool.ReadOnly())`
- [Program.cs:371](../../src/Compliance.App/Program.cs#L371): `.AddMcpTool<GetWorkDigest>(tool => tool.ReadOnly())`
- [Program.cs:372](../../src/Compliance.App/Program.cs#L372): `.AddMcpTool<GetWorkDigestPreference>(tool => tool.ReadOnly())`
- [Program.cs:373](../../src/Compliance.App/Program.cs#L373): `.AddMcpTool<SetWorkDigestPreference>()`
- [Program.cs:415](../../src/Compliance.App/Program.cs#L415): `app.MapPortiaMcp(AuthorizationDenialLog.McpPath).RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser);`
- [Program.cs:418](../../src/Compliance.App/Program.cs#L418): `app.MapGet(`
- [Program.cs:424](../../src/Compliance.App/Program.cs#L424): `app.MapGet(`
- [Program.cs:441](../../src/Compliance.App/Program.cs#L441): `app.MapPost(`
- [ComplianceHealthExtensions.cs:18](../../src/Compliance.App/Hosting/Health/ComplianceHealthExtensions.cs#L18): `endpoints.MapHealthChecks(`
- [ComplianceHealthExtensions.cs:22](../../src/Compliance.App/Hosting/Health/ComplianceHealthExtensions.cs#L22): `endpoints.MapHealthChecks(`
- [ComplianceHealthExtensions.cs:26](../../src/Compliance.App/Hosting/Health/ComplianceHealthExtensions.cs#L26): `endpoints.MapHealthChecks(`

### Canonical model checklist

These approved entity names are a semantic checklist. They do not imply one aggregate, table, endpoint or projector per entity.

`Organization`, `OrganizationalUnit`, `Person`, `WorkRelationship`, `JobProfile`, `Responsibility`, `FederatedIdentity`, `PlatformUser`, `Membership`, `Team`, `AccessRole`, `Permission`, `RoleAssignment`, `SeparationOfDutyConstraint`, `SeparationOfDutyException`, `TrustedIssuer`, `PlatformOperatorGrant`, `FirmStaffMember`, `ServiceEngagement`, `EngagementAssignment`, `Application`, `ClientService`, `SystemInstance`, `Resource`, `IntegrationEndpoint`, `Device`, `DeviceComponent`, `ComputeInstance`, `NetworkInterface`, `Network`, `NetworkConnection`, `SoftwareInstallation`, `Location`, `EndpointClass`, `OperationalProcess`, `InformationAsset`, `Provider`, `DataFlow`, `Account`, `ServiceIdentity`, `Group`, `GroupMember`, `Role`, `Entitlement`, `RoleEntitlement`, `AccessAssignment`, `EffectiveAccess`, `SourceSystem`, `ExternalIdentifier`, `Observation`, `Correlation`, `Snapshot`, `IncidentReference`, `ControlRiskTreatment`, `SignificantChange`, `ComplianceIncident`.
