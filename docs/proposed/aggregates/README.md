# Aggregate and technical-owner index

Selected target: **132 business/authority owners** and **14 technical/storage responsibilities**. Technical responsibilities are not business aggregates.

Groups are derived from source feature paths. They are navigation labels, not bounded-context decisions.

## [Placeholder: source capability area is not recorded]

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [ArtifactDownloadGrant](artifact-download-grant.md) | business_aggregate | 2 | 2 | One exact receiving grant owns the authenticated encrypted original artifact ticket/result after EvidenceArtifact first admitted and pinned availability with frozen202. Complete required held source uses and exact issue DecisionAt precede native issued-v2; cancellation fences delayed issuance and terminal proofs settle all uses. Original expiry never renews under the same root identity. Existing artifact-issued v1 decoder remains read-only compatibility. |
| [CommitmentControlLink](commitment-control-link.md) | business_aggregate | 3 | 3 | One commitment-to-control link has one pending/accepted version line: each accepted version binds an exact commitment version to an exact control version with rationale, a successor needs independent review, and a retired link stays terminal (M0-D09 D09-09). Neither CommitmentDraft nor ControlDraft owns the other side's version list. |

## AccessControl

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |

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

## AttestRecords

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |

## AuditorRequests

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |

## AuditPackages

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |

## AuditPopulations

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |

## Boundaries

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |

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

## Exports

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |
| [ClientExport](client-export.md) | business_aggregate | 3 | 4 | Final export delivery requires archive counts matching frozen included/withheld inventory and a successful exact-manifest delivery for each required attest package. |

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

## Tenants

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |

## UserIdentities

| Owner | Class | Commands | Events | Invariant |
| --- | --- | --- | --- | --- |

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

Full selected owner records: [data/aggregates](../data/aggregates).
