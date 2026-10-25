# Remaining owner and reactor inventory

Source snapshot: `8743b1b9229ca5ab9f2e21b65dd3e3525d9cb479` (`ref: refs/heads/fix/1022-all-source-receipts-20261024`), plus current worktree files. This is a read-only static inventory; queued artifacts are not merged or runtime-validated by this report. Exact owner hashes, source references, actual producers/consumers and selected command/reactor matches are in the JSON.

The AST found **127 direct Aggregate classes**, with 54 holding nonstatic collection fields. After separating assigned overlays, current configuration, legacy adapters and bounded transactions, **44 unassigned owners still retain history in command state**, and **13 more retain legacy raw current content/full-State payloads**. These are representation findings, not 44 claims of unlimited business lifetimes; some domains impose explicit caps.

## Smallest independent next verticals

1. **principal-classification** — AccessPrincipalClassification. High: small native compaction; replace all-decision command state with revision/latest ref and selected native acceptance. Move AccessClassifications history reconstruction to verified event reader. ClassifyAccessPrincipal already publishes immutable content; no new protocol is needed. Preserve source receipt/digest path and current restricted visibility.
   Evidence: `src/Compliance.Core/Features/AccessReviews/AccessPrincipalClassification.cs:26` `readonly List<AccessPrincipalClassified> _decisions = [];`.
2. **criteria-overlay** — CriteriaTextOverlay. High: small native compaction, latest revision/ref plus immutable historical source reader. Preserve exact licensed overlay bytes/order/digest, current license gate, source receipt prefix, catalog projection checkpoints.
   Evidence: `src/Compliance.Core/Features/Criteria/CriteriaTextOverlay.cs:28` `readonly List<CriteriaTextOverlayEntryRevised> _revisions = [];`.
3. **training-requirement** — TrainingRequirement. High: small native compaction across Define/Revise, Get/List/LaunchTrainingCampaign and exact version source capture. Keep identifier/program in current authority and independently verify requested historical fact/content; no live source permit is manufactured.
   Evidence: `src/Compliance.Core/Features/PolicyDistribution/TrainingRequirement.cs:22` `readonly List<TrainingRequirementDefined> _versions = [];`.
4. **evaluation-plan** — ControlEvaluationPlan. High: one legacy Define producer and three verified-version consumers; native compact owner + history reader + migrated facts/results. ControlReader already supplies source version. Current Control held-use guarantee remains selected-contract gap; it does not block exact immutable historical plan content/native replay work.
   Evidence: `src/Compliance.Core/Features/Evaluations/ControlEvaluationPlan.cs:14` `readonly List<ControlEvaluationPlanVersionView> _versions = [];`.
5. **system-instance-scope** — SystemInstanceAccessReviewScope. High: one legacy personal Decide producer/Get read and coverage consumer; compact effective-date authority plus immutable decision index. Preserve exact instance revision/SoD waiver, monotonic effective dates, original actor/intent/revision replay before mutable source checks; no new routing contract.
   Evidence: `src/Compliance.Core/Features/Applications/SystemInstanceAccessReviewScope.cs:16` `readonly List<AccessReviewScopeDecisionView> _decisions = [];`.
6. **risk-method** — RiskMethod. Medium: one Publish producer, Get/GetVersion, risk assessment/acceptance/readiness source consumers. Risk109 does not include this owner. Queue after Risk109 if consumer files overlap; same body/digest and exact method version.
   Evidence: `src/Compliance.Core/Features/Risks/RiskMethod.cs:14` `readonly List<RiskMethodVersionView> _versions = [];`.
7. **program-criteria-selection** — ComplianceProgram. Medium: compact criteria selection history plus native Create/Revise/Select producers and historical selection reader. Current Program is mostly scalar; preserve as-of selection/receipt semantics rather than rewriting history into current authority.
   Evidence: `src/Compliance.Core/Features/Programs/ComplianceProgram.cs:14` `readonly List<(long Revision, Uuid EditionId, DateTimeOffset ChangedAt)> _criteriaEditionHistory = [];`.

## Remaining coherent slices

| Slice | Actual owning classes | Constraint |
|---|---|---|
| work-accountability | WorkItemAssignment | LegacyWorkAssignmentLedger is read-only importer, not a new active program-wide writer. Projection identity packages changed source-derived Work IDs; old assignments/URLs require explicit qualification, no fabricated aliases. |
| mapping-applicability | ControlCriterionMapping, CriterionApplicability | Identity stage fixes scope projection/Work keys but do not compact _facts/_versions. Preserve immutable line identity, independent review, selected source versions and receipt prefix. |
| control-operation-lines | ControlOperatingPlan, ControlOccurrence | Use exact selected plan/attestation/review event history reader. Do not redesign canonical window identity or range reservation protocol; Calendar28 is independently queued. |
| evidence-request | EvidenceRequest | Preserve exact use reservations and current request/source disclosure; generic SourceUseReservations compaction is parent-owned. |
| corrective-actions-findings | CorrectiveAction, FindingAggregate | Preserve existing FindingClosureProcess, exact evidence+action proof set, abort/fence and lost-response forward recovery; other generic reservations excluded. |
| risk-governance-treatment | RiskGovernance, RiskTreatmentAssertion, RiskTreatmentAction | Actual owner lists/dictionaries remain outside Risk109; preserve accepted action review process/evidence holds and trigger dedup source IDs. |
| readiness | ProgramReadiness, ReadinessGapPlan | ReadinessAssessment is already one-fact native and not new compaction work. Keep ReadinessRun/Decision process proof/settlement/cancellation. |
| advisor | AdvisorHandoff, AdvisorFeedbackThread | Previously fixed scoped feedback projection identity is separate. Preserve existing child intents/retries and exact artifact/handoff version; protected download remains separately fenced. |
| auditor-requests | AuditorRequest, AuditorResponse | AuditorFeedbackThread and AuditorSampleSelection are already single-fact native; do not split them again. Preserve full resolution source-use proof set. |
| access-review-campaigns | AccessExpectation, AccessReviewCampaign, AccessReviewItem | Root is currently validating receipt/causal-proof correction, which is distinct from owner compaction. Per-item CommandId scope must remain; preserve whole frozen bulk selection/hold/provisional/publication/fence protocol. |
| policy-communication | PolicyDistributionCampaign, PolicyCampaignParticipant | Native Policy itself already compact; PolicyCampaign audience/source receipt and causal admission proof logic is current source. Do not drop recurring waiver/admission histories from reads. |
| package-manifest | AuditPackageManifest | S29 package/delivery/build producers are already native, source bytes published and verified; history compaction remains. AuditPackageDelivery one fact is not a lifetime owner gap. |
| application-import-source | ApplicationImportSource | Batch transaction separately classified finite. Preserve verified actual physical import marker/effect proof closure, cancellation fences, and publication lineage; no legacy import marker lookup inventions. |
| independence-staff-engagement | FirmStaffRecord, FirmProfessionalDutyDesignation, PracticeIndependenceCommitment, ClientServiceIndependence, ServiceEngagementIndependence | Catalog53 only covers rule catalogs; it does not include these owners. Existing typed registration/designation/engagement/practice processes supply actual producers and must retain independent personal decisions, exact proof and settlement. |
| administrator-guard | TenantManagerGuard | Do not prune safety contributions by age or use projections as current manager authority. PlatformOperatorRoster is already compact, separate owner. |
| recovery-slots | EmailAddress, UserIdentity | IdentityRecovery itself is bounded selected preparation/stage; preserve both identity-slot protection, challenge claim, post-accept completion forward and independent notice delivery. |
| digest-dispatch | WorkDigestDispatch, WeeklyWorkDigest | WorkDigestDispatch keeps every week; WeeklyWorkDigest is one week with attempts. Actual processor already owns durable delivery recovery; changing sender policy is not required. |
| provider-gap | ProviderAssuranceGap | Provider/Report/Review/RiskAcceptance children already bounded/native. Maintain authoritative exact RiskAccepted proof and historical link lookup. |
| audit-population | AuditPopulation | Real CSV/JSON export builds bytes before owner RecordExport; selected independent AuditPopulationExportProcess not implemented as its durable owner/process seam. Exact frozen source/output proof and evidence/retention remain required; existing ports remove a generic internal byte-worker blocker. |
| examination-baselines | ExaminationEngagement | Queued baseline identity11 fixes scoped projection keys/ambiguity only. Native compact owner/content/result/history plus existing admission/source proofs remain required; do not assume realm-wide BaselineId authority. |
| examination-documents | TypeIIPlan, ManagementAssertion, ManagementRepresentationLetter, AuditObservation, AuditOutcome, ExaminationSignOff | Some are one-shot facts; do not call all lifetime-growing. Retain current personal/exec source validation and exact package/period version lineage; SD101 uses these immutable proof seams and must be composed. |
| rollover-client-export | ProgramRollover, ClientExport | Rollover versions and export attempts have explicit32 caps. Preserve actual period/outcome/source bytes and signed/attest delivery receipts; no new trusted callback or external delivery authority. |
| retention-legacy-payloads | ArtifactDisclosure, ArtifactDisposition, ArtifactEngagementHold, EvidenceHandlingPolicy, EvidenceRedaction | Persisted combined artifact_retention migration is an actual selected trusted-protocol qualification gap; disposition remains fenced. Do independent fresh-stream owner/read/policy work without inventing import authority. |
| finite-transaction-representation | AccessReviewDecisionBatch, ApplicationImportBatch | Keep source publication/commit proof manifests complete and selected cancellation/lost-response recovery; do not delete acceptance coverage to make fields smaller. |

## Queued, excluded from new ownership

- **SystemDescription**: frozen, parent runtime/integration pending; 101 total paths; `/tmp/compliance-system-description-prepared/handoff.json`. Not counted complete.
- **RiskDraft**: frozen Risk109; current package covers only RiskDraft/RiskEvaluation owners; `/tmp/compliance-risk-native-prepared/handoff.json`. Not counted complete.
- **RiskEvaluation**: frozen Risk109; exclude from new ownership; other Risk owners remain; `/tmp/compliance-risk-native-prepared/handoff.json`. Not counted complete.
- **TypeIIObservationCalendar**: frozen Calendar28, parent runtime/integration pending; `/tmp/compliance-calendar-prepared/handoff.json`. Not counted complete.
- **IndependenceRuleCatalog**: frozen combined catalog53, parent runtime/integration pending; `/tmp/compliance-ratification-native-prepared/handoff.json`. Not counted complete.
- **IndependenceRuleRatificationCatalog**: frozen combined catalog53, parent runtime/integration pending; `/tmp/compliance-ratification-native-prepared/handoff.json`. Not counted complete.
- **ComplianceIncident**: active parent WIP; not frozen and not complete; `/tmp/compliance-incident-native-prepared/owning-paths.txt`. Not counted complete.

Risk109 covers only RiskDraft and RiskEvaluation. RiskMethod, RiskGovernance, RiskTreatmentAssertion and RiskTreatmentAction remain. Catalog53 covers only the two rule catalogs. Baseline identity11 fixes keys/ambiguity, not ExaminationEngagement history compaction.

## Concrete reactor/proof plumbing task

`src/Compliance.Core/Features/SourceFacts/SourceFactProofs.cs:29` resolves `AggregateVersion−1` as a physical source offset. Actual callers include invitation delivery, identity recovery, named-record processes, Readiness decision admission, provider acceptance linking and administrator effects. Parent Incident WIP owns the exact-offset helper correction; remaining callers need coordinated source-backed migration/regressions. This is internal implementation work, not an external prerequisite.

## Local qualification boundaries

- **operation-only-routes**: Existing#1042/#1055: installed append binds per first owner, no selected realm-wide operation root guarantee. Bare/global operation lookup remains disabled; scoped owning mutation/read work proceeds. Evidence: `docs/product/audits/2026-10-24/review-blockers.json`.
- **protected-download-cross-artifact-grant-identity**: Artifact-local admission and tenant/GrantId child conflict across two artifacts reusing an ID; no selected trusted refusal/terminal settlement contract closes both pins. AES configured store is independent and already qualified separately; do not ship public protocol. Evidence: `docs/product/audits/2026-10-24/review-blockers.json`.
- **control-source-use-pair**: Selected source-admission/owner-extension catalogs and SourceUsePurpose contain no Control/current-version acquire/settle purpose/receiver pair. Local compact authority and immutable historical-source native work is unblocked; foreign current checks alone are not held-source proof. Evidence: `docs/product/audits/2026-10-24/control-source-use-qualification.json`.
- **persisted-retention-history**: Existing combined artifact_retention placement/release/basis history has no selected trusted import/cutover mapping. Native fresh independent basis/legal-hold work proceeds; do not silently dual-read or claim persisted rollout. Evidence: `docs/product/audits/2026-10-24/review-blockers.json`.
- **system-description-live-current-permits**: Queued SD accepts verified immutable TypeI/TypeII historical snapshot proofs, but explicit live-current approval requires a held-permit protocol absent from selected catalogs. Nonempty unqualified persisted sources fail closed; this does not block immutable as-of approval/export/read implementation. Evidence: `/tmp/compliance-system-description-prepared/approval-qualification.md`.
- **incident-approved-successor-producer**: Current trusted RecordComplianceIncidentSystemDescriptionDecision is exact no_change only; approved-successor reactor/receiver selected for Change does not authorize Incident. Preserve no_change receiver and report absent selected Incident callback/source-use declaration; independently implement owner/read/resolution. Evidence: `/tmp/compliance-incident-native-prepared/protocol-qualification.md`.

## Exclusions that prevent inflated work counts

- **NamedRecord / Provider / Location / OperationalProcess**: Actual native base stores only pending,current,previous refs. Concrete aliases inherit it; generic read/rules content is not command authority.
- **ControlDraft / CommitmentDraft / Policy / SystemBoundary / SignificantChange / TypeIIPeriod**: Current actual native owners use header+immutable authority/content ref+NativeAcceptance/selected fact; wide Rules/View/Reader objects are read composition. Generic SourceUseReservations remain parent-owned and excluded.
- **MethodologyTemplate / MethodologyTemplateContribution / TemplateApplication**: Current owners retain bounded version/status/header/ref authority. Do not treat their view/history/source readers as owner state.
- **ReadinessAssessment / AuditorFeedbackThread / AuditorSampleSelection / AuditPackageDelivery / ProviderAssuranceReview / ProviderRiskAcceptance**: Independent child roots retain one materialization/recorded fact or bounded lifecycle. No per-family lifetime collection; existing processes are real technical orchestration, not missing business owners.
- **TenantIdentityProvider**: Audience allow-list is current provider configuration, not lifetime append history; static error dictionaries are immutable constants.
- **LegacyWorkAssignmentLedger**: Read-only legacy adapter used by WorkQueueReader; active WorkCommands write WorkItemAssignment and call ImportLegacy once. Do not count another active program-wide owner writer.
- **PopulationSnapshot / PopulationSnapshotChunk / ImmutableSnapshot**: Immutable one-shot captured source/blob/chunk roots; bounded root manifests/captured rows are not lifetime histories. Technical source capture/publication can still have independent qualification work.
- **Rules/View/Reader/Directory/ProcessRecord collections**: AST inventory includes actual classes directly inheriting Aggregate only. Broad rg hits from dictionaries in immutable read/rules composition are excluded; technical ProcessDriver/ProcessRecord state is not automatically another business owner.

## Technical processes

- **AuditPopulationExportProcess**: Selected independent technical export process has no implementation type/name in current source; real synchronous generator performs artifact intake+retention before population receipt append. Move into durable admitted process/output seam when compacting Population; internal work, not an external blocker.
- **IdentityRecoveryNoticeProcess**: Do not report missing merely from absent class name: actual settle receiver dispatches verified completion notice, retries independently after swap completed. Ownership/transport qualification can be reviewed, but concrete producer exists.
- **WorkforceRosterSnapshotProcess / ProgramScopeSnapshotProcess**: No named process types, but real snapshotter/freezer producers exist. Name absence alone is not a missing business-owner defect. Audit exact immutable source cut/output recovery before deciding a process refactor; no speculative blocker.

No source edits, builds/tests, Git commands, Jev calls or issue creation were performed. Source files were parsed and read only; the two report artifacts and temporary static inventory were written under /tmp.
