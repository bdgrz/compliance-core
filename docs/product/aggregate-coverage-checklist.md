# Aggregate coverage checklist

Status: working implementation checklist, reconciled against the accepted
feature requirements in [`backlog.md`](backlog.md). An aggregate is checked
only when its business boundary is implemented and focused behavior tests
exist. Cross-cutting primitives, projections, external references, and
read-only catalogs do not get duplicate aggregate roots.

## Scope and completion rule

- [x] Every P0 and P1 feature below maps to a behavior-tested aggregate root or
  explicitly reuses an existing root that owns the invariant.
- [x] Every new aggregate in this pass was developed test-first: a focused
  behavior test failed before its implementation and passes afterward.
- [x] Every aggregate event retains enough domain data to replay its state.
- [x] No root combines records that can change independently or requires an
  atomic write to another stream.
- [x] P2 hypotheses and unresolved commercial/business decisions remain
  deferred; they do not justify speculative aggregate roots.

Legend: `[x]` existing root or reuse is present; `[ ]` implementation or
behavioral coverage remains; `[~]` intentionally deferred by the backlog.

## Foundation enablers

- [x] EN-01 Authorization and actor references — `Tenant`, `Member`,
  `AccessGrant`, `Role`, `Team`, `TeamMember`, `RolePermission`, `TeamRole`,
  `SeparationOfDutiesWaiver`, and the independence/access ledgers.
- [x] EN-02 Versioned records and impact preview — each owning feature keeps
  versions and decisions in its own aggregate; shared rules live in
  `Features/Versioning`.
- [x] EN-03 Immutable snapshots — `ImmutableSnapshot`, `PopulationSnapshot`,
  and `PopulationSnapshotChunk`; consuming features bind exact snapshot IDs.
- [x] EN-04 Review and approval — decisions stay in the aggregate that owns
  the reviewed record; there is no universal Review aggregate.
- [x] EN-05 Import and replay — `ImportBatch` and `ApplicationImportLedger`,
  with domain acceptance in the owning record workflows.
- [x] EN-06 Governed artifacts — `EvidenceArtifact`, `EvidenceRequestLedger`,
  `EvidenceRedaction`, and `ArtifactRetention`.

## P0 features

- [x] R1-01 Program — `ComplianceProgram`.
- [x] R1-02 Boundary and intended scope — `SystemBoundary`; `ClientService`
  owns the service identity.
- [x] R1-03 Criteria — platform `CriteriaCatalog` is immutable shared source
  data; tenant selection is part of `ComplianceProgram`; licensed overlays
  use `CriteriaTextOverlayLedger`.
- [x] R1-04 Membership, teams, roles, grants, and separation of duties —
  existing access-control roots and `IndependenceLedger`.
- [x] R1-05 Controls — `ControlDraft`.
- [x] R1-06 Criteria mapping and applicability —
  `ControlCriterionMappingLedger` and `CriterionApplicabilityLedger`.
- [x] R1-07 Risk and treatment — `RiskDraft`, `RiskEvaluation`,
  `RiskGovernanceLedger`, and `RiskMethod`.
- [x] R1-08 Readiness and gap plan — `ReadinessLedger`.
- [x] R1-10 Application inventory — `DeclaredApplication` and
  `ApplicationImportLedger`.
- [x] R1-11 Workforce and identity owners — `Person`, `WorkRelationship`,
  `ServiceIdentity`, and workforce observation roots.
- [x] R1-12 Technology and information inventory — `TechnologyComponent`,
  `InformationAsset`, `DataFlow`, `LocationRegister`, and
  `OperationalProcessRegister`.
- [x] R1-13 Commitments and requirements — `CommitmentDraft`.
- [x] R1-14 Providers and subservice organizations — `ProviderRegister` and
  `ProviderAssuranceRegister`.
- [x] R1-15 Tenant provisioning and lifecycle — `Tenant`, `TenantInvitation`,
  `TenantOwner`, `TenantSlug`, and `PlatformOperatorRoster`.
- [x] R2-01 Responsibility and cadence — responsibility assignments are
  events on the exact source-record aggregate; `ResponsibilitySet` is a
  projection. Cadence and occurrences belong to `ControlOperationsLedger`.
- [x] R2-02 Policies and review cycles — `Policy`; distribution state belongs
  to `PolicyDistributionCampaign` and `TrainingCatalog`.
- [x] R2-03 Evidence requests and captured artifacts —
  `EvidenceRequestLedger` and `EvidenceArtifact`.
- [x] R2-04 Control operation and attestation — `ControlOperationsLedger`.
- [x] R2-05 Control evaluation — `ControlEvaluationPlan` and
  `ControlEvaluationLedger`.
- [x] R2-06 Access populations and campaigns — `AccessPopulation`,
  `AccessReviewCampaign`, and `AccessReviewSystemLedger`.
- [x] R2-07 Findings and corrective actions — `RemediationLedger`.
- [x] R2-08 Advisor collaboration — `AdvisorHandoff`.
- [x] R2-09 Type I entry decision — `ReadinessLedger`; immutable source
  snapshots use `ImmutableSnapshot`.
- [x] R2-10 Policy distribution and training — `PolicyDistributionCampaign`
  and `TrainingCatalog`.
- [x] R2-11 Accountable work — `WorkAssignmentLedger` and
  `WorkDigestPreference`; `WeeklyWorkDigest` owns per-member/week scheduling,
  suppression, deterministic delivery attempts, and outcomes. The
  cross-feature work queue remains a projection.
- [x] R2-12 Evidence handling and disclosure policy — `EvidenceHandlingPolicy`
  owns versioned classification for exact artifact content; `ArtifactDisclosure`
  owns approved exact-recipient access; `ArtifactEngagementHold` owns
  engagement/report preservation; `ArtifactDisposition` owns authorized
  disposition intent, retry history, and tombstone. Reuse `ArtifactRetention`,
  `EvidenceArtifact`, and `EvidenceRedaction` for their existing invariants.

## P1 examination and continuity features

- [x] T1-01 Type I engagement and baseline — `ExaminationEngagement` owns
  engagement terms and its immutable baseline/amendment history in one
  stream. Tests cover blockers, exact sources, optimistic revision, immutable
  freeze, amendment attribution, and duplicate identity.
- [x] T1-02 System description — `SystemDescription` keeps immutable versions,
  exact source reconciliation, review/approval decisions, period-change
  dispositions, snapshot bindings, and export identities in one stream.
- [x] T1-04 Auditor requests and responses — `AuditorRequest` owns request
  terms and revisions, assignment, compartmented discussion, versioned
  responses, internal review, exact delivery, external disposition, resolution,
  and reopen history. Sample selections bind exact population snapshot IDs and
  digests; cross-stream existence/authorization checks belong to handlers.
- [x] T1-05 and T3-05 Audit packages — one `AuditPackage` root supports both
  Type I baselines and Type II close snapshots. It retains exact deterministic
  manifests, authored source labels, progress and retryable build attempts,
  validation findings and rationale-backed overrides, exact-recipient
  disclosure, delivered archive identities, and immutable amendments. Source
  authorization/freshness and table reconciliation findings are supplied by
  handlers and validators; the root binds their outcomes to the exact snapshot.
- [x] T1-06 and T3-04 Findings and exceptions — reuse `RemediationLedger`;
  add story-specific behavior there only where baseline/sample provenance or
  immutable external wording is not already enforced.
- [x] T1-07 Type I outcome and Type II plan — separate roots retain the
  versioned `ManagementAssertion`, externally sourced and fully signed
  `ManagementRepresentationLetter`, auditor-authored `AuditOutcome`, immutable
  `ExaminationSignOff`, and reviewed/approved `TypeIIPlan`. Each binds exact
  shared record versions by identity and digest. The plan is approved before a
  later `TypeIIPeriod` binds it; no root copies or updates another root's state.
- [x] T2-01 and T3-01 Type II period and close — `TypeIIPeriod` binds the
  independently approved plan version and immutable opening snapshot, derives
  stable expected control-occurrence identities, records attributable period
  amendments, reconciles close sources and completeness, requires exact
  unresolved-item acknowledgement by an independent executive, and freezes an
  immutable close snapshot with exact amendment lineage. The program-scoped
  `TypeIIObservationCalendar` serializes active date reservations, imports
  existing period streams, protects overlapping starts and amendments, and
  releases a range only against an exact frozen close snapshot. Closed periods
  remain in history without blocking later active ranges. Control operations,
  evidence, plan, and examination decisions remain in their own streams.
- [x] T2-02 and T2-03 Recurring operation and evidence — reuse
  `ControlOperationsLedger`, `EvidenceRequestLedger`, and `EvidenceArtifact`;
  do not create a second occurrence or evidence aggregate.
- [x] T2-04 Readiness monitoring — projection/calculation definitions and
  as-of results; no new source aggregate is required.
- [x] T2-05 Recurring access reviews — reuse `AccessReviewCampaign` and
  `AccessPopulation`.
- [x] T2-07 Significant changes and incidents — separate `SignificantChange`
  and `ComplianceIncident` roots own their source provenance, chronology,
  restricted details, impact assessment, exact follow-up decisions, and
  resolution; no operational ticket system is copied into the compliance stream.
- [x] T3-02 Audit populations — one `AuditPopulation` root per population owns
  the versioned definition, collected source facts, row classifications,
  completeness reconciliation, freeze, amendment history, and export identity.
- [x] T3-03 Samples — the `AuditorRequest` root owns stable selections to
  exact frozen population rows; no second sample-selection root.
- [x] T3-06 Type II management sign-off and outcome — `TypeIIPeriod` freezes the
  exact close snapshot; separate `ManagementAssertion`,
  `ManagementRepresentationLetter`, `AuditOutcome`, and `ExaminationSignOff`
  roots retain management wording and approvals, external auditor provenance,
  exact report/package/snapshot bindings, unresolved-item acknowledgements,
  and final commitments. No auditor opinion is generated by the product.
- [x] T3-07 Program rollover — `ProgramRollover` owns the proposal and
  independent approval that creates one next period from an exact completed
  Type II source; the continuing plan remains in `ComplianceProgram` and each
  period remains in `TypeIIPeriod`.
- [x] F1-01 Client export — one `ClientExport` root per export job owns exact
  source versions, included/withheld reconciliation, retryable build attempts,
  attest-delivery decisions, and final delivery identity; tenant lifecycle
  remains in `Tenant`.
- [x] F1-04 Reusable methodology templates — platform `MethodologyTemplate`
  owns authored and approved versions; tenant `TemplateApplication` owns exact
  application provenance and successor decisions; tenant
  `MethodologyTemplateContribution` owns source-identity-bearing proposal,
  de-identification review, and explicit platform acceptance. Platform versions
  retain only accepted de-identified content and approval provenance. Drafts
  are created through owning tenant aggregates with replay-safe receipts, never
  a multi-stream transaction.
- [x] F1-06 Tenant identity-provider configuration — one
  `TenantIdentityProvider` root per organization connection owns secure issuer
  and audience configuration, test history, credential rotation, and disable;
  `UserIdentity` continues to own platform-user identity bindings and membership.
- [x] F1-07 and F1-08 Service engagements and independence — reuse
  `IndependenceLedger`, `IndependenceRuleCatalog`, and engagement assignments;
  no duplicate service-engagement root.

## Deferred feature hypotheses and product decisions

- [~] R1-09 readiness import adoption — the shared `ImportBatch` exists; do
  not add an aggregate for readiness mappings until the owning records and
  accepted mapping workflow are ready.
- [~] T1-03 external auditor access — P2; no access-grant aggregate is added
  ahead of its accepted visibility and revocation workflow.
- [~] T2-06 periodic policy/risk/vendor reviews — P2 and covered by their
  existing source-record review workflows.
- [~] T2-08 optional collection — P2; do not add provider/collection roots
  ahead of the approved connector-runtime requirements.
- [~] T2-09 management compliance reviews — P2; M0-D19 directs recurring
  governance through existing control occurrences, with the review record and
  follow-up actions retained by that workflow.
- [~] F1-02, F1-05 portfolio summaries and cross-client work — projections;
  no source aggregate unless an accepted command changes business state.
- [~] F1-03 staffing capacity — P2; no capacity-planning aggregate yet.
- [~] BO-01–03 delegation and commercial records — exact lifecycle, authority,
  and policy remain decision-gated; no speculative roots.

## TDD iteration log

- [x] R2-08 Advisor collaboration: `AdvisorHandoff`; focused tests were written
  before implementation and pass.
- [x] T1-01 Examination engagement and baseline: `ExaminationEngagement`;
  focused behavior tests failed before the implementation and pass afterward.
- [x] T1-02 System description: `SystemDescription`; focused behavior tests
  failed before the implementation and pass afterward.
- [x] T1-04 Auditor requests and responses: `AuditorRequest`; focused behavior
  tests failed before the implementation and pass afterward, including response
  versioning, review gates, delivery receipts, accepted disposition, reopen,
  frozen sample identity, and event replay.
- [x] R2-12 Evidence handling and disclosure: `EvidenceHandlingPolicy`,
  `ArtifactDisclosure`, `ArtifactEngagementHold`, and `ArtifactDisposition`;
  focused tests were written first, failed before their roots existed, and now
  pass across policy revision, exact-recipient delivery/revocation, hold timing,
  retained-data gates, retryable execution, and tombstone creation.
- [x] T1-05/T3-05 Audit packages: `AuditPackage`; focused tests failed before
  the root existed and pass for deterministic manifest identity, exact frozen
  scope/version selection, partial-build rejection and retry, progress,
  blocking validation, independent override, recipient-specific disclosure,
  exact delivery identity, Type II reuse, amendment preservation, and replay.
- [x] T1-07 Type I outcome and Type II plan: `ManagementAssertion`,
  `ManagementRepresentationLetter`, `AuditOutcome`, `ExaminationSignOff`, and
  `TypeIIPlan`; each received focused failing behavior tests before its
  implementation and now passes exact-binding, attribution, independence,
  completeness, provenance, approval-gate, and replay tests.
- [x] T2-01/T3-01 Type II period and program calendar: `TypeIIPeriod` and
  `TypeIIObservationCalendar`; focused tests failed before implementation and
  pass approved-plan binding, stable occurrence generation, opening-gap
  acknowledgement, calendar migration, inclusive overlap rejection, adjacent
  date allowance, closed-period date reuse, amendment
  reservation/completion/cancellation/release, close
  completeness, exact material acknowledgement, snapshot lineage, and replay.
- [x] All identified P1 aggregate gaps above have behavior tests and verified
  implementations; deferred `[~]` items remain decision-gated by the backlog.
- [x] T2-07 Change and incident oversight: separate `SignificantChange` and
  `ComplianceIncident`; focused tests failed before the roots were implemented
  and now pass native capture, provenance, impact gates, correction history,
  resolution, and replay behavior.
- [x] T3-02 Audit populations: `AuditPopulation`; focused tests failed before
  implementation and now pass manual source capture, field authorization,
  stable row identity, completeness reconciliation, freeze, correction through
  a successor population, export identity, and replay.
- [x] T3-06/T3-07 Type II close and rollover: `TypeIIPeriod` plus the existing
  examination decision roots and `ProgramRollover`; tests failed before the new
  period/rollover roots existed and now pass exact-source close, sign-off,
  completed-period gating, single successor creation, and replay.
- [x] F1-01 Client export: `ClientExport`; focused tests failed before the root
  existed and now pass source reconciliation, retryable builds, withheld-record
  accountability, interrupted attest delivery followed by a successful retry,
  final-delivery gating, and replay.
- [x] F1-04 Reusable methodology templates: `MethodologyTemplate`,
  `TemplateApplication`, and `MethodologyTemplateContribution`; focused tests
  failed before the roots existed and now pass independent authorship/review/
  approval, version pinning, draft receipts, successor decisions, de-identified
  contribution review/acceptance, source-identity isolation, and replay.
- [x] F1-06 Tenant identity-provider lifecycle: `TenantIdentityProvider`;
  focused tests failed before the root existed and now pass secure issuer and
  audience checks, test-before-enable, rotation requiring retest, safe failure
  details, disable without deleting identity history, secret-reference
  non-disclosure, tenant-bound replay, and replay of lifecycle history.
