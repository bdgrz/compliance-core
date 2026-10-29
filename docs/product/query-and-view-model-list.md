# Query and view-model inventory

This is the requirement-backed read-layer plan for the committed product scope. It names query operations and their feature-owned response views. A query is a read, list, preview, or derived assessment; a POST transport does not make a read a command. The inventory tracks required contracts and implementation coverage separately: a source file alone does not prove a handler, projection, API route, or authorization policy exists.

## Existing inventory

Before this backfill, the source inventory recorded **216 query contracts** and **436 models/supporting contracts**. The current source inventory contains **272 unique query contracts**. The 57 rows below are required read workflows, not a unique contract count; some workflows reuse query contracts that were already in the source inventory. The full, source-linked query list and feature-owned models are in the [component inventory](component-inventory.md#existing-queries) and [model appendix](component-inventory.md#existing-models-and-supporting-contracts). Source presence does not prove that a query is authorized, registered, projected, exposed, or satisfies its owning story. Reconcile by behavior before adding a new name.

## Current handler and HTTP gaps

Reconciled 2026-10-19 against the query contracts, Core handlers, and typed `MapPortia*` registrations. All query contracts in this table and their response-view contracts have source files. These 44 reads have no Core request handler and no typed HTTP registration. Their read-model repositories and projector support must be established with the owning feature before exposing a route. The query table below remains the requirement source for the expected response.

| Feature | Queries without a handler or typed HTTP registration |
| --- | --- |
| AttestRecords | `GetOwnAttestRecordDelivery`, `GetOwnAttestRecordPackage`, `ListOutstandingAttestDeliveryHolds` |
| AuditPackages | `GetAuditPackage`, `GetAuditPackageDelivery`, `GetAuditPackageManifest`, `GetAuditPackageValidation`, `ListAuditPackages` |
| AuditPopulations | `GetAuditPopulation`, `GetObservationPeriodClose`, `GetPopulationReconciliation`, `ListAuditPopulationDefinitions`, `ListAuditPopulationRows`, `PreviewObservationPeriodClose` |
| AuditorRequests | `GetAuditorRequest`, `GetSampleSelection`, `ListAuditorRequests`, `ListRequestResponses` |
| Examinations | `GetAuditOutcome`, `GetExaminationSignOff`, `GetManagementAssertion`, `GetTypeIIPlan`, `ListAuditObservations` |
| FirmPortfolio | `GetAuthorizedClientSummary`, `ListAuthorizedClientPortfolio`, `ListMyCrossClientWork` |
| MethodologyTemplates | `GetMethodologyTemplateVersion`, `GetTemplateApplication`, `ListMethodologyTemplates`, `PreviewMethodologyTemplateApplication` |
| Operations | `GetControlCalendar`, `GetManagementReview`, `GetObservationPeriod`, `ListDueGovernanceReviews`, `ListManagementReviewActions`, `ListMissingOccurrences`, `ListObservationPeriods` |
| Programs | `GetNextPeriodPlan`, `GetProgramRollover`, `ListCarryForwardReferences`, `PreviewProgramRollover` |
| SystemDescription | `GetSystemDescription`, `ListSystemDescriptionVersions`, `PreviewSystemDescriptionImpact` |

The route gap is not an instruction to expose every command over HTTP. Keep internal worker callbacks and scheduler commands off the public API; decide route exposure per accepted user workflow and its authorization contract.

The three incident reads are now implemented with a tenant-partitioned directory projection, Core handlers, and program-authorized HTTP routes. Detail and impact assessment reads return not-found for restricted records outside the current assessor or dedicated restricted-read grant; list reads filter hidden rows before pagination and do not emit a cursor that would reveal trailing restricted rows.

## Required read workflows to backfill

The 57 planned query contracts and 57 response-view types listed below now have one source file each under their owning feature. This fills the contract-file gap recorded in the source audit. Wiring and behavior status varies by query; a contract alone does not prove handler, read-model, API/MCP, or story-level authorization coverage. Existing contracts with incomplete consumers are tracked as completion gaps rather than duplicated here.

### Advisor collaboration — R2-08

| Query | Response view | Required read |
| --- | --- | --- |
| [GetAdvisorRequest](../../src/Compliance.Common/Features/AdvisorCollaboration/Queries/GetAdvisorRequest.cs#L9) | [AdvisorRequestView](../../src/Compliance.Common/Features/AdvisorCollaboration/Models/AdvisorRequestView.cs#L6) | One scoped advisor handoff, including selected immutable record versions and lifecycle state. |
| [GetAdvisorSharedSource](../../src/Compliance.Common/Features/AdvisorCollaboration/Queries/GetAdvisorSharedSource.cs#L9) | [AdvisorSharedSourceView](../../src/Compliance.Common/Features/AdvisorCollaboration/Models/AdvisorSharedSourceView.cs#L8) | Current content for one exact control, policy, evidence artifact, readiness gap, or boundary question selected in an active handoff. |
| [PreviewAdvisorHandoff](../../src/Compliance.Common/Features/AdvisorCollaboration/Queries/PreviewAdvisorHandoff.cs#L7) | [AdvisorHandoffPreviewView](../../src/Compliance.Common/Features/AdvisorCollaboration/Models/AdvisorHandoffPreviewView.cs#L6), [AdvisorHandoffPreviewItemView](../../src/Compliance.Common/Features/AdvisorCollaboration/Models/AdvisorHandoffPreviewItemView.cs#L4) | Compliance Lead-only exact-content preview of a draft handoff, including new/changed versions and source versions removed from an explicitly selected ended shared handoff. |
| [ListAdvisorRequests](../../src/Compliance.Common/Features/AdvisorCollaboration/Queries/ListAdvisorRequests.cs#L9) | `Page<[AdvisorRequestSummaryView](../../src/Compliance.Common/Features/AdvisorCollaboration/Models/AdvisorRequestSummaryView.cs#L6)>` | Scoped handoff directory with status, review readiness, and optional comparison baseline. |
| [ListAdvisorFeedback](../../src/Compliance.Common/Features/AdvisorCollaboration/Queries/ListAdvisorFeedback.cs#L9) | `Page<[AdvisorFeedbackView](../../src/Compliance.Common/Features/AdvisorCollaboration/Models/AdvisorFeedbackView.cs#L6)>` | Feedback, corrections, responses, and resolution actor/time for a handoff or selected record. |

`GetAdvisorRequest`, `GetAdvisorSharedSource`, `PreviewAdvisorHandoff`, `ListAdvisorRequests`, and `ListAdvisorFeedback`
are available through HTTP and MCP. `PreviewAdvisorHandoff` is limited to the Compliance Lead,
loads the exact selected content, validates current source versions, and checks the draft revision
again after loading. Repeated handoffs name a prior shared handoff that has ended; exact unchanged
sources are excluded, changed/new content is previewed, and removed source versions are listed
separately. The handoff directory is a Fitz projection over handoff and
feedback events, with tenant-partitioned program indexes, stable cursor ordering, and a read-freshness
fence against its source event stream. Shared-source reads revalidate the selected native version
before and after loading content; approved control and policy versions must still be the latest
approved versions, and evidence requires the exact current program support link. Feedback pages
mark a selected validation historical when its source is superseded or otherwise no longer current.
Evidence bytes still flow through the governed artifact-download operation, which accepts the exact
handoff selection and rechecks its authorization. Firm Advisors can read only an unexpired
`ready_for_review` handoff after current engagement or program-scoped guest-grant checks. Internal
`comment` and `change_request` feedback appears as program-manager work; response changes its next
action to resolution, and resolution removes it from active work while preserving feedback history.
`validated` feedback remains review history, never a work item. The firm Advisor engagement path
remains blocked on the accepted authority prerequisite in #269.

`ListAdvisorRequests` returns the full lifecycle directory to client program readers. Assigned
Advisors and scoped guests receive only unexpired `ready_for_review` handoffs, through a separate
status-indexed query path; the query backfills that index for existing directory entries. The
list handler checks the visibility decision again after reading, so a revoked guest or engagement
assignment cannot fall back to the full directory during the request.

### Type I engagement — T1-01

| Query | Response view | Required read |
| --- | --- | --- |
| [GetExaminationEngagement](../../src/Compliance.Common/Features/Examinations/Queries/GetExaminationEngagement.cs#L9) | [ExaminationEngagementView](../../src/Compliance.Common/Features/Examinations/Models/ExaminationEngagementView.cs#L6) | Engagement identity, period, lifecycle, and baseline binding. |
| [GetTypeIBaseline](../../src/Compliance.Common/Features/Examinations/Queries/GetTypeIBaseline.cs#L9) | [TypeIBaselineView](../../src/Compliance.Common/Features/Examinations/Models/TypeIBaselineView.cs#L6) | Frozen baseline manifest, exact source versions, and verification state. |
| [PreviewTypeIBaseline](../../src/Compliance.Common/Features/Examinations/Queries/PreviewTypeIBaseline.cs#L9) | [TypeIBaselinePreviewView](../../src/Compliance.Common/Features/Examinations/Models/TypeIBaselinePreviewView.cs#L7) | Current exact source references, uncovered source-family blockers, and scope differences before confirmation. |

### System description — T1-02

| Query | Response view | Required read |
| --- | --- | --- |
| [GetSystemDescription](../../src/Compliance.Common/Features/SystemDescription/Queries/GetSystemDescription.cs#L9) | [SystemDescriptionView](../../src/Compliance.Common/Features/SystemDescription/Models/SystemDescriptionView.cs#L6) | Current or requested version, review/approval state, and engagement binding. |
| [ListSystemDescriptionVersions](../../src/Compliance.Common/Features/SystemDescription/Queries/ListSystemDescriptionVersions.cs#L9) | `Page<[SystemDescriptionVersionView](../../src/Compliance.Common/Features/SystemDescription/Models/SystemDescriptionVersionView.cs#L6)>` | Version history with authorship and decision state. |
| [PreviewSystemDescriptionImpact](../../src/Compliance.Common/Features/SystemDescription/Queries/PreviewSystemDescriptionImpact.cs#L9) | [SystemDescriptionImpactPreview](../../src/Compliance.Common/Features/SystemDescription/Models/SystemDescriptionImpactPreview.cs#L6) | Records and baseline references affected by a proposed successor. |

### Auditor requests and samples — T1-04, T3-03

| Query | Response view | Required read |
| --- | --- | --- |
| [GetAuditorRequest](../../src/Compliance.Common/Features/AuditorRequests/Queries/GetAuditorRequest.cs#L9) | [AuditorRequestView](../../src/Compliance.Common/Features/AuditorRequests/Models/AuditorRequestView.cs#L6) | Request, assignment, due state, responses, and disclosure state. |
| [ListAuditorRequests](../../src/Compliance.Common/Features/AuditorRequests/Queries/ListAuditorRequests.cs#L9) | `Page<[AuditorRequestSummaryView](../../src/Compliance.Common/Features/AuditorRequests/Models/AuditorRequestSummaryView.cs#L6)>` | Engagement-scoped requests and their current work status. |
| [GetSampleSelection](../../src/Compliance.Common/Features/AuditorRequests/Queries/GetSampleSelection.cs#L9) | [SampleSelectionView](../../src/Compliance.Common/Features/AuditorRequests/Models/SampleSelectionView.cs#L6) | Frozen selection method, population reference, selected rows, and manifest. |
| [ListRequestResponses](../../src/Compliance.Common/Features/AuditorRequests/Queries/ListRequestResponses.cs#L9) | `Page<[AuditorRequestResponseView](../../src/Compliance.Common/Features/AuditorRequests/Models/AuditorRequestResponseView.cs#L6)>` | Submitted responses and governed delivery/disposition receipts. |

### Audit packages — T1-05, T3-05

| Query | Response view | Required read |
| --- | --- | --- |
| [GetAuditPackage](../../src/Compliance.Common/Features/AuditPackages/Queries/GetAuditPackage.cs#L9) | [AuditPackageView](../../src/Compliance.Common/Features/AuditPackages/Models/AuditPackageView.cs#L6) | Package lifecycle, build result, validation, disclosure, and delivery state. |
| [ListAuditPackages](../../src/Compliance.Common/Features/AuditPackages/Queries/ListAuditPackages.cs#L9) | `Page<[AuditPackageSummaryView](../../src/Compliance.Common/Features/AuditPackages/Models/AuditPackageSummaryView.cs#L6)>` | Packages for the authorized engagement or period. |
| [GetAuditPackageManifest](../../src/Compliance.Common/Features/AuditPackages/Queries/GetAuditPackageManifest.cs#L9) | [AuditPackageManifestView](../../src/Compliance.Common/Features/AuditPackages/Models/AuditPackageManifestView.cs#L6) | Exact included records, versions, evidence references, and integrity metadata. |
| [GetAuditPackageValidation](../../src/Compliance.Common/Features/AuditPackages/Queries/GetAuditPackageValidation.cs#L9) | [AuditPackageValidationView](../../src/Compliance.Common/Features/AuditPackages/Models/AuditPackageValidationView.cs#L6) | Validation findings and any separately approved override. |
| [GetAuditPackageDelivery](../../src/Compliance.Common/Features/AuditPackages/Queries/GetAuditPackageDelivery.cs#L9) | [AuditPackageDeliveryView](../../src/Compliance.Common/Features/AuditPackages/Models/AuditPackageDeliveryView.cs#L6) | Attributed delivery attempt and recipient/provider receipt. |

### Audit observations and outcomes — T1-06/07, T3-04/06

| Query | Response view | Required read |
| --- | --- | --- |
| [ListAuditObservations](../../src/Compliance.Common/Features/Examinations/Queries/ListAuditObservations.cs#L9) | `Page<[AuditObservationView](../../src/Compliance.Common/Features/Examinations/Models/AuditObservationView.cs#L6)>` | Engagement/period observations, evidence references, and linked findings. |
| [GetAuditOutcome](../../src/Compliance.Common/Features/Examinations/Queries/GetAuditOutcome.cs#L9) | [AuditOutcomeView](../../src/Compliance.Common/Features/Examinations/Models/AuditOutcomeView.cs#L6) | Recorded outcome and its authorized, attributable basis. |
| [GetTypeIIPlan](../../src/Compliance.Common/Features/Examinations/Queries/GetTypeIIPlan.cs#L9) | [TypeIIPlanView](../../src/Compliance.Common/Features/Examinations/Models/TypeIIPlanView.cs#L6) | Proposed or approved period plan and decision history. |
| [GetManagementAssertion](../../src/Compliance.Common/Features/Examinations/Queries/GetManagementAssertion.cs#L9) | [ManagementAssertionView](../../src/Compliance.Common/Features/Examinations/Models/ManagementAssertionView.cs#L6) | Management-authored assertion and signature/status. |
| [GetExaminationSignOff](../../src/Compliance.Common/Features/Examinations/Queries/GetExaminationSignOff.cs#L9) | [ExaminationSignOffView](../../src/Compliance.Common/Features/Examinations/Models/ExaminationSignOffView.cs#L6) | Sign-off record, author, timestamp, and exact package/period references. |

### Observation periods and recurrence — T2-01/02/03

| Query | Response view | Required read |
| --- | --- | --- |
| [GetObservationPeriod](../../src/Compliance.Common/Features/Operations/Queries/GetObservationPeriod.cs#L9) | [ObservationPeriodView](../../src/Compliance.Common/Features/Operations/Models/ObservationPeriodView.cs#L6) | Period lifecycle, dates, and governing program. |
| [ListObservationPeriods](../../src/Compliance.Common/Features/Operations/Queries/ListObservationPeriods.cs#L9) | `Page<[ObservationPeriodSummaryView](../../src/Compliance.Common/Features/Operations/Models/ObservationPeriodSummaryView.cs#L6)>` | Program period history and current status. |
| [GetControlCalendar](../../src/Compliance.Common/Features/Operations/Queries/GetControlCalendar.cs#L9) | [ControlCalendarView](../../src/Compliance.Common/Features/Operations/Models/ControlCalendarView.cs#L6) | Due control occurrences over the requested range and their source schedules. |
| [ListMissingOccurrences](../../src/Compliance.Common/Features/Operations/Queries/ListMissingOccurrences.cs#L9) | `Page<[MissingOccurrenceView](../../src/Compliance.Common/Features/Operations/Models/MissingOccurrenceView.cs#L6)>` | Expected control occurrences with no accepted performance record. |

### Recurring governance — T2-06/09

| Query | Response view | Required read |
| --- | --- | --- |
| [ListDueGovernanceReviews](../../src/Compliance.Common/Features/Operations/Queries/ListDueGovernanceReviews.cs#L9) | `Page<[GovernanceReviewDueView](../../src/Compliance.Common/Features/Operations/Models/GovernanceReviewDueView.cs#L6)>` | Reviews due in the selected period, with source and owner. |
| [GetManagementReview](../../src/Compliance.Common/Features/Operations/Queries/GetManagementReview.cs#L9) | [ManagementReviewView](../../src/Compliance.Common/Features/Operations/Models/ManagementReviewView.cs#L6) | Management review record, inputs, participants, and completion state. |
| [ListManagementReviewActions](../../src/Compliance.Common/Features/Operations/Queries/ListManagementReviewActions.cs#L9) | `Page<[ManagementReviewActionView](../../src/Compliance.Common/Features/Operations/Models/ManagementReviewActionView.cs#L6)>` | Actions arising from a review and their ownership/status. |

### Changes and incidents — T2-07

| Query | Response view | Required read |
| --- | --- | --- |
| [GetSignificantChange](../../src/Compliance.Common/Features/Changes/Queries/GetSignificantChange.cs#L9) | [SignificantChangeView](../../src/Compliance.Common/Features/Changes/Models/SignificantChangeView.cs#L6) | Change record, impact assessment, and linked review work. |
| [ListSignificantChanges](../../src/Compliance.Common/Features/Changes/Queries/ListSignificantChanges.cs#L9) | `Page<[SignificantChangeSummaryView](../../src/Compliance.Common/Features/Changes/Models/SignificantChangeSummaryView.cs#L6)>` | Changes within the authorized program/period scope. |
| [GetComplianceIncident](../../src/Compliance.Common/Features/Incidents/Queries/GetComplianceIncident.cs#L9) | [ComplianceIncidentView](../../src/Compliance.Common/Features/Incidents/Models/ComplianceIncidentView.cs#L6) | Current incident chronology, assessment, resolution, and evidence references. |
| [ListComplianceIncidents](../../src/Compliance.Common/Features/Incidents/Queries/ListComplianceIncidents.cs#L9) | `Page<[ComplianceIncidentSummaryView](../../src/Compliance.Common/Features/Incidents/Models/ComplianceIncidentSummaryView.cs#L6)>` | Incidents within authorized program/period scope. |
| [GetComplianceImpactAssessment](../../src/Compliance.Common/Features/Incidents/Queries/GetComplianceImpactAssessment.cs#L9) | [ComplianceIncidentImpactAssessmentView](../../src/Compliance.Common/Features/Incidents/Models/ComplianceIncidentImpactAssessmentView.cs#L6) | Current assessed control, risk, evidence, and system-description impacts. |

### Period close and populations — T3-01/02

| Query | Response view | Required read |
| --- | --- | --- |
| [PreviewObservationPeriodClose](../../src/Compliance.Common/Features/AuditPopulations/Queries/PreviewObservationPeriodClose.cs#L9) | [ObservationPeriodClosePreview](../../src/Compliance.Common/Features/AuditPopulations/Models/ObservationPeriodClosePreview.cs#L6) | Completeness and unresolved-item report before freezing close. |
| [GetObservationPeriodClose](../../src/Compliance.Common/Features/AuditPopulations/Queries/GetObservationPeriodClose.cs#L9) | [ObservationPeriodCloseView](../../src/Compliance.Common/Features/AuditPopulations/Models/ObservationPeriodCloseView.cs#L6) | Frozen close manifest, verification, and amendment history. |
| [ListAuditPopulationDefinitions](../../src/Compliance.Common/Features/AuditPopulations/Queries/ListAuditPopulationDefinitions.cs#L9) | `Page<[AuditPopulationDefinitionView](../../src/Compliance.Common/Features/AuditPopulations/Models/AuditPopulationDefinitionView.cs#L6)>` | Authorized definitions and version state for the period. |
| [GetAuditPopulation](../../src/Compliance.Common/Features/AuditPopulations/Queries/GetAuditPopulation.cs#L9) | [AuditPopulationView](../../src/Compliance.Common/Features/AuditPopulations/Models/AuditPopulationView.cs#L6) | Frozen population metadata, source, and reconciliation state. |
| [ListAuditPopulationRows](../../src/Compliance.Common/Features/AuditPopulations/Queries/ListAuditPopulationRows.cs#L9) | `Page<[AuditPopulationRowView](../../src/Compliance.Common/Features/AuditPopulations/Models/AuditPopulationRowView.cs#L6)>` | Population members with classification and provenance. |
| [GetPopulationReconciliation](../../src/Compliance.Common/Features/AuditPopulations/Queries/GetPopulationReconciliation.cs#L9) | [PopulationReconciliationView](../../src/Compliance.Common/Features/AuditPopulations/Models/PopulationReconciliationView.cs#L6) | Reconciliation totals, exceptions, and resolution state. |

### Continuity — T3-07

| Query | Response view | Required read |
| --- | --- | --- |
| [PreviewProgramRollover](../../src/Compliance.Common/Features/Programs/Queries/PreviewProgramRollover.cs#L9) | [ProgramRolloverPreview](../../src/Compliance.Common/Features/Programs/Models/ProgramRolloverPreview.cs#L6) | Carry-forward candidates and unresolved dependencies before proposal. |
| [GetProgramRollover](../../src/Compliance.Common/Features/Programs/Queries/GetProgramRollover.cs#L9) | [ProgramRolloverView](../../src/Compliance.Common/Features/Programs/Models/ProgramRolloverView.cs#L6) | Rollover proposal, decision, and successor-period binding. |
| [GetNextPeriodPlan](../../src/Compliance.Common/Features/Programs/Queries/GetNextPeriodPlan.cs#L9) | [NextPeriodPlanView](../../src/Compliance.Common/Features/Programs/Models/NextPeriodPlanView.cs#L6) | Proposed successor-period dates and carry-forward plan. |
| [ListCarryForwardReferences](../../src/Compliance.Common/Features/Programs/Queries/ListCarryForwardReferences.cs#L9) | `Page<[CarryForwardReferenceView](../../src/Compliance.Common/Features/Programs/Models/CarryForwardReferenceView.cs#L6)>` | Source-to-successor references for carried work and records. |

### Firm portfolio and personal cross-client work — F1-02/05

| Query | Response view | Required read |
| --- | --- | --- |
| [ListAuthorizedClientPortfolio](../../src/Compliance.Common/Features/FirmPortfolio/Queries/ListAuthorizedClientPortfolio.cs#L9) | `Page<[AuthorizedClientSummaryView](../../src/Compliance.Common/Features/FirmPortfolio/Models/AuthorizedClientSummaryView.cs#L6)>` | Only client summaries within the actor's explicit portfolio authority. |
| [GetAuthorizedClientSummary](../../src/Compliance.Common/Features/FirmPortfolio/Queries/GetAuthorizedClientSummary.cs#L9) | [AuthorizedClientSummaryView](../../src/Compliance.Common/Features/FirmPortfolio/Models/AuthorizedClientSummaryView.cs#L6) | One authorized client's permitted portfolio summary. |
| [ListMyCrossClientWork](../../src/Compliance.Common/Features/FirmPortfolio/Queries/ListMyCrossClientWork.cs#L9) | `Page<[CrossClientWorkItemView](../../src/Compliance.Common/Features/FirmPortfolio/Models/CrossClientWorkItemView.cs#L6)>` | The actor's own eligible work across authorized clients, independently filtered per client. |

### Methodology templates — F1-04

| Query | Response view | Required read |
| --- | --- | --- |
| [ListMethodologyTemplates](../../src/Compliance.Common/Features/MethodologyTemplates/Queries/ListMethodologyTemplates.cs#L9) | `Page<[MethodologyTemplateSummaryView](../../src/Compliance.Common/Features/MethodologyTemplates/Models/MethodologyTemplateSummaryView.cs#L6)>` | Published templates visible to authorized tenant administrators. |
| [GetMethodologyTemplateVersion](../../src/Compliance.Common/Features/MethodologyTemplates/Queries/GetMethodologyTemplateVersion.cs#L9) | [MethodologyTemplateVersionView](../../src/Compliance.Common/Features/MethodologyTemplates/Models/MethodologyTemplateVersionView.cs#L6) | Immutable template version and provenance. |
| [PreviewMethodologyTemplateApplication](../../src/Compliance.Common/Features/MethodologyTemplates/Queries/PreviewMethodologyTemplateApplication.cs#L9) | [MethodologyTemplateApplicationPreview](../../src/Compliance.Common/Features/MethodologyTemplates/Models/MethodologyTemplateApplicationPreview.cs#L6) | Proposed additions/changes and conflicts before applying a template. |
| [GetTemplateApplication](../../src/Compliance.Common/Features/MethodologyTemplates/Queries/GetTemplateApplication.cs#L9) | [TemplateApplicationView](../../src/Compliance.Common/Features/MethodologyTemplates/Models/TemplateApplicationView.cs#L6) | Application record, source version, and created-record provenance. |

### Own-attest delivery and offboarding — F1-01/07/08

| Query | Response view | Required read |
| --- | --- | --- |
| [GetOwnAttestRecordPackage](../../src/Compliance.Common/Features/AttestRecords/Queries/GetOwnAttestRecordPackage.cs#L9) | [OwnAttestRecordPackageView](../../src/Compliance.Common/Features/AttestRecords/Models/OwnAttestRecordPackageView.cs#L6) | Required record package manifest and delivery readiness. |
| [GetOwnAttestRecordDelivery](../../src/Compliance.Common/Features/AttestRecords/Queries/GetOwnAttestRecordDelivery.cs#L9) | [OwnAttestRecordDeliveryView](../../src/Compliance.Common/Features/AttestRecords/Models/OwnAttestRecordDeliveryView.cs#L6) | Attributed delivery and verification receipt. |
| [ListOutstandingAttestDeliveryHolds](../../src/Compliance.Common/Features/AttestRecords/Queries/ListOutstandingAttestDeliveryHolds.cs#L9) | `Page<[AttestDeliveryHoldView](../../src/Compliance.Common/Features/AttestRecords/Models/AttestDeliveryHoldView.cs#L6)>` | Engagement holds still requiring governed delivery or release. |

## Explicitly excluded from this backfill

- T2-08 integration endpoints, collection runs, and reconciliation queries remain optional collection hypotheses; manual source completeness and reconciliation remain the requirement baseline.
- F1-03 staff capacity and assignment-conflict queries are excluded until that staffing-capacity feature is accepted.
- BO-01/02/03 organization handover, hosted billing, and add-on queries remain excluded while their policy decisions are unsettled.
- Direct external auditor account and identity administration queries are excluded; T1/T3 request and access workflows above do not imply a separate auditor account product.
- Privacy lifecycle query families are not enumerated because the required Privacy scope remains unresolved.

## Read-model rules

Every listed query must enforce tenant, program, permission, sensitive-field, and independence boundaries before returning rows, totals, cursor state, exports, or derived results. Cross-client portfolio and personal-work reads require explicit authority and independent per-client filtering. Preview responses are derived and non-authoritative; they must identify relevant source versions and be recomputed at the owning command boundary. Historical and reproducible results must retain or reference exact input versions. Do not create one generic view that crosses feature ownership or leaks fields unnecessary to its consumer.
