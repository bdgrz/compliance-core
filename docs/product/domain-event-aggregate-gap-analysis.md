# Domain event and aggregate gap analysis

Historical analysis dated 2026-10-22. Use the [2026-10-23 aggregate and reactor specification](audits/2026-10-23/README.md) for the current 103-owner, 418-contract inventory and current command/reactor routes.

Analysis date: 2026-10-22. Scope: all backend feature families, accepted product decisions, current C# contracts and aggregate behavior, and the live open GitHub issues. This is an analysis, not implementation acceptance.

The main gaps are missing lifecycle decisions and overly broad owners. Most of the event vocabulary already exists. Adding reducers to every declared event would incorrectly implement superseded workflows, while counting each aggregate class would conceal incomplete commands, migrations, and cross-record decisions.

## Source baseline

The analyzed checkout is branch `codex/issue-21-finding-aggregate-boundary`, commit `fc7b2543a1c591f8b2cd0787312f6af9e162b3f8`, plus six existing uncommitted Finding migration files. Those edits include a migration-aware `GetFindingHandler` and replay-scope checks. GitHub `develop` was `c0f7a66bb13e89e6dea1570931922106a00b6115` at readback. Findings about the new Finding boundary therefore describe local work, not merged delivery.

The complete row inventory is [aggregate-command-coverage.json](aggregate-command-coverage.json). The [coverage audit](aggregate-command-coverage-audit.md) and [boundary ledger](aggregate-boundary-ledger.md) remain supporting inventories. Independent source scans matched all 409 event declarations, 102 aggregate types, and the 409 reducer registrations to the current inventory. Request and route figures below come from that inventory with source checks on the reported gaps; its syntactic call graph is not a proof of executable workflow completeness.

| Measure | Current checkout | Meaning |
| --- | ---: | --- |
| Domain event contracts | 409 | Includes migration, audit, compatibility, and dormant contracts. |
| Emitted business aggregate facts | 381 | All have a reducer; this does not establish payload completeness or correct ownership. |
| Migration-control facts | 5 | Finding migration foundation. |
| Audit-only facts | 1 | `UserIdentityAuthenticated`; an aggregate reducer is not required. |
| Reducer-only compatibility facts | 2 | `SystemInstanceDeclared`, `TenantIdentityProviderTestedV2`. |
| Dormant facts with an accepted missing feature | 7 | Listed below; all seven are generic payload stubs. |
| Dormant superseded facts | 13 | Do not backfill their old behavior. |
| Aggregate types | 102 | Includes migration roots and a legacy replay adapter. |
| Aggregates with some registered mutation path | 101 | Does not establish that every producing operation is routed. |
| Request contracts | 649 | 647 Common contracts plus two Core contracts. |
| Registered Core/Common handler registrations | 591 | All have matching handler classes. |
| Active commands without handlers | 8 | Distinct from five deferred readiness-import commands. |
| Fact-producing operations without a registered request route | 37 | 20 unrouted operations, 16 direct executor paths, one legacy producer. |
| Read/reconstruction contracts without registered handlers | 44 | Includes superseded reads; do not implement them indiscriminately. |

No application behavior was changed and no tests were run for this analysis. Existing test files were inspected as source evidence; their presence is not a passing result.

## Findings that affect the manual workflow first

### Operating plan proposals have no recovery transition

**Confirmed missing decision and event; high confidence.** A pending plan prevents every new proposal in [ControlOperationsLedger.cs](../../src/Compliance.Core/Features/Operations/ControlOperationsLedger.cs#L151). Approval then rejects a proposal whose control version has been superseded in [ApproveControlOperatingPlanHandler.cs](../../src/Compliance.Core/Features/Operations/ApproveControlOperatingPlanHandler.cs#L36). The sequence “propose for control v1, approve control v2” leaves the old plan unapprovable and prevents its replacement. The only current plan transitions are proposal and approval.

R2-01 explicitly requires revision and schedule-revision behavior in [backlog.md](backlog.md#L3088). Add an attributed withdrawal or revision of an unapproved proposal. Candidate names are `ControlOperatingPlanProposalWithdrawn` or `ControlOperatingPlanProposalRevised`; these are proposed names, not accepted contracts. Preserve the rejected proposal, exact source version, expected revision, actor, and reason. A fabricated approval is not a recovery operation.

The natural boundary is a plan line for one control. Separating it from the program-wide operations ledger also requires explicit open-occurrence reassignment choreography. The recovery transition is useful independently of that migration.

### Campaign delivery and negative outcomes need retained dimensions

**Confirmed backlog gap; the state contract needs reconciliation.** R2-10 requires per-person delivery, failed or missing delivery, reminders, declined acknowledgement, and source failure to remain distinct ([requirements](backlog.md#L3782), [acceptance](backlog.md#L3814)). Live [#283](https://github.com/bdgrz/compliance-core/issues/283) still lists these as open. Accepted [M0-D12](decisions/m0-d12-policy-acknowledgement-training.md#L33), however, defines the current primary acknowledgement states and delegates reminders to shared work. Retained delivery/refusal/source outcomes may be separate dimensions rather than new primary states.

[PolicyDistributionCampaign.cs](../../src/Compliance.Core/Features/PolicyDistribution/PolicyDistributionCampaign.cs#L51) reduces launch, audience amendment, acknowledgement, completion, waiver, and close. Its [state calculation](../../src/Compliance.Core/Features/PolicyDistribution/PolicyDistributionCampaign.cs#L332) can produce only removed, acknowledged, completed, excepted, overdue, or pending. [CampaignParticipantView](../../src/Compliance.Common/Features/PolicyDistribution/CampaignParticipantView.cs) has no delivery, refusal, reminder, or source-failure history.

Proposed fact shapes are `CampaignMaterialDelivered`, `CampaignDeliveryFailed`, `PolicyAcknowledgementDeclined`, and `TrainingCompletionSourceFailed`. A final design may combine outcomes in typed records. Preserve campaign/person/material version, observed versus recorded time, recorder versus performer, source/evidence, delivery attempt identity, and failure or refusal reason. An email dispatch is not proof of acknowledgement, and missing delivery may be derived from the absence of a retained delivery fact. Existing weekly digest and escalation facts can provide reminder history; a campaign-specific reminder event or aggregate is not inherently missing. M0-D12 routes non-member work to the campaign owner and forbids emailing the non-member.

A campaign-participant owner is a strong partition candidate. Keep the campaign header and frozen audience identity separate from each person's decisions and delivery attempts. Reuse snapshot chunks; preserve the launch/close reconciliation invariant. Batch LMS completion review is also missing, but should reuse a governed import boundary rather than introduce an unreviewed parallel completion stream. Manual completion already exists.

### Evidence correction and replacement need journey proof

**Confirmed correction requirement; medium-confidence design question.** R2-03 requires historical links to survive correction and replacement and exposes a correction operation ([backlog](backlog.md#L3187)). [EvidenceIntake](../../src/Compliance.Core/Features/Evidence/EvidenceIntake.cs#L7) intentionally gives identical content one identity and preserves its initial metadata. [EvidenceSupport](../../src/Compliance.Core/Features/Evidence/EvidenceSupport.cs#L8) owns one immutable exact-version relationship and has only `EvidenceSupportLinked`.

Different bytes can already create a different artifact, and a new support relationship can already be added. Revising an owning record and linking new evidence to its exact successor revision may already satisfy replacement while old links remain historical. The unresolved case is invalidating an erroneous support assertion against the same exact target revision. If that decision is required, candidate facts are `EvidenceArtifactSuccessorLinked` and `EvidenceSupportSuperseded` or `EvidenceSupportWithdrawn`. They are not confirmed mandatory missing events. Prove the correction journey first and adopt explicit disposition semantics only where the existing immutable-link model cannot express the accepted outcome.

Keep content identity and original collection attribution immutable. Reuse the existing artifact and support owners; do not create another artifact aggregate or treat redaction as general correction. Priorities and ownership belong with R2-03 and the evidence governance work in [#285](https://github.com/bdgrz/compliance-core/issues/285).

### Accepted risk triggers are rejected by the current owner

**Confirmed incomplete behavior using an existing event; high confidence.** [M0-D10](decisions/m0-d10-risk-method.md#L12) requires significant changes, incidents, and new material vendors to trigger reassessment. [RiskGovernanceLedger.RaiseTrigger](../../src/Compliance.Core/Features/Risks/RiskGovernanceLedger.cs#L579) accepts only method and boundary changes. The current reactors cover those two kinds.

Extend `RiskReassessmentTriggered` and its source-backed command path; react idempotently to committed change, incident, and provider facts. Define which risks are affected and retain the exact source revision. This needs neither a new trigger aggregate nor a duplicate event family. Annual and acceptance-expiry status is already calculated by [RiskEvaluationStatus](../../src/Compliance.Core/Features/Risks/RiskEvaluationStatus.cs); elapsed time alone does not require a new `RiskAcceptanceExpired` event.

### Readiness facts do not yet supply the complete journey

**Delivery and source-rule gaps, not an absent readiness aggregate.** [ReadinessRules](../../src/Compliance.Core/Features/Readiness/ReadinessRules.cs#L16) explicitly limits current assessment coverage, including unassessed workforce completeness and evidence. The complete R2-09 input contract still needs source-by-source acceptance under [#489](https://github.com/bdgrz/compliance-core/issues/489).

[DecideTypeIEntryHandler](../../src/Compliance.Core/Features/Readiness/DecideTypeIEntryHandler.cs#L24) records a decision, while gap plans remain in `ReadinessLedger`; there is no committed-fact consumer turning required follow-up into shared accountable work. Reuse `ReadinessAssessmentRecorded`, `ReadinessGapPlanned`, `TypeIEntryDecisionRecorded`, findings, and work assignment. Add a link-back fact only where it is needed to prove exact idempotent creation. Keep unresolved rule policy with [#492](https://github.com/bdgrz/compliance-core/issues/492) and entry acceptance with [#297](https://github.com/bdgrz/compliance-core/issues/297).

## Missing domain behavior and proposed owners

### Received auditor observations and exceptions need one owner

**Confirmed missing received-exception record and behavior; proposed aggregate boundary.** [M0-D27](decisions/m0-d27-attest-scope.md#L27) and [T3-04](backlog.md#L5196) require an externally authored `AuditTestException` distinct from management interpretation. There is no implementation of that record in `src`. [AuditObservationRecorded](../../src/Compliance.Common/Features/Examinations/AuditObservationRecorded.cs) and [AuditObservationFindingLinked](../../src/Compliance.Common/Features/Examinations/AuditObservationFindingLinked.cs) exist only as generic contracts without producers, reducers, or an owning aggregate.

Use one received-observation/exception owner per stable external observation identity. The name can reconcile `AuditObservation` and `AuditTestException`; creating both as competing roots would duplicate authority. Preserve:

- Auditor wording, external author/source, received time, and correction lineage.
- Exact examination and Type I baseline or Type II close references, plus affected samples, frozen population rows, controls, evidence, changes, incidents, and risks.
- Management response separately from the external wording.
- Exact links to shared findings; remediation, risk acceptance, closure review, and reopen remain with those existing owners.

The two existing event stubs cannot replay these facts: they contain only tenant ID, subject ID, revision, optional member ID, and occurrence time. Evolve their payloads before wiring them. An externally received correction or a management response needs retained facts; proposed names include `AuditTestExceptionCorrectionRecorded` and `AuditTestExceptionManagementResponseRecorded` if those decisions are owned here. Names and wire evolution require the feature design.

Owning work: [#301](https://github.com/bdgrz/compliance-core/issues/301) and [#314](https://github.com/bdgrz/compliance-core/issues/314). Do not implement an auditor test engine, workpapers, or report drafting; M0-D27 explicitly excludes them.

### Own attest record packages need durable build and delivery authority

**Confirmed missing operational path and completion authority; a new aggregate is a design candidate.** [M0-D27](decisions/m0-d27-attest-scope.md#L47) requires an exact engagement record package and attributable delivery into the attest team's audit software before engagement close or offboarding. Common has dedicated own-attest requests and views; Core has serializer registrations but no dedicated handlers or package/delivery reader for them.

[ClientExport.RecordAttestPackageDelivery](../../src/Compliance.Core/Features/Exports/ClientExport.cs#L117) already records attributed delivered/not-delivered attempts bound to an engagement, package and manifest hash, and [final export delivery](../../src/Compliance.Core/Features/Exports/ClientExport.cs#L178) requires successful receipts for every declared required package. It also supports engagement-collaboration scope. Reuse or extend that authority where it fits. The absent behavior is source-complete package assembly and the production close/hold/offboarding gate, not the existence of every delivery primitive.

The four dormant contracts are:

| Existing contract | Missing retained meaning |
| --- | --- |
| `OwnAttestRecordPackagePrepared` | Engagement, exact request/response/population/sample/evidence versions, manifest/content identity, build intent and successful result. |
| `OwnAttestRecordDeliveryRecorded` | Exact package/manifest, recipient or destination, delivery reference, delivery time, and recorder attribution. |
| `OwnAttestRecordDeliveryVerified` | Exact recorded delivery and the verification evidence/authority. |
| `DeliveredEngagementHoldReleased` | Exact engagement hold, delivered package/version, attributable release basis, and expected source version. |

All four currently carry only the same five generic fields as the observation stubs. A producer cannot recover the missing content from those payloads.

A bounded `OwnAttestRecordPackage` lifecycle per package/build identity is one candidate, reusing the existing `AuditPackage` format and artifact storage. Extending the existing ClientExport/package authority is another. Extract a delivery receipt owner only if its independent decisions require that consistency boundary. Durable request/build-failure facts are needed if a build crosses process boundaries; reuse the existing package build lifecycle where appropriate instead of automatically adding another set of events.

There is an observable guard gap today: [CloseServiceEngagementHandler](../../src/Compliance.Core/Features/AccessControl/CloseServiceEngagementHandler.cs) calls the independence ledger without package/delivery evidence, and [CloseActualEngagement](../../src/Compliance.Core/Features/AccessControl/IndependenceLedger.Revocation.cs#L31) checks current sequence/acceptance but no record delivery. Add source-bound close eligibility and a recoverable completion decision. Existing assignment-revocation facts already express the closure; a new `ServiceEngagementClosed` event is not automatically necessary. Immediate revocation of access must remain possible even when record packaging is unfinished; distinguish that action from successful business close.

Owning work: [#545](https://github.com/bdgrz/compliance-core/issues/545), [#546](https://github.com/bdgrz/compliance-core/issues/546), service-engagement acceptance, and evidence governance. Advisory-note retention is separately policy-gated by [#346](https://github.com/bdgrz/compliance-core/issues/346).

### Offboarding has no terminal decision

**Confirmed missing lifecycle transition on an existing owner.** [Tenant](../../src/Compliance.Core/Features/Tenants/Tenant.cs#L234) can start offboarding. `TenantOffboardingCompleted` has no reducer/producer, and `CompleteTenantOffboarding` has no handler. The event is another five-field stub and cannot identify the export, attest-delivery, retained-content, hold, or approved disposition basis for completion.

Complete the terminal tenant transition using authoritative package/export and retention results. Keep bounded asynchronous execution state separate only if it needs an independent process identity; a second tenant aggregate is unnecessary. Completion must describe the approved offboarding outcome without pretending retained content has been deleted. Reuse existing `ClientExport`, `ArtifactRetention`, `ArtifactEngagementHold`, and `ArtifactDisposition` owners.

The existing retention assessment deliberately fails closed: [ArtifactRetention.Assess](../../src/Compliance.Core/Features/Retention/ArtifactRetention.cs#L152) always includes unavailable engagement-hold, historical-reliance, approval, and storage-enforcement blockers. Those authorities need composition; the engagement-hold aggregate itself already exists. Owning work: [#128](https://github.com/bdgrz/compliance-core/issues/128), [#545](https://github.com/bdgrz/compliance-core/issues/545), and [#546](https://github.com/bdgrz/compliance-core/issues/546).

## Aggregate boundaries hidden inside existing ledgers

These rows identify independently changing records currently sharing broad command state. Proposed names describe responsibilities, not a requirement to create that exact set of classes. Every split needs its atomic invariant, stream identity, command cutover, and migration proof before acceptance under [#1022](https://github.com/bdgrz/compliance-core/issues/1022).

| Current owner | Proposed tighter ownership | Invariant and migration constraint |
| --- | --- | --- |
| [RemediationLedger](../../src/Compliance.Core/Features/Remediation/RemediationLedger.cs) | One `FindingAggregate` per finding. **Confirmed correction; implementation in progress locally.** | Preserve all seven finding facts, source lineage, corrective-action decisions, and exact acceptance/closure history. Fence source writers and cut over every command, reader, work consumer, and authorization read. The new root is present; it is not missing. |
| [AccessReviewCampaign](../../src/Compliance.Core/Features/AccessReviews/AccessReviewCampaign.cs) | Campaign header plus per-item review owner. **Confirmed split candidate.** | Frozen item identity, reviewer authority, decision, provider change, verification/exception, and campaign completion. A read-model total alone cannot authorize close. |
| [ControlOperationsLedger](../../src/Compliance.Core/Features/Operations/ControlOperationsLedger.cs) | Plan line per control; occurrence per occurrence ID. | Approved effective plan/version, exact attestation/review rounds, deterministic schedule identities, and explicit open-work reassignment. |
| [ControlEvaluationLedger](../../src/Compliance.Core/Features/Evaluations/ControlEvaluationLedger.cs) | One control evaluation. | Frozen evaluation procedure and control version, review independence, deviations, retest, and exact round references. `ControlEvaluationPlan` already exists. |
| [RiskGovernanceLedger](../../src/Compliance.Core/Features/Risks/RiskGovernanceLedger.cs) | Governance and treatment per risk. | Owner, acceptance, actions, independent completion review, and source-bound reassessment. `RiskDraft`, `RiskEvaluation`, and `RiskMethod` already exist. |
| [ReadinessLedger](../../src/Compliance.Core/Features/Readiness/ReadinessLedger.cs) | Immutable assessment and stable gap plan; narrow program entry gate. | Exact reproducible inputs, current assessment selection, carried-forward gap identity, and the program-wide initial Type I entry invariant. Do not independently split entry decisions and lose that invariant. |
| [EvidenceRequestLedger](../../src/Compliance.Core/Features/Evidence/EvidenceRequestLedger.cs) | One evidence request. | Per-request concurrency, owner/due/support, and attributed terminal fulfillment/cancellation. Program listing belongs in a projection. |
| [ApplicationImportLedger](../../src/Compliance.Core/Features/Applications/ApplicationImportLedger.cs) | Execution per batch, with narrow source acceptance/claim authority. | One accepting batch per source, stable source claims, frozen plan, complete target effect proof, and atomic visibility of committed results. `ImportBatch` already owns staging. Relocating emitters alone breaks the current invariant. |
| [ProviderRegister](../../src/Compliance.Core/Features/Providers/ProviderRegister.cs) | One provider plus explicit active-name reservation if required. | Stable provider identity and tenant-wide unique active name. Current creation/revision facts remain useful. |
| [ProviderAssuranceRegister](../../src/Compliance.Core/Features/Providers/ProviderAssuranceRegister.cs) | Assurance report and coverage-gap lifecycles; attributable immutable reviews. | Exact report/provider/evidence version, risk links, and review cadence. A 200-record limit per provider does not bound a tenant-wide register. |
| [AccessReviewSystemLedger](../../src/Compliance.Core/Features/AccessReviews/AccessReviewSystemLedger.cs) | Expectation lineage and individual approved exceptions. | Same-system scope, supersession, overlap rules, approval, and expiry; retain a narrow conflict authority if overlap must be atomic. |
| [ControlCriterionMappingLedger](../../src/Compliance.Core/Features/ControlMappings/ControlCriterionMappingLedger.cs) | Mapping per control/edition/criterion. | Deterministic uniqueness, exact reviewed version, supersession, and coverage projection. |
| [CriterionApplicabilityLedger](../../src/Compliance.Core/Features/ControlMappings/CriterionApplicabilityLedger.cs) | Applicability decision per edition/criterion. | Only reviewed current decisions affect coverage; retain withdrawn and superseded history. |
| [TrainingCatalog](../../src/Compliance.Core/Features/PolicyDistribution/TrainingCatalog.cs) | One training requirement. | Versioned requirement identity, exact material/course version and source; program index and any name reservation remain explicit. |
| [LocationRegister](../../src/Compliance.Core/Features/TechnologyInventory/LocationRegister.cs), [OperationalProcessRegister](../../src/Compliance.Core/Features/TechnologyInventory/OperationalProcessRegister.cs) | Individual location and process records. | Stable IDs, active-name uniqueness where required, and historical boundary references. |
| [PolicyDistributionCampaign](../../src/Compliance.Core/Features/PolicyDistribution/PolicyDistributionCampaign.cs) | Header/frozen audience plus participant lifecycle. | Exact version/person attribution, audience amendments, delivery/completion/exception history, and reconciled close. |
| [AccessPopulation](../../src/Compliance.Core/Features/AccessReviews/AccessPopulation.cs), [AuditPopulation](../../src/Compliance.Core/Features/AuditPopulations/AuditPopulation.cs) | Immutable chunked population plus independently changing row decisions where necessary. | Frozen completeness/content identity, exact classification/reconciliation, amendments, and stable sample references. These roots already exist. |
| [TypeIIObservationCalendar](../../src/Compliance.Core/Features/Operations/TypeIIObservationCalendar.cs) | Retain only narrow reservation authority. | Non-overlap is a real cross-period invariant; `TypeIIPeriod` already owns an individual period. Prove reservation retention and amendment/cancel recovery rather than inventing another period root. |

## Collection growth and payload bounds

The direct-field source scan found collections in **50 of 102 roots**. This is not a count of unsafe roots: collections can have legitimate finite domain bounds, while a root with one `_current` field can hide large nested snapshots. [AuditPackage](../../src/Compliance.Core/Features/AuditPackages/AuditPackage.cs), [AuditorRequest](../../src/Compliance.Core/Features/AuditorRequests/AuditorRequest.cs), and `AuditPopulation` require payload review even though their collections can be nested.

The 50 directly detected roots divide into these disjoint review groups:

| Group | Count | Disposition |
| --- | ---: | --- |
| First correction and split candidate | 2 | `RemediationLedger`, `AccessReviewCampaign`. |
| Other ledger/population/calendar candidates | 17 | The corresponding roots in the boundary table, excluding nested-snapshot `AuditPopulation`. |
| Replay-only adapter | 1 | `LegacyWorkAssignmentLedger`; finish migration and remove runtime consumers. |
| Direct-collection guards found | 5 | `EvidenceRedaction`, `FindingAggregate`, `ExaminationEngagement`, `ImportBatch`, `TypeIIPeriod`. These guards are useful evidence, not complete safety proofs. |
| Remaining history and total-bound reviews | 25 | Listed below. |

The remaining 25 are `AdvisorHandoff`, `ArtifactRetention`, `CommitmentDraft`, `ComplianceProgram`, `ControlDraft`, `ControlEvaluationPlan`, `CriteriaTextOverlayLedger`, `DeclaredApplication`, `FirmProfessionalDutyCatalog`, `FirmStaffDirectory`, `IndependenceLedger`, `IndependenceRuleCatalog`, `IndependenceRuleRatificationCatalog`, `PlatformOperatorRoster`, `Policy`, `RiskEvaluation`, `RiskMethod`, `SystemBoundary`, `SystemDescription`, `SystemInstanceAccessReviewScope`, `Tenant`, `TenantManagerGuard`, `WeeklyWorkDigest`, `WorkDigestDispatch`, and `WorkItemAssignment`.

Resolve each using a domain bound or a history/read-side partition. In particular:

- `AdvisorHandoff` intentionally keeps feedback admission atomic with revocation. A feedback-thread extraction needs an authoritative admission fence; splitting by noun alone would weaken that behavior.
- `IndependenceLedger` serializes client-wide service and professional independence decisions. Extract historical records and independently changing identities only after retaining the cross-engagement exclusion/sequence authority.
- `FirmStaffDirectory`'s 1,000-member cap and the rule catalogs' 100-entry caps do not by themselves bound request receipts, decisions, and assertion histories.
- `TenantIdentityProvider` also needs lifetime review: its 100-test limit and credential revision cap of 100 bound stored history by eventually refusing legitimate future operations. Decide whether these are accepted product limits or move historical attempts from connection command state.
- `WorkDigestDispatch` grows across dates; partition dispatch identity or prove a finite retained scheduling window. `WorkItemAssignment` exists and should retain only the state needed for future assignment decisions; history belongs in a fold/projection.
- Check serialized bytes with all collection and string dimensions combined. Per-field maxima or row counts alone do not establish that an event fits the configured Fitz frame.

No arbitrary new business cap should be introduced merely to retain a broad stream. The full current identity/invariant index remains in the [boundary ledger](aggregate-boundary-ledger.md#full-root-identity-and-invariant-index).

## Existing facts with missing command paths or consumers

These gaps are material to delivery but must not inflate the missing-event or missing-aggregate totals.

| Capability | Existing source | Remaining behavior |
| --- | --- | --- |
| Finding cutover | `FindingAggregate` plus migration/fence roots; seven ordinary producers remain unrouted. | Cut over production finding commands and all consumers after exact parity. Local `GetFinding` switches only on recorded migration completion. |
| Artifact disposition | `ArtifactDisposition.Approve`, `RequestExecution`. | Register authorized command handlers and compose current hold/reliance/storage authority under #285. |
| Client export | `ClientExport.Request`, `RecordAttestPackageDelivery`. | Authorized request and exact attest-delivery read/record paths under #545. |
| Type II due generation | [TypeIIPeriod](../../src/Compliance.Core/Features/Operations/TypeIIPeriod.cs#L141) emits `DueControlOccurrencesGenerated`. | No current Core materialization consumer converts the durable generated plan into authoritative occurrence records. Preserve deterministic IDs and replay-safe creation. |
| Period close | `TypeIIPeriod.RecordCloseValidation`, `AmendClose`. | Validation has no caller/request; amendment lacks its handler. Build close reconciliation from exact source versions and incomplete/missing material, then authorize/freeze under #310. |
| Type II plan and assertion revision | `TypeIIPlan.Revise`, `ManagementAssertion.Revise`. | Supply registered revision routes under #303; use existing facts. |
| Template application/contribution | `MethodologyTemplate.ReviseDraftWithAcceptedContribution`, `TemplateApplication.RecordDraftCreated`. | Connect reviewed contribution/application effects under #276. |
| Next-period continuity | `ProgramRollover.Revise`, `RecordNextPeriodCreated`. | Authorized selection revision, deterministic period creation, and source-bound result recording under #316. |
| Package build progress | `AuditPackage.RecordBuildProgress`. | Trusted build callback/request route under #302. |
| Email/invitation results | Four EmailAddress callbacks and two TenantInvitation callbacks execute aggregates directly. | They can already persist facts; make the internal request/authority seam explicit if required by #1022. They are not missing emitters. |
| Digest execution | Ten WorkDigestDispatch operations execute directly from its processor. | Same distinction: explicit registered reaction paths versus already emitted facts. |
| Incident follow-up | [ComplianceIncident.Resolve](../../src/Compliance.Core/Features/Incidents/ComplianceIncident.cs#L203) compares caller-supplied completed IDs with assessment IDs. | Authoritative same-tenant/program ownership and completion proof is absent from [the handler](../../src/Compliance.Core/Features/Incidents/RecordComplianceIncidentResolutionHandler.cs). Existing `IncidentResolutionRecorded` can retain the validated decision; a new completion event is not inherently required. Add exact work linkage only where the choreography needs it. |
| Program Type II stage | Program directory reacts to Type I entry but not `ObservationPeriodStarted`. | Define and project the accepted Type II stage transition using the existing period fact. |

There are **20 operations with no registered request route**: the first nine rows above contribute 13 outside the Finding cutover; the seven Finding producers contribute the other seven. There are additionally **16 direct executor operations** and **one legacy `ImportBatch.Cancel` producer**, giving the inventory total of 37. Direct executor use is a composition/authority concern, not proof that those events cannot be emitted.

The eight active command contracts without handlers are `AmendTypeIIPeriodClose`, `ApproveArtifactDisposition`, `ExecuteArtifactDisposition`, `RequestClientExport`, `RequestOwnAttestRecordPackage`, `RecordOwnAttestRecordPackageResult`, `RecordOwnAttestRecordDelivery`, and `CompleteTenantOffboarding`.

## Coverage across the product

Every feature family was reconciled with current source. “Present” below means the domain owner/vocabulary exists; it does not claim complete HTTP/MCP behavior, current CI, merged delivery, or production acceptance.

| Domain family | Present ownership | Gap disposition |
| --- | --- | --- |
| Tenant, account, sign-in, membership and RBAC | Tenant/owner/slug/invitation/provider, UserIdentity/EmailAddress, Member, grant/role/permission/team edges, operator roster/manager guard. | Terminal offboarding missing; handover policy and history bounds remain. Do not add Organization/PlatformUser roots merely to duplicate these owners. |
| Firm services and independence | IndependenceLedger, staff/professional-duty/rule/ratification catalogs; retained service mutations, evaluations and assignment/revocation facts. | Own-attest close eligibility and broad histories; preserve client-wide independence authority. A separate ServiceEngagement class is not proof of a missing domain lifecycle. |
| Program, service, criteria and boundary | ComplianceProgram, ClientService, SystemBoundary, CriteriaTextOverlayLedger, shared immutable catalogs/snapshots. | Version-history bounds and later stage composition; no absent basic program or boundary root. |
| Application and system instance | DeclaredApplication, DeclaredSystemInstance; application relationship and scope decisions. | Import partitioning, relationship/history bounds, and legacy instance cutover. Current local instance owner already exists. |
| Workforce and governed nonhuman identities | Person, WorkRelationship, ServiceIdentity, WorkforceSourceObservation/Resolution. | Wider source integration remains separate; no reason to create a competing employee or principal aggregate. |
| Technology, information and commitments | TechnologyComponent, InformationAsset, DataFlow, Location/Process registers, CommitmentDraft. | Item boundaries for registers and version/responsibility history review. Device/provider graph concepts are not automatically missing roots. |
| Controls and mappings | ControlDraft, ControlCriterionMappingLedger, CriterionApplicabilityLedger. | Mapping/applicability partition and total version/decision bounds. |
| Operating plans and occurrences | ControlOperationsLedger. | Proposal recovery and plan/occurrence split; Type II generation consumer missing. |
| Evaluations | ControlEvaluationPlan and ControlEvaluationLedger. | Per-evaluation owner and procedure/version history. |
| Policy and training | Policy, PolicyDistributionCampaign, TrainingCatalog. | Delivery/refusal/source outcomes, shared reminder integration, participant and requirement boundaries, reviewed batch LMS intake. |
| Access governance | AccessPopulation, AccessReviewCampaign, AccessReviewSystemLedger, SystemInstanceAccessReviewScope. | Per-item decisions, expectation/exception identity and population size. Manual access graph facts already live in AccessPopulation; EffectiveAccess is derived. |
| Risks | RiskDraft, RiskEvaluation, RiskMethod, RiskGovernanceLedger. | Accepted trigger kinds, per-risk governance, history bounds. |
| Providers | ProviderRegister, ProviderAssuranceRegister. | Individual provider/report/gap ownership. Provider retirement is explicitly later scope in [provider-register-v1](provider-register-v1.md), not a current V1 contract omission. |
| Evidence, retention and disclosure | Artifact/support/redaction, handling policy, retention, engagement hold, disclosure and disposition. | Correction lineage and authority/execution composition; these roots already exist. |
| Findings and accountable work | RemediationLedger; local per-finding foundation; WorkItemAssignment and digest owners. | Full finding migration/cutover, shared follow-up choreography, dispatch/history bounds. No generic duplicate task root is needed. |
| Advisor collaboration | AdvisorHandoff owns selection, access and feedback. | Feedback growth versus atomic revocation/admission; no absent basic handoff/feedback facts. |
| Readiness and Type I entry | ReadinessLedger, assessment/plan/annotation/entry facts. | Source-rule completeness, stable gap plans and work creation; retain narrow program entry decision. |
| Examination setup and description | ExaminationEngagement/baseline, SystemDescription. | Many reads remain unwired; owners and rich lifecycle facts are already implemented. |
| Auditor requests, populations and packages | AuditorRequest, AuditPopulation, AuditPackage. | Missing reads/composition and payload review. Received observations/exceptions remain a separate absent owner. |
| Management assertion, letter and outcome | ManagementAssertion, ManagementRepresentationLetter, AuditOutcome, ExaminationSignOff, TypeIIPlan. | Revision routes, exact source authority and full delivery sequence. Do not invent auditor opinion calculations. |
| Type II calendar, close and rollover | TypeIIPeriod, TypeIIObservationCalendar, ProgramRollover. | Generation materialization, source-backed close validation, amend/read paths, rollover effects. |
| Templates and imports | MethodologyTemplate/Contribution, TemplateApplication, ImportBatch/ApplicationImportLedger. | Accepted contribution/application callbacks; source/batch boundaries; readiness-material import remains deferred. |
| Export, own-attest records and offboarding | ClientExport, including attest delivery receipts, and tenant start lifecycle. | Source-complete own-attest package and delivery/hold/close composition; terminal offboarding decision missing. |
| Hosted business operations | Organization identity and access foundation only. | Handover, subscription, billing administration, offering and add-on entitlement lifecycle remain absent and policy-gated as below. |

## Deferred scope and facts that should not be backfilled

### Commercial and organization handover

The [business operations contract](business-operations.md) defines accepted direction, while [its readiness decisions](business-operations.md#readiness-decisions) leave consent, billing authority, payer, commercial terms, lifecycle restrictions, and add-on activation rules unresolved. Live work is [#639](https://github.com/bdgrz/compliance-core/issues/639), [#640](https://github.com/bdgrz/compliance-core/issues/640), [#641](https://github.com/bdgrz/compliance-core/issues/641), with policy decisions [#642](https://github.com/bdgrz/compliance-core/issues/642)–[#645](https://github.com/bdgrz/compliance-core/issues/645).

Likely future owners are an attributed organization handover decision, a per-organization hosted subscription, versioned offering/terms, and per-organization add-on entitlement/activation. Candidate facts include handover acceptance, subscription commitment/change/cancellation, payment or invoice outcomes, add-on selection, activation/failure, and disablement. Exact names and aggregate boundaries remain proposals until those policies are adopted. They gate paid hosted delivery, not the manual compliance release. Existing RBAC grants may be sufficient for much of delegation; purchase must not grant professional practice authority.

### Other deliberate exclusions

- Five readiness-material import commands and eight candidate import facts remain deferred under M0-D04. Reuse or generalize the existing governed import lifecycle before choosing another stream family.
- Provider connectors/agents, external-auditor direct access, firm capacity planning, and Privacy lifecycle remain separately scoped or policy-gated. Privacy support limitations are already recorded in readiness; disclosure does not implement Privacy workflows.
- Periodic policy/risk/provider/management reviews reuse ordinary control occurrences and shared findings under M0-D19. Do not create a separate review aggregate for each noun.
- The canonical entity catalog explicitly does not prescribe aggregate roots. Account, Group, Role, Entitlement, external identifiers, correlation, and graph results can be snapshot facts, embedded values, observations, or projections with an existing owner.
- Elapsed due dates, overdue state, annual review status, and effective access can be derived. Create an event when a decision or observed fact must be retained, not just because a status changes with the clock.

The 13 dormant superseded contracts are `FutureControlScheduleRevised`, `MissingOccurrenceRecorded`, `ObservationPeriodEnded`, `NextObservationPeriodStarted`, `OpenRemediationCarriedForward`, the three `TypeIIOperatingPlan*` contracts, the four `GovernanceReviewScheduled`/`ManagementReview*` contracts, and `IndependenceRuleVersionRatified`. Their accepted replacements are documented in the [coverage audit](aggregate-command-coverage-audit.md#superseded-or-deferred-contracts). Inspect retained stores before removing contracts; migrate any historical usage first.

## Migration and acceptance requirements

For every new owner or split, make the identity and atomic decision explicit before moving event code. Preserve tenant/program scope, business revision, physical source position, event ID/type/time/payload lineage, and stable request identity. A projection may index facts but cannot create authority for a consequential decision.

Use the Finding migration pattern as a local starting point: fence/drain the old writer, import one source fact at a time with bounded progress, verify exact source-to-target parity, and use a fresh projection identity when event areas change. Cut over every writer, history/read consumer, source contributor, and authorization reader before pruning a legacy reducer or adapter. Counts alone do not prove lineage or parity.

The existing explicit ownership exceptions also need disposition: seven dual-replay finding facts; `RemediationPartitioningStarted`; import cancellation in batch/source roots; work assignment in current/legacy roots; inventory revision collision sentinels; and shared responsibility facts. Preserve deliberate collision guards separately from state-owning reducers. Shared vocabulary is acceptable only when scope and stream identity select one owner unambiguously.

An implementation slice should prove the invalid and stale command paths, personal HTTP-only authority where applicable, tenant isolation, idempotent retry, replay, concurrent decisions, partial migration/effect failure, and the intended correction or recovery path. Follow the repository's focused failing test, implementation, full unit suite, exact PR-head review and CI sequence. External effects need durable intent and observed completion; unknown delivery outcomes cannot be blindly retried.

## Recommended delivery order

1. Finish and independently review the current Finding migration/cutover under #1022, preserving the local work already present.
2. Deliver operating-plan proposal recovery and campaign delivery/negative outcomes as separate bounded slices; agree their event payloads and exact owners first.
3. Prove the evidence correction/replacement journey and complete missing source-backed risk/readiness/work choreography, reusing current event vocabulary where it already represents the decision.
4. Partition the highest-growth operational ledgers one invariant at a time: access-review items, plans/occurrences, evaluations, evidence requests, risk governance and readiness gap plans. Source/batch imports require their own acceptance/visibility design.
5. Deliver received auditor observations/exceptions and own-attest record-package authority with their exact baseline/engagement prerequisites. Connect close eligibility, export, retention and offboarding completion.
6. Finish Type II generation/close/rollover and template callbacks plus their read contracts; keep commercial and other policy-gated families in their own selected queue.

This is a proposed sequence within the relevant product horizons. Live Project 2 readiness, prerequisite acceptance, and one-task WIP still govern selection; an open parent issue or a class existing in source does not settle readiness.

## Unregistered read and reconstruction contracts

The current inventory contains these 44 contracts without registered handlers. Some are obsolete vocabulary; reconcile each with its current workflow before implementing or removing it.

`GetAuditOutcome`, `GetAuditPackage`, `GetAuditPackageDelivery`, `GetAuditPackageManifest`, `GetAuditPackageValidation`, `GetAuditPopulation`, `GetAuditorRequest`, `GetAuthorizedClientSummary`, `GetControlCalendar`, `GetExaminationSignOff`, `GetManagementAssertion`, `GetManagementReview`, `GetMethodologyTemplateVersion`, `GetNextPeriodPlan`, `GetObservationPeriod`, `GetObservationPeriodClose`, `GetOwnAttestRecordDelivery`, `GetOwnAttestRecordPackage`, `GetPopulationReconciliation`, `GetProgramRollover`, `GetSampleSelection`, `GetSystemDescription`, `GetTemplateApplication`, `GetTypeIIPlan`, `ListAuditObservations`, `ListAuditPackages`, `ListAuditPopulationDefinitions`, `ListAuditPopulationRows`, `ListAuditorRequests`, `ListAuthorizedClientPortfolio`, `ListCarryForwardReferences`, `ListDueGovernanceReviews`, `ListManagementReviewActions`, `ListMethodologyTemplates`, `ListMissingOccurrences`, `ListMyCrossClientWork`, `ListObservationPeriods`, `ListOutstandingAttestDeliveryHolds`, `ListRequestResponses`, `ListSystemDescriptionVersions`, `PreviewMethodologyTemplateApplication`, `PreviewObservationPeriodClose`, `PreviewProgramRollover`, and `PreviewSystemDescriptionImpact`.

## Documentation reconciliation

[domain-event-list.md](domain-event-list.md) still states 404 events and presents historical backfill families. [#1022](https://github.com/bdgrz/compliance-core/issues/1022) describes the merged 404-event/99-root baseline. The local 409-event/102-root inventory includes five migration facts and the three roots added for the Finding boundary/migration foundation. Those numbers describe different source states; they are not evidence of five missing business events.

[command-inventory-gap-analysis.md](command-inventory-gap-analysis.md) contains historical missing-declaration claims for owners now present, including system description, examination/Type II and package/population behavior. [aggregate-coverage-checklist.md](aggregate-coverage-checklist.md) records owner-level work and cannot prove every producer route, read handler, payload invariant, or recovery transition. Keep each source snapshot explicit when updating these inventories. This report adds the lifecycle and product-flow gaps that declaration matching cannot discover.
