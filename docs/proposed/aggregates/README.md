# Aggregate and technical-owner index

Selected target: **131 business/authority owners** and **14 technical/storage responsibilities**. Technical responsibilities are not business aggregates.

Groups are derived from source feature paths. They are navigation labels, not bounded-context decisions.

## [Placeholder: source capability area is not recorded]

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [ApplicationNameClaim](application-name-claim.md) | business_identity_or_uniqueness_authority | 2 | 2 | One normalized application name or alias is held by at most one active application within a tenant. Claim precedes the application fact that uses the name; release follows retirement or rename. An attributed merge retires the losing application with a merged_into reference and transfers or releases its names to the survivor (M0-D05 rules D05-09, D05-10). |
| [ArtifactDownloadGrant](artifact-download-grant.md) | business_aggregate | 2 | 2 | One exact receiving grant owns the authenticated encrypted original artifact ticket/result after EvidenceArtifact first admitted and pinned availability with frozen202. Complete required held source uses and exact issue DecisionAt precede native issued-v2; cancellation fences delayed issuance and terminal proofs settle all uses. Original expiry never renews under the same root identity. Existing artifact-issued v1 decoder remains read-only compatibility. |
| [CommitmentControlLink](commitment-control-link.md) | business_aggregate | 3 | 3 | One commitment-to-control link has one pending/accepted version line: each accepted version binds an exact commitment version to an exact control version with rationale, a successor needs independent review, and a retired link stays terminal (M0-D09 D09-09). Neither CommitmentDraft nor ControlDraft owns the other side's version list. |
| [IdentityRecovery](identity-recovery.md) | business_aggregate | 4 | 4 | Immutable both-proof-verified candidate and preparation; complete protection proofs precede accepted exact swap. Preparation can reject/fence; accepted swap completes forward. Exact account, old/replacement identities and actor/proof provenance retained without bearer tokens. |
| [ImportedOwnerResolution](imported-owner-resolution.md) | business_aggregate | 1 | 1 | An imported named owner is bound to a workforce Person only by exact normalized email match at import, otherwise it stays an unresolved_owner reference until one attributed manual resolution names the Person; a name-only or ambiguous match never binds, and the resolution is immutable once recorded (M0-D04 D04-02). |
| [AuditPopulationExportProcess](audit-population-export-process.md) | technical_process | 0 | 0 | Immutable approved population manifest, format, exact source proofs, output content ref and attempt state; retention is proof-only input. |
| [EmailChallengeDeliveryLedger](email-challenge-delivery-ledger.md) | technical_delivery_journal | 0 | 0 | [Placeholder: value not recorded] |
| [IdentityRecoveryNoticeProcess](identity-recovery-notice-process.md) | technical_process | 0 | 0 | Independent completion notice payload hash/recipient and delivery attempts; does not control completed recovery. |
| [ProgramScopeSnapshotProcess](program-scope-snapshot-process.md) | technical_process | 0 | 0 | Complete program/boundary source proofs and immutable snapshot/manifest content; regeneration reproduces that exact content and changes no domain decision. |
| [WorkforceRosterSnapshotProcess](workforce-roster-snapshot-process.md) | technical_process | 0 | 0 | Complete authoritative workforce source cut and immutable roster content; amendments create a new snapshot identity, never rewrite an accepted snapshot. |

## AccessControl

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [AccessGrant](access-grant.md) | business_aggregate | 3 | 2 | One stable grant identity has one immutable set of scope/principal/effective terms and, once revoked, can never be reissued. |
| [ClientIndependenceDecision](client-independence-decision.md) | immutable_business_artifact_or_decision | 1 | 1 | One immutable client independence preview and exact rule/service-epoch proofs; it never grants production acceptance. The evaluation records the rule-set version, every nonattest service considered, the result (compatible, conditionally_compatible or impaired), the partner evaluation reference when conditional, and the management acknowledgement reference (M0-D26 D26-06; Jev 0.55, moderate) |
| [ClientServiceIndependence](client-service-independence.md) | business_governor | 6 | 6 | Complete immutable nonattest-service history manifest, current service epoch and acceptance admission barrier. A changed service set advances epoch and makes old acceptance ineligible immediately through authoritative composed reads. Acceptance admission refuses an attest engagement when the firm provided any service classified impairing under the active ratified rule set within the 12 months before the examination period starts (M0-D26 D26-03; Jev 0.62, moderate). When the current client evaluation is conditionally_compatible, acceptance admission also requires a recorded partner evaluation that references that evaluation (M0-D26 D26-04; Jev two-step 0.80, gate owner 0.58 moderate) |
| [EngagementManagementAcknowledgement](engagement-management-acknowledgement.md) | immutable_business_artifact_or_decision | 2 | 1 | One personal management acknowledgement bound to exact engagement revision and complete service history. |
| [FirmProfessionalDutyDesignation](firm-professional-duty-designation.md) | business_aggregate | 6 | 6 | One active signed duty designation and monotonic signed authority revision per exact subject/duty/scope; retain continuity proof. The platform scope is explicit and is not an empty tenant UUID. |
| [FirmStaffIdentityClaim](firm-staff-identity-claim.md) | business_identity_or_uniqueness_authority | 1 | 1 | Permanent canonical user to staffMemberId/practice mapping; disable/status changes never release this mapping. |
| [FirmStaffRecord](firm-staff-record.md) | business_aggregate | 8 | 7 | Immutable canonical user, practice and registration source, plus one staff record active/status revision. |
| [IndependenceRuleCatalog](independence-rule-catalog.md) | business_governor | 3 | 3 | Authored independence rules advance consecutively and cannot falsely claim professional ratification. A rule change publishes a new consecutive rule-set version; evaluations keep the rule-set version they were made under and are never rewritten (M0-D26 D26-05; Jev 0.97) |
| [IndependenceRuleRatificationCatalog](independence-rule-ratification-catalog.md) | business_governor | 3 | 3 | Only valid professional proof can ratify an exact immutable rule digest, and active ratified rule version advances monotonically. |
| [Member](member.md) | business_aggregate | 8 | 5 | One tenant-user membership has one affiliation/expiry episode; deprovision closes authority and a fresh episode cannot start until authority cleanup completes. |
| [PartnerIndependenceDecision](partner-independence-decision.md) | immutable_business_artifact_or_decision | 2 | 1 | One personal partner evaluation bound to exact engagement revision, complete service epoch and ratified-rule digest. |
| [PracticeIndependenceCommitment](practice-independence-commitment.md) | business_identity_or_uniqueness_authority | 3 | 3 | Permanent practice binding for subjectKind=staff or canonical_user; acquire both user and staff subjects in canonical stream-key order. No release/revoke command exists. |
| [ProfessionalAssertionIdentityClaim](professional-assertion-identity-claim.md) | business_identity_or_uniqueness_authority | 1 | 1 | Permanent binding of signed assertion identity to digest, designation, canonical user and duty scope; claim identity cannot be recycled. |
| [Role](role.md) | business_aggregate | 3 | 3 | One role has one active/deleted lifecycle, normalized display name and built-in protection. |
| [RolePermission](role-permission.md) | business_aggregate | 2 | 2 | One normalized role-permission tuple has a single active relationship at a time. |
| [SeparationOfDutiesWaiver](separation-of-duties-waiver.md) | business_aggregate | 2 | 2 | Exact record/version/action/beneficiary exception can be approved once only by a different administrator who is neither requester nor beneficiary, during its validity window. |
| [ServiceEngagementIndependence](service-engagement-independence.md) | business_aggregate | 17 | 16 | One engagement lifecycle, proposed and actual staff assignments, current content revision and accepted epoch bindings. Engagement revision and service epoch live in state; historical epoch streams cannot compete independently. An impaired independence evaluation result blocks acceptance of the attest engagement with no override (M0-D26 D26-07; Jev 0.81) |
| [Team](team.md) | business_aggregate | 2 | 2 | One team definition has an active/deleted lifecycle and built-in team cannot be deleted. |
| [TeamMember](team-member.md) | business_aggregate | 6 | 2 | One tenant/team/member relationship is active only for one current membership episode. |
| [TeamRole](team-role.md) | business_aggregate | 2 | 2 | One tenant/team/role relationship has a single active assignment. |
| [TenantManagerGuard](tenant-manager-guard.md) | business_governor | 2 | 6 | Removal from effective administrator set must leave another active tenant administrator. |

## AccessReviews

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [AccessExpectation](access-expectation.md) | business_aggregate | 3 | 3 | One exact system-instance expectation lineage owns predecessor/successor competition, independent approval and same-line exceptions. Different lines may coexist, including different policies on one system. No cross-line exclusion governor or ban is implied; a future ban requires a separately reviewed precise predicate. |
| [AccessPopulation](access-population.md) | business_aggregate | 3 | 3 | Immutable accepted population definition, exact system instance and source revision. |
| [AccessPopulationException](access-population-exception.md) | business_aggregate | 1 | 1 | One expiring exception or missing-population exception decision. |
| [AccessPrincipalClassification](access-principal-classification.md) | business_aggregate | 1 | 1 | Independent classification decision history for one accepted principal. |
| [AccessReviewCampaign](access-review-campaign.md) | business_governor | 5 | 5 | Frozen campaign snapshot, generation, and compact completion gate. |
| [AccessReviewDecisionBatch](access-review-decision-batch.md) | business_aggregate | 4 | 4 | One immutable exact preview/item selection and shared-rationale bulk decision, item reservations and one all-item publication decision. Partial item application stays unpublished. |
| [AccessReviewItem](access-review-item.md) | business_aggregate | 12 | 11 | One reviewer decision, reassignment, remediation verification, exception and history for one frozen item. A privileged entitlement item accepts only an individual named reviewer decision; bulk or delegated decisions are refused for privileged access (M0-D07 D07-09; source AccessReviewRules.cs) |

## AdvisorCollaboration

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [AdvisorFeedbackThread](advisor-feedback-thread.md) | business_aggregate | 4 | 4 | Immutable contribution with its correction/response lifecycle. |
| [AdvisorHandoff](advisor-handoff.md) | business_aggregate | 8 | 8 | Small selection, readiness, revocation/closure and admission-generation authority. |

## Applications

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [ApplicationImportBatch](application-import-batch.md) | business_aggregate | 8 | 9 | Frozen source rows, row outcomes, per-batch execution and terminal disposition. |
| [ApplicationImportSource](application-import-source.md) | business_governor | 2 | 2 | One exact tenant/source-key/namespace authority serializes batch admission and durable source-record mapping publication. Complete verified published-batch order and frozen SourceClaims define mutable replace/remove mappings; source revision/generation fences stale plans and omissions. There is no independent ApplicationSourceClaim root. |
| [ApplicationRelationship](application-relationship.md) | business_aggregate | 3 | 3 | One exact directed source/kind/target relationship lifecycle with unchanged deterministic UUIDv5 tuple identity; active endpoint/source-visibility and exact source/target revision guards remain. No independent tuple claim/release authority is introduced. |
| [DeclaredApplication](declared-application.md) | business_aggregate | 8 | 7 | Application creation, metadata revision, retirement and import admission generation. Name and alias uniqueness is decided by ApplicationNameClaim, which the application must hold before using a name; a merge retires this application with a merged_into reference to the survivor (M0-D05 D05-10) |
| [DeclaredSystemInstance](declared-system-instance.md) | business_aggregate | 3 | 3 | One system instance has one immutable application parent and registered identity; metadata revisions and terminal retirement follow its own expected revision. |
| [SystemInstanceAccessReviewScope](system-instance-access-review-scope.md) | business_aggregate | 1 | 1 | Scope decisions for one instance advance a monotonic effective date and expected decision sequence with independent approver or exact-scope waiver. |

## AuditorRequests

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [AuditorFeedbackThread](auditor-feedback-thread.md) | business_aggregate | 1 | 1 | Independent comments and responses with stable command identity. |
| [AuditorRequest](auditor-request.md) | business_aggregate | 11 | 10 | Admission seal, response-completeness and final resolution decision. |
| [AuditorResponse](auditor-response.md) | business_aggregate | 8 | 8 | One response draft, independent disclosure review, delivery and exact disposition lifecycle. Response admission and disposition remain linked to the parent request through immutable proofs. |
| [AuditorSampleSelection](auditor-sample-selection.md) | business_aggregate | 1 | 1 | Independent exact sample selection lifecycle. |

## AuditPackages

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [AuditPackageDelivery](audit-package-delivery.md) | business_aggregate | 1 | 1 | One delivery authorization and terminal delivery for exact recipient/version. |
| [AuditPackageManifest](audit-package-manifest.md) | business_aggregate | 8 | 8 | Manifest identity, selected immutable content version and delivery gate. |
| [AuditPackageBuildProcess](audit-package-build-process.md) | technical_process_or_storage_owner | 0 | 0 | Technical build attempts, validation and archive evidence. |

## AuditPopulations

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [AuditPopulation](audit-population.md) | business_aggregate | 8 | 8 | One population owns complete current definition/source/row classification, reconciliation invalidation, completeness proof and freeze seal. Immutable row/version bodies are values outside retained command state; no independent AuditPopulationRowDecision root or projected-count freeze authority. |

## Boundaries

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [SystemBoundary](system-boundary.md) | business_aggregate | 6 | 8 | A boundary has one current draft; approval requires latest accepted review of exact draft revision, an independent/waived actor and strictly increasing approved effective date; exact-scope responsibility changes cannot create retroactive unwaived decision conflict. |

## Changes

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [SignificantChange](significant-change.md) | business_aggregate | 5 | 5 | One change version may be assessed once; current-version independent description decision and required follow-up eligibility gate advancement. |

## Commitments

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [CommitmentDraft](commitment-draft.md) | business_aggregate | 5 | 7 | One tenant/program/kind/identifier has immutable identity/source creation, revisioned authored content, exact verified-source accepted review and independent approval that publishes strictly later effective versions; retired commitment remains terminal. |

## ControlMappings

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [ControlCriterionMapping](control-criterion-mapping.md) | business_aggregate | 3 | 3 | One mapping tuple proposal and immutable approved successor line. |
| [CriterionApplicability](criterion-applicability.md) | business_aggregate | 3 | 3 | One applicability decision lineage. |

## Controls

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [ControlDraft](control-draft.md) | business_aggregate | 10 | 12 | Only the exact current draft or retirement can be independently reviewed then activated, and accepted successor/retirement changes one control version line without overlapping effective intervals. Responsibilities cannot create unwaived conflicts with decisions already made on the exact revision. |

## Criteria

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [CriteriaTextOverlay](criteria-text-overlay.md) | business_aggregate | 1 | 1 | One licensed text overlay and sequential revision line. |

## Evaluations

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [ControlEvaluation](control-evaluation.md) | business_aggregate | 5 | 5 | One evaluation including steps, conclusions, review and deviation dispositions. A retest is a new evaluationId using this same type and an immutable predecessor evaluation/version proof. |
| [ControlEvaluationPlan](control-evaluation-plan.md) | business_aggregate | 1 | 1 | A plan version has a stable identity and immutable procedure; only a successor to the current plan version can be appended and exact duplicate identity must have the same content. |

## Evidence

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [EvidenceArtifact](evidence-artifact.md) | business_aggregate | 5 | 5 | Immutable content remains unavailable until clean inspection; malware alone can be released with attributed clean rescan; secret/invalid rejection is final. |
| [EvidenceRedaction](evidence-redaction.md) | business_aggregate | 2 | 2 | A redaction approval must target current exact original/derived preparation and be independent of its preparer unless exact scoped waiver proof authorizes self-approval. |
| [EvidenceRequest](evidence-request.md) | business_aggregate | 5 | 5 | One open-to-fulfilled/cancelled lifecycle. |
| [EvidenceSupport](evidence-support.md) | business_aggregate | 1 | 1 | One relationship identity immutably binds exact artifact content/revision, target revision and covered period with attributed sufficiency rationale. |

## Examinations

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [AuditObservation](audit-observation.md) | business_aggregate | 4 | 4 | Received auditor source corrections preserve examination/snapshot and exact source lineage; response/link commands must name current source version, and each selected command identity has immutable intent. |
| [AuditOutcome](audit-outcome.md) | business_aggregate | 1 | 1 | One auditor-supplied report conclusion is immutable for its outcome identity and exact delivered package/snapshot provenance. |
| [ExaminationEngagement](examination-engagement.md) | business_aggregate | 4 | 4 | Freeze locks Type I setup to exact approved boundary and complete source coverage; amendments preserve prior baseline and enumerate changed sources. |
| [ExaminationSignOff](examination-sign-off.md) | business_aggregate | 1 | 1 | One final personal management decision binds exact package/assertion/letter/outcome and acknowledges exactly every unresolved material item. |
| [ManagementAssertion](management-assertion.md) | business_aggregate | 2 | 3 | One exact current authored assertion version receives at most one immutable independent named CEO/CISO decision; revision creates changed wording/input bindings. |
| [ManagementRepresentationLetter](management-representation-letter.md) | business_aggregate | 2 | 2 | Letter becomes signed exactly when every fixed named required signer has one immutable distinct signed artifact; no unlisted signer may sign. |
| [TypeIIPlan](type-ii-plan.md) | business_aggregate | 3 | 4 | Only current proposed content with exact carried-gap acknowledgements receives an independent review; executive approval requires review and three distinct roles, after which plan is immutable. |

## Exports

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [ClientExport](client-export.md) | business_aggregate | 6 | 5 | Final export delivery requires archive counts matching frozen included/withheld inventory and a successful exact-manifest delivery for each required attest package. |

## Incidents

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [ComplianceIncident](compliance-incident.md) | business_aggregate | 5 | 5 | One incident version may be assessed once; current-version independent description decision and required follow-up eligibility gate advancement; incident resolution requires every admitted follow-up work item completed. |

## MethodologyTemplates

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [MethodologyTemplate](methodology-template.md) | business_aggregate | 5 | 5 | Publish current authored template only after independent review and distinct third-party approver; published content immutable; retirement stops further template revisions. Contributed records require exact accepted deidentified provenance. |
| [MethodologyTemplateContribution](methodology-template-contribution.md) | business_aggregate | 3 | 3 | Only immutable proposed deidentified record that receives independent approved review then acceptance by a third platform operator becomes shareable; tenant/source lineage stays in tenant-owned stream. |
| [TemplateApplication](template-application.md) | business_aggregate | 3 | 4 | One application pins exact published template provenance, fixed planned tenant draft identities and completion state; one pending later-version adoption proposal receives one immutable disposition before next successor. |

## Operations

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [ControlOccurrence](control-occurrence.md) | business_aggregate | 5 | 3 | One planned/attested/corrected/reviewed occurrence for a control performance window. |
| [ControlOperatingPlan](control-operating-plan.md) | business_aggregate | 2 | 2 | One independently approved, non-overlapping plan version line for a control. |
| [TypeIIObservationCalendar](type-ii-observation-calendar.md) | business_governor | 2 | 2 | Active program Type II date ranges, including both current and reserved amendment ranges, must never overlap; a pending amendment must finish or cancel before date range release. |
| [TypeIIPeriod](type-ii-period.md) | business_aggregate | 10 | 11 | One period binds exact approved opening plan/population; close must reconcile the approved expected population, independently acknowledge all unresolved material items, and freeze the same approved sources. Close amendments preserve original expected population/acknowledgements and chain exact changed sources. |

## Policies

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [Policy](policy.md) | business_aggregate | 9 | 8 | A policy has one pending revision/retirement; immutable approved version intervals form one successor line, activation needs the latest accepted independent review and independent approval, and retired/approved content cannot be edited. |

## PolicyDistribution

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [PolicyCampaignParticipant](policy-campaign-participant.md) | business_aggregate | 6 | 5 | Independent acknowledgement, completion, exception and delivery status. |
| [PolicyDistributionCampaign](policy-distribution-campaign.md) | business_aggregate | 6 | 7 | Immutable policy/material version and frozen audience plus closure generation. The campaign admits every participant result against its frozen audience, so its stream grows with the audience for the campaign's bounded life; this is accepted (Jev keep_per_child_admission 1.00) and must not be reduced to a projection check |
| [TrainingRequirement](training-requirement.md) | business_aggregate | 2 | 1 | One normalized training requirement creation and version line. |

## Programs

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [ClientService](client-service.md) | business_aggregate | 3 | 3 | Once retired a service cannot be revised, and its program identity cannot be rebound; create identity is immutable. |
| [ComplianceProgram](compliance-program.md) | business_aggregate | 3 | 3 | Program target dates must form a coherent chronological plan; one program identity has immutable creation intent. Criteria selection separately chooses current edition. |
| [ProgramRollover](program-rollover.md) | business_aggregate | 3 | 6 | One exact carry-forward proposal version, including the exact successor examination, plan and period identities, requires independent approval and an atomic execution claim before successor writes; the claim pins that version against revision. The recoverable private process coordinates existing source owners and stores exact completion proof. |

## Providers

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [Provider](provider.md) | business_aggregate | 4 | 5 | One provider content, version and retirement lifecycle. Retirement is a distinct terminal fact (planned ProviderRetired), not a value inside a revision |
| [ProviderAssuranceGap](provider-assurance-gap.md) | business_aggregate | 3 | 3 | One current assurance gap with exact source-version and closure lineage. |
| [ProviderAssuranceReport](provider-assurance-report.md) | business_aggregate | 4 | 4 | Evidence report for one provider and exact provider revision. |
| [ProviderAssuranceReview](provider-assurance-review.md) | business_aggregate | 3 | 3 | One immutable personal same-provider due-diligence review bound to exact report revision, evidence kind, validity window and cadence. |
| [ProviderNameClaim](provider-name-claim.md) | business_identity_or_uniqueness_authority | 2 | 2 | Authoritative unique active name reservation. |
| [ProviderRiskAcceptance](provider-risk-acceptance.md) | business_aggregate | 1 | 1 | One immutable accepted-risk linkage admitted on an open provider gap; source gap serializes duplicate tuple and closure. Never owns gap closure. |

## Readiness

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [ProgramReadiness](program-readiness.md) | business_governor | 7 | 7 | Compact latest-assessment pointer, decision admission/seal generation, latest management decision and permanent approved Type I entry pointer. Serialize latest-assessment adoption with decision append; keep full assessment/decision content in immutable manifests. Run admission precedes child computation and reserves exactly one pending latest-adoption generation. |
| [ReadinessAssessment](readiness-assessment.md) | business_aggregate | 1 | 1 | One immutable assessment source/rule revision and findings. |
| [ReadinessGapPlan](readiness-gap-plan.md) | business_aggregate | 4 | 4 | One stable gap action/owner/date plan and independent annotations; admission/seal generation fences changes during a readiness decision. |

## Remediation

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [CorrectiveAction](corrective-action.md) | business_aggregate | 4 | 4 | Independent corrective-action lifecycle, evidence and completion proof. |
| [FindingAggregate](finding-aggregate.md) | business_aggregate | 10 | 8 | Finding description, status and closure admission generation. |
| [FindingMigrationAggregate](finding-migration-aggregate.md) | technical_process_or_storage_owner | 0 | 0 | Technical process invariant: migration has one exact immutable source fence, advances one contiguous physical source offset after target import, and completes only with exact parity count/digest at the fence. |
| [RemediationMigrationFenceAggregate](remediation-migration-fence-aggregate.md) | technical_process_or_storage_owner | 0 | 0 | Technical fence invariant: exactly one migration can seal the exact legacy remediation head and subsequent legacy writers must observe that fence. |

## Retention

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [ArtifactDisclosure](artifact-disclosure.md) | business_aggregate | 3 | 3 | One disclosure binds immutable artifact content, handling-policy revision, recipient and purpose; delivery requires that approved identity and revocation is final. |
| [ArtifactDisposition](artifact-disposition.md) | business_aggregate | 4 | 3 | Only an approved exact source with elapsed retention/no active holds or reliance may start execution; one outstanding attempt; disposed result is terminal and yields tombstone. |
| [ArtifactEngagementHold](artifact-engagement-hold.md) | business_aggregate | 4 | 3 | One preservation hold cannot release before one year after its exact report issuance; report identity and release decision are immutable. Release also requires the attributed decision that the engagement record package was delivered into the attest team's audit software; until that delivery fact exists the hold blocks disposition and offboarding cannot complete (M0-D27 D27-05) |
| [ArtifactLegalHold](artifact-legal-hold.md) | business_aggregate | 2 | 2 | One legal hold lifecycle and one exact release after placement. |
| [ArtifactRetentionBasis](artifact-retention-basis.md) | business_aggregate | 1 | 2 | One supported retention basis for exact content identity. |
| [EvidenceHandlingPolicy](evidence-handling-policy.md) | business_aggregate | 3 | 4 | One exact artifact identity has one current reviewed classification, changed only at expected revision with attributed rationale. |

## Risks

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [RiskDraft](risk-draft.md) | business_aggregate | 2 | 2 | Normalized identifier has one creation identity/content within tenant/program and narrative updates require current revision. No additional risk-governance decision is made by this class. |
| [RiskEvaluation](risk-evaluation.md) | business_aggregate | 3 | 3 | Assessment phases follow inherent/current method; residual requires treatment and mitigate admission proof; acceptance must name current residual and accept treatment, satisfy score/appetite/authority/expiry, and be independent from assessor/owner unless exactly waived. |
| [RiskGovernance](risk-governance.md) | business_aggregate | 3 | 2 | One risk’s governance and reassessment obligations. |
| [RiskMethod](risk-method.md) | business_aggregate | 1 | 1 | The program method advances sequentially by expected published version and each immutable definition has complete valid 5x5 scales and permitted appetite. |
| [RiskTreatmentAction](risk-treatment-action.md) | business_aggregate | 7 | 7 | One accountable action, completion and independent review lifecycle. |
| [RiskTreatmentAssertion](risk-treatment-assertion.md) | business_aggregate | 3 | 3 | One pending/accepted assertion line per risk/control. |

## Snapshots

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [ImmutableSnapshot](immutable-snapshot.md) | immutable_business_artifact_or_decision | 2 | 1 | One snapshot identity can freeze exactly one canonical manifest/hash with immutable, attributed amendment lineage. |
| [PopulationSnapshot](population-snapshot.md) | immutable_business_artifact_or_decision | 1 | 1 | Freeze exactly one canonical immutable population identity, optionally a manifest over ordered immutable chunk identities; amendments have separate roots. |
| [PopulationSnapshotChunk](population-snapshot-chunk.md) | technical_process_or_storage_owner | 0 | 1 | Technical invariant only: one snapshot/index stores one immutable kind+row digest, changed bytes conflict. No independent business decision or lifecycle. |

## SystemDescription

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [SystemDescription](system-description.md) | business_aggregate | 8 | 8 | Immutable narrative versions, completeness and approval against exact source snapshot. |
| [SystemDescriptionExportProcess](system-description-export-process.md) | technical_process_or_storage_owner | 0 | 0 | Technical export generation and binding attempt state. |

## TechnologyInventory

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [DataFlow](data-flow.md) | business_aggregate | 2 | 1 | One tenant inventory identity has one record kind; flow endpoint/asset scope and encryption or approved-exception terms must match highest admitted carried classification, and effective date never moves backward. |
| [InformationAsset](information-asset.md) | business_aggregate | 2 | 1 | One tenant inventory identity has one record kind and one revisioned information classification/retention/owner/lifecycle record. |
| [Location](location.md) | business_aggregate | 4 | 3 | Independent location record lifecycle. |
| [LocationNameClaim](location-name-claim.md) | business_identity_or_uniqueness_authority | 2 | 2 | Authoritative active-name uniqueness claim. |
| [OperationalProcess](operational-process.md) | business_aggregate | 4 | 4 | Independent governed operational process lifecycle. Retirement is a distinct terminal fact (planned OperationalProcessRetired), not a value inside a revision |
| [OperationalProcessNameClaim](operational-process-name-claim.md) | business_identity_or_uniqueness_authority | 2 | 2 | Authoritative active normalized-name uniqueness. |
| [TechnologyComponent](technology-component.md) | business_aggregate | 2 | 1 | One tenant inventory identity has exactly one record kind; a component keeps immutable category/system-instance binding, cloud account keyed to its unique instance, and category-specific valid content/lifecycle. |

## Tenants

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [PlatformOperatorRoster](platform-operator-roster.md) | business_governor | 3 | 3 | One atomic platform roster decision binds every exact active operator identity and assignment episode, current acting-operator eligibility, and the last-operator prohibition. Keep all current identities in verified immutable authority with native OCC; no count-only substitute, membership cap, or separate PlatformOperatorGrant root. |
| [Tenant](tenant.md) | business_aggregate | 12 | 14 | One tenant lifecycle activates only after its slug and first-administrator conditions; offboarding forbids new active service; one current slug transition admitted at a time. Lifecycle: registered becomes active only after the slug is confirmed and the first administrator is assigned; active and suspended alternate only by an acting platform operator; active or suspended may start offboarding; offboarding completes only after the attest-record package delivery decision and retention disposition are recorded; every other transition is refused and a repeat of the current state is a no-op. |
| [TenantInvitation](tenant-invitation.md) | business_aggregate | 4 | 2 | One immutable offer, token acceptance and terminal lifecycle; each invitation has exact recipient, purpose, scopes and expiry. |
| [TenantInvitationMailboxClaim](tenant-invitation-mailbox-claim.md) | business_identity_or_uniqueness_authority | 4 | 2 | At most one pending invitation per mailbox. InvitationId and generation live in state and every release compares both; generation is never part of the stream key. |
| [TenantSlug](tenant-slug.md) | business_identity_or_uniqueness_authority | 2 | 4 | One normalized global slug can belong to one tenant and once surrendered is permanently retired. |
| [TenantInvitationDeliveryProcess](tenant-invitation-delivery-process.md) | technical_process_or_storage_owner | 0 | 0 | Technical delivery process with one immutable effect payload and multiple attempt records. Attempt identity is state, never a new unconstrained process owner. |

## UserIdentities

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [EmailAddress](email-address.md) | business_aggregate | 6 | 6 | A normalized address reserves exactly one platform user; verification requires its current challenge; a claimed recovery binds one old/replacement pair. |
| [TenantIdentityProvider](tenant-identity-provider.md) | business_aggregate | 5 | 5 | Connection enables only with successful test of current credential epoch; rotation invalidates pending/old result; disabled connection stays terminal. |
| [UserIdentity](user-identity.md) | business_aggregate | 7 | 6 | External provider+subject associates to exactly one platform user; revoked identity cannot authenticate and has one exact replacement. |

## Work

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [WorkItemAssignment](work-item-assignment.md) | business_aggregate | 4 | 3 | One source-derived work item has current assignee/escalation revision; escalation is accepted at most once, assignment commands require expected revision. Source eligibility and completion belong to producing workflow. |
| [WeeklyWorkDigest](weekly-work-digest.md) | technical_process_or_storage_owner | 0 | 4 | Technical delivery process invariant: exactly one schedule per member-week; only next deterministic attempt can run and callback must match its exact request; terminal outcomes/suppression stop further delivery. |
| [WorkDigestDispatch](work-digest-dispatch.md) | technical_process_or_storage_owner | 0 | 4 | Technical dispatch invariant: one reservation/message per member-week, one in-flight send, retries only after definite transient rejection, and ambiguous external outcome freezes until authorized evidence-backed retry. |
| [WorkDigestPreference](work-digest-preference.md) | technical_settings_state | 1 | 1 | One member stores current notification consent and valid scheduling time zone; identical current setting is no-op. This is chiefly a settings state holder, with no multi-part business invariant identified. |

## Workforce

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [Person](person.md) | business_aggregate | 3 | 3 | One recorded workforce person has immutable creation identity, revisioned contact content and at most one current platform membership correlation. |
| [ServiceIdentity](service-identity.md) | business_aggregate | 2 | 2 | One governed nonhuman identity has exactly one accountable person/team owner, approved purpose, valid lifecycle/expiry and review date within one year. |
| [WorkRelationship](work-relationship.md) | business_aggregate | 2 | 2 | One stable source worker ID belongs permanently to one person; end date/status and sponsor/self-manager constraints must form valid governed relationship at each revision. |
| [WorkforceObservationResolution](workforce-observation-resolution.md) | business_aggregate | 1 | 1 | One observation may receive one final attributed resolved/dismissed decision; changed resolution intent conflicts. |
| [WorkforceSourceObservation](workforce-source-observation.md) | business_aggregate | 2 | 2 | One immutable external source revision has one target/fact correlation and at most one final attributed reconciliation decision tied to exact canonical revision. |

Full selected owner records: [data/aggregates](../data/aggregates).
