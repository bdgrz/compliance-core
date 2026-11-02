# Domain naming review — Jev results

This is an advisory pass over each aggregate/technical-responsibility page and each selected command/event section in docs/proposed. It evaluates names against their captured meanings; it does not rename source contracts or establish domain authority. Jev returns typed judgments rather than explanations, so every result marked concern or uncertain needs human review before changing a name.

- Run status: **complete**
- Started: 2026-11-02T15:35:33.442Z
- Completed: 2026-11-02T15:37:44.131Z
- Source commit: `89c2ef38dce3ed370d00b1a2708b41a6c5f6ef63`
- Review script SHA-256: `446a2deeb36d8171702926a368ce2f841074f1b15a0f64c9842ac01d88a8d8e2`
- Pinned model: `jev-1.14.0`; returned model(s): `jev-1.14.0`.
- Input fingerprint: `f29f609b30e648bfd941423543dbc574d8e9a255627ef8e7e4721bc12a126f65`
- Jev input tokens recorded: 2152194.
- Per-item question set: clarity score plus business-user, auditor, engineering, jargon, and ambiguity judgments. Full typed answers are in [naming-review-jev.json](data/naming-review-jev.json).

## High-confidence fit rule

For business aggregates, a fit requires P(clear)=0.80 or higher, score confidence at least 0.75, business-user/auditor/engineering probabilities at least 0.80, and jargon/ambiguity probabilities no greater than 0.20. Technical responsibility labels are judged for engineering clarity, jargon, and ambiguity; they are not treated as business aggregates. A high-confidence concern is a review signal, not an automatic rename. All other outcomes remain uncertain.

## Coverage summary

| Name type | Reviewed | High-confidence fit | High-confidence concern | Uncertain |
| --- | ---: | ---: | ---: | ---: |
| aggregate | 131 | 0 | 11 | 120 |
| technical_responsibility | 14 | 0 | 3 | 11 |
| command | 507 | 0 | 73 | 434 |
| event | 505 | 0 | 42 | 463 |

## Aggregate names

| Name | Owner | P(clear) | Score confidence | Business user | Auditor | Engineering | Jargon risk | Ambiguity risk | Jev result |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| [AccessExpectation](aggregates/access-expectation.md) | AccessExpectation | 0.14 | 0.70 | 0.31 | 0.52 | 0.53 | 0.19 | 0.76 | uncertain; review manually |
| [AccessGrant](aggregates/access-grant.md) | AccessGrant | 0.88 | 0.88 | 0.77 | 0.83 | 0.66 | 0.09 | 0.63 | uncertain; review manually |
| [AccessPopulationException](aggregates/access-population-exception.md) | AccessPopulationException | 0.07 | 0.73 | 0.23 | 0.51 | 0.70 | 0.21 | 0.68 | uncertain; review manually |
| [AccessPopulation](aggregates/access-population.md) | AccessPopulation | 0.14 | 0.71 | 0.24 | 0.50 | 0.68 | 0.25 | 0.70 | uncertain; review manually |
| [AccessPrincipalClassification](aggregates/access-principal-classification.md) | AccessPrincipalClassification | 0.34 | 0.64 | 0.52 | 0.79 | 0.79 | 0.12 | 0.50 | uncertain; review manually |
| [AccessReviewCampaign](aggregates/access-review-campaign.md) | AccessReviewCampaign | 0.83 | 0.82 | 0.78 | 0.81 | 0.82 | 0.12 | 0.42 | uncertain; review manually |
| [AccessReviewDecisionBatch](aggregates/access-review-decision-batch.md) | AccessReviewDecisionBatch | 0.46 | 0.51 | 0.43 | 0.68 | 0.83 | 0.29 | 0.50 | uncertain; review manually |
| [AccessReviewItem](aggregates/access-review-item.md) | AccessReviewItem | 0.78 | 0.78 | 0.72 | 0.79 | 0.82 | 0.10 | 0.41 | uncertain; review manually |
| [AdvisorFeedbackThread](aggregates/advisor-feedback-thread.md) | AdvisorFeedbackThread | 0.64 | 0.62 | 0.76 | 0.81 | 0.84 | 0.22 | 0.35 | uncertain; review manually |
| [AdvisorHandoff](aggregates/advisor-handoff.md) | AdvisorHandoff | 0.30 | 0.64 | 0.40 | 0.62 | 0.73 | 0.19 | 0.59 | uncertain; review manually |
| [ApplicationImportBatch](aggregates/application-import-batch.md) | ApplicationImportBatch | 0.28 | 0.61 | 0.64 | 0.71 | 0.75 | 0.24 | 0.56 | uncertain; review manually |
| [ApplicationImportSource](aggregates/application-import-source.md) | ApplicationImportSource | 0.23 | 0.71 | 0.36 | 0.55 | 0.76 | 0.19 | 0.63 | uncertain; review manually |
| [ApplicationNameClaim](aggregates/application-name-claim.md) | ApplicationNameClaim | 0.54 | 0.52 | 0.41 | 0.72 | 0.83 | 0.13 | 0.44 | uncertain; review manually |
| [ApplicationRelationship](aggregates/application-relationship.md) | ApplicationRelationship | 0.68 | 0.67 | 0.52 | 0.68 | 0.66 | 0.12 | 0.70 | uncertain; review manually |
| [ArtifactDisclosure](aggregates/artifact-disclosure.md) | ArtifactDisclosure | 0.72 | 0.71 | 0.58 | 0.82 | 0.78 | 0.11 | 0.53 | uncertain; review manually |
| [ArtifactDisposition](aggregates/artifact-disposition.md) | ArtifactDisposition | 0.57 | 0.56 | 0.45 | 0.76 | 0.79 | 0.12 | 0.51 | uncertain; review manually |
| [ArtifactDownloadGrant](aggregates/artifact-download-grant.md) | ArtifactDownloadGrant | 0.46 | 0.50 | 0.51 | 0.68 | 0.82 | 0.28 | 0.54 | uncertain; review manually |
| [ArtifactEngagementHold](aggregates/artifact-engagement-hold.md) | ArtifactEngagementHold | 0.38 | 0.58 | 0.38 | 0.73 | 0.80 | 0.12 | 0.50 | uncertain; review manually |
| [ArtifactLegalHold](aggregates/artifact-legal-hold.md) | ArtifactLegalHold | 0.73 | 0.72 | 0.68 | 0.86 | 0.81 | 0.09 | 0.40 | uncertain; review manually |
| [ArtifactRetentionBasis](aggregates/artifact-retention-basis.md) | ArtifactRetentionBasis | 0.37 | 0.60 | 0.39 | 0.72 | 0.79 | 0.15 | 0.52 | uncertain; review manually |
| [AuditObservation](aggregates/audit-observation.md) | AuditObservation | 0.58 | 0.55 | 0.65 | 0.75 | 0.75 | 0.13 | 0.57 | uncertain; review manually |
| [AuditOutcome](aggregates/audit-outcome.md) | AuditOutcome | 0.90 | 0.90 | 0.82 | 0.87 | 0.82 | 0.08 | 0.42 | uncertain; review manually |
| [AuditPackageDelivery](aggregates/audit-package-delivery.md) | AuditPackageDelivery | 0.66 | 0.65 | 0.72 | 0.76 | 0.71 | 0.15 | 0.62 | uncertain; review manually |
| [AuditPackageManifest](aggregates/audit-package-manifest.md) | AuditPackageManifest | 0.64 | 0.63 | 0.51 | 0.80 | 0.85 | 0.13 | 0.40 | uncertain; review manually |
| [AuditPopulation](aggregates/audit-population.md) | AuditPopulation | 0.52 | 0.50 | 0.35 | 0.59 | 0.74 | 0.12 | 0.61 | uncertain; review manually |
| [AuditorFeedbackThread](aggregates/auditor-feedback-thread.md) | AuditorFeedbackThread | 0.70 | 0.69 | 0.70 | 0.79 | 0.77 | 0.29 | 0.48 | uncertain; review manually |
| [AuditorRequest](aggregates/auditor-request.md) | AuditorRequest | 0.81 | 0.81 | 0.70 | 0.74 | 0.77 | 0.10 | 0.50 | uncertain; review manually |
| [AuditorResponse](aggregates/auditor-response.md) | AuditorResponse | 0.86 | 0.86 | 0.74 | 0.81 | 0.81 | 0.08 | 0.45 | uncertain; review manually |
| [AuditorSampleSelection](aggregates/auditor-sample-selection.md) | AuditorSampleSelection | 0.68 | 0.67 | 0.68 | 0.80 | 0.84 | 0.11 | 0.36 | uncertain; review manually |
| [ClientExport](aggregates/client-export.md) | ClientExport | 0.56 | 0.53 | 0.56 | 0.69 | 0.66 | 0.18 | 0.64 | uncertain; review manually |
| [ClientIndependenceDecision](aggregates/client-independence-decision.md) | ClientIndependenceDecision | 0.61 | 0.60 | 0.63 | 0.78 | 0.73 | 0.11 | 0.57 | uncertain; review manually |
| [ClientServiceIndependence](aggregates/client-service-independence.md) | ClientServiceIndependence | 0.30 | 0.65 | 0.45 | 0.67 | 0.60 | 0.14 | 0.69 | uncertain; review manually |
| [ClientService](aggregates/client-service.md) | ClientService | 0.47 | 0.48 | 0.41 | 0.41 | 0.37 | 0.13 | 0.81 | high-confidence concern |
| [CommitmentControlLink](aggregates/commitment-control-link.md) | CommitmentControlLink | 0.15 | 0.60 | 0.42 | 0.67 | 0.71 | 0.25 | 0.55 | uncertain; review manually |
| [CommitmentDraft](aggregates/commitment-draft.md) | CommitmentDraft | 0.42 | 0.51 | 0.55 | 0.76 | 0.80 | 0.15 | 0.46 | uncertain; review manually |
| [ComplianceIncident](aggregates/compliance-incident.md) | ComplianceIncident | 0.90 | 0.90 | 0.89 | 0.89 | 0.79 | 0.07 | 0.54 | uncertain; review manually |
| [ComplianceProgram](aggregates/compliance-program.md) | ComplianceProgram | 0.94 | 0.94 | 0.84 | 0.86 | 0.79 | 0.06 | 0.41 | uncertain; review manually |
| [ControlCriterionMapping](aggregates/control-criterion-mapping.md) | ControlCriterionMapping | 0.44 | 0.54 | 0.42 | 0.75 | 0.81 | 0.16 | 0.41 | uncertain; review manually |
| [ControlDraft](aggregates/control-draft.md) | ControlDraft | 0.42 | 0.51 | 0.47 | 0.73 | 0.79 | 0.15 | 0.44 | uncertain; review manually |
| [ControlEvaluationPlan](aggregates/control-evaluation-plan.md) | ControlEvaluationPlan | 0.77 | 0.76 | 0.70 | 0.80 | 0.83 | 0.14 | 0.37 | uncertain; review manually |
| [ControlEvaluation](aggregates/control-evaluation.md) | ControlEvaluation | 0.80 | 0.80 | 0.72 | 0.85 | 0.81 | 0.09 | 0.35 | uncertain; review manually |
| [ControlOccurrence](aggregates/control-occurrence.md) | ControlOccurrence | 0.38 | 0.60 | 0.39 | 0.74 | 0.74 | 0.14 | 0.66 | uncertain; review manually |
| [ControlOperatingPlan](aggregates/control-operating-plan.md) | ControlOperatingPlan | 0.39 | 0.59 | 0.60 | 0.74 | 0.76 | 0.14 | 0.63 | uncertain; review manually |
| [CorrectiveAction](aggregates/corrective-action.md) | CorrectiveAction | 0.73 | 0.68 | 0.87 | 0.89 | 0.67 | 0.06 | 0.56 | uncertain; review manually |
| [CriteriaTextOverlay](aggregates/criteria-text-overlay.md) | CriteriaTextOverlay | 0.04 | 0.67 | 0.12 | 0.54 | 0.78 | 0.34 | 0.63 | high-confidence concern |
| [CriterionApplicability](aggregates/criterion-applicability.md) | CriterionApplicability | 0.52 | 0.51 | 0.52 | 0.81 | 0.79 | 0.09 | 0.53 | uncertain; review manually |
| [DataFlow](aggregates/data-flow.md) | DataFlow | 0.40 | 0.57 | 0.32 | 0.70 | 0.54 | 0.24 | 0.77 | uncertain; review manually |
| [DeclaredApplication](aggregates/declared-application.md) | DeclaredApplication | 0.38 | 0.56 | 0.49 | 0.71 | 0.78 | 0.23 | 0.47 | uncertain; review manually |
| [DeclaredSystemInstance](aggregates/declared-system-instance.md) | DeclaredSystemInstance | 0.41 | 0.55 | 0.35 | 0.70 | 0.83 | 0.17 | 0.42 | uncertain; review manually |
| [EmailAddress](aggregates/email-address.md) | EmailAddress | 0.70 | 0.66 | 0.86 | 0.88 | 0.72 | 0.09 | 0.56 | uncertain; review manually |
| [EngagementManagementAcknowledgement](aggregates/engagement-management-acknowledgement.md) | EngagementManagementAcknowledgement | 0.31 | 0.64 | 0.51 | 0.77 | 0.72 | 0.11 | 0.55 | uncertain; review manually |
| [EvidenceArtifact](aggregates/evidence-artifact.md) | EvidenceArtifact | 0.17 | 0.66 | 0.48 | 0.77 | 0.78 | 0.18 | 0.50 | uncertain; review manually |
| [EvidenceHandlingPolicy](aggregates/evidence-handling-policy.md) | EvidenceHandlingPolicy | 0.55 | 0.53 | 0.77 | 0.81 | 0.68 | 0.11 | 0.61 | uncertain; review manually |
| [EvidenceRedaction](aggregates/evidence-redaction.md) | EvidenceRedaction | 0.70 | 0.69 | 0.76 | 0.85 | 0.79 | 0.09 | 0.54 | uncertain; review manually |
| [EvidenceRequest](aggregates/evidence-request.md) | EvidenceRequest | 0.82 | 0.81 | 0.78 | 0.74 | 0.64 | 0.11 | 0.66 | uncertain; review manually |
| [EvidenceSupport](aggregates/evidence-support.md) | EvidenceSupport | 0.30 | 0.56 | 0.27 | 0.58 | 0.35 | 0.25 | 0.73 | uncertain; review manually |
| [ExaminationEngagement](aggregates/examination-engagement.md) | ExaminationEngagement | 0.77 | 0.77 | 0.72 | 0.84 | 0.81 | 0.07 | 0.49 | uncertain; review manually |
| [ExaminationSignOff](aggregates/examination-sign-off.md) | ExaminationSignOff | 0.76 | 0.75 | 0.74 | 0.83 | 0.79 | 0.09 | 0.52 | uncertain; review manually |
| [FindingAggregate](aggregates/finding-aggregate.md) | FindingAggregate | 0.29 | 0.58 | 0.37 | 0.64 | 0.52 | 0.48 | 0.61 | uncertain; review manually |
| [FirmProfessionalDutyDesignation](aggregates/firm-professional-duty-designation.md) | FirmProfessionalDutyDesignation | 0.24 | 0.72 | 0.37 | 0.75 | 0.71 | 0.14 | 0.62 | uncertain; review manually |
| [FirmStaffIdentityClaim](aggregates/firm-staff-identity-claim.md) | FirmStaffIdentityClaim | 0.19 | 0.67 | 0.29 | 0.63 | 0.75 | 0.33 | 0.60 | uncertain; review manually |
| [FirmStaffRecord](aggregates/firm-staff-record.md) | FirmStaffRecord | 0.40 | 0.53 | 0.79 | 0.75 | 0.64 | 0.20 | 0.60 | uncertain; review manually |
| [IdentityRecovery](aggregates/identity-recovery.md) | IdentityRecovery | 0.57 | 0.55 | 0.60 | 0.67 | 0.69 | 0.18 | 0.67 | uncertain; review manually |
| [ImmutableSnapshot](aggregates/immutable-snapshot.md) | ImmutableSnapshot | 0.14 | 0.54 | 0.29 | 0.59 | 0.58 | 0.42 | 0.62 | uncertain; review manually |
| [ImportedOwnerResolution](aggregates/imported-owner-resolution.md) | ImportedOwnerResolution | 0.18 | 0.65 | 0.41 | 0.68 | 0.77 | 0.25 | 0.51 | uncertain; review manually |
| [IndependenceRuleCatalog](aggregates/independence-rule-catalog.md) | IndependenceRuleCatalog | 0.46 | 0.51 | 0.52 | 0.75 | 0.62 | 0.20 | 0.58 | uncertain; review manually |
| [IndependenceRuleRatificationCatalog](aggregates/independence-rule-ratification-catalog.md) | IndependenceRuleRatificationCatalog | 0.23 | 0.69 | 0.29 | 0.63 | 0.75 | 0.26 | 0.56 | uncertain; review manually |
| [InformationAsset](aggregates/information-asset.md) | InformationAsset | 0.45 | 0.51 | 0.55 | 0.74 | 0.63 | 0.18 | 0.68 | uncertain; review manually |
| [LocationNameClaim](aggregates/location-name-claim.md) | LocationNameClaim | 0.38 | 0.54 | 0.43 | 0.68 | 0.82 | 0.18 | 0.55 | uncertain; review manually |
| [Location](aggregates/location.md) | Location | 0.92 | 0.92 | 0.68 | 0.70 | 0.55 | 0.09 | 0.72 | uncertain; review manually |
| [ManagementAssertion](aggregates/management-assertion.md) | ManagementAssertion | 0.56 | 0.53 | 0.57 | 0.74 | 0.71 | 0.11 | 0.60 | uncertain; review manually |
| [ManagementRepresentationLetter](aggregates/management-representation-letter.md) | ManagementRepresentationLetter | 0.90 | 0.90 | 0.83 | 0.89 | 0.85 | 0.07 | 0.30 | uncertain; review manually |
| [Member](aggregates/member.md) | Member | 0.42 | 0.53 | 0.40 | 0.59 | 0.41 | 0.10 | 0.80 | high-confidence concern |
| [MethodologyTemplateContribution](aggregates/methodology-template-contribution.md) | MethodologyTemplateContribution | 0.19 | 0.68 | 0.36 | 0.73 | 0.80 | 0.16 | 0.57 | uncertain; review manually |
| [MethodologyTemplate](aggregates/methodology-template.md) | MethodologyTemplate | 0.67 | 0.66 | 0.48 | 0.77 | 0.77 | 0.11 | 0.64 | uncertain; review manually |
| [OperationalProcessNameClaim](aggregates/operational-process-name-claim.md) | OperationalProcessNameClaim | 0.08 | 0.60 | 0.19 | 0.50 | 0.79 | 0.41 | 0.64 | high-confidence concern |
| [OperationalProcess](aggregates/operational-process.md) | OperationalProcess | 0.57 | 0.56 | 0.42 | 0.68 | 0.72 | 0.17 | 0.74 | uncertain; review manually |
| [PartnerIndependenceDecision](aggregates/partner-independence-decision.md) | PartnerIndependenceDecision | 0.40 | 0.57 | 0.38 | 0.74 | 0.72 | 0.10 | 0.66 | uncertain; review manually |
| [Person](aggregates/person.md) | Person | 0.73 | 0.72 | 0.56 | 0.68 | 0.52 | 0.07 | 0.74 | uncertain; review manually |
| [PlatformOperatorRoster](aggregates/platform-operator-roster.md) | PlatformOperatorRoster | 0.45 | 0.51 | 0.26 | 0.71 | 0.81 | 0.21 | 0.54 | uncertain; review manually |
| [PolicyCampaignParticipant](aggregates/policy-campaign-participant.md) | PolicyCampaignParticipant | 0.55 | 0.53 | 0.59 | 0.83 | 0.80 | 0.11 | 0.53 | uncertain; review manually |
| [PolicyDistributionCampaign](aggregates/policy-distribution-campaign.md) | PolicyDistributionCampaign | 0.44 | 0.55 | 0.43 | 0.77 | 0.78 | 0.19 | 0.48 | uncertain; review manually |
| [Policy](aggregates/policy.md) | Policy | 0.95 | 0.95 | 0.83 | 0.77 | 0.43 | 0.06 | 0.75 | uncertain; review manually |
| [PopulationSnapshot](aggregates/population-snapshot.md) | PopulationSnapshot | 0.62 | 0.61 | 0.26 | 0.67 | 0.76 | 0.24 | 0.68 | uncertain; review manually |
| [PracticeIndependenceCommitment](aggregates/practice-independence-commitment.md) | PracticeIndependenceCommitment | 0.26 | 0.69 | 0.36 | 0.68 | 0.76 | 0.13 | 0.61 | uncertain; review manually |
| [ProfessionalAssertionIdentityClaim](aggregates/professional-assertion-identity-claim.md) | ProfessionalAssertionIdentityClaim | 0.06 | 0.71 | 0.13 | 0.46 | 0.68 | 0.26 | 0.71 | high-confidence concern |
| [ProgramReadiness](aggregates/program-readiness.md) | ProgramReadiness | 0.47 | 0.49 | 0.63 | 0.69 | 0.71 | 0.11 | 0.65 | uncertain; review manually |
| [ProgramRollover](aggregates/program-rollover.md) | ProgramRollover | 0.44 | 0.53 | 0.37 | 0.62 | 0.74 | 0.18 | 0.70 | uncertain; review manually |
| [ProviderAssuranceGap](aggregates/provider-assurance-gap.md) | ProviderAssuranceGap | 0.60 | 0.58 | 0.65 | 0.81 | 0.82 | 0.09 | 0.42 | uncertain; review manually |
| [ProviderAssuranceReport](aggregates/provider-assurance-report.md) | ProviderAssuranceReport | 0.79 | 0.79 | 0.80 | 0.89 | 0.86 | 0.08 | 0.37 | uncertain; review manually |
| [ProviderAssuranceReview](aggregates/provider-assurance-review.md) | ProviderAssuranceReview | 0.77 | 0.77 | 0.77 | 0.85 | 0.83 | 0.08 | 0.50 | uncertain; review manually |
| [ProviderNameClaim](aggregates/provider-name-claim.md) | ProviderNameClaim | 0.44 | 0.51 | 0.35 | 0.63 | 0.79 | 0.20 | 0.59 | uncertain; review manually |
| [ProviderRiskAcceptance](aggregates/provider-risk-acceptance.md) | ProviderRiskAcceptance | 0.54 | 0.51 | 0.68 | 0.75 | 0.73 | 0.11 | 0.65 | uncertain; review manually |
| [Provider](aggregates/provider.md) | Provider | 0.74 | 0.73 | 0.71 | 0.70 | 0.51 | 0.09 | 0.68 | uncertain; review manually |
| [ReadinessAssessment](aggregates/readiness-assessment.md) | ReadinessAssessment | 0.70 | 0.69 | 0.79 | 0.81 | 0.79 | 0.11 | 0.50 | uncertain; review manually |
| [ReadinessGapPlan](aggregates/readiness-gap-plan.md) | ReadinessGapPlan | 0.60 | 0.58 | 0.64 | 0.74 | 0.76 | 0.16 | 0.57 | uncertain; review manually |
| [RiskDraft](aggregates/risk-draft.md) | RiskDraft | 0.24 | 0.60 | 0.55 | 0.72 | 0.60 | 0.27 | 0.68 | uncertain; review manually |
| [RiskEvaluation](aggregates/risk-evaluation.md) | RiskEvaluation | 0.78 | 0.78 | 0.77 | 0.86 | 0.80 | 0.07 | 0.51 | uncertain; review manually |
| [RiskGovernance](aggregates/risk-governance.md) | RiskGovernance | 0.69 | 0.68 | 0.58 | 0.77 | 0.66 | 0.09 | 0.68 | uncertain; review manually |
| [RiskMethod](aggregates/risk-method.md) | RiskMethod | 0.59 | 0.58 | 0.36 | 0.70 | 0.79 | 0.10 | 0.49 | uncertain; review manually |
| [RiskTreatmentAction](aggregates/risk-treatment-action.md) | RiskTreatmentAction | 0.60 | 0.58 | 0.64 | 0.79 | 0.71 | 0.13 | 0.54 | uncertain; review manually |
| [RiskTreatmentAssertion](aggregates/risk-treatment-assertion.md) | RiskTreatmentAssertion | 0.18 | 0.70 | 0.29 | 0.64 | 0.80 | 0.17 | 0.51 | uncertain; review manually |
| [RolePermission](aggregates/role-permission.md) | RolePermission | 0.62 | 0.58 | 0.70 | 0.84 | 0.75 | 0.10 | 0.41 | uncertain; review manually |
| [Role](aggregates/role.md) | Role | 0.95 | 0.95 | 0.75 | 0.85 | 0.60 | 0.06 | 0.57 | uncertain; review manually |
| [SeparationOfDutiesWaiver](aggregates/separation-of-duties-waiver.md) | SeparationOfDutiesWaiver | 0.72 | 0.72 | 0.72 | 0.91 | 0.86 | 0.07 | 0.38 | uncertain; review manually |
| [ServiceEngagementIndependence](aggregates/service-engagement-independence.md) | ServiceEngagementIndependence | 0.30 | 0.67 | 0.53 | 0.73 | 0.68 | 0.12 | 0.64 | uncertain; review manually |
| [ServiceIdentity](aggregates/service-identity.md) | ServiceIdentity | 0.41 | 0.55 | 0.28 | 0.56 | 0.67 | 0.16 | 0.69 | uncertain; review manually |
| [SignificantChange](aggregates/significant-change.md) | SignificantChange | 0.70 | 0.69 | 0.58 | 0.75 | 0.65 | 0.08 | 0.68 | uncertain; review manually |
| [SystemBoundary](aggregates/system-boundary.md) | SystemBoundary | 0.67 | 0.67 | 0.51 | 0.74 | 0.80 | 0.09 | 0.51 | uncertain; review manually |
| [SystemDescription](aggregates/system-description.md) | SystemDescription | 0.65 | 0.64 | 0.58 | 0.76 | 0.76 | 0.13 | 0.60 | uncertain; review manually |
| [SystemInstanceAccessReviewScope](aggregates/system-instance-access-review-scope.md) | SystemInstanceAccessReviewScope | 0.40 | 0.58 | 0.43 | 0.71 | 0.77 | 0.21 | 0.49 | uncertain; review manually |
| [TeamMember](aggregates/team-member.md) | TeamMember | 0.68 | 0.66 | 0.81 | 0.83 | 0.72 | 0.09 | 0.57 | uncertain; review manually |
| [TeamRole](aggregates/team-role.md) | TeamRole | 0.68 | 0.65 | 0.67 | 0.78 | 0.73 | 0.09 | 0.52 | uncertain; review manually |
| [Team](aggregates/team.md) | Team | 0.97 | 0.97 | 0.84 | 0.81 | 0.51 | 0.06 | 0.62 | uncertain; review manually |
| [TechnologyComponent](aggregates/technology-component.md) | TechnologyComponent | 0.30 | 0.65 | 0.26 | 0.63 | 0.72 | 0.20 | 0.61 | uncertain; review manually |
| [TemplateApplication](aggregates/template-application.md) | TemplateApplication | 0.34 | 0.59 | 0.31 | 0.54 | 0.64 | 0.18 | 0.63 | uncertain; review manually |
| [TenantIdentityProvider](aggregates/tenant-identity-provider.md) | TenantIdentityProvider | 0.10 | 0.63 | 0.20 | 0.59 | 0.59 | 0.31 | 0.69 | high-confidence concern |
| [TenantInvitationMailboxClaim](aggregates/tenant-invitation-mailbox-claim.md) | TenantInvitationMailboxClaim | 0.17 | 0.62 | 0.27 | 0.60 | 0.77 | 0.35 | 0.62 | uncertain; review manually |
| [TenantInvitation](aggregates/tenant-invitation.md) | TenantInvitation | 0.96 | 0.96 | 0.86 | 0.88 | 0.78 | 0.08 | 0.42 | uncertain; review manually |
| [TenantManagerGuard](aggregates/tenant-manager-guard.md) | TenantManagerGuard | 0.08 | 0.59 | 0.17 | 0.55 | 0.73 | 0.41 | 0.59 | high-confidence concern |
| [TenantSlug](aggregates/tenant-slug.md) | TenantSlug | 0.30 | 0.56 | 0.12 | 0.51 | 0.85 | 0.33 | 0.46 | high-confidence concern |
| [Tenant](aggregates/tenant.md) | Tenant | 0.86 | 0.85 | 0.51 | 0.67 | 0.61 | 0.11 | 0.57 | uncertain; review manually |
| [TrainingRequirement](aggregates/training-requirement.md) | TrainingRequirement | 0.90 | 0.90 | 0.85 | 0.87 | 0.77 | 0.08 | 0.45 | uncertain; review manually |
| [TypeIIObservationCalendar](aggregates/type-ii-observation-calendar.md) | TypeIIObservationCalendar | 0.17 | 0.72 | 0.17 | 0.56 | 0.76 | 0.23 | 0.52 | high-confidence concern |
| [TypeIIPeriod](aggregates/type-ii-period.md) | TypeIIPeriod | 0.14 | 0.76 | 0.16 | 0.41 | 0.78 | 0.15 | 0.52 | high-confidence concern |
| [TypeIIPlan](aggregates/type-ii-plan.md) | TypeIIPlan | 0.26 | 0.67 | 0.19 | 0.54 | 0.78 | 0.12 | 0.46 | high-confidence concern |
| [UserIdentity](aggregates/user-identity.md) | UserIdentity | 0.64 | 0.63 | 0.58 | 0.66 | 0.60 | 0.13 | 0.74 | uncertain; review manually |
| [WorkItemAssignment](aggregates/work-item-assignment.md) | WorkItemAssignment | 0.71 | 0.70 | 0.82 | 0.86 | 0.83 | 0.10 | 0.48 | uncertain; review manually |
| [WorkRelationship](aggregates/work-relationship.md) | WorkRelationship | 0.43 | 0.53 | 0.45 | 0.68 | 0.60 | 0.11 | 0.73 | uncertain; review manually |
| [WorkforceObservationResolution](aggregates/workforce-observation-resolution.md) | WorkforceObservationResolution | 0.14 | 0.68 | 0.35 | 0.68 | 0.75 | 0.21 | 0.55 | uncertain; review manually |
| [WorkforceSourceObservation](aggregates/workforce-source-observation.md) | WorkforceSourceObservation | 0.10 | 0.79 | 0.25 | 0.67 | 0.80 | 0.18 | 0.55 | uncertain; review manually |

## Technical responsibility labels

| Name | Owner | P(clear) | Score confidence | Business user | Auditor | Engineering | Jargon risk | Ambiguity risk | Jev result |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| [AuditPackageBuildProcess](aggregates/audit-package-build-process.md) | AuditPackageBuildProcess | 0.70 | 0.69 | 0.20 | 0.60 | 0.66 | 0.70 | 0.66 | uncertain; review manually |
| [AuditPopulationExportProcess](aggregates/audit-population-export-process.md) | AuditPopulationExportProcess | 0.61 | 0.58 | 0.19 | 0.58 | 0.65 | 0.57 | 0.65 | uncertain; review manually |
| [EmailChallengeDeliveryLedger](aggregates/email-challenge-delivery-ledger.md) | EmailChallengeDeliveryLedger | 0.79 | 0.67 | 0.17 | 0.64 | 0.71 | 0.69 | 0.61 | uncertain; review manually |
| [FindingMigrationAggregate](aggregates/finding-migration-aggregate.md) | FindingMigrationAggregate | 0.59 | 0.51 | 0.13 | 0.46 | 0.62 | 0.82 | 0.62 | high-confidence concern |
| [IdentityRecoveryNoticeProcess](aggregates/identity-recovery-notice-process.md) | IdentityRecoveryNoticeProcess | 0.81 | 0.80 | 0.29 | 0.59 | 0.69 | 0.62 | 0.63 | uncertain; review manually |
| [PopulationSnapshotChunk](aggregates/population-snapshot-chunk.md) | PopulationSnapshotChunk | 0.58 | 0.53 | 0.08 | 0.25 | 0.76 | 0.88 | 0.61 | high-confidence concern |
| [ProgramScopeSnapshotProcess](aggregates/program-scope-snapshot-process.md) | ProgramScopeSnapshotProcess | 0.55 | 0.52 | 0.11 | 0.44 | 0.63 | 0.75 | 0.66 | uncertain; review manually |
| [RemediationMigrationFenceAggregate](aggregates/remediation-migration-fence-aggregate.md) | RemediationMigrationFenceAggregate | 0.37 | 0.52 | 0.05 | 0.19 | 0.63 | 0.88 | 0.67 | high-confidence concern |
| [SystemDescriptionExportProcess](aggregates/system-description-export-process.md) | SystemDescriptionExportProcess | 0.80 | 0.79 | 0.26 | 0.56 | 0.70 | 0.75 | 0.66 | uncertain; review manually |
| [TenantInvitationDeliveryProcess](aggregates/tenant-invitation-delivery-process.md) | TenantInvitationDeliveryProcess | 0.84 | 0.84 | 0.27 | 0.66 | 0.71 | 0.70 | 0.62 | uncertain; review manually |
| [WeeklyWorkDigest](aggregates/weekly-work-digest.md) | WeeklyWorkDigest | 0.38 | 0.53 | 0.36 | 0.58 | 0.46 | 0.39 | 0.65 | uncertain; review manually |
| [WorkDigestDispatch](aggregates/work-digest-dispatch.md) | WorkDigestDispatch | 0.71 | 0.70 | 0.17 | 0.56 | 0.76 | 0.75 | 0.58 | uncertain; review manually |
| [WorkDigestPreference](aggregates/work-digest-preference.md) | WorkDigestPreference | 0.87 | 0.87 | 0.43 | 0.77 | 0.82 | 0.24 | 0.38 | uncertain; review manually |
| [WorkforceRosterSnapshotProcess](aggregates/workforce-roster-snapshot-process.md) | WorkforceRosterSnapshotProcess | 0.80 | 0.79 | 0.24 | 0.70 | 0.73 | 0.49 | 0.60 | uncertain; review manually |

## Command names

| Name | Owner | P(clear) | Score confidence | Business user | Auditor | Engineering | Jargon risk | Ambiguity risk | Jev result |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| [ApproveAccessExpectation](commands/access-expectation.md#approveaccessexpectation) | AccessExpectation | 0.57 | 0.53 | 0.34 | 0.63 | 0.81 | 0.20 | 0.52 | uncertain; review manually |
| [ExemptAccessExpectation](commands/access-expectation.md#exemptaccessexpectation) | AccessExpectation | 0.33 | 0.53 | 0.27 | 0.55 | 0.73 | 0.22 | 0.69 | uncertain; review manually |
| [ProposeAccessExpectation](commands/access-expectation.md#proposeaccessexpectation) | AccessExpectation | 0.38 | 0.51 | 0.30 | 0.59 | 0.83 | 0.23 | 0.59 | uncertain; review manually |
| [GrantAccess](commands/access-grant.md#grantaccess) | AccessGrant | 0.76 | 0.75 | 0.81 | 0.77 | 0.53 | 0.12 | 0.65 | uncertain; review manually |
| [ProvisionGuestProgramAccess](commands/access-grant.md#provisionguestprogramaccess) | AccessGrant | 0.62 | 0.53 | 0.68 | 0.78 | 0.73 | 0.25 | 0.62 | uncertain; review manually |
| [RevokeAccessGrant](commands/access-grant.md#revokeaccessgrant) | AccessGrant | 0.97 | 0.97 | 0.84 | 0.89 | 0.85 | 0.09 | 0.33 | uncertain; review manually |
| [ExemptMissingAccessPopulation](commands/access-population-exception.md#exemptmissingaccesspopulation) | AccessPopulationException | 0.26 | 0.56 | 0.18 | 0.45 | 0.76 | 0.40 | 0.66 | high-confidence concern |
| [AcceptAccessPopulation](commands/access-population.md#acceptaccesspopulation) | AccessPopulation | 0.35 | 0.52 | 0.27 | 0.56 | 0.75 | 0.27 | 0.57 | uncertain; review manually |
| [OpenAccessPopulation](commands/access-population.md#openaccesspopulation) | AccessPopulation | 0.04 | 0.41 | 0.20 | 0.39 | 0.54 | 0.59 | 0.76 | high-confidence concern |
| [RecordAccessPopulationFacts](commands/access-population.md#recordaccesspopulationfacts) | AccessPopulation | 0.22 | 0.02 | 0.44 | 0.71 | 0.77 | 0.38 | 0.58 | uncertain; review manually |
| [ClassifyAccessPrincipal](commands/access-principal-classification.md#classifyaccessprincipal) | AccessPrincipalClassification | 0.81 | 0.80 | 0.54 | 0.80 | 0.82 | 0.11 | 0.49 | uncertain; review manually |
| [CompleteAccessReviewCampaign](commands/access-review-campaign.md#completeaccessreviewcampaign) | AccessReviewCampaign | 0.66 | 0.62 | 0.66 | 0.69 | 0.71 | 0.25 | 0.65 | uncertain; review manually |
| [LaunchAccessReviewCampaign](commands/access-review-campaign.md#launchaccessreviewcampaign) | AccessReviewCampaign | 0.77 | 0.75 | 0.85 | 0.86 | 0.83 | 0.13 | 0.36 | uncertain; review manually |
| [RecordBulkAccessDecision](commands/access-review-campaign.md#recordbulkaccessdecision) | AccessReviewCampaign | 0.36 | 0.26 | 0.61 | 0.73 | 0.70 | 0.27 | 0.57 | uncertain; review manually |
| [AbortAccessReviewCampaignCompletion](commands/access-review-campaign.md#abortaccessreviewcampaigncompletion) | AccessReviewCampaign | 0.54 | 0.47 | 0.63 | 0.70 | 0.72 | 0.40 | 0.60 | uncertain; review manually |
| [CompleteAccessReviewCampaignClosure](commands/access-review-campaign.md#completeaccessreviewcampaignclosure) | AccessReviewCampaign | 0.28 | 0.33 | 0.40 | 0.50 | 0.57 | 0.43 | 0.67 | uncertain; review manually |
| [CancelAccessReviewDecisionBatch](commands/access-review-decision-batch.md#cancelaccessreviewdecisionbatch) | AccessReviewDecisionBatch | 0.78 | 0.76 | 0.63 | 0.77 | 0.76 | 0.18 | 0.54 | uncertain; review manually |
| [PublishAccessReviewDecisionBatch](commands/access-review-decision-batch.md#publishaccessreviewdecisionbatch) | AccessReviewDecisionBatch | 0.70 | 0.62 | 0.50 | 0.71 | 0.82 | 0.29 | 0.52 | uncertain; review manually |
| [CompleteAccessReviewDecisionBatchCancellation](commands/access-review-decision-batch.md#completeaccessreviewdecisionbatchcancellation) | AccessReviewDecisionBatch | 0.21 | 0.00 | 0.38 | 0.55 | 0.65 | 0.53 | 0.62 | uncertain; review manually |
| [MaterializeAccessReviewDecisionBatch](commands/access-review-decision-batch.md#materializeaccessreviewdecisionbatch) | AccessReviewDecisionBatch | 0.01 | 0.53 | 0.11 | 0.21 | 0.52 | 0.81 | 0.74 | high-confidence concern |
| [ExemptAccessRemediation](commands/access-review-item.md#exemptaccessremediation) | AccessReviewItem | 0.53 | 0.48 | 0.42 | 0.73 | 0.68 | 0.13 | 0.66 | uncertain; review manually |
| [ReassignAccessReviewResponsibility](commands/access-review-item.md#reassignaccessreviewresponsibility) | AccessReviewItem | 0.88 | 0.87 | 0.78 | 0.84 | 0.73 | 0.10 | 0.41 | uncertain; review manually |
| [RecordAccessDecision](commands/access-review-item.md#recordaccessdecision) | AccessReviewItem | 0.43 | 0.02 | 0.75 | 0.79 | 0.68 | 0.28 | 0.54 | uncertain; review manually |
| [RecordAccessRemediationChange](commands/access-review-item.md#recordaccessremediationchange) | AccessReviewItem | 0.52 | 0.25 | 0.65 | 0.78 | 0.82 | 0.27 | 0.45 | uncertain; review manually |
| [VerifyAccessRemediation](commands/access-review-item.md#verifyaccessremediation) | AccessReviewItem | 0.74 | 0.72 | 0.64 | 0.77 | 0.72 | 0.17 | 0.49 | uncertain; review manually |
| [MaterializeAccessReviewItem](commands/access-review-item.md#materializeaccessreviewitem) | AccessReviewItem | 0.00 | 0.81 | 0.09 | 0.17 | 0.38 | 0.85 | 0.78 | high-confidence concern |
| [ReserveAccessReviewItemDecision](commands/access-review-item.md#reserveaccessreviewitemdecision) | AccessReviewItem | 0.05 | 0.34 | 0.25 | 0.36 | 0.57 | 0.61 | 0.74 | uncertain; review manually |
| [ApplyAccessReviewItemDecision](commands/access-review-item.md#applyaccessreviewitemdecision) | AccessReviewItem | 0.32 | 0.50 | 0.51 | 0.62 | 0.63 | 0.36 | 0.61 | uncertain; review manually |
| [FenceAccessReviewItemDecision](commands/access-review-item.md#fenceaccessreviewitemdecision) | AccessReviewItem | 0.02 | 0.58 | 0.11 | 0.18 | 0.43 | 0.81 | 0.77 | high-confidence concern |
| [AuthorizeAccessReviewItemCampaignCompletionUse](commands/access-review-item.md#authorizeaccessreviewitemcampaigncompletionuse) | AccessReviewItem | 0.02 | 0.58 | 0.17 | 0.35 | 0.49 | 0.50 | 0.75 | high-confidence concern |
| [SettleAccessReviewItemCampaignCompletionUse](commands/access-review-item.md#settleaccessreviewitemcampaigncompletionuse) | AccessReviewItem | 0.03 | 0.53 | 0.10 | 0.27 | 0.46 | 0.60 | 0.78 | high-confidence concern |
| [SettleAccessReviewItemDecision](commands/access-review-item.md#settleaccessreviewitemdecision) | AccessReviewItem | 0.03 | 0.51 | 0.18 | 0.36 | 0.53 | 0.57 | 0.74 | high-confidence concern |
| [CorrectAdvisorFeedback](commands/advisor-feedback-thread.md#correctadvisorfeedback) | AdvisorFeedbackThread | 0.66 | 0.61 | 0.70 | 0.77 | 0.76 | 0.17 | 0.60 | uncertain; review manually |
| [ResolveAdvisorFeedback](commands/advisor-feedback-thread.md#resolveadvisorfeedback) | AdvisorFeedbackThread | 0.32 | 0.52 | 0.50 | 0.64 | 0.57 | 0.31 | 0.72 | uncertain; review manually |
| [RespondToAdvisorFeedback](commands/advisor-feedback-thread.md#respondtoadvisorfeedback) | AdvisorFeedbackThread | 0.84 | 0.83 | 0.88 | 0.86 | 0.77 | 0.10 | 0.49 | uncertain; review manually |
| [MaterializeAdvisorFeedback](commands/advisor-feedback-thread.md#materializeadvisorfeedback) | AdvisorFeedbackThread | 0.01 | 0.73 | 0.09 | 0.17 | 0.47 | 0.85 | 0.76 | high-confidence concern |
| [CloseAdvisorHandoff](commands/advisor-handoff.md#closeadvisorhandoff) | AdvisorHandoff | 0.92 | 0.92 | 0.60 | 0.79 | 0.82 | 0.09 | 0.45 | uncertain; review manually |
| [CreateAdvisorHandoff](commands/advisor-handoff.md#createadvisorhandoff) | AdvisorHandoff | 0.81 | 0.78 | 0.42 | 0.76 | 0.87 | 0.12 | 0.36 | uncertain; review manually |
| [MarkAdvisorHandoffReadyForReview](commands/advisor-handoff.md#markadvisorhandoffreadyforreview) | AdvisorHandoff | 0.81 | 0.79 | 0.64 | 0.82 | 0.82 | 0.14 | 0.41 | uncertain; review manually |
| [RecordAdvisorFeedback](commands/advisor-handoff.md#recordadvisorfeedback) | AdvisorHandoff | 0.34 | 0.15 | 0.79 | 0.80 | 0.68 | 0.29 | 0.55 | uncertain; review manually |
| [ReviseAdvisorHandoffSelection](commands/advisor-handoff.md#reviseadvisorhandoffselection) | AdvisorHandoff | 0.59 | 0.56 | 0.48 | 0.74 | 0.81 | 0.14 | 0.48 | uncertain; review manually |
| [RevokeAdvisorHandoff](commands/advisor-handoff.md#revokeadvisorhandoff) | AdvisorHandoff | 0.93 | 0.93 | 0.60 | 0.81 | 0.86 | 0.11 | 0.40 | uncertain; review manually |
| [AuthorizeAdvisorArtifactDownloadUse](commands/advisor-handoff.md#authorizeadvisorartifactdownloaduse) | AdvisorHandoff | 0.12 | 0.37 | 0.38 | 0.54 | 0.51 | 0.35 | 0.71 | uncertain; review manually |
| [SettleAdvisorArtifactDownloadUse](commands/advisor-handoff.md#settleadvisorartifactdownloaduse) | AdvisorHandoff | 0.06 | 0.40 | 0.16 | 0.33 | 0.39 | 0.57 | 0.78 | high-confidence concern |
| [AcceptApplicationImport](commands/application-import-batch.md#acceptapplicationimport) | ApplicationImportBatch | 0.57 | 0.50 | 0.67 | 0.70 | 0.74 | 0.19 | 0.63 | uncertain; review manually |
| [CancelApplicationImport](commands/application-import-batch.md#cancelapplicationimport) | ApplicationImportBatch | 0.87 | 0.87 | 0.85 | 0.84 | 0.80 | 0.10 | 0.57 | uncertain; review manually |
| [CorrelateApplicationImportRow](commands/application-import-batch.md#correlateapplicationimportrow) | ApplicationImportBatch | 0.18 | 0.61 | 0.27 | 0.62 | 0.82 | 0.30 | 0.59 | uncertain; review manually |
| [ExecuteApplicationImport](commands/application-import-batch.md#executeapplicationimport) | ApplicationImportBatch | 0.44 | 0.19 | 0.61 | 0.67 | 0.78 | 0.34 | 0.52 | uncertain; review manually |
| [FreezeApplicationImportOmissionProposal](commands/application-import-batch.md#freezeapplicationimportomissionproposal) | ApplicationImportBatch | 0.05 | 0.43 | 0.17 | 0.37 | 0.63 | 0.58 | 0.71 | high-confidence concern |
| [StageApplicationImport](commands/application-import-batch.md#stageapplicationimport) | ApplicationImportBatch | 0.07 | 0.05 | 0.29 | 0.53 | 0.67 | 0.51 | 0.73 | uncertain; review manually |
| [CommitApplicationImportBatch](commands/application-import-batch.md#commitapplicationimportbatch) | ApplicationImportBatch | 0.29 | 0.25 | 0.42 | 0.51 | 0.70 | 0.49 | 0.65 | uncertain; review manually |
| [CompleteApplicationImportCancellation](commands/application-import-batch.md#completeapplicationimportcancellation) | ApplicationImportBatch | 0.30 | 0.22 | 0.47 | 0.59 | 0.68 | 0.42 | 0.64 | uncertain; review manually |
| [ClaimApplicationImportSource](commands/application-import-source.md#claimapplicationimportsource) | ApplicationImportSource | 0.24 | 0.59 | 0.31 | 0.54 | 0.79 | 0.23 | 0.63 | uncertain; review manually |
| [ReleaseApplicationImportSource](commands/application-import-source.md#releaseapplicationimportsource) | ApplicationImportSource | 0.29 | 0.56 | 0.32 | 0.50 | 0.73 | 0.26 | 0.73 | uncertain; review manually |
| [ClaimApplicationName](commands/application-name-claim.md#claimapplicationname) | ApplicationNameClaim | 0.66 | 0.63 | 0.48 | 0.71 | 0.82 | 0.16 | 0.52 | uncertain; review manually |
| [ReleaseApplicationName](commands/application-name-claim.md#releaseapplicationname) | ApplicationNameClaim | 0.68 | 0.67 | 0.52 | 0.70 | 0.79 | 0.12 | 0.57 | uncertain; review manually |
| [AddApplicationRelationship](commands/application-relationship.md#addapplicationrelationship) | ApplicationRelationship | 0.70 | 0.68 | 0.68 | 0.82 | 0.84 | 0.10 | 0.44 | uncertain; review manually |
| [ApproveApplicationSuccessor](commands/application-relationship.md#approveapplicationsuccessor) | ApplicationRelationship | 0.43 | 0.50 | 0.45 | 0.62 | 0.76 | 0.11 | 0.62 | uncertain; review manually |
| [RemoveApplicationRelationship](commands/application-relationship.md#removeapplicationrelationship) | ApplicationRelationship | 0.89 | 0.88 | 0.77 | 0.87 | 0.85 | 0.10 | 0.45 | uncertain; review manually |
| [ApproveArtifactDisclosure](commands/artifact-disclosure.md#approveartifactdisclosure) | ArtifactDisclosure | 0.85 | 0.84 | 0.74 | 0.84 | 0.82 | 0.10 | 0.44 | uncertain; review manually |
| [RecordArtifactDisclosureDelivery](commands/artifact-disclosure.md#recordartifactdisclosuredelivery) | ArtifactDisclosure | 0.24 | 0.10 | 0.53 | 0.70 | 0.72 | 0.31 | 0.60 | uncertain; review manually |
| [RevokeArtifactDisclosure](commands/artifact-disclosure.md#revokeartifactdisclosure) | ArtifactDisclosure | 0.81 | 0.80 | 0.69 | 0.78 | 0.83 | 0.10 | 0.51 | uncertain; review manually |
| [ApproveArtifactDisposition](commands/artifact-disposition.md#approveartifactdisposition) | ArtifactDisposition | 0.23 | 0.63 | 0.34 | 0.63 | 0.77 | 0.15 | 0.63 | uncertain; review manually |
| [ExecuteArtifactDisposition](commands/artifact-disposition.md#executeartifactdisposition) | ArtifactDisposition | 0.23 | 0.55 | 0.28 | 0.55 | 0.72 | 0.34 | 0.64 | uncertain; review manually |
| [RecordArtifactDispositionResult](commands/artifact-disposition.md#recordartifactdispositionresult) | ArtifactDisposition | 0.32 | 0.12 | 0.45 | 0.70 | 0.80 | 0.35 | 0.51 | uncertain; review manually |
| [FenceArtifactDispositionForHold](commands/artifact-disposition.md#fenceartifactdispositionforhold) | ArtifactDisposition | 0.01 | 0.57 | 0.13 | 0.25 | 0.56 | 0.79 | 0.74 | high-confidence concern |
| [CancelProtectedArtifactDownloadGrant](commands/artifact-download-grant.md#cancelprotectedartifactdownloadgrant) | ArtifactDownloadGrant | 0.48 | 0.39 | 0.54 | 0.73 | 0.81 | 0.23 | 0.48 | uncertain; review manually |
| [IssueProtectedArtifactDownloadGrant](commands/artifact-download-grant.md#issueprotectedartifactdownloadgrant) | ArtifactDownloadGrant | 0.52 | 0.45 | 0.44 | 0.70 | 0.83 | 0.21 | 0.50 | uncertain; review manually |
| [PlaceArtifactEngagementHold](commands/artifact-engagement-hold.md#placeartifactengagementhold) | ArtifactEngagementHold | 0.67 | 0.63 | 0.47 | 0.74 | 0.82 | 0.12 | 0.49 | uncertain; review manually |
| [RecordEngagementReportIssued](commands/artifact-engagement-hold.md#recordengagementreportissued) | ArtifactEngagementHold | 0.61 | 0.42 | 0.75 | 0.79 | 0.79 | 0.23 | 0.51 | uncertain; review manually |
| [ReleaseArtifactEngagementHold](commands/artifact-engagement-hold.md#releaseartifactengagementhold) | ArtifactEngagementHold | 0.89 | 0.88 | 0.64 | 0.79 | 0.83 | 0.09 | 0.43 | uncertain; review manually |
| [RecordEngagementPackageDelivered](commands/artifact-engagement-hold.md#recordengagementpackagedelivered) | ArtifactEngagementHold | 0.61 | 0.37 | 0.67 | 0.77 | 0.79 | 0.17 | 0.53 | uncertain; review manually |
| [PlaceArtifactLegalHold](commands/artifact-legal-hold.md#placeartifactlegalhold) | ArtifactLegalHold | 0.92 | 0.92 | 0.65 | 0.83 | 0.83 | 0.11 | 0.48 | uncertain; review manually |
| [ReleaseArtifactLegalHold](commands/artifact-legal-hold.md#releaseartifactlegalhold) | ArtifactLegalHold | 0.96 | 0.96 | 0.78 | 0.88 | 0.85 | 0.09 | 0.41 | uncertain; review manually |
| [RecordArtifactRetentionBasis](commands/artifact-retention-basis.md#recordartifactretentionbasis) | ArtifactRetentionBasis | 0.28 | 0.53 | 0.33 | 0.64 | 0.82 | 0.22 | 0.59 | uncertain; review manually |
| [RecordAuditObservation](commands/audit-observation.md#recordauditobservation) | AuditObservation | 0.62 | 0.49 | 0.63 | 0.80 | 0.80 | 0.23 | 0.59 | uncertain; review manually |
| [CorrectAuditObservation](commands/audit-observation.md#correctauditobservation) | AuditObservation | 0.78 | 0.76 | 0.60 | 0.78 | 0.74 | 0.15 | 0.61 | uncertain; review manually |
| [RecordAuditObservationManagementResponse](commands/audit-observation.md#recordauditobservationmanagementresponse) | AuditObservation | 0.22 | 0.29 | 0.35 | 0.64 | 0.67 | 0.39 | 0.67 | uncertain; review manually |
| [LinkAuditObservationFinding](commands/audit-observation.md#linkauditobservationfinding) | AuditObservation | 0.55 | 0.45 | 0.64 | 0.70 | 0.69 | 0.23 | 0.67 | uncertain; review manually |
| [RecordExaminationOutcome](commands/audit-outcome.md#recordexaminationoutcome) | AuditOutcome | 0.51 | 0.33 | 0.68 | 0.82 | 0.81 | 0.20 | 0.40 | uncertain; review manually |
| [MaterializeRecordedAuditPackageDelivery](commands/audit-package-delivery.md#materializerecordedauditpackagedelivery) | AuditPackageDelivery | 0.01 | 0.51 | 0.13 | 0.25 | 0.54 | 0.79 | 0.72 | high-confidence concern |
| [AmendAuditPackage](commands/audit-package-manifest.md#amendauditpackage) | AuditPackageManifest | 0.47 | 0.49 | 0.61 | 0.73 | 0.76 | 0.18 | 0.65 | uncertain; review manually |
| [ApproveAuditPackageDisclosure](commands/audit-package-manifest.md#approveauditpackagedisclosure) | AuditPackageManifest | 0.74 | 0.73 | 0.70 | 0.75 | 0.80 | 0.13 | 0.56 | uncertain; review manually |
| [ApproveAuditPackageValidationOverride](commands/audit-package-manifest.md#approveauditpackagevalidationoverride) | AuditPackageManifest | 0.54 | 0.49 | 0.39 | 0.65 | 0.75 | 0.26 | 0.60 | uncertain; review manually |
| [CreateAuditPackage](commands/audit-package-manifest.md#createauditpackage) | AuditPackageManifest | 0.86 | 0.83 | 0.62 | 0.77 | 0.84 | 0.14 | 0.46 | uncertain; review manually |
| [RecordAuditPackageBuildResult](commands/audit-package-manifest.md#recordauditpackagebuildresult) | AuditPackageManifest | 0.46 | 0.13 | 0.44 | 0.68 | 0.84 | 0.37 | 0.44 | uncertain; review manually |
| [RecordAuditPackageDelivery](commands/audit-package-manifest.md#recordauditpackagedelivery) | AuditPackageManifest | 0.70 | 0.54 | 0.74 | 0.77 | 0.67 | 0.22 | 0.71 | uncertain; review manually |
| [RequestAuditPackageBuild](commands/audit-package-manifest.md#requestauditpackagebuild) | AuditPackageManifest | 0.69 | 0.65 | 0.50 | 0.70 | 0.83 | 0.26 | 0.46 | uncertain; review manually |
| [ValidateAuditPackage](commands/audit-package-manifest.md#validateauditpackage) | AuditPackageManifest | 0.79 | 0.78 | 0.74 | 0.72 | 0.73 | 0.16 | 0.64 | uncertain; review manually |
| [AmendAuditPopulation](commands/audit-population.md#amendauditpopulation) | AuditPopulation | 0.37 | 0.57 | 0.37 | 0.67 | 0.69 | 0.17 | 0.65 | uncertain; review manually |
| [ClassifyAuditPopulationRow](commands/audit-population.md#classifyauditpopulationrow) | AuditPopulation | 0.74 | 0.73 | 0.44 | 0.71 | 0.83 | 0.10 | 0.51 | uncertain; review manually |
| [DefineAuditPopulation](commands/audit-population.md#defineauditpopulation) | AuditPopulation | 0.64 | 0.60 | 0.30 | 0.57 | 0.80 | 0.14 | 0.54 | uncertain; review manually |
| [FreezeAuditPopulation](commands/audit-population.md#freezeauditpopulation) | AuditPopulation | 0.85 | 0.85 | 0.39 | 0.66 | 0.83 | 0.15 | 0.57 | uncertain; review manually |
| [GenerateAuditPopulation](commands/audit-population.md#generateauditpopulation) | AuditPopulation | 0.26 | 0.60 | 0.31 | 0.50 | 0.62 | 0.29 | 0.72 | uncertain; review manually |
| [ReconcileAuditPopulation](commands/audit-population.md#reconcileauditpopulation) | AuditPopulation | 0.43 | 0.55 | 0.33 | 0.58 | 0.78 | 0.13 | 0.73 | uncertain; review manually |
| [RecordAuditPopulationFacts](commands/audit-population.md#recordauditpopulationfacts) | AuditPopulation | 0.24 | 0.31 | 0.36 | 0.62 | 0.65 | 0.31 | 0.67 | uncertain; review manually |
| [ReviseAuditPopulationDefinition](commands/audit-population.md#reviseauditpopulationdefinition) | AuditPopulation | 0.63 | 0.59 | 0.46 | 0.73 | 0.82 | 0.17 | 0.48 | uncertain; review manually |
| [MaterializeAuditorComment](commands/auditor-feedback-thread.md#materializeauditorcomment) | AuditorFeedbackThread | 0.04 | 0.44 | 0.12 | 0.32 | 0.44 | 0.81 | 0.70 | high-confidence concern |
| [AmendSampleSelection](commands/auditor-request.md#amendsampleselection) | AuditorRequest | 0.50 | 0.45 | 0.60 | 0.76 | 0.75 | 0.21 | 0.50 | uncertain; review manually |
| [AssignAuditorRequest](commands/auditor-request.md#assignauditorrequest) | AuditorRequest | 0.94 | 0.94 | 0.79 | 0.85 | 0.80 | 0.10 | 0.46 | uncertain; review manually |
| [CreateAuditResponseDraft](commands/auditor-request.md#createauditresponsedraft) | AuditorRequest | 0.65 | 0.55 | 0.76 | 0.84 | 0.81 | 0.26 | 0.46 | uncertain; review manually |
| [RecordAuditorRequest](commands/auditor-request.md#recordauditorrequest) | AuditorRequest | 0.46 | 0.10 | 0.73 | 0.81 | 0.80 | 0.27 | 0.52 | uncertain; review manually |
| [RecordAuditorRequestComment](commands/auditor-request.md#recordauditorrequestcomment) | AuditorRequest | 0.59 | 0.32 | 0.75 | 0.85 | 0.81 | 0.23 | 0.44 | uncertain; review manually |
| [RecordSampleSelection](commands/auditor-request.md#recordsampleselection) | AuditorRequest | 0.38 | 0.23 | 0.52 | 0.73 | 0.77 | 0.33 | 0.49 | uncertain; review manually |
| [ReopenAuditorRequest](commands/auditor-request.md#reopenauditorrequest) | AuditorRequest | 0.90 | 0.90 | 0.85 | 0.87 | 0.84 | 0.09 | 0.46 | uncertain; review manually |
| [ResolveAuditorRequest](commands/auditor-request.md#resolveauditorrequest) | AuditorRequest | 0.87 | 0.87 | 0.56 | 0.72 | 0.77 | 0.12 | 0.59 | uncertain; review manually |
| [ReviseAuditorRequest](commands/auditor-request.md#reviseauditorrequest) | AuditorRequest | 0.83 | 0.82 | 0.79 | 0.82 | 0.81 | 0.13 | 0.50 | uncertain; review manually |
| [AbortAuditorRequestResolution](commands/auditor-request.md#abortauditorrequestresolution) | AuditorRequest | 0.49 | 0.36 | 0.63 | 0.70 | 0.73 | 0.34 | 0.57 | uncertain; review manually |
| [CompleteAuditorRequestResolution](commands/auditor-request.md#completeauditorrequestresolution) | AuditorRequest | 0.31 | 0.12 | 0.48 | 0.59 | 0.67 | 0.41 | 0.60 | uncertain; review manually |
| [DeliverAuditResponse](commands/auditor-response.md#deliverauditresponse) | AuditorResponse | 0.91 | 0.89 | 0.82 | 0.85 | 0.83 | 0.14 | 0.48 | uncertain; review manually |
| [RecordAuditorResponseDisposition](commands/auditor-response.md#recordauditorresponsedisposition) | AuditorResponse | 0.45 | 0.18 | 0.43 | 0.76 | 0.83 | 0.25 | 0.46 | uncertain; review manually |
| [ReviewAuditResponse](commands/auditor-response.md#reviewauditresponse) | AuditorResponse | 0.62 | 0.59 | 0.79 | 0.80 | 0.73 | 0.09 | 0.58 | uncertain; review manually |
| [ReviseAuditResponseDraft](commands/auditor-response.md#reviseauditresponsedraft) | AuditorResponse | 0.70 | 0.65 | 0.83 | 0.83 | 0.83 | 0.19 | 0.39 | uncertain; review manually |
| [SubmitAuditResponse](commands/auditor-response.md#submitauditresponse) | AuditorResponse | 0.94 | 0.94 | 0.89 | 0.86 | 0.80 | 0.10 | 0.44 | uncertain; review manually |
| [MaterializeAuditorResponse](commands/auditor-response.md#materializeauditorresponse) | AuditorResponse | 0.01 | 0.73 | 0.11 | 0.22 | 0.63 | 0.84 | 0.77 | high-confidence concern |
| [AuthorizeAuditorResponseRequestResolutionUse](commands/auditor-response.md#authorizeauditorresponserequestresolutionuse) | AuditorResponse | 0.04 | 0.43 | 0.14 | 0.29 | 0.50 | 0.63 | 0.74 | high-confidence concern |
| [SettleAuditorResponseRequestResolutionUse](commands/auditor-response.md#settleauditorresponserequestresolutionuse) | AuditorResponse | 0.08 | 0.40 | 0.09 | 0.24 | 0.48 | 0.60 | 0.76 | high-confidence concern |
| [MaterializeAuditorSampleSelection](commands/auditor-sample-selection.md#materializeauditorsampleselection) | AuditorSampleSelection | 0.03 | 0.47 | 0.11 | 0.30 | 0.75 | 0.81 | 0.67 | high-confidence concern |
| [RecordClientExportBuildResult](commands/client-export.md#recordclientexportbuildresult) | ClientExport | 0.23 | 0.00 | 0.29 | 0.59 | 0.82 | 0.55 | 0.47 | uncertain; review manually |
| [RecordClientExportDelivery](commands/client-export.md#recordclientexportdelivery) | ClientExport | 0.46 | 0.00 | 0.75 | 0.78 | 0.79 | 0.26 | 0.50 | uncertain; review manually |
| [RecordOwnAttestRecordDelivery](commands/client-export.md#recordownattestrecorddelivery) | ClientExport | 0.28 | 0.28 | 0.29 | 0.47 | 0.57 | 0.39 | 0.69 | uncertain; review manually |
| [RecordOwnAttestRecordPackageResult](commands/client-export.md#recordownattestrecordpackageresult) | ClientExport | 0.30 | 0.30 | 0.21 | 0.43 | 0.60 | 0.41 | 0.69 | uncertain; review manually |
| [RequestClientExport](commands/client-export.md#requestclientexport) | ClientExport | 0.65 | 0.63 | 0.71 | 0.72 | 0.78 | 0.21 | 0.62 | uncertain; review manually |
| [RequestOwnAttestRecordPackage](commands/client-export.md#requestownattestrecordpackage) | ClientExport | 0.29 | 0.57 | 0.25 | 0.46 | 0.68 | 0.33 | 0.64 | uncertain; review manually |
| [MaterializeClientIndependencePreview](commands/client-independence-decision.md#materializeclientindependencepreview) | ClientIndependenceDecision | 0.15 | 0.00 | 0.17 | 0.42 | 0.74 | 0.73 | 0.60 | high-confidence concern |
| [EvaluateClientIndependence](commands/client-service-independence.md#evaluateclientindependence) | ClientServiceIndependence | 0.76 | 0.75 | 0.60 | 0.81 | 0.77 | 0.10 | 0.56 | uncertain; review manually |
| [RecordNonattestService](commands/client-service-independence.md#recordnonattestservice) | ClientServiceIndependence | 0.73 | 0.67 | 0.37 | 0.79 | 0.82 | 0.14 | 0.43 | uncertain; review manually |
| [AcquireEngagementServiceEpoch](commands/client-service-independence.md#acquireengagementserviceepoch) | ClientServiceIndependence | 0.16 | 0.27 | 0.13 | 0.43 | 0.71 | 0.42 | 0.60 | high-confidence concern |
| [ReleaseEngagementServiceEpoch](commands/client-service-independence.md#releaseengagementserviceepoch) | ClientServiceIndependence | 0.15 | 0.50 | 0.15 | 0.38 | 0.75 | 0.42 | 0.66 | high-confidence concern |
| [AuthorizeClientServicePersonalDecisionUse](commands/client-service-independence.md#authorizeclientservicepersonaldecisionuse) | ClientServiceIndependence | 0.07 | 0.38 | 0.13 | 0.31 | 0.55 | 0.61 | 0.74 | high-confidence concern |
| [SettleClientServicePersonalDecisionUse](commands/client-service-independence.md#settleclientservicepersonaldecisionuse) | ClientServiceIndependence | 0.03 | 0.61 | 0.09 | 0.23 | 0.27 | 0.55 | 0.86 | high-confidence concern |
| [CreateClientService](commands/client-service.md#createclientservice) | ClientService | 0.79 | 0.77 | 0.65 | 0.70 | 0.83 | 0.14 | 0.41 | uncertain; review manually |
| [RetireClientService](commands/client-service.md#retireclientservice) | ClientService | 0.86 | 0.85 | 0.59 | 0.78 | 0.87 | 0.11 | 0.36 | uncertain; review manually |
| [ReviseClientService](commands/client-service.md#reviseclientservice) | ClientService | 0.86 | 0.85 | 0.72 | 0.77 | 0.82 | 0.12 | 0.44 | uncertain; review manually |
| [ProposeCommitmentControlLink](commands/commitment-control-link.md#proposecommitmentcontrollink) | CommitmentControlLink | 0.33 | 0.53 | 0.31 | 0.61 | 0.79 | 0.20 | 0.62 | uncertain; review manually |
| [ReviewCommitmentControlLink](commands/commitment-control-link.md#reviewcommitmentcontrollink) | CommitmentControlLink | 0.61 | 0.59 | 0.50 | 0.70 | 0.83 | 0.10 | 0.52 | uncertain; review manually |
| [RetireCommitmentControlLink](commands/commitment-control-link.md#retirecommitmentcontrollink) | CommitmentControlLink | 0.80 | 0.78 | 0.41 | 0.72 | 0.88 | 0.10 | 0.32 | uncertain; review manually |
| [ApproveCommitmentDraft](commands/commitment-draft.md#approvecommitmentdraft) | CommitmentDraft | 0.78 | 0.76 | 0.54 | 0.69 | 0.85 | 0.16 | 0.44 | uncertain; review manually |
| [CreateCommitmentDraft](commands/commitment-draft.md#createcommitmentdraft) | CommitmentDraft | 0.66 | 0.62 | 0.47 | 0.67 | 0.88 | 0.19 | 0.36 | uncertain; review manually |
| [RetireCommitment](commands/commitment-draft.md#retirecommitment) | CommitmentDraft | 0.76 | 0.74 | 0.52 | 0.69 | 0.73 | 0.09 | 0.52 | uncertain; review manually |
| [ReviewCommitmentDraft](commands/commitment-draft.md#reviewcommitmentdraft) | CommitmentDraft | 0.72 | 0.70 | 0.66 | 0.76 | 0.84 | 0.10 | 0.43 | uncertain; review manually |
| [ReviseCommitmentDraft](commands/commitment-draft.md#revisecommitmentdraft) | CommitmentDraft | 0.83 | 0.81 | 0.56 | 0.75 | 0.88 | 0.15 | 0.30 | uncertain; review manually |
| [AssessComplianceIncident](commands/compliance-incident.md#assesscomplianceincident) | ComplianceIncident | 0.85 | 0.84 | 0.86 | 0.84 | 0.82 | 0.07 | 0.51 | uncertain; review manually |
| [RecordComplianceIncident](commands/compliance-incident.md#recordcomplianceincident) | ComplianceIncident | 0.79 | 0.62 | 0.90 | 0.88 | 0.84 | 0.22 | 0.45 | uncertain; review manually |
| [RecordComplianceIncidentResolution](commands/compliance-incident.md#recordcomplianceincidentresolution) | ComplianceIncident | 0.74 | 0.60 | 0.83 | 0.86 | 0.85 | 0.19 | 0.37 | uncertain; review manually |
| [RecordComplianceIncidentSystemDescriptionDecision](commands/compliance-incident.md#recordcomplianceincidentsystemdescriptiondecision) | ComplianceIncident | 0.36 | 0.39 | 0.36 | 0.60 | 0.74 | 0.25 | 0.63 | uncertain; review manually |
| [ReviseComplianceIncident](commands/compliance-incident.md#revisecomplianceincident) | ComplianceIncident | 0.77 | 0.76 | 0.76 | 0.82 | 0.86 | 0.15 | 0.44 | uncertain; review manually |
| [CreateProgram](commands/compliance-program.md#createprogram) | ComplianceProgram | 0.96 | 0.96 | 0.82 | 0.85 | 0.81 | 0.08 | 0.32 | uncertain; review manually |
| [ReviseProgram](commands/compliance-program.md#reviseprogram) | ComplianceProgram | 0.59 | 0.56 | 0.71 | 0.68 | 0.58 | 0.18 | 0.67 | uncertain; review manually |
| [SelectProgramCriteriaEdition](commands/compliance-program.md#selectprogramcriteriaedition) | ComplianceProgram | 0.56 | 0.52 | 0.36 | 0.61 | 0.79 | 0.22 | 0.46 | uncertain; review manually |
| [ProposeControlCriterionMapping](commands/control-criterion-mapping.md#proposecontrolcriterionmapping) | ControlCriterionMapping | 0.50 | 0.44 | 0.33 | 0.70 | 0.83 | 0.19 | 0.44 | uncertain; review manually |
| [RetireControlCriterionMapping](commands/control-criterion-mapping.md#retirecontrolcriterionmapping) | ControlCriterionMapping | 0.69 | 0.66 | 0.40 | 0.72 | 0.84 | 0.16 | 0.46 | uncertain; review manually |
| [ReviewControlCriterionMapping](commands/control-criterion-mapping.md#reviewcontrolcriterionmapping) | ControlCriterionMapping | 0.53 | 0.49 | 0.46 | 0.76 | 0.80 | 0.11 | 0.55 | uncertain; review manually |
| [ApproveControl](commands/control-draft.md#approvecontrol) | ControlDraft | 0.61 | 0.59 | 0.53 | 0.61 | 0.45 | 0.09 | 0.67 | uncertain; review manually |
| [CreateControlDraft](commands/control-draft.md#createcontroldraft) | ControlDraft | 0.61 | 0.50 | 0.49 | 0.68 | 0.82 | 0.28 | 0.50 | uncertain; review manually |
| [DesignateControlOwnerPerson](commands/control-draft.md#designatecontrolownerperson) | ControlDraft | 0.58 | 0.50 | 0.57 | 0.74 | 0.67 | 0.18 | 0.61 | uncertain; review manually |
| [DiscardControlDraft](commands/control-draft.md#discardcontroldraft) | ControlDraft | 0.66 | 0.61 | 0.54 | 0.75 | 0.72 | 0.25 | 0.57 | uncertain; review manually |
| [ProposeControlRetirement](commands/control-draft.md#proposecontrolretirement) | ControlDraft | 0.64 | 0.61 | 0.57 | 0.73 | 0.80 | 0.16 | 0.56 | uncertain; review manually |
| [ProposeControlSuccessor](commands/control-draft.md#proposecontrolsuccessor) | ControlDraft | 0.39 | 0.54 | 0.30 | 0.55 | 0.74 | 0.17 | 0.58 | uncertain; review manually |
| [RetireControl](commands/control-draft.md#retirecontrol) | ControlDraft | 0.66 | 0.64 | 0.53 | 0.74 | 0.57 | 0.10 | 0.63 | uncertain; review manually |
| [ReviewControl](commands/control-draft.md#reviewcontrol) | ControlDraft | 0.61 | 0.60 | 0.66 | 0.69 | 0.57 | 0.08 | 0.69 | uncertain; review manually |
| [ReviseControlDraft](commands/control-draft.md#revisecontroldraft) | ControlDraft | 0.71 | 0.68 | 0.62 | 0.76 | 0.81 | 0.22 | 0.48 | uncertain; review manually |
| [WithdrawControlProposal](commands/control-draft.md#withdrawcontrolproposal) | ControlDraft | 0.58 | 0.52 | 0.71 | 0.73 | 0.67 | 0.19 | 0.63 | uncertain; review manually |
| [DefineControlEvaluationPlan](commands/control-evaluation-plan.md#definecontrolevaluationplan) | ControlEvaluationPlan | 0.62 | 0.53 | 0.64 | 0.75 | 0.79 | 0.17 | 0.55 | uncertain; review manually |
| [DisposeControlEvaluationDeviation](commands/control-evaluation.md#disposecontrolevaluationdeviation) | ControlEvaluation | 0.12 | 0.41 | 0.14 | 0.46 | 0.59 | 0.50 | 0.69 | high-confidence concern |
| [RecordControlEvaluationStep](commands/control-evaluation.md#recordcontrolevaluationstep) | ControlEvaluation | 0.63 | 0.39 | 0.69 | 0.82 | 0.85 | 0.24 | 0.34 | uncertain; review manually |
| [ReviewControlEvaluation](commands/control-evaluation.md#reviewcontrolevaluation) | ControlEvaluation | 0.73 | 0.71 | 0.56 | 0.73 | 0.78 | 0.16 | 0.56 | uncertain; review manually |
| [StartControlEvaluation](commands/control-evaluation.md#startcontrolevaluation) | ControlEvaluation | 0.79 | 0.78 | 0.77 | 0.79 | 0.81 | 0.20 | 0.46 | uncertain; review manually |
| [SubmitControlEvaluation](commands/control-evaluation.md#submitcontrolevaluation) | ControlEvaluation | 0.72 | 0.71 | 0.70 | 0.77 | 0.74 | 0.17 | 0.60 | uncertain; review manually |
| [AttestControlOccurrence](commands/control-occurrence.md#attestcontroloccurrence) | ControlOccurrence | 0.81 | 0.80 | 0.41 | 0.78 | 0.81 | 0.14 | 0.52 | uncertain; review manually |
| [CorrectControlAttestation](commands/control-occurrence.md#correctcontrolattestation) | ControlOccurrence | 0.45 | 0.46 | 0.42 | 0.71 | 0.60 | 0.20 | 0.65 | uncertain; review manually |
| [OpenControlOccurrence](commands/control-occurrence.md#opencontroloccurrence) | ControlOccurrence | 0.45 | 0.45 | 0.42 | 0.67 | 0.79 | 0.31 | 0.61 | uncertain; review manually |
| [ReviewControlOccurrence](commands/control-occurrence.md#reviewcontroloccurrence) | ControlOccurrence | 0.87 | 0.87 | 0.62 | 0.77 | 0.83 | 0.10 | 0.46 | uncertain; review manually |
| [MaterializeControlOccurrence](commands/control-occurrence.md#materializecontroloccurrence) | ControlOccurrence | 0.00 | 0.88 | 0.10 | 0.18 | 0.55 | 0.85 | 0.74 | high-confidence concern |
| [ApproveControlOperatingPlan](commands/control-operating-plan.md#approvecontroloperatingplan) | ControlOperatingPlan | 0.93 | 0.93 | 0.52 | 0.78 | 0.85 | 0.09 | 0.38 | uncertain; review manually |
| [ProposeControlOperatingPlan](commands/control-operating-plan.md#proposecontroloperatingplan) | ControlOperatingPlan | 0.78 | 0.77 | 0.42 | 0.75 | 0.85 | 0.12 | 0.44 | uncertain; review manually |
| [CompleteCorrectiveAction](commands/corrective-action.md#completecorrectiveaction) | CorrectiveAction | 0.87 | 0.85 | 0.79 | 0.82 | 0.80 | 0.13 | 0.46 | uncertain; review manually |
| [MaterializeCorrectiveAction](commands/corrective-action.md#materializecorrectiveaction) | CorrectiveAction | 0.00 | 0.91 | 0.10 | 0.17 | 0.59 | 0.86 | 0.74 | high-confidence concern |
| [AuthorizeCorrectiveActionFindingClosureUse](commands/corrective-action.md#authorizecorrectiveactionfindingclosureuse) | CorrectiveAction | 0.04 | 0.37 | 0.13 | 0.32 | 0.57 | 0.64 | 0.73 | high-confidence concern |
| [SettleCorrectiveActionFindingClosureUse](commands/corrective-action.md#settlecorrectiveactionfindingclosureuse) | CorrectiveAction | 0.02 | 0.53 | 0.10 | 0.26 | 0.58 | 0.63 | 0.76 | high-confidence concern |
| [SetCriteriaTextOverlay](commands/criteria-text-overlay.md#setcriteriatextoverlay) | CriteriaTextOverlay | 0.16 | 0.50 | 0.19 | 0.41 | 0.79 | 0.25 | 0.58 | high-confidence concern |
| [ProposeCriterionNotApplicable](commands/criterion-applicability.md#proposecriterionnotapplicable) | CriterionApplicability | 0.28 | 0.54 | 0.37 | 0.67 | 0.79 | 0.35 | 0.61 | uncertain; review manually |
| [ReviewCriterionApplicability](commands/criterion-applicability.md#reviewcriterionapplicability) | CriterionApplicability | 0.51 | 0.47 | 0.45 | 0.74 | 0.77 | 0.14 | 0.58 | uncertain; review manually |
| [WithdrawCriterionNotApplicable](commands/criterion-applicability.md#withdrawcriterionnotapplicable) | CriterionApplicability | 0.60 | 0.57 | 0.44 | 0.69 | 0.79 | 0.16 | 0.51 | uncertain; review manually |
| [RecordDataFlow](commands/data-flow.md#recorddataflow) | DataFlow | 0.46 | 0.10 | 0.58 | 0.77 | 0.75 | 0.30 | 0.53 | uncertain; review manually |
| [ReviseDataFlow](commands/data-flow.md#revisedataflow) | DataFlow | 0.68 | 0.64 | 0.60 | 0.80 | 0.83 | 0.21 | 0.51 | uncertain; review manually |
| [ApplyApplicationImportEffect](commands/declared-application.md#applyapplicationimporteffect) | DeclaredApplication | 0.06 | 0.33 | 0.15 | 0.35 | 0.80 | 0.57 | 0.61 | high-confidence concern |
| [DeclareApplication](commands/declared-application.md#declareapplication) | DeclaredApplication | 0.82 | 0.81 | 0.65 | 0.78 | 0.83 | 0.15 | 0.45 | uncertain; review manually |
| [RetireApplication](commands/declared-application.md#retireapplication) | DeclaredApplication | 0.90 | 0.90 | 0.65 | 0.84 | 0.86 | 0.10 | 0.50 | uncertain; review manually |
| [ReviseApplication](commands/declared-application.md#reviseapplication) | DeclaredApplication | 0.81 | 0.80 | 0.73 | 0.82 | 0.81 | 0.15 | 0.48 | uncertain; review manually |
| [ReserveApplicationImportTarget](commands/declared-application.md#reserveapplicationimporttarget) | DeclaredApplication | 0.04 | 0.12 | 0.16 | 0.27 | 0.67 | 0.71 | 0.72 | high-confidence concern |
| [FenceApplicationImportTarget](commands/declared-application.md#fenceapplicationimporttarget) | DeclaredApplication | 0.02 | 0.50 | 0.09 | 0.19 | 0.52 | 0.79 | 0.76 | high-confidence concern |
| [ReleaseApplicationImportTarget](commands/declared-application.md#releaseapplicationimporttarget) | DeclaredApplication | 0.08 | 0.30 | 0.17 | 0.28 | 0.71 | 0.63 | 0.70 | high-confidence concern |
| [ApplyApplicationNameChange](commands/declared-application.md#applyapplicationnamechange) | DeclaredApplication | 0.35 | 0.00 | 0.67 | 0.67 | 0.69 | 0.36 | 0.64 | uncertain; review manually |
| [DeclareSystemInstance](commands/declared-system-instance.md#declaresysteminstance) | DeclaredSystemInstance | 0.76 | 0.74 | 0.37 | 0.65 | 0.85 | 0.17 | 0.43 | uncertain; review manually |
| [RetireSystemInstance](commands/declared-system-instance.md#retiresysteminstance) | DeclaredSystemInstance | 0.90 | 0.90 | 0.49 | 0.81 | 0.87 | 0.11 | 0.33 | uncertain; review manually |
| [ReviseSystemInstance](commands/declared-system-instance.md#revisesysteminstance) | DeclaredSystemInstance | 0.82 | 0.81 | 0.56 | 0.79 | 0.84 | 0.16 | 0.36 | uncertain; review manually |
| [CompleteEmailChallenge](commands/email-address.md#completeemailchallenge) | EmailAddress | 0.59 | 0.51 | 0.32 | 0.68 | 0.78 | 0.26 | 0.37 | uncertain; review manually |
| [IssueEmailChallenge](commands/email-address.md#issueemailchallenge) | EmailAddress | 0.64 | 0.58 | 0.41 | 0.75 | 0.84 | 0.17 | 0.33 | uncertain; review manually |
| [ReserveEmail](commands/email-address.md#reserveemail) | EmailAddress | 0.40 | 0.41 | 0.41 | 0.55 | 0.54 | 0.33 | 0.74 | uncertain; review manually |
| [CompleteIdentityRecovery](commands/email-address.md#completeidentityrecovery) | EmailAddress | 0.41 | 0.51 | 0.57 | 0.61 | 0.42 | 0.16 | 0.71 | uncertain; review manually |
| [StartIdentityRecovery](commands/email-address.md#startidentityrecovery) | EmailAddress | 0.45 | 0.50 | 0.68 | 0.69 | 0.60 | 0.14 | 0.63 | uncertain; review manually |
| [CompleteEmailRecoveryChallenge](commands/email-address.md#completeemailrecoverychallenge) | EmailAddress | 0.17 | 0.42 | 0.32 | 0.47 | 0.62 | 0.31 | 0.73 | uncertain; review manually |
| [MaterializeEngagementManagementAcknowledgement](commands/engagement-management-acknowledgement.md#materializeengagementmanagementacknowledgement) | EngagementManagementAcknowledgement | 0.01 | 0.76 | 0.10 | 0.29 | 0.53 | 0.83 | 0.74 | high-confidence concern |
| [AcknowledgeEngagementManagement](commands/engagement-management-acknowledgement.md#acknowledgeengagementmanagement) | EngagementManagementAcknowledgement | 0.64 | 0.63 | 0.39 | 0.66 | 0.71 | 0.14 | 0.63 | uncertain; review manually |
| [CaptureEvidenceArtifact](commands/evidence-artifact.md#captureevidenceartifact) | EvidenceArtifact | 0.36 | 0.21 | 0.49 | 0.75 | 0.77 | 0.36 | 0.52 | uncertain; review manually |
| [IssueArtifactDownload](commands/evidence-artifact.md#issueartifactdownload) | EvidenceArtifact | 0.56 | 0.38 | 0.65 | 0.82 | 0.78 | 0.30 | 0.49 | uncertain; review manually |
| [RecordEvidenceArtifactInspection](commands/evidence-artifact.md#recordevidenceartifactinspection) | EvidenceArtifact | 0.66 | 0.50 | 0.70 | 0.85 | 0.82 | 0.24 | 0.39 | uncertain; review manually |
| [ReleaseEvidenceArtifactQuarantine](commands/evidence-artifact.md#releaseevidenceartifactquarantine) | EvidenceArtifact | 0.51 | 0.38 | 0.60 | 0.81 | 0.79 | 0.32 | 0.46 | uncertain; review manually |
| [SettleArtifactDownloadAvailabilityUse](commands/evidence-artifact.md#settleartifactdownloadavailabilityuse) | EvidenceArtifact | 0.03 | 0.37 | 0.08 | 0.22 | 0.42 | 0.77 | 0.76 | high-confidence concern |
| [AssignArtifactHandlingPolicy](commands/evidence-handling-policy.md#assignartifacthandlingpolicy) | EvidenceHandlingPolicy | 0.65 | 0.61 | 0.51 | 0.74 | 0.79 | 0.17 | 0.47 | uncertain; review manually |
| [AuthorizeArtifactHandlingDownloadUse](commands/evidence-handling-policy.md#authorizeartifacthandlingdownloaduse) | EvidenceHandlingPolicy | 0.31 | 0.51 | 0.24 | 0.56 | 0.63 | 0.35 | 0.65 | uncertain; review manually |
| [SettleArtifactHandlingDownloadUse](commands/evidence-handling-policy.md#settleartifacthandlingdownloaduse) | EvidenceHandlingPolicy | 0.05 | 0.39 | 0.09 | 0.27 | 0.52 | 0.73 | 0.72 | high-confidence concern |
| [ApproveEvidenceRedaction](commands/evidence-redaction.md#approveevidenceredaction) | EvidenceRedaction | 0.93 | 0.93 | 0.77 | 0.86 | 0.86 | 0.07 | 0.36 | uncertain; review manually |
| [PrepareEvidenceRedaction](commands/evidence-redaction.md#prepareevidenceredaction) | EvidenceRedaction | 0.53 | 0.47 | 0.55 | 0.74 | 0.82 | 0.20 | 0.50 | uncertain; review manually |
| [CancelEvidenceRequest](commands/evidence-request.md#cancelevidencerequest) | EvidenceRequest | 0.95 | 0.95 | 0.89 | 0.91 | 0.88 | 0.09 | 0.25 | uncertain; review manually |
| [FulfilEvidenceRequest](commands/evidence-request.md#fulfilevidencerequest) | EvidenceRequest | 0.56 | 0.49 | 0.56 | 0.81 | 0.82 | 0.14 | 0.47 | uncertain; review manually |
| [OpenEvidenceRequest](commands/evidence-request.md#openevidencerequest) | EvidenceRequest | 0.58 | 0.45 | 0.66 | 0.78 | 0.74 | 0.33 | 0.54 | uncertain; review manually |
| [AuthorizeEvidenceRequestActionCompletionUse](commands/evidence-request.md#authorizeevidencerequestactioncompletionuse) | EvidenceRequest | 0.01 | 0.43 | 0.11 | 0.24 | 0.37 | 0.78 | 0.78 | high-confidence concern |
| [SettleEvidenceRequestActionCompletionUse](commands/evidence-request.md#settleevidencerequestactioncompletionuse) | EvidenceRequest | 0.03 | 0.52 | 0.09 | 0.24 | 0.43 | 0.74 | 0.79 | high-confidence concern |
| [LinkEvidenceSupport](commands/evidence-support.md#linkevidencesupport) | EvidenceSupport | 0.42 | 0.48 | 0.39 | 0.69 | 0.71 | 0.17 | 0.57 | uncertain; review manually |
| [AmendTypeIBaseline](commands/examination-engagement.md#amendtypeibaseline) | ExaminationEngagement | 0.35 | 0.53 | 0.16 | 0.55 | 0.81 | 0.16 | 0.48 | high-confidence concern |
| [CreateExaminationEngagement](commands/examination-engagement.md#createexaminationengagement) | ExaminationEngagement | 0.60 | 0.49 | 0.48 | 0.81 | 0.80 | 0.14 | 0.50 | uncertain; review manually |
| [FreezeTypeIBaseline](commands/examination-engagement.md#freezetypeibaseline) | ExaminationEngagement | 0.57 | 0.52 | 0.17 | 0.58 | 0.81 | 0.19 | 0.46 | high-confidence concern |
| [ReviseExaminationEngagement](commands/examination-engagement.md#reviseexaminationengagement) | ExaminationEngagement | 0.26 | 0.63 | 0.56 | 0.73 | 0.71 | 0.14 | 0.69 | uncertain; review manually |
| [RecordManagementExaminationReview](commands/examination-sign-off.md#recordmanagementexaminationreview) | ExaminationSignOff | 0.57 | 0.38 | 0.61 | 0.79 | 0.75 | 0.19 | 0.57 | uncertain; review manually |
| [AddCorrectiveAction](commands/finding-aggregate.md#addcorrectiveaction) | FindingAggregate | 0.85 | 0.84 | 0.86 | 0.89 | 0.80 | 0.09 | 0.38 | uncertain; review manually |
| [CloseFinding](commands/finding-aggregate.md#closefinding) | FindingAggregate | 0.95 | 0.95 | 0.81 | 0.88 | 0.74 | 0.07 | 0.42 | uncertain; review manually |
| [LinkFindingAcceptance](commands/finding-aggregate.md#linkfindingacceptance) | FindingAggregate | 0.27 | 0.51 | 0.32 | 0.61 | 0.70 | 0.26 | 0.70 | uncertain; review manually |
| [RaiseFinding](commands/finding-aggregate.md#raisefinding) | FindingAggregate | 0.61 | 0.58 | 0.52 | 0.80 | 0.79 | 0.14 | 0.53 | uncertain; review manually |
| [RaiseOccurrenceFinding](commands/finding-aggregate.md#raiseoccurrencefinding) | FindingAggregate | 0.65 | 0.61 | 0.41 | 0.75 | 0.81 | 0.18 | 0.54 | uncertain; review manually |
| [ReopenFinding](commands/finding-aggregate.md#reopenfinding) | FindingAggregate | 0.95 | 0.95 | 0.85 | 0.90 | 0.84 | 0.07 | 0.38 | uncertain; review manually |
| [ReviseFinding](commands/finding-aggregate.md#revisefinding) | FindingAggregate | 0.86 | 0.85 | 0.74 | 0.85 | 0.77 | 0.11 | 0.51 | uncertain; review manually |
| [RaiseEvaluationDeviationFinding](commands/finding-aggregate.md#raiseevaluationdeviationfinding) | FindingAggregate | 0.57 | 0.50 | 0.40 | 0.68 | 0.76 | 0.11 | 0.56 | uncertain; review manually |
| [AbortFindingClosure](commands/finding-aggregate.md#abortfindingclosure) | FindingAggregate | 0.59 | 0.50 | 0.62 | 0.79 | 0.78 | 0.34 | 0.48 | uncertain; review manually |
| [CompleteFindingClosure](commands/finding-aggregate.md#completefindingclosure) | FindingAggregate | 0.49 | 0.35 | 0.60 | 0.67 | 0.72 | 0.28 | 0.55 | uncertain; review manually |
| [RecordFirmProfessionalDutyDesignation](commands/firm-professional-duty-designation.md#recordfirmprofessionaldutydesignation) | FirmProfessionalDutyDesignation | 0.31 | 0.30 | 0.43 | 0.72 | 0.74 | 0.29 | 0.62 | uncertain; review manually |
| [RevokeFirmProfessionalDutyDesignation](commands/firm-professional-duty-designation.md#revokefirmprofessionaldutydesignation) | FirmProfessionalDutyDesignation | 0.80 | 0.79 | 0.53 | 0.78 | 0.76 | 0.13 | 0.57 | uncertain; review manually |
| [RejectProfessionalDutyChange](commands/firm-professional-duty-designation.md#rejectprofessionaldutychange) | FirmProfessionalDutyDesignation | 0.55 | 0.48 | 0.74 | 0.73 | 0.67 | 0.21 | 0.62 | uncertain; review manually |
| [ApplyProfessionalDutyChange](commands/firm-professional-duty-designation.md#applyprofessionaldutychange) | FirmProfessionalDutyDesignation | 0.20 | 0.51 | 0.47 | 0.53 | 0.55 | 0.34 | 0.70 | uncertain; review manually |
| [AuthorizeFirmProfessionalDutyDesignationEngagementUse](commands/firm-professional-duty-designation.md#authorizefirmprofessionaldutydesignationengagementuse) | FirmProfessionalDutyDesignation | 0.25 | 0.62 | 0.26 | 0.55 | 0.66 | 0.21 | 0.65 | uncertain; review manually |
| [SettleFirmProfessionalDutyDesignationEngagementUse](commands/firm-professional-duty-designation.md#settlefirmprofessionaldutydesignationengagementuse) | FirmProfessionalDutyDesignation | 0.15 | 0.67 | 0.15 | 0.43 | 0.68 | 0.27 | 0.66 | high-confidence concern |
| [ClaimFirmStaffIdentity](commands/firm-staff-identity-claim.md#claimfirmstaffidentity) | FirmStaffIdentityClaim | 0.26 | 0.55 | 0.49 | 0.65 | 0.70 | 0.22 | 0.70 | uncertain; review manually |
| [RegisterFirmStaff](commands/firm-staff-record.md#registerfirmstaff) | FirmStaffRecord | 0.74 | 0.72 | 0.81 | 0.83 | 0.82 | 0.16 | 0.48 | uncertain; review manually |
| [SetFirmStaffStatus](commands/firm-staff-record.md#setfirmstaffstatus) | FirmStaffRecord | 0.78 | 0.77 | 0.69 | 0.84 | 0.80 | 0.16 | 0.54 | uncertain; review manually |
| [RejectFirmStaffRegistration](commands/firm-staff-record.md#rejectfirmstaffregistration) | FirmStaffRecord | 0.76 | 0.74 | 0.81 | 0.81 | 0.82 | 0.16 | 0.47 | uncertain; review manually |
| [CompleteFirmStaffRegistration](commands/firm-staff-record.md#completefirmstaffregistration) | FirmStaffRecord | 0.46 | 0.34 | 0.62 | 0.65 | 0.78 | 0.38 | 0.55 | uncertain; review manually |
| [AuthorizeFirmStaffRecordEngagementUse](commands/firm-staff-record.md#authorizefirmstaffrecordengagementuse) | FirmStaffRecord | 0.38 | 0.56 | 0.35 | 0.61 | 0.74 | 0.24 | 0.59 | uncertain; review manually |
| [SettleFirmStaffRecordEngagementUse](commands/firm-staff-record.md#settlefirmstaffrecordengagementuse) | FirmStaffRecord | 0.18 | 0.69 | 0.19 | 0.46 | 0.72 | 0.28 | 0.66 | high-confidence concern |
| [AuthorizeFirmStaffProfessionalDutyUse](commands/firm-staff-record.md#authorizefirmstaffprofessionaldutyuse) | FirmStaffRecord | 0.51 | 0.48 | 0.48 | 0.68 | 0.70 | 0.15 | 0.62 | uncertain; review manually |
| [SettleFirmStaffProfessionalDutyUse](commands/firm-staff-record.md#settlefirmstaffprofessionaldutyuse) | FirmStaffRecord | 0.13 | 0.73 | 0.18 | 0.46 | 0.72 | 0.30 | 0.67 | high-confidence concern |
| [RejectIdentityRecoveryPreparation](commands/identity-recovery.md#rejectidentityrecoverypreparation) | IdentityRecovery | 0.31 | 0.52 | 0.40 | 0.63 | 0.78 | 0.34 | 0.59 | uncertain; review manually |
| [CompleteAcceptedIdentityRecovery](commands/identity-recovery.md#completeacceptedidentityrecovery) | IdentityRecovery | 0.49 | 0.37 | 0.31 | 0.55 | 0.76 | 0.37 | 0.50 | uncertain; review manually |
| [MaterializeIdentityRecoveryPreparation](commands/identity-recovery.md#materializeidentityrecoverypreparation) | IdentityRecovery | 0.01 | 0.43 | 0.14 | 0.28 | 0.55 | 0.73 | 0.75 | high-confidence concern |
| [AcceptProtectedIdentityRecovery](commands/identity-recovery.md#acceptprotectedidentityrecovery) | IdentityRecovery | 0.32 | 0.57 | 0.31 | 0.46 | 0.70 | 0.30 | 0.61 | uncertain; review manually |
| [AmendProgramScopeSnapshot](commands/immutable-snapshot.md#amendprogramscopesnapshot) | ImmutableSnapshot | 0.23 | 0.56 | 0.31 | 0.58 | 0.78 | 0.28 | 0.55 | uncertain; review manually |
| [FreezeProgramScopeSnapshot](commands/immutable-snapshot.md#freezeprogramscopesnapshot) | ImmutableSnapshot | 0.41 | 0.47 | 0.34 | 0.63 | 0.83 | 0.31 | 0.54 | uncertain; review manually |
| [RecordImportedOwnerResolution](commands/imported-owner-resolution.md#recordimportedownerresolution) | ImportedOwnerResolution | 0.36 | 0.44 | 0.40 | 0.70 | 0.78 | 0.24 | 0.49 | uncertain; review manually |
| [ReviseIndependenceRules](commands/independence-rule-catalog.md#reviseindependencerules) | IndependenceRuleCatalog | 0.69 | 0.67 | 0.64 | 0.79 | 0.77 | 0.14 | 0.47 | uncertain; review manually |
| [AuthorizeIndependenceRuleCatalogEngagementUse](commands/independence-rule-catalog.md#authorizeindependencerulecatalogengagementuse) | IndependenceRuleCatalog | 0.20 | 0.60 | 0.18 | 0.51 | 0.65 | 0.34 | 0.69 | high-confidence concern |
| [SettleIndependenceRuleCatalogEngagementUse](commands/independence-rule-catalog.md#settleindependencerulecatalogengagementuse) | IndependenceRuleCatalog | 0.13 | 0.60 | 0.12 | 0.41 | 0.71 | 0.32 | 0.68 | high-confidence concern |
| [RatifyIndependenceRuleVersion](commands/independence-rule-ratification-catalog.md#ratifyindependenceruleversion) | IndependenceRuleRatificationCatalog | 0.78 | 0.77 | 0.52 | 0.82 | 0.84 | 0.11 | 0.40 | uncertain; review manually |
| [AuthorizeIndependenceRuleRatificationCatalogEngagementUse](commands/independence-rule-ratification-catalog.md#authorizeindependenceruleratificationcatalogengagementuse) | IndependenceRuleRatificationCatalog | 0.18 | 0.55 | 0.12 | 0.43 | 0.65 | 0.43 | 0.68 | high-confidence concern |
| [SettleIndependenceRuleRatificationCatalogEngagementUse](commands/independence-rule-ratification-catalog.md#settleindependenceruleratificationcatalogengagementuse) | IndependenceRuleRatificationCatalog | 0.12 | 0.61 | 0.09 | 0.37 | 0.69 | 0.38 | 0.68 | high-confidence concern |
| [RecordInformationAsset](commands/information-asset.md#recordinformationasset) | InformationAsset | 0.67 | 0.58 | 0.57 | 0.77 | 0.77 | 0.21 | 0.56 | uncertain; review manually |
| [ReviseInformationAsset](commands/information-asset.md#reviseinformationasset) | InformationAsset | 0.73 | 0.71 | 0.58 | 0.81 | 0.84 | 0.17 | 0.44 | uncertain; review manually |
| [ClaimLocationName](commands/location-name-claim.md#claimlocationname) | LocationNameClaim | 0.49 | 0.44 | 0.53 | 0.68 | 0.77 | 0.14 | 0.51 | uncertain; review manually |
| [ReleaseLocationName](commands/location-name-claim.md#releaselocationname) | LocationNameClaim | 0.52 | 0.49 | 0.51 | 0.62 | 0.75 | 0.14 | 0.60 | uncertain; review manually |
| [RecordLocation](commands/location.md#recordlocation) | Location | 0.75 | 0.65 | 0.80 | 0.85 | 0.81 | 0.23 | 0.41 | uncertain; review manually |
| [ReviseLocation](commands/location.md#reviselocation) | Location | 0.73 | 0.71 | 0.72 | 0.82 | 0.84 | 0.15 | 0.45 | uncertain; review manually |
| [CancelLocationChange](commands/location.md#cancellocationchange) | Location | 0.66 | 0.63 | 0.82 | 0.79 | 0.79 | 0.21 | 0.47 | uncertain; review manually |
| [ApplyLocationChange](commands/location.md#applylocationchange) | Location | 0.37 | 0.29 | 0.69 | 0.62 | 0.65 | 0.28 | 0.71 | uncertain; review manually |
| [ApproveManagementAssertion](commands/management-assertion.md#approvemanagementassertion) | ManagementAssertion | 0.77 | 0.75 | 0.61 | 0.81 | 0.84 | 0.09 | 0.43 | uncertain; review manually |
| [RecordManagementAssertion](commands/management-assertion.md#recordmanagementassertion) | ManagementAssertion | 0.31 | 0.00 | 0.65 | 0.80 | 0.79 | 0.45 | 0.60 | uncertain; review manually |
| [RecordManagementRepresentationLetter](commands/management-representation-letter.md#recordmanagementrepresentationletter) | ManagementRepresentationLetter | 0.44 | 0.00 | 0.68 | 0.82 | 0.82 | 0.44 | 0.61 | uncertain; review manually |
| [SignManagementRepresentationLetter](commands/management-representation-letter.md#signmanagementrepresentationletter) | ManagementRepresentationLetter | 0.95 | 0.95 | 0.83 | 0.88 | 0.87 | 0.07 | 0.38 | uncertain; review manually |
| [CompleteMemberRetirement](commands/member.md#completememberretirement) | Member | 0.42 | 0.44 | 0.34 | 0.65 | 0.67 | 0.24 | 0.66 | uncertain; review manually |
| [RegisterMember](commands/member.md#registermember) | Member | 0.58 | 0.51 | 0.64 | 0.75 | 0.58 | 0.25 | 0.70 | uncertain; review manually |
| [SuspendMember](commands/member.md#suspendmember) | Member | 0.64 | 0.62 | 0.71 | 0.80 | 0.69 | 0.13 | 0.74 | uncertain; review manually |
| [DeprovisionMember](commands/member.md#deprovisionmember) | Member | 0.51 | 0.45 | 0.40 | 0.84 | 0.75 | 0.28 | 0.57 | uncertain; review manually |
| [ReinstateMember](commands/member.md#reinstatemember) | Member | 0.71 | 0.69 | 0.72 | 0.81 | 0.76 | 0.09 | 0.63 | uncertain; review manually |
| [ApplyMemberWithdrawal](commands/member.md#applymemberwithdrawal) | Member | 0.17 | 0.26 | 0.54 | 0.61 | 0.75 | 0.46 | 0.61 | uncertain; review manually |
| [ApplyNeutralMemberSourceMutation](commands/member.md#applyneutralmembersourcemutation) | Member | 0.00 | 0.50 | 0.05 | 0.17 | 0.40 | 0.80 | 0.83 | high-confidence concern |
| [ApplyMemberRestoration](commands/member.md#applymemberrestoration) | Member | 0.37 | 0.33 | 0.56 | 0.66 | 0.73 | 0.28 | 0.65 | uncertain; review manually |
| [AcceptTemplateContribution](commands/methodology-template-contribution.md#accepttemplatecontribution) | MethodologyTemplateContribution | 0.66 | 0.63 | 0.58 | 0.75 | 0.83 | 0.10 | 0.42 | uncertain; review manually |
| [ProposeDeidentifiedTemplateContribution](commands/methodology-template-contribution.md#proposedeidentifiedtemplatecontribution) | MethodologyTemplateContribution | 0.36 | 0.56 | 0.26 | 0.72 | 0.86 | 0.17 | 0.45 | uncertain; review manually |
| [ReviewTemplateContribution](commands/methodology-template-contribution.md#reviewtemplatecontribution) | MethodologyTemplateContribution | 0.81 | 0.80 | 0.58 | 0.82 | 0.86 | 0.07 | 0.37 | uncertain; review manually |
| [ApproveMethodologyTemplateVersion](commands/methodology-template.md#approvemethodologytemplateversion) | MethodologyTemplate | 0.86 | 0.85 | 0.44 | 0.83 | 0.86 | 0.14 | 0.33 | uncertain; review manually |
| [CreateMethodologyTemplate](commands/methodology-template.md#createmethodologytemplate) | MethodologyTemplate | 0.70 | 0.65 | 0.44 | 0.82 | 0.86 | 0.14 | 0.43 | uncertain; review manually |
| [RetireMethodologyTemplate](commands/methodology-template.md#retiremethodologytemplate) | MethodologyTemplate | 0.55 | 0.50 | 0.65 | 0.77 | 0.81 | 0.14 | 0.55 | uncertain; review manually |
| [ReviewMethodologyTemplate](commands/methodology-template.md#reviewmethodologytemplate) | MethodologyTemplate | 0.61 | 0.59 | 0.81 | 0.75 | 0.68 | 0.10 | 0.71 | uncertain; review manually |
| [ReviseMethodologyTemplateDraft](commands/methodology-template.md#revisemethodologytemplatedraft) | MethodologyTemplate | 0.71 | 0.68 | 0.67 | 0.76 | 0.80 | 0.26 | 0.52 | uncertain; review manually |
| [ClaimOperationalProcessName](commands/operational-process-name-claim.md#claimoperationalprocessname) | OperationalProcessNameClaim | 0.15 | 0.54 | 0.32 | 0.57 | 0.75 | 0.32 | 0.63 | uncertain; review manually |
| [ReleaseOperationalProcessName](commands/operational-process-name-claim.md#releaseoperationalprocessname) | OperationalProcessNameClaim | 0.38 | 0.54 | 0.33 | 0.50 | 0.67 | 0.20 | 0.72 | uncertain; review manually |
| [RecordOperationalProcess](commands/operational-process.md#recordoperationalprocess) | OperationalProcess | 0.42 | 0.06 | 0.64 | 0.79 | 0.81 | 0.30 | 0.50 | uncertain; review manually |
| [ReviseOperationalProcess](commands/operational-process.md#reviseoperationalprocess) | OperationalProcess | 0.69 | 0.67 | 0.72 | 0.82 | 0.82 | 0.15 | 0.59 | uncertain; review manually |
| [CancelOperationalProcessChange](commands/operational-process.md#canceloperationalprocesschange) | OperationalProcess | 0.56 | 0.48 | 0.76 | 0.76 | 0.73 | 0.26 | 0.68 | uncertain; review manually |
| [ApplyOperationalProcessChange](commands/operational-process.md#applyoperationalprocesschange) | OperationalProcess | 0.09 | 0.00 | 0.46 | 0.57 | 0.66 | 0.43 | 0.69 | uncertain; review manually |
| [MaterializePartnerIndependenceDecision](commands/partner-independence-decision.md#materializepartnerindependencedecision) | PartnerIndependenceDecision | 0.04 | 0.47 | 0.10 | 0.32 | 0.55 | 0.77 | 0.79 | high-confidence concern |
| [RecordPartnerIndependenceEvaluation](commands/partner-independence-decision.md#recordpartnerindependenceevaluation) | PartnerIndependenceDecision | 0.55 | 0.32 | 0.43 | 0.77 | 0.83 | 0.22 | 0.47 | uncertain; review manually |
| [CorrelatePersonMembership](commands/person.md#correlatepersonmembership) | Person | 0.19 | 0.57 | 0.22 | 0.54 | 0.66 | 0.35 | 0.70 | uncertain; review manually |
| [RecordPerson](commands/person.md#recordperson) | Person | 0.49 | 0.21 | 0.66 | 0.80 | 0.77 | 0.29 | 0.51 | uncertain; review manually |
| [RevisePerson](commands/person.md#reviseperson) | Person | 0.79 | 0.78 | 0.64 | 0.81 | 0.80 | 0.13 | 0.40 | uncertain; review manually |
| [GrantPlatformOperator](commands/platform-operator-roster.md#grantplatformoperator) | PlatformOperatorRoster | 0.76 | 0.74 | 0.42 | 0.72 | 0.77 | 0.16 | 0.57 | uncertain; review manually |
| [RevokePlatformOperator](commands/platform-operator-roster.md#revokeplatformoperator) | PlatformOperatorRoster | 0.91 | 0.91 | 0.62 | 0.84 | 0.85 | 0.11 | 0.44 | uncertain; review manually |
| [SeedPlatformOperatorRoster](commands/platform-operator-roster.md#seedplatformoperatorroster) | PlatformOperatorRoster | 0.04 | 0.15 | 0.14 | 0.37 | 0.69 | 0.71 | 0.75 | high-confidence concern |
| [AcknowledgePolicy](commands/policy-campaign-participant.md#acknowledgepolicy) | PolicyCampaignParticipant | 0.89 | 0.89 | 0.88 | 0.87 | 0.69 | 0.08 | 0.56 | uncertain; review manually |
| [ApproveCampaignWaiver](commands/policy-campaign-participant.md#approvecampaignwaiver) | PolicyCampaignParticipant | 0.66 | 0.64 | 0.66 | 0.70 | 0.72 | 0.13 | 0.63 | uncertain; review manually |
| [RecordTrainingCompletion](commands/policy-campaign-participant.md#recordtrainingcompletion) | PolicyCampaignParticipant | 0.78 | 0.60 | 0.90 | 0.89 | 0.78 | 0.19 | 0.40 | uncertain; review manually |
| [ApplyPolicyParticipantResult](commands/policy-campaign-participant.md#applypolicyparticipantresult) | PolicyCampaignParticipant | 0.03 | 0.37 | 0.27 | 0.46 | 0.48 | 0.50 | 0.70 | uncertain; review manually |
| [AuthorizePolicyCampaignParticipantCampaignClosureUse](commands/policy-campaign-participant.md#authorizepolicycampaignparticipantcampaignclosureuse) | PolicyCampaignParticipant | 0.02 | 0.55 | 0.15 | 0.35 | 0.43 | 0.48 | 0.76 | high-confidence concern |
| [SettlePolicyCampaignParticipantCampaignClosureUse](commands/policy-campaign-participant.md#settlepolicycampaignparticipantcampaignclosureuse) | PolicyCampaignParticipant | 0.03 | 0.56 | 0.09 | 0.23 | 0.43 | 0.60 | 0.78 | high-confidence concern |
| [CloseCampaign](commands/policy-distribution-campaign.md#closecampaign) | PolicyDistributionCampaign | 0.64 | 0.63 | 0.75 | 0.74 | 0.64 | 0.13 | 0.60 | uncertain; review manually |
| [LaunchPolicyCampaign](commands/policy-distribution-campaign.md#launchpolicycampaign) | PolicyDistributionCampaign | 0.80 | 0.79 | 0.69 | 0.75 | 0.76 | 0.14 | 0.54 | uncertain; review manually |
| [LaunchTrainingCampaign](commands/policy-distribution-campaign.md#launchtrainingcampaign) | PolicyDistributionCampaign | 0.84 | 0.83 | 0.77 | 0.80 | 0.77 | 0.13 | 0.43 | uncertain; review manually |
| [ReconcileCampaignAudience](commands/policy-distribution-campaign.md#reconcilecampaignaudience) | PolicyDistributionCampaign | 0.22 | 0.60 | 0.32 | 0.64 | 0.73 | 0.35 | 0.67 | uncertain; review manually |
| [AbortPolicyCampaignClosure](commands/policy-distribution-campaign.md#abortpolicycampaignclosure) | PolicyDistributionCampaign | 0.50 | 0.39 | 0.58 | 0.73 | 0.80 | 0.32 | 0.48 | uncertain; review manually |
| [CompletePolicyCampaignClosure](commands/policy-distribution-campaign.md#completepolicycampaignclosure) | PolicyDistributionCampaign | 0.39 | 0.09 | 0.44 | 0.63 | 0.75 | 0.31 | 0.52 | uncertain; review manually |
| [ApprovePolicy](commands/policy.md#approvepolicy) | Policy | 0.91 | 0.91 | 0.88 | 0.83 | 0.74 | 0.07 | 0.59 | uncertain; review manually |
| [ApprovePolicyRetirement](commands/policy.md#approvepolicyretirement) | Policy | 0.92 | 0.92 | 0.75 | 0.83 | 0.82 | 0.09 | 0.50 | uncertain; review manually |
| [ConfirmPolicyReview](commands/policy.md#confirmpolicyreview) | Policy | 0.57 | 0.52 | 0.67 | 0.70 | 0.65 | 0.13 | 0.68 | uncertain; review manually |
| [CreatePolicyDraft](commands/policy.md#createpolicydraft) | Policy | 0.79 | 0.73 | 0.70 | 0.81 | 0.84 | 0.26 | 0.48 | uncertain; review manually |
| [DiscardPolicyDraft](commands/policy.md#discardpolicydraft) | Policy | 0.85 | 0.83 | 0.73 | 0.85 | 0.85 | 0.25 | 0.34 | uncertain; review manually |
| [ProposePolicyRetirement](commands/policy.md#proposepolicyretirement) | Policy | 0.84 | 0.83 | 0.72 | 0.84 | 0.84 | 0.08 | 0.51 | uncertain; review manually |
| [ProposePolicySuccessor](commands/policy.md#proposepolicysuccessor) | Policy | 0.38 | 0.55 | 0.31 | 0.62 | 0.77 | 0.15 | 0.64 | uncertain; review manually |
| [ReviewPolicyDraft](commands/policy.md#reviewpolicydraft) | Policy | 0.71 | 0.69 | 0.86 | 0.79 | 0.75 | 0.13 | 0.60 | uncertain; review manually |
| [RevisePolicyDraft](commands/policy.md#revisepolicydraft) | Policy | 0.67 | 0.63 | 0.79 | 0.79 | 0.77 | 0.19 | 0.57 | uncertain; review manually |
| [FreezePopulationSnapshot](commands/population-snapshot.md#freezepopulationsnapshot) | PopulationSnapshot | 0.15 | 0.11 | 0.25 | 0.55 | 0.74 | 0.51 | 0.67 | uncertain; review manually |
| [ReservePracticeIndependence](commands/practice-independence-commitment.md#reservepracticeindependence) | PracticeIndependenceCommitment | 0.14 | 0.53 | 0.24 | 0.47 | 0.64 | 0.33 | 0.73 | uncertain; review manually |
| [CommitPracticeIndependence](commands/practice-independence-commitment.md#commitpracticeindependence) | PracticeIndependenceCommitment | 0.31 | 0.55 | 0.25 | 0.51 | 0.53 | 0.31 | 0.70 | uncertain; review manually |
| [SettlePracticeIndependenceReservation](commands/practice-independence-commitment.md#settlepracticeindependencereservation) | PracticeIndependenceCommitment | 0.10 | 0.46 | 0.15 | 0.40 | 0.62 | 0.37 | 0.71 | high-confidence concern |
| [ClaimProfessionalAssertionIdentity](commands/professional-assertion-identity-claim.md#claimprofessionalassertionidentity) | ProfessionalAssertionIdentityClaim | 0.18 | 0.56 | 0.22 | 0.59 | 0.74 | 0.21 | 0.64 | uncertain; review manually |
| [DecideReadiness](commands/program-readiness.md#decidereadiness) | ProgramReadiness | 0.30 | 0.64 | 0.50 | 0.63 | 0.66 | 0.16 | 0.66 | uncertain; review manually |
| [DecideTypeIEntry](commands/program-readiness.md#decidetypeientry) | ProgramReadiness | 0.13 | 0.64 | 0.11 | 0.23 | 0.73 | 0.28 | 0.69 | high-confidence concern |
| [RunReadinessAssessment](commands/program-readiness.md#runreadinessassessment) | ProgramReadiness | 0.83 | 0.82 | 0.71 | 0.83 | 0.81 | 0.20 | 0.54 | uncertain; review manually |
| [AbortReadinessDecision](commands/program-readiness.md#abortreadinessdecision) | ProgramReadiness | 0.42 | 0.47 | 0.68 | 0.70 | 0.74 | 0.33 | 0.56 | uncertain; review manually |
| [AdoptReadinessAssessment](commands/program-readiness.md#adoptreadinessassessment) | ProgramReadiness | 0.50 | 0.40 | 0.45 | 0.62 | 0.80 | 0.30 | 0.53 | uncertain; review manually |
| [CompleteReadinessDecision](commands/program-readiness.md#completereadinessdecision) | ProgramReadiness | 0.27 | 0.46 | 0.39 | 0.51 | 0.70 | 0.35 | 0.63 | uncertain; review manually |
| [CompleteTypeIEntryDecision](commands/program-readiness.md#completetypeientrydecision) | ProgramReadiness | 0.30 | 0.43 | 0.12 | 0.32 | 0.77 | 0.36 | 0.56 | high-confidence concern |
| [ApproveProgramRollover](commands/program-rollover.md#approveprogramrollover) | ProgramRollover | 0.73 | 0.71 | 0.55 | 0.74 | 0.82 | 0.09 | 0.49 | uncertain; review manually |
| [ProposeProgramRollover](commands/program-rollover.md#proposeprogramrollover) | ProgramRollover | 0.79 | 0.78 | 0.50 | 0.75 | 0.86 | 0.10 | 0.47 | uncertain; review manually |
| [ClaimProgramRolloverExecution](commands/program-rollover.md#claimprogramrolloverexecution) | ProgramRollover | 0.39 | 0.44 | 0.30 | 0.56 | 0.79 | 0.38 | 0.54 | uncertain; review manually |
| [CloseProviderCoverageGap](commands/provider-assurance-gap.md#closeprovidercoveragegap) | ProviderAssuranceGap | 0.85 | 0.84 | 0.67 | 0.83 | 0.84 | 0.11 | 0.38 | uncertain; review manually |
| [LinkProviderCoverageGapRiskAcceptance](commands/provider-assurance-gap.md#linkprovidercoveragegapriskacceptance) | ProviderAssuranceGap | 0.11 | 0.33 | 0.26 | 0.49 | 0.65 | 0.39 | 0.73 | uncertain; review manually |
| [RecordProviderCoverageGap](commands/provider-assurance-gap.md#recordprovidercoveragegap) | ProviderAssuranceGap | 0.54 | 0.22 | 0.51 | 0.74 | 0.84 | 0.25 | 0.41 | uncertain; review manually |
| [RecordAssuranceReport](commands/provider-assurance-report.md#recordassurancereport) | ProviderAssuranceReport | 0.74 | 0.62 | 0.75 | 0.80 | 0.77 | 0.21 | 0.46 | uncertain; review manually |
| [ReviseAssuranceReport](commands/provider-assurance-report.md#reviseassurancereport) | ProviderAssuranceReport | 0.79 | 0.78 | 0.76 | 0.81 | 0.80 | 0.15 | 0.46 | uncertain; review manually |
| [AuthorizeProviderAssuranceReportProviderReviewUse](commands/provider-assurance-report.md#authorizeproviderassurancereportproviderreviewuse) | ProviderAssuranceReport | 0.04 | 0.35 | 0.21 | 0.42 | 0.52 | 0.54 | 0.75 | uncertain; review manually |
| [SettleProviderAssuranceReportProviderReviewUse](commands/provider-assurance-report.md#settleproviderassurancereportproviderreviewuse) | ProviderAssuranceReport | 0.07 | 0.43 | 0.12 | 0.33 | 0.59 | 0.53 | 0.72 | high-confidence concern |
| [RecordProviderReview](commands/provider-assurance-review.md#recordproviderreview) | ProviderAssuranceReview | 0.53 | 0.31 | 0.64 | 0.75 | 0.80 | 0.21 | 0.55 | uncertain; review manually |
| [AbortProviderReview](commands/provider-assurance-review.md#abortproviderreview) | ProviderAssuranceReview | 0.58 | 0.53 | 0.74 | 0.74 | 0.79 | 0.30 | 0.52 | uncertain; review manually |
| [CompleteProviderAssuranceReview](commands/provider-assurance-review.md#completeproviderassurancereview) | ProviderAssuranceReview | 0.47 | 0.38 | 0.56 | 0.62 | 0.73 | 0.21 | 0.60 | uncertain; review manually |
| [ClaimProviderName](commands/provider-name-claim.md#claimprovidername) | ProviderNameClaim | 0.56 | 0.50 | 0.54 | 0.69 | 0.78 | 0.17 | 0.54 | uncertain; review manually |
| [ReleaseProviderName](commands/provider-name-claim.md#releaseprovidername) | ProviderNameClaim | 0.62 | 0.59 | 0.57 | 0.69 | 0.76 | 0.14 | 0.52 | uncertain; review manually |
| [MaterializeProviderRiskAcceptanceLink](commands/provider-risk-acceptance.md#materializeproviderriskacceptancelink) | ProviderRiskAcceptance | 0.01 | 0.71 | 0.13 | 0.29 | 0.66 | 0.73 | 0.70 | high-confidence concern |
| [RecordProvider](commands/provider.md#recordprovider) | Provider | 0.44 | 0.23 | 0.68 | 0.75 | 0.73 | 0.28 | 0.60 | uncertain; review manually |
| [ReviseProvider](commands/provider.md#reviseprovider) | Provider | 0.77 | 0.75 | 0.65 | 0.79 | 0.80 | 0.17 | 0.46 | uncertain; review manually |
| [CancelProviderChange](commands/provider.md#cancelproviderchange) | Provider | 0.64 | 0.58 | 0.73 | 0.72 | 0.70 | 0.28 | 0.67 | uncertain; review manually |
| [ApplyProviderChange](commands/provider.md#applyproviderchange) | Provider | 0.19 | 0.12 | 0.47 | 0.47 | 0.61 | 0.56 | 0.71 | uncertain; review manually |
| [ComputeReadinessAssessment](commands/readiness-assessment.md#computereadinessassessment) | ReadinessAssessment | 0.52 | 0.42 | 0.63 | 0.68 | 0.69 | 0.33 | 0.58 | uncertain; review manually |
| [AnnotateReadinessGap](commands/readiness-gap-plan.md#annotatereadinessgap) | ReadinessGapPlan | 0.63 | 0.59 | 0.49 | 0.75 | 0.83 | 0.15 | 0.54 | uncertain; review manually |
| [PlanReadinessGap](commands/readiness-gap-plan.md#planreadinessgap) | ReadinessGapPlan | 0.44 | 0.45 | 0.46 | 0.70 | 0.77 | 0.19 | 0.51 | uncertain; review manually |
| [AuthorizeReadinessGapPlanReadinessDecisionUse](commands/readiness-gap-plan.md#authorizereadinessgapplanreadinessdecisionuse) | ReadinessGapPlan | 0.02 | 0.18 | 0.13 | 0.28 | 0.45 | 0.68 | 0.74 | high-confidence concern |
| [SettleReadinessGapPlanReadinessDecisionUse](commands/readiness-gap-plan.md#settlereadinessgapplanreadinessdecisionuse) | ReadinessGapPlan | 0.03 | 0.54 | 0.09 | 0.22 | 0.46 | 0.68 | 0.75 | high-confidence concern |
| [CreateRiskDraft](commands/risk-draft.md#createriskdraft) | RiskDraft | 0.65 | 0.59 | 0.65 | 0.79 | 0.81 | 0.26 | 0.50 | uncertain; review manually |
| [ReviseRiskDraft](commands/risk-draft.md#reviseriskdraft) | RiskDraft | 0.76 | 0.74 | 0.71 | 0.81 | 0.83 | 0.15 | 0.39 | uncertain; review manually |
| [AcceptRisk](commands/risk-evaluation.md#acceptrisk) | RiskEvaluation | 0.89 | 0.89 | 0.81 | 0.82 | 0.65 | 0.07 | 0.52 | uncertain; review manually |
| [ChooseRiskTreatment](commands/risk-evaluation.md#chooserisktreatment) | RiskEvaluation | 0.72 | 0.71 | 0.62 | 0.76 | 0.66 | 0.11 | 0.58 | uncertain; review manually |
| [RecordRiskAssessment](commands/risk-evaluation.md#recordriskassessment) | RiskEvaluation | 0.68 | 0.49 | 0.81 | 0.86 | 0.78 | 0.19 | 0.53 | uncertain; review manually |
| [AssignRiskOwner](commands/risk-governance.md#assignriskowner) | RiskGovernance | 0.97 | 0.97 | 0.90 | 0.90 | 0.82 | 0.07 | 0.39 | uncertain; review manually |
| [RaiseRiskReassessmentTriggers](commands/risk-governance.md#raiseriskreassessmenttriggers) | RiskGovernance | 0.21 | 0.39 | 0.28 | 0.55 | 0.65 | 0.48 | 0.66 | uncertain; review manually |
| [RecordRiskReassessmentObligation](commands/risk-governance.md#recordriskreassessmentobligation) | RiskGovernance | 0.31 | 0.00 | 0.58 | 0.71 | 0.68 | 0.30 | 0.66 | uncertain; review manually |
| [PublishRiskMethodVersion](commands/risk-method.md#publishriskmethodversion) | RiskMethod | 0.75 | 0.73 | 0.35 | 0.71 | 0.83 | 0.23 | 0.41 | uncertain; review manually |
| [AddRiskTreatmentAction](commands/risk-treatment-action.md#addrisktreatmentaction) | RiskTreatmentAction | 0.90 | 0.89 | 0.71 | 0.87 | 0.85 | 0.11 | 0.28 | uncertain; review manually |
| [CancelRiskTreatmentAction](commands/risk-treatment-action.md#cancelrisktreatmentaction) | RiskTreatmentAction | 0.91 | 0.91 | 0.81 | 0.89 | 0.83 | 0.10 | 0.36 | uncertain; review manually |
| [ReviewRiskTreatmentActionCompletion](commands/risk-treatment-action.md#reviewrisktreatmentactioncompletion) | RiskTreatmentAction | 0.86 | 0.85 | 0.65 | 0.83 | 0.79 | 0.12 | 0.43 | uncertain; review manually |
| [ReviseRiskTreatmentAction](commands/risk-treatment-action.md#reviserisktreatmentaction) | RiskTreatmentAction | 0.91 | 0.90 | 0.75 | 0.85 | 0.85 | 0.12 | 0.31 | uncertain; review manually |
| [SubmitRiskTreatmentActionCompletion](commands/risk-treatment-action.md#submitrisktreatmentactioncompletion) | RiskTreatmentAction | 0.79 | 0.71 | 0.58 | 0.79 | 0.82 | 0.17 | 0.39 | uncertain; review manually |
| [AbortRiskTreatmentActionReview](commands/risk-treatment-action.md#abortrisktreatmentactionreview) | RiskTreatmentAction | 0.36 | 0.42 | 0.59 | 0.60 | 0.68 | 0.51 | 0.54 | uncertain; review manually |
| [CompleteRiskTreatmentActionReview](commands/risk-treatment-action.md#completerisktreatmentactionreview) | RiskTreatmentAction | 0.34 | 0.48 | 0.45 | 0.57 | 0.58 | 0.25 | 0.76 | uncertain; review manually |
| [ProposeRiskControlTreatment](commands/risk-treatment-assertion.md#proposeriskcontroltreatment) | RiskTreatmentAssertion | 0.41 | 0.52 | 0.48 | 0.72 | 0.75 | 0.15 | 0.58 | uncertain; review manually |
| [RetireRiskControlTreatment](commands/risk-treatment-assertion.md#retireriskcontroltreatment) | RiskTreatmentAssertion | 0.73 | 0.71 | 0.48 | 0.75 | 0.77 | 0.17 | 0.54 | uncertain; review manually |
| [ReviewRiskControlTreatment](commands/risk-treatment-assertion.md#reviewriskcontroltreatment) | RiskTreatmentAssertion | 0.76 | 0.75 | 0.67 | 0.79 | 0.74 | 0.09 | 0.62 | uncertain; review manually |
| [AssignRolePermission](commands/role-permission.md#assignrolepermission) | RolePermission | 0.89 | 0.88 | 0.80 | 0.87 | 0.74 | 0.11 | 0.42 | uncertain; review manually |
| [RemoveRolePermission](commands/role-permission.md#removerolepermission) | RolePermission | 0.89 | 0.88 | 0.85 | 0.90 | 0.80 | 0.11 | 0.36 | uncertain; review manually |
| [DefineRole](commands/role.md#definerole) | Role | 0.54 | 0.40 | 0.76 | 0.82 | 0.71 | 0.19 | 0.46 | uncertain; review manually |
| [DeleteRole](commands/role.md#deleterole) | Role | 0.76 | 0.72 | 0.84 | 0.85 | 0.78 | 0.32 | 0.62 | uncertain; review manually |
| [RenameRole](commands/role.md#renamerole) | Role | 0.97 | 0.97 | 0.92 | 0.92 | 0.86 | 0.08 | 0.26 | uncertain; review manually |
| [ApproveSeparationOfDutiesWaiver](commands/separation-of-duties-waiver.md#approveseparationofdutieswaiver) | SeparationOfDutiesWaiver | 0.92 | 0.92 | 0.64 | 0.88 | 0.85 | 0.08 | 0.35 | uncertain; review manually |
| [RecordSeparationOfDutiesWaiver](commands/separation-of-duties-waiver.md#recordseparationofdutieswaiver) | SeparationOfDutiesWaiver | 0.29 | 0.02 | 0.63 | 0.82 | 0.79 | 0.29 | 0.55 | uncertain; review manually |
| [AcceptServiceEngagement](commands/service-engagement-independence.md#acceptserviceengagement) | ServiceEngagementIndependence | 0.55 | 0.50 | 0.59 | 0.76 | 0.78 | 0.13 | 0.54 | uncertain; review manually |
| [AmendServiceEngagement](commands/service-engagement-independence.md#amendserviceengagement) | ServiceEngagementIndependence | 0.39 | 0.50 | 0.66 | 0.79 | 0.69 | 0.23 | 0.61 | uncertain; review manually |
| [AssignServiceEngagementStaff](commands/service-engagement-independence.md#assignserviceengagementstaff) | ServiceEngagementIndependence | 0.84 | 0.83 | 0.75 | 0.87 | 0.82 | 0.09 | 0.53 | uncertain; review manually |
| [CloseServiceEngagement](commands/service-engagement-independence.md#closeserviceengagement) | ServiceEngagementIndependence | 0.87 | 0.87 | 0.80 | 0.86 | 0.76 | 0.11 | 0.58 | uncertain; review manually |
| [CreateServiceEngagement](commands/service-engagement-independence.md#createserviceengagement) | ServiceEngagementIndependence | 0.89 | 0.88 | 0.66 | 0.85 | 0.84 | 0.14 | 0.38 | uncertain; review manually |
| [ProposeServiceEngagementStaff](commands/service-engagement-independence.md#proposeserviceengagementstaff) | ServiceEngagementIndependence | 0.63 | 0.60 | 0.58 | 0.75 | 0.81 | 0.15 | 0.50 | uncertain; review manually |
| [RecordDirectoryIndependenceReevaluation](commands/service-engagement-independence.md#recorddirectoryindependencereevaluation) | ServiceEngagementIndependence | 0.14 | 0.00 | 0.17 | 0.39 | 0.74 | 0.64 | 0.60 | high-confidence concern |
| [RevokeServiceEngagementActualStaff](commands/service-engagement-independence.md#revokeserviceengagementactualstaff) | ServiceEngagementIndependence | 0.58 | 0.53 | 0.50 | 0.75 | 0.74 | 0.18 | 0.55 | uncertain; review manually |
| [WithdrawServiceEngagementStaffProposal](commands/service-engagement-independence.md#withdrawserviceengagementstaffproposal) | ServiceEngagementIndependence | 0.81 | 0.79 | 0.78 | 0.84 | 0.79 | 0.12 | 0.42 | uncertain; review manually |
| [RejectServiceEngagementAcceptance](commands/service-engagement-independence.md#rejectserviceengagementacceptance) | ServiceEngagementIndependence | 0.56 | 0.48 | 0.62 | 0.74 | 0.66 | 0.17 | 0.62 | uncertain; review manually |
| [RejectServiceEngagementStaffAssignment](commands/service-engagement-independence.md#rejectserviceengagementstaffassignment) | ServiceEngagementIndependence | 0.48 | 0.48 | 0.65 | 0.72 | 0.71 | 0.15 | 0.67 | uncertain; review manually |
| [CancelEngagementPersonalDecision](commands/service-engagement-independence.md#cancelengagementpersonaldecision) | ServiceEngagementIndependence | 0.16 | 0.61 | 0.39 | 0.56 | 0.52 | 0.18 | 0.74 | uncertain; review manually |
| [CompleteServiceEngagementAcceptance](commands/service-engagement-independence.md#completeserviceengagementacceptance) | ServiceEngagementIndependence | 0.44 | 0.25 | 0.50 | 0.63 | 0.73 | 0.38 | 0.57 | uncertain; review manually |
| [CompleteServiceEngagementStaffAssignment](commands/service-engagement-independence.md#completeserviceengagementstaffassignment) | ServiceEngagementIndependence | 0.34 | 0.41 | 0.39 | 0.58 | 0.71 | 0.40 | 0.55 | uncertain; review manually |
| [AuthorizeEngagementPracticeCommitments](commands/service-engagement-independence.md#authorizeengagementpracticecommitments) | ServiceEngagementIndependence | 0.41 | 0.50 | 0.28 | 0.53 | 0.74 | 0.23 | 0.63 | uncertain; review manually |
| [SettleEngagementPersonalDecision](commands/service-engagement-independence.md#settleengagementpersonaldecision) | ServiceEngagementIndependence | 0.08 | 0.57 | 0.21 | 0.38 | 0.49 | 0.39 | 0.75 | uncertain; review manually |
| [AuthorizeEngagementPersonalDecision](commands/service-engagement-independence.md#authorizeengagementpersonaldecision) | ServiceEngagementIndependence | 0.19 | 0.59 | 0.33 | 0.49 | 0.65 | 0.31 | 0.70 | uncertain; review manually |
| [RecordServiceIdentity](commands/service-identity.md#recordserviceidentity) | ServiceIdentity | 0.45 | 0.21 | 0.54 | 0.76 | 0.77 | 0.30 | 0.52 | uncertain; review manually |
| [ReviseServiceIdentity](commands/service-identity.md#reviseserviceidentity) | ServiceIdentity | 0.71 | 0.69 | 0.56 | 0.78 | 0.80 | 0.20 | 0.43 | uncertain; review manually |
| [AssessSignificantChange](commands/significant-change.md#assesssignificantchange) | SignificantChange | 0.65 | 0.63 | 0.70 | 0.77 | 0.76 | 0.11 | 0.55 | uncertain; review manually |
| [LinkImpactReviewWork](commands/significant-change.md#linkimpactreviewwork) | SignificantChange | 0.23 | 0.42 | 0.28 | 0.42 | 0.40 | 0.48 | 0.74 | uncertain; review manually |
| [RecordSignificantChange](commands/significant-change.md#recordsignificantchange) | SignificantChange | 0.48 | 0.19 | 0.60 | 0.75 | 0.81 | 0.32 | 0.53 | uncertain; review manually |
| [RecordSignificantChangeSystemDescriptionDecision](commands/significant-change.md#recordsignificantchangesystemdescriptiondecision) | SignificantChange | 0.37 | 0.30 | 0.30 | 0.57 | 0.73 | 0.35 | 0.61 | uncertain; review manually |
| [ReviseSignificantChange](commands/significant-change.md#revisesignificantchange) | SignificantChange | 0.78 | 0.77 | 0.66 | 0.80 | 0.79 | 0.15 | 0.50 | uncertain; review manually |
| [ApproveBoundary](commands/system-boundary.md#approveboundary) | SystemBoundary | 0.71 | 0.70 | 0.28 | 0.58 | 0.79 | 0.11 | 0.51 | uncertain; review manually |
| [CreateBoundary](commands/system-boundary.md#createboundary) | SystemBoundary | 0.29 | 0.54 | 0.29 | 0.41 | 0.75 | 0.20 | 0.58 | uncertain; review manually |
| [DiscardBoundaryDraft](commands/system-boundary.md#discardboundarydraft) | SystemBoundary | 0.59 | 0.54 | 0.67 | 0.78 | 0.80 | 0.30 | 0.44 | uncertain; review manually |
| [ProposeBoundarySuccessor](commands/system-boundary.md#proposeboundarysuccessor) | SystemBoundary | 0.17 | 0.64 | 0.23 | 0.41 | 0.75 | 0.24 | 0.67 | uncertain; review manually |
| [ReviewBoundary](commands/system-boundary.md#reviewboundary) | SystemBoundary | 0.69 | 0.68 | 0.50 | 0.67 | 0.71 | 0.11 | 0.60 | uncertain; review manually |
| [ReviseBoundaryDraft](commands/system-boundary.md#reviseboundarydraft) | SystemBoundary | 0.74 | 0.72 | 0.53 | 0.74 | 0.82 | 0.24 | 0.35 | uncertain; review manually |
| [ApproveSystemDescription](commands/system-description.md#approvesystemdescription) | SystemDescription | 0.91 | 0.91 | 0.77 | 0.79 | 0.85 | 0.12 | 0.44 | uncertain; review manually |
| [BindSystemDescriptionSources](commands/system-description.md#bindsystemdescriptionsources) | SystemDescription | 0.36 | 0.42 | 0.30 | 0.56 | 0.80 | 0.34 | 0.59 | uncertain; review manually |
| [BindSystemDescriptionToExamination](commands/system-description.md#bindsystemdescriptiontoexamination) | SystemDescription | 0.54 | 0.45 | 0.45 | 0.67 | 0.82 | 0.23 | 0.54 | uncertain; review manually |
| [CreateSystemDescriptionDraft](commands/system-description.md#createsystemdescriptiondraft) | SystemDescription | 0.83 | 0.82 | 0.64 | 0.79 | 0.87 | 0.24 | 0.37 | uncertain; review manually |
| [ProposeSystemDescriptionSuccessor](commands/system-description.md#proposesystemdescriptionsuccessor) | SystemDescription | 0.11 | 0.64 | 0.24 | 0.45 | 0.78 | 0.28 | 0.66 | uncertain; review manually |
| [RecordSystemDescriptionNoChangeDecision](commands/system-description.md#recordsystemdescriptionnochangedecision) | SystemDescription | 0.51 | 0.35 | 0.59 | 0.73 | 0.78 | 0.22 | 0.54 | uncertain; review manually |
| [ReviewSystemDescription](commands/system-description.md#reviewsystemdescription) | SystemDescription | 0.88 | 0.88 | 0.80 | 0.80 | 0.83 | 0.09 | 0.53 | uncertain; review manually |
| [ReviseSystemDescriptionDraft](commands/system-description.md#revisesystemdescriptiondraft) | SystemDescription | 0.85 | 0.84 | 0.67 | 0.79 | 0.86 | 0.24 | 0.32 | uncertain; review manually |
| [DecideAccessReviewScope](commands/system-instance-access-review-scope.md#decideaccessreviewscope) | SystemInstanceAccessReviewScope | 0.56 | 0.52 | 0.51 | 0.71 | 0.74 | 0.15 | 0.56 | uncertain; review manually |
| [AssignTeamMember](commands/team-member.md#assignteammember) | TeamMember | 0.87 | 0.86 | 0.84 | 0.78 | 0.73 | 0.10 | 0.53 | uncertain; review manually |
| [RemoveTeamMember](commands/team-member.md#removeteammember) | TeamMember | 0.94 | 0.94 | 0.91 | 0.82 | 0.67 | 0.09 | 0.60 | uncertain; review manually |
| [ApplyAdministratorAssignmentRestoration](commands/team-member.md#applyadministratorassignmentrestoration) | TeamMember | 0.08 | 0.00 | 0.30 | 0.39 | 0.71 | 0.67 | 0.63 | uncertain; review manually |
| [ApplyAdministratorAssignmentWithdrawal](commands/team-member.md#applyadministratorassignmentwithdrawal) | TeamMember | 0.08 | 0.28 | 0.32 | 0.46 | 0.75 | 0.63 | 0.63 | uncertain; review manually |
| [ApplyNeutralAdministratorAssignmentMutation](commands/team-member.md#applyneutraladministratorassignmentmutation) | TeamMember | 0.01 | 0.64 | 0.07 | 0.16 | 0.27 | 0.87 | 0.85 | high-confidence concern |
| [CleanupRetiredMembershipRelationship](commands/team-member.md#cleanupretiredmembershiprelationship) | TeamMember | 0.14 | 0.32 | 0.50 | 0.65 | 0.63 | 0.47 | 0.67 | uncertain; review manually |
| [AssignTeamRole](commands/team-role.md#assignteamrole) | TeamRole | 0.83 | 0.82 | 0.85 | 0.80 | 0.78 | 0.09 | 0.52 | uncertain; review manually |
| [RemoveTeamRole](commands/team-role.md#removeteamrole) | TeamRole | 0.89 | 0.88 | 0.84 | 0.81 | 0.79 | 0.10 | 0.52 | uncertain; review manually |
| [DefineTeam](commands/team.md#defineteam) | Team | 0.50 | 0.37 | 0.71 | 0.75 | 0.73 | 0.27 | 0.60 | uncertain; review manually |
| [DeleteTeam](commands/team.md#deleteteam) | Team | 0.83 | 0.81 | 0.89 | 0.87 | 0.81 | 0.15 | 0.59 | uncertain; review manually |
| [RecordTechnologyComponent](commands/technology-component.md#recordtechnologycomponent) | TechnologyComponent | 0.34 | 0.37 | 0.42 | 0.68 | 0.75 | 0.32 | 0.54 | uncertain; review manually |
| [ReviseTechnologyComponent](commands/technology-component.md#revisetechnologycomponent) | TechnologyComponent | 0.65 | 0.60 | 0.40 | 0.73 | 0.82 | 0.22 | 0.46 | uncertain; review manually |
| [ApplyMethodologyTemplate](commands/template-application.md#applymethodologytemplate) | TemplateApplication | 0.53 | 0.49 | 0.46 | 0.65 | 0.71 | 0.20 | 0.58 | uncertain; review manually |
| [ProposeTemplateSuccessorAdoption](commands/template-application.md#proposetemplatesuccessoradoption) | TemplateApplication | 0.32 | 0.57 | 0.20 | 0.52 | 0.76 | 0.22 | 0.60 | high-confidence concern |
| [RecordTemplateSuccessorDisposition](commands/template-application.md#recordtemplatesuccessordisposition) | TemplateApplication | 0.17 | 0.09 | 0.16 | 0.45 | 0.75 | 0.39 | 0.52 | high-confidence concern |
| [ConfigureTenantIdentityProvider](commands/tenant-identity-provider.md#configuretenantidentityprovider) | TenantIdentityProvider | 0.75 | 0.71 | 0.46 | 0.76 | 0.81 | 0.20 | 0.52 | uncertain; review manually |
| [DisableTenantIdentityProvider](commands/tenant-identity-provider.md#disabletenantidentityprovider) | TenantIdentityProvider | 0.94 | 0.94 | 0.65 | 0.82 | 0.81 | 0.13 | 0.43 | uncertain; review manually |
| [RecordTenantIdentityProviderTestResult](commands/tenant-identity-provider.md#recordtenantidentityprovidertestresult) | TenantIdentityProvider | 0.53 | 0.08 | 0.48 | 0.79 | 0.83 | 0.32 | 0.36 | uncertain; review manually |
| [RotateTenantIdentityProviderCredentials](commands/tenant-identity-provider.md#rotatetenantidentityprovidercredentials) | TenantIdentityProvider | 0.95 | 0.95 | 0.61 | 0.86 | 0.86 | 0.15 | 0.32 | uncertain; review manually |
| [TestTenantIdentityProvider](commands/tenant-identity-provider.md#testtenantidentityprovider) | TenantIdentityProvider | 0.83 | 0.82 | 0.58 | 0.80 | 0.82 | 0.15 | 0.52 | uncertain; review manually |
| [InviteTenantMember](commands/tenant-invitation-mailbox-claim.md#invitetenantmember) | TenantInvitationMailboxClaim | 0.71 | 0.62 | 0.81 | 0.84 | 0.61 | 0.13 | 0.53 | uncertain; review manually |
| [InviteOrganizationMember](commands/tenant-invitation-mailbox-claim.md#inviteorganizationmember) | TenantInvitationMailboxClaim | 0.55 | 0.49 | 0.81 | 0.74 | 0.60 | 0.14 | 0.72 | uncertain; review manually |
| [InviteGuestConsultant](commands/tenant-invitation-mailbox-claim.md#inviteguestconsultant) | TenantInvitationMailboxClaim | 0.69 | 0.63 | 0.84 | 0.79 | 0.57 | 0.13 | 0.64 | uncertain; review manually |
| [AcceptTenantInvitation](commands/tenant-invitation-mailbox-claim.md#accepttenantinvitation) | TenantInvitationMailboxClaim | 0.80 | 0.78 | 0.87 | 0.87 | 0.73 | 0.12 | 0.41 | uncertain; review manually |
| [AcceptTenantInvitation](commands/tenant-invitation.md#accepttenantinvitation) | TenantInvitation | 0.93 | 0.93 | 0.87 | 0.87 | 0.69 | 0.12 | 0.40 | uncertain; review manually |
| [InviteGuestConsultant](commands/tenant-invitation.md#inviteguestconsultant) | TenantInvitation | 0.59 | 0.56 | 0.84 | 0.80 | 0.56 | 0.13 | 0.64 | uncertain; review manually |
| [InviteOrganizationMember](commands/tenant-invitation.md#inviteorganizationmember) | TenantInvitation | 0.63 | 0.60 | 0.75 | 0.75 | 0.60 | 0.12 | 0.70 | uncertain; review manually |
| [InviteTenantMember](commands/tenant-invitation.md#invitetenantmember) | TenantInvitation | 0.76 | 0.74 | 0.80 | 0.78 | 0.62 | 0.12 | 0.66 | uncertain; review manually |
| [CompleteAdministratorWithdrawal](commands/tenant-manager-guard.md#completeadministratorwithdrawal) | TenantManagerGuard | 0.31 | 0.29 | 0.50 | 0.66 | 0.60 | 0.52 | 0.64 | uncertain; review manually |
| [CompleteAdministratorRestoration](commands/tenant-manager-guard.md#completeadministratorrestoration) | TenantManagerGuard | 0.35 | 0.28 | 0.47 | 0.58 | 0.64 | 0.50 | 0.63 | uncertain; review manually |
| [RegisterTenantSlug](commands/tenant-slug.md#registertenantslug) | TenantSlug | 0.45 | 0.31 | 0.30 | 0.73 | 0.83 | 0.20 | 0.44 | uncertain; review manually |
| [SurrenderTenantSlug](commands/tenant-slug.md#surrendertenantslug) | TenantSlug | 0.52 | 0.46 | 0.23 | 0.74 | 0.83 | 0.17 | 0.43 | uncertain; review manually |
| [ActivateTenant](commands/tenant.md#activatetenant) | Tenant | 0.85 | 0.84 | 0.70 | 0.83 | 0.76 | 0.11 | 0.51 | uncertain; review manually |
| [ChangeTenantSlug](commands/tenant.md#changetenantslug) | Tenant | 0.84 | 0.82 | 0.30 | 0.84 | 0.79 | 0.14 | 0.47 | uncertain; review manually |
| [CompleteTenantOffboarding](commands/tenant.md#completetenantoffboarding) | Tenant | 0.67 | 0.60 | 0.57 | 0.75 | 0.69 | 0.33 | 0.55 | uncertain; review manually |
| [ConfirmTenantSlug](commands/tenant.md#confirmtenantslug) | Tenant | 0.72 | 0.69 | 0.30 | 0.74 | 0.80 | 0.18 | 0.56 | uncertain; review manually |
| [ConfirmTenantSlugSurrender](commands/tenant.md#confirmtenantslugsurrender) | Tenant | 0.38 | 0.45 | 0.28 | 0.64 | 0.64 | 0.25 | 0.65 | uncertain; review manually |
| [OffboardTenant](commands/tenant.md#offboardtenant) | Tenant | 0.70 | 0.69 | 0.28 | 0.73 | 0.75 | 0.16 | 0.60 | uncertain; review manually |
| [ReactivateTenant](commands/tenant.md#reactivatetenant) | Tenant | 0.83 | 0.82 | 0.77 | 0.78 | 0.75 | 0.17 | 0.64 | uncertain; review manually |
| [RegisterTenant](commands/tenant.md#registertenant) | Tenant | 0.71 | 0.66 | 0.60 | 0.82 | 0.76 | 0.16 | 0.58 | uncertain; review manually |
| [RejectTenantSlug](commands/tenant.md#rejecttenantslug) | Tenant | 0.08 | 0.48 | 0.22 | 0.43 | 0.51 | 0.28 | 0.76 | uncertain; review manually |
| [RejectTenantSlugSurrender](commands/tenant.md#rejecttenantslugsurrender) | Tenant | 0.10 | 0.54 | 0.12 | 0.31 | 0.32 | 0.35 | 0.82 | high-confidence concern |
| [RequestTenantSlugSurrender](commands/tenant.md#requesttenantslugsurrender) | Tenant | 0.13 | 0.61 | 0.18 | 0.39 | 0.54 | 0.28 | 0.77 | high-confidence concern |
| [SuspendTenant](commands/tenant.md#suspendtenant) | Tenant | 0.82 | 0.81 | 0.73 | 0.87 | 0.79 | 0.10 | 0.54 | uncertain; review manually |
| [DefineTrainingRequirement](commands/training-requirement.md#definetrainingrequirement) | TrainingRequirement | 0.75 | 0.73 | 0.75 | 0.82 | 0.84 | 0.20 | 0.39 | uncertain; review manually |
| [ReviseTrainingRequirement](commands/training-requirement.md#revisetrainingrequirement) | TrainingRequirement | 0.73 | 0.70 | 0.73 | 0.83 | 0.81 | 0.22 | 0.54 | uncertain; review manually |
| [AcquireTypeIIPeriodRange](commands/type-ii-observation-calendar.md#acquiretypeiiperiodrange) | TypeIIObservationCalendar | 0.07 | 0.11 | 0.11 | 0.34 | 0.62 | 0.71 | 0.72 | high-confidence concern |
| [SettleTypeIIPeriodRange](commands/type-ii-observation-calendar.md#settletypeiiperiodrange) | TypeIIObservationCalendar | 0.11 | 0.37 | 0.12 | 0.34 | 0.56 | 0.64 | 0.73 | high-confidence concern |
| [AmendTypeIIPeriodClose](commands/type-ii-period.md#amendtypeiiperiodclose) | TypeIIPeriod | 0.38 | 0.50 | 0.26 | 0.67 | 0.77 | 0.18 | 0.66 | uncertain; review manually |
| [ApproveTypeIIPeriodClose](commands/type-ii-period.md#approvetypeiiperiodclose) | TypeIIPeriod | 0.88 | 0.88 | 0.33 | 0.74 | 0.85 | 0.09 | 0.39 | uncertain; review manually |
| [FreezeTypeIIPeriodClose](commands/type-ii-period.md#freezetypeiiperiodclose) | TypeIIPeriod | 0.53 | 0.45 | 0.25 | 0.61 | 0.79 | 0.42 | 0.61 | uncertain; review manually |
| [GenerateDueControlOccurrences](commands/type-ii-period.md#generateduecontroloccurrences) | TypeIIPeriod | 0.16 | 0.41 | 0.18 | 0.47 | 0.61 | 0.43 | 0.67 | high-confidence concern |
| [StartTypeIIObservationPeriod](commands/type-ii-period.md#starttypeiiobservationperiod) | TypeIIPeriod | 0.77 | 0.76 | 0.33 | 0.72 | 0.83 | 0.10 | 0.38 | uncertain; review manually |
| [AmendTypeIIObservationPeriod](commands/type-ii-period.md#amendtypeiiobservationperiod) | TypeIIPeriod | 0.73 | 0.72 | 0.32 | 0.77 | 0.78 | 0.13 | 0.53 | uncertain; review manually |
| [CancelTypeIIPeriodAmendment](commands/type-ii-period.md#canceltypeiiperiodamendment) | TypeIIPeriod | 0.79 | 0.76 | 0.51 | 0.83 | 0.86 | 0.14 | 0.37 | uncertain; review manually |
| [RejectTypeIIPeriodOpening](commands/type-ii-period.md#rejecttypeiiperiodopening) | TypeIIPeriod | 0.48 | 0.38 | 0.29 | 0.57 | 0.78 | 0.22 | 0.62 | uncertain; review manually |
| [RejectTypeIIPeriodAmendment](commands/type-ii-period.md#rejecttypeiiperiodamendment) | TypeIIPeriod | 0.44 | 0.46 | 0.41 | 0.68 | 0.79 | 0.20 | 0.59 | uncertain; review manually |
| [ApplyTypeIIPeriodRange](commands/type-ii-period.md#applytypeiiperiodrange) | TypeIIPeriod | 0.09 | 0.10 | 0.16 | 0.40 | 0.71 | 0.42 | 0.61 | high-confidence concern |
| [ApproveTypeIIOperatingPlan](commands/type-ii-plan.md#approvetypeiioperatingplan) | TypeIIPlan | 0.52 | 0.47 | 0.29 | 0.40 | 0.81 | 0.16 | 0.52 | uncertain; review manually |
| [ProposeTypeIIOperatingPlan](commands/type-ii-plan.md#proposetypeiioperatingplan) | TypeIIPlan | 0.47 | 0.48 | 0.27 | 0.43 | 0.82 | 0.16 | 0.51 | uncertain; review manually |
| [ReviewTypeIIOperatingPlan](commands/type-ii-plan.md#reviewtypeiioperatingplan) | TypeIIPlan | 0.79 | 0.78 | 0.39 | 0.56 | 0.82 | 0.12 | 0.47 | uncertain; review manually |
| [ContinueWithOidcProvider](commands/user-identity.md#continuewithoidcprovider) | UserIdentity | 0.10 | 0.31 | 0.12 | 0.48 | 0.48 | 0.46 | 0.69 | high-confidence concern |
| [ContinueWithDeveloperIdentity](commands/user-identity.md#continuewithdeveloperidentity) | UserIdentity | 0.07 | 0.38 | 0.17 | 0.38 | 0.52 | 0.50 | 0.73 | high-confidence concern |
| [LinkOidcProviderIdentity](commands/user-identity.md#linkoidcprovideridentity) | UserIdentity | 0.36 | 0.48 | 0.21 | 0.66 | 0.62 | 0.28 | 0.64 | uncertain; review manually |
| [ProtectRecoveryIdentity](commands/user-identity.md#protectrecoveryidentity) | UserIdentity | 0.09 | 0.31 | 0.28 | 0.43 | 0.57 | 0.39 | 0.74 | uncertain; review manually |
| [SettleRecoveryIdentityProtection](commands/user-identity.md#settlerecoveryidentityprotection) | UserIdentity | 0.05 | 0.42 | 0.13 | 0.29 | 0.49 | 0.59 | 0.74 | high-confidence concern |
| [RegisterRecoveredIdentity](commands/user-identity.md#registerrecoveredidentity) | UserIdentity | 0.39 | 0.49 | 0.36 | 0.57 | 0.72 | 0.27 | 0.60 | uncertain; review manually |
| [RevokeRecoveredOldIdentity](commands/user-identity.md#revokerecoveredoldidentity) | UserIdentity | 0.48 | 0.38 | 0.35 | 0.63 | 0.77 | 0.23 | 0.52 | uncertain; review manually |
| [SetWorkDigestPreference](commands/work-digest-preference.md#setworkdigestpreference) | WorkDigestPreference | 0.57 | 0.43 | 0.38 | 0.74 | 0.84 | 0.20 | 0.34 | uncertain; review manually |
| [AssignWorkItem](commands/work-item-assignment.md#assignworkitem) | WorkItemAssignment | 0.87 | 0.86 | 0.81 | 0.85 | 0.76 | 0.09 | 0.53 | uncertain; review manually |
| [ClaimWorkItem](commands/work-item-assignment.md#claimworkitem) | WorkItemAssignment | 0.71 | 0.70 | 0.64 | 0.71 | 0.73 | 0.14 | 0.62 | uncertain; review manually |
| [DelegateWorkItem](commands/work-item-assignment.md#delegateworkitem) | WorkItemAssignment | 0.81 | 0.80 | 0.73 | 0.80 | 0.69 | 0.09 | 0.62 | uncertain; review manually |
| [EscalateWorkItem](commands/work-item-assignment.md#escalateworkitem) | WorkItemAssignment | 0.87 | 0.87 | 0.75 | 0.84 | 0.85 | 0.08 | 0.55 | uncertain; review manually |
| [RecordWorkRelationship](commands/work-relationship.md#recordworkrelationship) | WorkRelationship | 0.45 | 0.28 | 0.63 | 0.79 | 0.79 | 0.19 | 0.58 | uncertain; review manually |
| [ReviseWorkRelationship](commands/work-relationship.md#reviseworkrelationship) | WorkRelationship | 0.74 | 0.72 | 0.57 | 0.80 | 0.82 | 0.18 | 0.45 | uncertain; review manually |
| [ResolveWorkforceObservation](commands/workforce-observation-resolution.md#resolveworkforceobservation) | WorkforceObservationResolution | 0.41 | 0.50 | 0.39 | 0.70 | 0.80 | 0.17 | 0.47 | uncertain; review manually |
| [ReconcileWorkforceSourceObservation](commands/workforce-source-observation.md#reconcileworkforcesourceobservation) | WorkforceSourceObservation | 0.24 | 0.54 | 0.23 | 0.53 | 0.73 | 0.32 | 0.68 | uncertain; review manually |
| [RecordWorkforceSourceObservation](commands/workforce-source-observation.md#recordworkforcesourceobservation) | WorkforceSourceObservation | 0.30 | 0.32 | 0.32 | 0.64 | 0.75 | 0.35 | 0.60 | uncertain; review manually |

## Event names

| Name | Owner | P(clear) | Score confidence | Business user | Auditor | Engineering | Jargon risk | Ambiguity risk | Jev result |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| [AccessExpectationApproved](events/access-expectation.md#accessexpectationapproved) | AccessExpectation | 0.67 | 0.65 | 0.60 | 0.80 | 0.86 | 0.09 | 0.45 | uncertain; review manually |
| [AccessExpectationExceptionRecorded](events/access-expectation.md#accessexpectationexceptionrecorded) | AccessExpectation | 0.19 | 0.60 | 0.39 | 0.68 | 0.79 | 0.32 | 0.63 | uncertain; review manually |
| [AccessExpectationProposed](events/access-expectation.md#accessexpectationproposed) | AccessExpectation | 0.70 | 0.68 | 0.52 | 0.75 | 0.87 | 0.10 | 0.42 | uncertain; review manually |
| [AccessGrantIssued](events/access-grant.md#accessgrantissued) | AccessGrant | 0.86 | 0.86 | 0.80 | 0.89 | 0.87 | 0.09 | 0.35 | uncertain; review manually |
| [AccessGrantRevoked](events/access-grant.md#accessgrantrevoked) | AccessGrant | 0.95 | 0.95 | 0.86 | 0.93 | 0.88 | 0.07 | 0.28 | uncertain; review manually |
| [AccessPopulationExceptionRecorded](events/access-population-exception.md#accesspopulationexceptionrecorded) | AccessPopulationException | 0.19 | 0.67 | 0.31 | 0.67 | 0.80 | 0.27 | 0.58 | uncertain; review manually |
| [AccessPopulationAccepted](events/access-population.md#accesspopulationaccepted) | AccessPopulation | 0.59 | 0.57 | 0.31 | 0.65 | 0.84 | 0.17 | 0.46 | uncertain; review manually |
| [AccessPopulationFactsRecorded](events/access-population.md#accesspopulationfactsrecorded) | AccessPopulation | 0.55 | 0.50 | 0.40 | 0.71 | 0.84 | 0.26 | 0.47 | uncertain; review manually |
| [AccessPopulationOpened](events/access-population.md#accesspopulationopened) | AccessPopulation | 0.48 | 0.36 | 0.27 | 0.55 | 0.85 | 0.29 | 0.51 | uncertain; review manually |
| [AccessPrincipalClassified](events/access-principal-classification.md#accessprincipalclassified) | AccessPrincipalClassification | 0.54 | 0.51 | 0.42 | 0.73 | 0.82 | 0.13 | 0.49 | uncertain; review manually |
| [AccessReviewCampaignCompleted](events/access-review-campaign.md#accessreviewcampaigncompleted) | AccessReviewCampaign | 0.84 | 0.83 | 0.80 | 0.82 | 0.86 | 0.12 | 0.44 | uncertain; review manually |
| [AccessReviewCampaignCompletionAccepted](events/access-review-campaign.md#accessreviewcampaigncompletionaccepted) | AccessReviewCampaign | 0.26 | 0.39 | 0.48 | 0.55 | 0.80 | 0.54 | 0.55 | uncertain; review manually |
| [AccessReviewCampaignCompletionCanceled](events/access-review-campaign.md#accessreviewcampaigncompletioncanceled) | AccessReviewCampaign | 0.40 | 0.49 | 0.64 | 0.76 | 0.79 | 0.22 | 0.60 | uncertain; review manually |
| [AccessReviewCampaignLaunched](events/access-review-campaign.md#accessreviewcampaignlaunched) | AccessReviewCampaign | 0.77 | 0.77 | 0.80 | 0.89 | 0.90 | 0.10 | 0.32 | uncertain; review manually |
| [AccessReviewDecisionBatchAdmitted](events/access-review-campaign.md#accessreviewdecisionbatchadmitted) | AccessReviewCampaign | 0.41 | 0.54 | 0.30 | 0.63 | 0.84 | 0.31 | 0.49 | uncertain; review manually |
| [AccessReviewDecisionBatchAccepted](events/access-review-decision-batch.md#accessreviewdecisionbatchaccepted) | AccessReviewDecisionBatch | 0.33 | 0.49 | 0.31 | 0.53 | 0.82 | 0.44 | 0.56 | uncertain; review manually |
| [AccessReviewDecisionBatchCanceled](events/access-review-decision-batch.md#accessreviewdecisionbatchcanceled) | AccessReviewDecisionBatch | 0.63 | 0.62 | 0.58 | 0.79 | 0.80 | 0.12 | 0.56 | uncertain; review manually |
| [AccessReviewDecisionBatchCancellationAccepted](events/access-review-decision-batch.md#accessreviewdecisionbatchcancellationaccepted) | AccessReviewDecisionBatch | 0.21 | 0.53 | 0.41 | 0.64 | 0.80 | 0.40 | 0.54 | uncertain; review manually |
| [AccessReviewDecisionBatchPublished](events/access-review-decision-batch.md#accessreviewdecisionbatchpublished) | AccessReviewDecisionBatch | 0.85 | 0.85 | 0.53 | 0.78 | 0.90 | 0.14 | 0.35 | uncertain; review manually |
| [AccessDecisionsRecorded](events/access-review-item.md#accessdecisionsrecorded) | AccessReviewItem | 0.30 | 0.58 | 0.72 | 0.78 | 0.61 | 0.24 | 0.63 | uncertain; review manually |
| [AccessRemediationChangeRecorded](events/access-review-item.md#accessremediationchangerecorded) | AccessReviewItem | 0.22 | 0.60 | 0.61 | 0.78 | 0.79 | 0.19 | 0.61 | uncertain; review manually |
| [AccessRemediationExceptionRecorded](events/access-review-item.md#accessremediationexceptionrecorded) | AccessReviewItem | 0.46 | 0.49 | 0.47 | 0.76 | 0.82 | 0.19 | 0.63 | uncertain; review manually |
| [AccessRemediationVerified](events/access-review-item.md#accessremediationverified) | AccessReviewItem | 0.61 | 0.57 | 0.73 | 0.83 | 0.72 | 0.11 | 0.49 | uncertain; review manually |
| [AccessReviewItemCampaignCompletionUseAuthorized](events/access-review-item.md#accessreviewitemcampaigncompletionuseauthorized) | AccessReviewItem | 0.14 | 0.56 | 0.17 | 0.48 | 0.76 | 0.52 | 0.65 | high-confidence concern |
| [AccessReviewItemCampaignCompletionUseSettled](events/access-review-item.md#accessreviewitemcampaigncompletionusesettled) | AccessReviewItem | 0.14 | 0.71 | 0.18 | 0.52 | 0.79 | 0.41 | 0.54 | high-confidence concern |
| [AccessReviewItemDecisionAuthorized](events/access-review-item.md#accessreviewitemdecisionauthorized) | AccessReviewItem | 0.28 | 0.60 | 0.32 | 0.51 | 0.79 | 0.35 | 0.63 | uncertain; review manually |
| [AccessReviewItemDecisionCanceled](events/access-review-item.md#accessreviewitemdecisioncanceled) | AccessReviewItem | 0.29 | 0.62 | 0.56 | 0.67 | 0.76 | 0.21 | 0.73 | uncertain; review manually |
| [AccessReviewItemDecisionSettled](events/access-review-item.md#accessreviewitemdecisionsettled) | AccessReviewItem | 0.20 | 0.71 | 0.43 | 0.65 | 0.84 | 0.17 | 0.57 | uncertain; review manually |
| [AccessReviewItemOpened](events/access-review-item.md#accessreviewitemopened) | AccessReviewItem | 0.42 | 0.47 | 0.51 | 0.60 | 0.79 | 0.26 | 0.55 | uncertain; review manually |
| [AccessReviewResponsibilityReassigned](events/access-review-item.md#accessreviewresponsibilityreassigned) | AccessReviewItem | 0.38 | 0.57 | 0.82 | 0.87 | 0.72 | 0.08 | 0.55 | uncertain; review manually |
| [AdvisorFeedbackCorrected](events/advisor-feedback-thread.md#advisorfeedbackcorrected) | AdvisorFeedbackThread | 0.76 | 0.74 | 0.78 | 0.90 | 0.84 | 0.08 | 0.47 | uncertain; review manually |
| [AdvisorFeedbackRecorded](events/advisor-feedback-thread.md#advisorfeedbackrecorded) | AdvisorFeedbackThread | 0.61 | 0.55 | 0.78 | 0.82 | 0.79 | 0.36 | 0.52 | uncertain; review manually |
| [AdvisorFeedbackResolved](events/advisor-feedback-thread.md#advisorfeedbackresolved) | AdvisorFeedbackThread | 0.79 | 0.78 | 0.85 | 0.90 | 0.78 | 0.06 | 0.58 | uncertain; review manually |
| [AdvisorFeedbackResponded](events/advisor-feedback-thread.md#advisorfeedbackresponded) | AdvisorFeedbackThread | 0.81 | 0.80 | 0.84 | 0.91 | 0.78 | 0.07 | 0.49 | uncertain; review manually |
| [AdvisorArtifactDownloadUseAuthorized](events/advisor-handoff.md#advisorartifactdownloaduseauthorized) | AdvisorHandoff | 0.18 | 0.42 | 0.34 | 0.61 | 0.62 | 0.25 | 0.69 | uncertain; review manually |
| [AdvisorArtifactDownloadUseSettled](events/advisor-handoff.md#advisorartifactdownloadusesettled) | AdvisorHandoff | 0.22 | 0.68 | 0.29 | 0.58 | 0.73 | 0.20 | 0.63 | uncertain; review manually |
| [AdvisorFeedbackAdmitted](events/advisor-handoff.md#advisorfeedbackadmitted) | AdvisorHandoff | 0.47 | 0.40 | 0.48 | 0.79 | 0.84 | 0.16 | 0.38 | uncertain; review manually |
| [AdvisorHandoffClosed](events/advisor-handoff.md#advisorhandoffclosed) | AdvisorHandoff | 0.77 | 0.77 | 0.71 | 0.83 | 0.87 | 0.09 | 0.35 | uncertain; review manually |
| [AdvisorHandoffCreated](events/advisor-handoff.md#advisorhandoffcreated) | AdvisorHandoff | 0.82 | 0.82 | 0.63 | 0.79 | 0.89 | 0.12 | 0.30 | uncertain; review manually |
| [AdvisorHandoffReadiedForReview](events/advisor-handoff.md#advisorhandoffreadiedforreview) | AdvisorHandoff | 0.62 | 0.55 | 0.64 | 0.80 | 0.82 | 0.16 | 0.40 | uncertain; review manually |
| [AdvisorHandoffRevoked](events/advisor-handoff.md#advisorhandoffrevoked) | AdvisorHandoff | 0.72 | 0.71 | 0.68 | 0.80 | 0.86 | 0.11 | 0.43 | uncertain; review manually |
| [AdvisorHandoffSelectionRevised](events/advisor-handoff.md#advisorhandoffselectionrevised) | AdvisorHandoff | 0.23 | 0.62 | 0.31 | 0.65 | 0.83 | 0.33 | 0.46 | uncertain; review manually |
| [ApplicationImportCanceled](events/application-import-batch.md#applicationimportcanceled) | ApplicationImportBatch | 0.70 | 0.69 | 0.79 | 0.84 | 0.75 | 0.16 | 0.50 | uncertain; review manually |
| [ApplicationImportCancellationAccepted](events/application-import-batch.md#applicationimportcancellationaccepted) | ApplicationImportBatch | 0.24 | 0.41 | 0.55 | 0.70 | 0.79 | 0.41 | 0.47 | uncertain; review manually |
| [ApplicationImportCommitted](events/application-import-batch.md#applicationimportcommitted) | ApplicationImportBatch | 0.73 | 0.72 | 0.48 | 0.80 | 0.85 | 0.21 | 0.35 | uncertain; review manually |
| [ApplicationImportExecutionAuthorized](events/application-import-batch.md#applicationimportexecutionauthorized) | ApplicationImportBatch | 0.27 | 0.52 | 0.49 | 0.64 | 0.65 | 0.32 | 0.59 | uncertain; review manually |
| [ApplicationImportFailed](events/application-import-batch.md#applicationimportfailed) | ApplicationImportBatch | 0.78 | 0.77 | 0.85 | 0.88 | 0.78 | 0.13 | 0.44 | uncertain; review manually |
| [ApplicationImportPlanSealed](events/application-import-batch.md#applicationimportplansealed) | ApplicationImportBatch | 0.58 | 0.56 | 0.42 | 0.70 | 0.84 | 0.21 | 0.37 | uncertain; review manually |
| [ApplicationImportRetirementProposalSealed](events/application-import-batch.md#applicationimportretirementproposalsealed) | ApplicationImportBatch | 0.30 | 0.60 | 0.25 | 0.58 | 0.73 | 0.29 | 0.58 | uncertain; review manually |
| [ApplicationImportRowCorrelated](events/application-import-batch.md#applicationimportrowcorrelated) | ApplicationImportBatch | 0.04 | 0.65 | 0.15 | 0.46 | 0.65 | 0.41 | 0.69 | high-confidence concern |
| [ApplicationImportStaged](events/application-import-batch.md#applicationimportstaged) | ApplicationImportBatch | 0.18 | 0.67 | 0.28 | 0.60 | 0.74 | 0.38 | 0.60 | uncertain; review manually |
| [ApplicationImportSourceAdmissionRecorded](events/application-import-source.md#applicationimportsourceadmissionrecorded) | ApplicationImportSource | 0.26 | 0.56 | 0.33 | 0.64 | 0.79 | 0.36 | 0.54 | uncertain; review manually |
| [ApplicationImportSourceSettled](events/application-import-source.md#applicationimportsourcesettled) | ApplicationImportSource | 0.15 | 0.72 | 0.24 | 0.52 | 0.76 | 0.19 | 0.66 | uncertain; review manually |
| [ApplicationNameClaimed](events/application-name-claim.md#applicationnameclaimed) | ApplicationNameClaim | 0.69 | 0.68 | 0.58 | 0.79 | 0.80 | 0.12 | 0.47 | uncertain; review manually |
| [ApplicationNameReleased](events/application-name-claim.md#applicationnamereleased) | ApplicationNameClaim | 0.78 | 0.77 | 0.70 | 0.84 | 0.80 | 0.09 | 0.46 | uncertain; review manually |
| [ApplicationRelationshipAdded](events/application-relationship.md#applicationrelationshipadded) | ApplicationRelationship | 0.69 | 0.68 | 0.69 | 0.83 | 0.83 | 0.11 | 0.40 | uncertain; review manually |
| [ApplicationRelationshipApproved](events/application-relationship.md#applicationrelationshipapproved) | ApplicationRelationship | 0.77 | 0.76 | 0.68 | 0.81 | 0.86 | 0.08 | 0.37 | uncertain; review manually |
| [ApplicationRelationshipRemoved](events/application-relationship.md#applicationrelationshipremoved) | ApplicationRelationship | 0.71 | 0.67 | 0.55 | 0.81 | 0.85 | 0.12 | 0.37 | uncertain; review manually |
| [ArtifactDisclosureApproved](events/artifact-disclosure.md#artifactdisclosureapproved) | ArtifactDisclosure | 0.39 | 0.55 | 0.55 | 0.70 | 0.74 | 0.10 | 0.69 | uncertain; review manually |
| [ArtifactDisclosureDelivered](events/artifact-disclosure.md#artifactdisclosuredelivered) | ArtifactDisclosure | 0.49 | 0.45 | 0.63 | 0.72 | 0.73 | 0.13 | 0.66 | uncertain; review manually |
| [ArtifactDisclosureRevoked](events/artifact-disclosure.md#artifactdisclosurerevoked) | ArtifactDisclosure | 0.54 | 0.50 | 0.60 | 0.75 | 0.76 | 0.08 | 0.64 | uncertain; review manually |
| [ArtifactDispositionApproved](events/artifact-disposition.md#artifactdispositionapproved) | ArtifactDisposition | 0.39 | 0.57 | 0.45 | 0.68 | 0.78 | 0.10 | 0.64 | uncertain; review manually |
| [ArtifactDispositionExecutionRequested](events/artifact-disposition.md#artifactdispositionexecutionrequested) | ArtifactDisposition | 0.35 | 0.52 | 0.40 | 0.68 | 0.81 | 0.27 | 0.61 | uncertain; review manually |
| [ArtifactDispositionResultRecorded](events/artifact-disposition.md#artifactdispositionresultrecorded) | ArtifactDisposition | 0.28 | 0.59 | 0.47 | 0.72 | 0.78 | 0.33 | 0.60 | uncertain; review manually |
| [ArtifactDownloadGrantCanceled](events/artifact-download-grant.md#artifactdownloadgrantcanceled) | ArtifactDownloadGrant | 0.84 | 0.83 | 0.71 | 0.85 | 0.86 | 0.13 | 0.43 | uncertain; review manually |
| [EvidenceArtifactDownloadIssued](events/artifact-download-grant.md#evidenceartifactdownloadissued) | ArtifactDownloadGrant | 0.63 | 0.57 | 0.54 | 0.75 | 0.73 | 0.21 | 0.55 | uncertain; review manually |
| [ArtifactEngagementHoldPlaced](events/artifact-engagement-hold.md#artifactengagementholdplaced) | ArtifactEngagementHold | 0.55 | 0.48 | 0.37 | 0.62 | 0.81 | 0.14 | 0.65 | uncertain; review manually |
| [ArtifactEngagementHoldReleased](events/artifact-engagement-hold.md#artifactengagementholdreleased) | ArtifactEngagementHold | 0.54 | 0.47 | 0.44 | 0.67 | 0.82 | 0.12 | 0.58 | uncertain; review manually |
| [EngagementReportIssued](events/artifact-engagement-hold.md#engagementreportissued) | ArtifactEngagementHold | 0.54 | 0.51 | 0.75 | 0.71 | 0.51 | 0.09 | 0.68 | uncertain; review manually |
| [ArtifactLegalHoldPlaced](events/artifact-legal-hold.md#artifactlegalholdplaced) | ArtifactLegalHold | 0.90 | 0.90 | 0.71 | 0.89 | 0.89 | 0.07 | 0.30 | uncertain; review manually |
| [ArtifactLegalHoldReleased](events/artifact-legal-hold.md#artifactlegalholdreleased) | ArtifactLegalHold | 0.93 | 0.93 | 0.75 | 0.89 | 0.88 | 0.06 | 0.32 | uncertain; review manually |
| [ArtifactRetentionBasisRecorded](events/artifact-retention-basis.md#artifactretentionbasisrecorded) | ArtifactRetentionBasis | 0.52 | 0.50 | 0.42 | 0.78 | 0.84 | 0.18 | 0.43 | uncertain; review manually |
| [ArtifactRetentionBound](events/artifact-retention-basis.md#artifactretentionbound) | ArtifactRetentionBasis | 0.01 | 0.52 | 0.18 | 0.36 | 0.50 | 0.38 | 0.71 | high-confidence concern |
| [AuditObservationCorrected](events/audit-observation.md#auditobservationcorrected) | AuditObservation | 0.36 | 0.52 | 0.65 | 0.76 | 0.65 | 0.18 | 0.71 | uncertain; review manually |
| [AuditObservationFindingLinked](events/audit-observation.md#auditobservationfindinglinked) | AuditObservation | 0.54 | 0.52 | 0.53 | 0.76 | 0.81 | 0.19 | 0.54 | uncertain; review manually |
| [AuditObservationManagementResponseRecorded](events/audit-observation.md#auditobservationmanagementresponserecorded) | AuditObservation | 0.43 | 0.45 | 0.60 | 0.80 | 0.78 | 0.22 | 0.57 | uncertain; review manually |
| [AuditObservationRecorded](events/audit-observation.md#auditobservationrecorded) | AuditObservation | 0.55 | 0.54 | 0.80 | 0.83 | 0.76 | 0.17 | 0.62 | uncertain; review manually |
| [AuditOutcomeRecorded](events/audit-outcome.md#auditoutcomerecorded) | AuditOutcome | 0.62 | 0.61 | 0.84 | 0.85 | 0.82 | 0.15 | 0.54 | uncertain; review manually |
| [AuditPackageDelivered](events/audit-package-delivery.md#auditpackagedelivered) | AuditPackageDelivery | 0.60 | 0.59 | 0.82 | 0.81 | 0.74 | 0.14 | 0.66 | uncertain; review manually |
| [AuditPackageAmended](events/audit-package-manifest.md#auditpackageamended) | AuditPackageManifest | 0.48 | 0.48 | 0.68 | 0.81 | 0.78 | 0.15 | 0.59 | uncertain; review manually |
| [AuditPackageBuildRequested](events/audit-package-manifest.md#auditpackagebuildrequested) | AuditPackageManifest | 0.35 | 0.40 | 0.42 | 0.76 | 0.83 | 0.33 | 0.54 | uncertain; review manually |
| [AuditPackageBuilt](events/audit-package-manifest.md#auditpackagebuilt) | AuditPackageManifest | 0.76 | 0.76 | 0.56 | 0.79 | 0.83 | 0.30 | 0.45 | uncertain; review manually |
| [AuditPackageCreated](events/audit-package-manifest.md#auditpackagecreated) | AuditPackageManifest | 0.73 | 0.73 | 0.82 | 0.85 | 0.82 | 0.10 | 0.43 | uncertain; review manually |
| [AuditPackageDeliveryAdmitted](events/audit-package-manifest.md#auditpackagedeliveryadmitted) | AuditPackageManifest | 0.14 | 0.54 | 0.24 | 0.49 | 0.60 | 0.56 | 0.69 | uncertain; review manually |
| [AuditPackageDisclosureApproved](events/audit-package-manifest.md#auditpackagedisclosureapproved) | AuditPackageManifest | 0.87 | 0.87 | 0.76 | 0.84 | 0.85 | 0.09 | 0.46 | uncertain; review manually |
| [AuditPackageValidated](events/audit-package-manifest.md#auditpackagevalidated) | AuditPackageManifest | 0.56 | 0.56 | 0.70 | 0.82 | 0.78 | 0.13 | 0.59 | uncertain; review manually |
| [AuditPackageValidationOverrideApproved](events/audit-package-manifest.md#auditpackagevalidationoverrideapproved) | AuditPackageManifest | 0.68 | 0.66 | 0.59 | 0.81 | 0.82 | 0.14 | 0.47 | uncertain; review manually |
| [AuditPopulationAmended](events/audit-population.md#auditpopulationamended) | AuditPopulation | 0.04 | 0.47 | 0.44 | 0.62 | 0.61 | 0.19 | 0.77 | uncertain; review manually |
| [AuditPopulationDefined](events/audit-population.md#auditpopulationdefined) | AuditPopulation | 0.76 | 0.75 | 0.47 | 0.75 | 0.80 | 0.12 | 0.50 | uncertain; review manually |
| [AuditPopulationDefinitionRevised](events/audit-population.md#auditpopulationdefinitionrevised) | AuditPopulation | 0.26 | 0.58 | 0.62 | 0.76 | 0.78 | 0.16 | 0.62 | uncertain; review manually |
| [AuditPopulationFactRecorded](events/audit-population.md#auditpopulationfactrecorded) | AuditPopulation | 0.15 | 0.68 | 0.53 | 0.64 | 0.67 | 0.25 | 0.72 | uncertain; review manually |
| [AuditPopulationFrozen](events/audit-population.md#auditpopulationfrozen) | AuditPopulation | 0.14 | 0.56 | 0.27 | 0.47 | 0.59 | 0.37 | 0.76 | uncertain; review manually |
| [AuditPopulationGenerated](events/audit-population.md#auditpopulationgenerated) | AuditPopulation | 0.12 | 0.59 | 0.32 | 0.59 | 0.62 | 0.33 | 0.74 | uncertain; review manually |
| [AuditPopulationReconciled](events/audit-population.md#auditpopulationreconciled) | AuditPopulation | 0.08 | 0.67 | 0.31 | 0.50 | 0.53 | 0.30 | 0.77 | uncertain; review manually |
| [AuditPopulationRowClassified](events/audit-population.md#auditpopulationrowclassified) | AuditPopulation | 0.23 | 0.61 | 0.46 | 0.62 | 0.73 | 0.26 | 0.65 | uncertain; review manually |
| [AuditorRequestCommentRecorded](events/auditor-feedback-thread.md#auditorrequestcommentrecorded) | AuditorFeedbackThread | 0.40 | 0.49 | 0.78 | 0.69 | 0.45 | 0.29 | 0.72 | uncertain; review manually |
| [AuditorCommentAdmitted](events/auditor-request.md#auditorcommentadmitted) | AuditorRequest | 0.67 | 0.66 | 0.53 | 0.76 | 0.82 | 0.18 | 0.49 | uncertain; review manually |
| [AuditorRequestAssigned](events/auditor-request.md#auditorrequestassigned) | AuditorRequest | 0.92 | 0.92 | 0.87 | 0.89 | 0.86 | 0.07 | 0.37 | uncertain; review manually |
| [AuditorRequestRecorded](events/auditor-request.md#auditorrequestrecorded) | AuditorRequest | 0.76 | 0.74 | 0.78 | 0.84 | 0.83 | 0.24 | 0.50 | uncertain; review manually |
| [AuditorRequestReopened](events/auditor-request.md#auditorrequestreopened) | AuditorRequest | 0.90 | 0.90 | 0.78 | 0.88 | 0.88 | 0.09 | 0.40 | uncertain; review manually |
| [AuditorRequestResolutionAccepted](events/auditor-request.md#auditorrequestresolutionaccepted) | AuditorRequest | 0.51 | 0.48 | 0.51 | 0.70 | 0.81 | 0.20 | 0.66 | uncertain; review manually |
| [AuditorRequestResolutionCanceled](events/auditor-request.md#auditorrequestresolutioncanceled) | AuditorRequest | 0.62 | 0.59 | 0.74 | 0.78 | 0.81 | 0.21 | 0.61 | uncertain; review manually |
| [AuditorRequestResolved](events/auditor-request.md#auditorrequestresolved) | AuditorRequest | 0.79 | 0.79 | 0.82 | 0.85 | 0.86 | 0.08 | 0.38 | uncertain; review manually |
| [AuditorRequestRevised](events/auditor-request.md#auditorrequestrevised) | AuditorRequest | 0.60 | 0.55 | 0.81 | 0.90 | 0.88 | 0.07 | 0.38 | uncertain; review manually |
| [AuditorResponseAdmitted](events/auditor-request.md#auditorresponseadmitted) | AuditorRequest | 0.50 | 0.46 | 0.48 | 0.68 | 0.71 | 0.19 | 0.59 | uncertain; review manually |
| [AuditorSampleSelectionAdmitted](events/auditor-request.md#auditorsampleselectionadmitted) | AuditorRequest | 0.64 | 0.62 | 0.48 | 0.76 | 0.88 | 0.25 | 0.33 | uncertain; review manually |
| [AuditResponseDraftCreated](events/auditor-response.md#auditresponsedraftcreated) | AuditorResponse | 0.58 | 0.45 | 0.68 | 0.80 | 0.79 | 0.20 | 0.36 | uncertain; review manually |
| [AuditResponseDraftRevised](events/auditor-response.md#auditresponsedraftrevised) | AuditorResponse | 0.74 | 0.71 | 0.67 | 0.84 | 0.81 | 0.14 | 0.43 | uncertain; review manually |
| [AuditorResponseDelivered](events/auditor-response.md#auditorresponsedelivered) | AuditorResponse | 0.89 | 0.89 | 0.85 | 0.89 | 0.85 | 0.09 | 0.53 | uncertain; review manually |
| [AuditorResponseDisclosureReviewed](events/auditor-response.md#auditorresponsedisclosurereviewed) | AuditorResponse | 0.60 | 0.59 | 0.68 | 0.86 | 0.84 | 0.08 | 0.49 | uncertain; review manually |
| [AuditorResponseDispositionRecorded](events/auditor-response.md#auditorresponsedispositionrecorded) | AuditorResponse | 0.35 | 0.58 | 0.47 | 0.82 | 0.84 | 0.15 | 0.54 | uncertain; review manually |
| [AuditorResponseRequestResolutionUseAuthorized](events/auditor-response.md#auditorresponserequestresolutionuseauthorized) | AuditorResponse | 0.18 | 0.50 | 0.15 | 0.44 | 0.69 | 0.56 | 0.68 | high-confidence concern |
| [AuditorResponseRequestResolutionUseSettled](events/auditor-response.md#auditorresponserequestresolutionusesettled) | AuditorResponse | 0.11 | 0.59 | 0.14 | 0.44 | 0.74 | 0.54 | 0.59 | high-confidence concern |
| [AuditorResponseSubmitted](events/auditor-response.md#auditorresponsesubmitted) | AuditorResponse | 0.89 | 0.89 | 0.86 | 0.87 | 0.87 | 0.08 | 0.45 | uncertain; review manually |
| [SampleSelectionRecorded](events/auditor-sample-selection.md#sampleselectionrecorded) | AuditorSampleSelection | 0.38 | 0.55 | 0.69 | 0.76 | 0.67 | 0.46 | 0.57 | uncertain; review manually |
| [ClientExportAttestPackageDeliveryRecorded](events/client-export.md#clientexportattestpackagedeliveryrecorded) | ClientExport | 0.36 | 0.46 | 0.42 | 0.73 | 0.75 | 0.30 | 0.64 | uncertain; review manually |
| [ClientExportBuildFailed](events/client-export.md#clientexportbuildfailed) | ClientExport | 0.65 | 0.57 | 0.60 | 0.77 | 0.83 | 0.37 | 0.41 | uncertain; review manually |
| [ClientExportBuilt](events/client-export.md#clientexportbuilt) | ClientExport | 0.56 | 0.51 | 0.49 | 0.67 | 0.83 | 0.30 | 0.56 | uncertain; review manually |
| [ClientExportDelivered](events/client-export.md#clientexportdelivered) | ClientExport | 0.43 | 0.55 | 0.69 | 0.72 | 0.65 | 0.17 | 0.72 | uncertain; review manually |
| [ClientExportRequested](events/client-export.md#clientexportrequested) | ClientExport | 0.77 | 0.76 | 0.73 | 0.81 | 0.84 | 0.11 | 0.56 | uncertain; review manually |
| [ClientIndependenceEvaluated](events/client-independence-decision.md#clientindependenceevaluated) | ClientIndependenceDecision | 0.13 | 0.71 | 0.50 | 0.60 | 0.62 | 0.25 | 0.71 | uncertain; review manually |
| [ClientIndependenceEvaluationAdmitted](events/client-service-independence.md#clientindependenceevaluationadmitted) | ClientServiceIndependence | 0.34 | 0.57 | 0.37 | 0.63 | 0.79 | 0.23 | 0.59 | uncertain; review manually |
| [ClientServicePersonalDecisionUseAuthorized](events/client-service-independence.md#clientservicepersonaldecisionuseauthorized) | ClientServiceIndependence | 0.26 | 0.59 | 0.25 | 0.53 | 0.71 | 0.24 | 0.68 | uncertain; review manually |
| [ClientServicePersonalDecisionUseSettled](events/client-service-independence.md#clientservicepersonaldecisionusesettled) | ClientServiceIndependence | 0.28 | 0.62 | 0.21 | 0.52 | 0.75 | 0.22 | 0.61 | uncertain; review manually |
| [EngagementServiceEpochAuthorized](events/client-service-independence.md#engagementserviceepochauthorized) | ClientServiceIndependence | 0.22 | 0.68 | 0.17 | 0.50 | 0.78 | 0.32 | 0.61 | high-confidence concern |
| [EngagementServiceEpochSettled](events/client-service-independence.md#engagementserviceepochsettled) | ClientServiceIndependence | 0.37 | 0.54 | 0.16 | 0.46 | 0.77 | 0.35 | 0.58 | high-confidence concern |
| [NonattestServiceRecorded](events/client-service-independence.md#nonattestservicerecorded) | ClientServiceIndependence | 0.76 | 0.74 | 0.50 | 0.80 | 0.76 | 0.14 | 0.43 | uncertain; review manually |
| [ClientServiceCreated](events/client-service.md#clientservicecreated) | ClientService | 0.94 | 0.94 | 0.78 | 0.84 | 0.87 | 0.09 | 0.37 | uncertain; review manually |
| [ClientServiceRetired](events/client-service.md#clientserviceretired) | ClientService | 0.67 | 0.65 | 0.70 | 0.83 | 0.86 | 0.09 | 0.50 | uncertain; review manually |
| [ClientServiceRevised](events/client-service.md#clientservicerevised) | ClientService | 0.60 | 0.54 | 0.62 | 0.83 | 0.84 | 0.11 | 0.46 | uncertain; review manually |
| [CommitmentControlLinkProposed](events/commitment-control-link.md#commitmentcontrollinkproposed) | CommitmentControlLink | 0.72 | 0.70 | 0.46 | 0.81 | 0.84 | 0.13 | 0.45 | uncertain; review manually |
| [CommitmentControlLinkRetired](events/commitment-control-link.md#commitmentcontrollinkretired) | CommitmentControlLink | 0.66 | 0.64 | 0.48 | 0.81 | 0.84 | 0.14 | 0.48 | uncertain; review manually |
| [CommitmentControlLinkReviewed](events/commitment-control-link.md#commitmentcontrollinkreviewed) | CommitmentControlLink | 0.17 | 0.68 | 0.27 | 0.57 | 0.74 | 0.21 | 0.62 | uncertain; review manually |
| [CommitmentApproved](events/commitment-draft.md#commitmentapproved) | CommitmentDraft | 0.62 | 0.62 | 0.81 | 0.86 | 0.72 | 0.06 | 0.51 | uncertain; review manually |
| [CommitmentDraftCreated](events/commitment-draft.md#commitmentdraftcreated) | CommitmentDraft | 0.89 | 0.88 | 0.71 | 0.86 | 0.88 | 0.15 | 0.28 | uncertain; review manually |
| [CommitmentDraftRevised](events/commitment-draft.md#commitmentdraftrevised) | CommitmentDraft | 0.74 | 0.72 | 0.66 | 0.84 | 0.87 | 0.13 | 0.31 | uncertain; review manually |
| [CommitmentRetired](events/commitment-draft.md#commitmentretired) | CommitmentDraft | 0.59 | 0.56 | 0.60 | 0.84 | 0.80 | 0.10 | 0.50 | uncertain; review manually |
| [CommitmentReviewed](events/commitment-draft.md#commitmentreviewed) | CommitmentDraft | 0.27 | 0.72 | 0.67 | 0.82 | 0.69 | 0.06 | 0.59 | uncertain; review manually |
| [ResponsibilityAssigned](events/commitment-draft.md#responsibilityassigned) | ControlDraft, CommitmentDraft, SystemBoundary | 0.64 | 0.64 | 0.78 | 0.83 | 0.55 | 0.07 | 0.57 | uncertain; review manually |
| [ResponsibilityRevoked](events/commitment-draft.md#responsibilityrevoked) | ControlDraft, CommitmentDraft, SystemBoundary | 0.73 | 0.73 | 0.75 | 0.86 | 0.62 | 0.08 | 0.46 | uncertain; review manually |
| [ComplianceIncidentImpactAssessed](events/compliance-incident.md#complianceincidentimpactassessed) | ComplianceIncident | 0.87 | 0.86 | 0.80 | 0.90 | 0.86 | 0.08 | 0.32 | uncertain; review manually |
| [ComplianceIncidentRecorded](events/compliance-incident.md#complianceincidentrecorded) | ComplianceIncident | 0.79 | 0.78 | 0.86 | 0.90 | 0.85 | 0.16 | 0.41 | uncertain; review manually |
| [ComplianceIncidentRevised](events/compliance-incident.md#complianceincidentrevised) | ComplianceIncident | 0.59 | 0.49 | 0.71 | 0.88 | 0.84 | 0.11 | 0.45 | uncertain; review manually |
| [ComplianceIncidentSystemDescriptionDecisionRecorded](events/compliance-incident.md#complianceincidentsystemdescriptiondecisionrecorded) | ComplianceIncident | 0.49 | 0.39 | 0.38 | 0.76 | 0.76 | 0.21 | 0.51 | uncertain; review manually |
| [IncidentResolutionRecorded](events/compliance-incident.md#incidentresolutionrecorded) | ComplianceIncident | 0.80 | 0.78 | 0.82 | 0.90 | 0.80 | 0.18 | 0.38 | uncertain; review manually |
| [ProgramCreated](events/compliance-program.md#programcreated) | ComplianceProgram | 0.93 | 0.93 | 0.86 | 0.89 | 0.87 | 0.07 | 0.31 | uncertain; review manually |
| [ProgramCriteriaEditionSelected](events/compliance-program.md#programcriteriaeditionselected) | ComplianceProgram | 0.70 | 0.67 | 0.44 | 0.81 | 0.86 | 0.10 | 0.35 | uncertain; review manually |
| [ProgramRevised](events/compliance-program.md#programrevised) | ComplianceProgram | 0.38 | 0.43 | 0.65 | 0.83 | 0.80 | 0.09 | 0.55 | uncertain; review manually |
| [ControlCriterionMappingProposed](events/control-criterion-mapping.md#controlcriterionmappingproposed) | ControlCriterionMapping | 0.64 | 0.62 | 0.31 | 0.79 | 0.87 | 0.13 | 0.33 | uncertain; review manually |
| [ControlCriterionMappingRetired](events/control-criterion-mapping.md#controlcriterionmappingretired) | ControlCriterionMapping | 0.61 | 0.60 | 0.33 | 0.80 | 0.87 | 0.11 | 0.40 | uncertain; review manually |
| [ControlCriterionMappingReviewed](events/control-criterion-mapping.md#controlcriterionmappingreviewed) | ControlCriterionMapping | 0.41 | 0.54 | 0.42 | 0.81 | 0.83 | 0.13 | 0.44 | uncertain; review manually |
| [ControlApproved](events/control-draft.md#controlapproved) | ControlDraft | 0.36 | 0.61 | 0.80 | 0.82 | 0.57 | 0.09 | 0.60 | uncertain; review manually |
| [ControlDraftCreated](events/control-draft.md#controldraftcreated) | ControlDraft | 0.91 | 0.90 | 0.76 | 0.86 | 0.88 | 0.12 | 0.27 | uncertain; review manually |
| [ControlDraftDiscarded](events/control-draft.md#controldraftdiscarded) | ControlDraft | 0.87 | 0.86 | 0.72 | 0.86 | 0.85 | 0.12 | 0.37 | uncertain; review manually |
| [ControlDraftRevised](events/control-draft.md#controldraftrevised) | ControlDraft | 0.79 | 0.77 | 0.68 | 0.87 | 0.86 | 0.11 | 0.35 | uncertain; review manually |
| [ControlOwnerPersonDesignated](events/control-draft.md#controlownerpersondesignated) | ControlDraft | 0.38 | 0.48 | 0.43 | 0.76 | 0.73 | 0.21 | 0.51 | uncertain; review manually |
| [ControlProposalWithdrawn](events/control-draft.md#controlproposalwithdrawn) | ControlDraft | 0.55 | 0.51 | 0.66 | 0.76 | 0.72 | 0.15 | 0.57 | uncertain; review manually |
| [ControlRetired](events/control-draft.md#controlretired) | ControlDraft | 0.60 | 0.59 | 0.64 | 0.83 | 0.78 | 0.08 | 0.50 | uncertain; review manually |
| [ControlRetirementProposed](events/control-draft.md#controlretirementproposed) | ControlDraft | 0.75 | 0.74 | 0.68 | 0.86 | 0.82 | 0.08 | 0.42 | uncertain; review manually |
| [ControlReviewed](events/control-draft.md#controlreviewed) | ControlDraft | 0.38 | 0.61 | 0.71 | 0.84 | 0.64 | 0.06 | 0.65 | uncertain; review manually |
| [ControlSuccessorProposed](events/control-draft.md#controlsuccessorproposed) | ControlDraft | 0.53 | 0.50 | 0.31 | 0.71 | 0.81 | 0.16 | 0.55 | uncertain; review manually |
| [ResponsibilityAssigned](events/control-draft.md#responsibilityassigned) | ControlDraft, CommitmentDraft, SystemBoundary | 0.63 | 0.63 | 0.80 | 0.84 | 0.47 | 0.08 | 0.62 | uncertain; review manually |
| [ResponsibilityRevoked](events/control-draft.md#responsibilityrevoked) | ControlDraft, CommitmentDraft, SystemBoundary | 0.73 | 0.72 | 0.81 | 0.89 | 0.66 | 0.07 | 0.42 | uncertain; review manually |
| [ControlEvaluationPlanVersionDefined](events/control-evaluation-plan.md#controlevaluationplanversiondefined) | ControlEvaluationPlan | 0.22 | 0.51 | 0.34 | 0.64 | 0.75 | 0.26 | 0.53 | uncertain; review manually |
| [ControlEvaluationDeviationDisposed](events/control-evaluation.md#controlevaluationdeviationdisposed) | ControlEvaluation | 0.15 | 0.62 | 0.21 | 0.66 | 0.75 | 0.25 | 0.58 | uncertain; review manually |
| [ControlEvaluationReviewed](events/control-evaluation.md#controlevaluationreviewed) | ControlEvaluation | 0.65 | 0.65 | 0.71 | 0.86 | 0.84 | 0.09 | 0.43 | uncertain; review manually |
| [ControlEvaluationStarted](events/control-evaluation.md#controlevaluationstarted) | ControlEvaluation | 0.89 | 0.89 | 0.80 | 0.86 | 0.88 | 0.11 | 0.33 | uncertain; review manually |
| [ControlEvaluationStepRecorded](events/control-evaluation.md#controlevaluationsteprecorded) | ControlEvaluation | 0.61 | 0.58 | 0.58 | 0.82 | 0.86 | 0.23 | 0.36 | uncertain; review manually |
| [ControlEvaluationSubmitted](events/control-evaluation.md#controlevaluationsubmitted) | ControlEvaluation | 0.76 | 0.75 | 0.68 | 0.84 | 0.89 | 0.11 | 0.36 | uncertain; review manually |
| [ControlOccurrenceAttested](events/control-occurrence.md#controloccurrenceattested) | ControlOccurrence | 0.57 | 0.55 | 0.39 | 0.75 | 0.88 | 0.12 | 0.45 | uncertain; review manually |
| [ControlOccurrenceOpened](events/control-occurrence.md#controloccurrenceopened) | ControlOccurrence | 0.61 | 0.59 | 0.46 | 0.73 | 0.88 | 0.18 | 0.44 | uncertain; review manually |
| [ControlOccurrenceReviewed](events/control-occurrence.md#controloccurrencereviewed) | ControlOccurrence | 0.74 | 0.74 | 0.63 | 0.84 | 0.88 | 0.07 | 0.41 | uncertain; review manually |
| [ControlOperatingPlanApproved](events/control-operating-plan.md#controloperatingplanapproved) | ControlOperatingPlan | 0.91 | 0.91 | 0.75 | 0.86 | 0.90 | 0.07 | 0.31 | uncertain; review manually |
| [ControlOperatingPlanProposed](events/control-operating-plan.md#controloperatingplanproposed) | ControlOperatingPlan | 0.72 | 0.70 | 0.59 | 0.80 | 0.88 | 0.10 | 0.43 | uncertain; review manually |
| [CorrectiveActionAddedV2](events/corrective-action.md#correctiveactionaddedv2) | CorrectiveAction | 0.56 | 0.43 | 0.46 | 0.70 | 0.82 | 0.32 | 0.49 | uncertain; review manually |
| [CorrectiveActionCompletedV2](events/corrective-action.md#correctiveactioncompletedv2) | CorrectiveAction | 0.81 | 0.76 | 0.66 | 0.80 | 0.85 | 0.25 | 0.35 | uncertain; review manually |
| [CorrectiveActionFindingClosureUseAuthorized](events/corrective-action.md#correctiveactionfindingclosureuseauthorized) | CorrectiveAction | 0.21 | 0.54 | 0.22 | 0.51 | 0.77 | 0.54 | 0.63 | uncertain; review manually |
| [CorrectiveActionFindingClosureUseSettled](events/corrective-action.md#correctiveactionfindingclosureusesettled) | CorrectiveAction | 0.15 | 0.67 | 0.23 | 0.48 | 0.80 | 0.48 | 0.57 | uncertain; review manually |
| [CriteriaTextOverlayEntryRevised](events/criteria-text-overlay.md#criteriatextoverlayentryrevised) | CriteriaTextOverlay | 0.28 | 0.54 | 0.18 | 0.62 | 0.85 | 0.23 | 0.43 | high-confidence concern |
| [CriterionApplicabilityReviewed](events/criterion-applicability.md#criterionapplicabilityreviewed) | CriterionApplicability | 0.30 | 0.64 | 0.52 | 0.80 | 0.83 | 0.11 | 0.49 | uncertain; review manually |
| [CriterionNotApplicableProposed](events/criterion-applicability.md#criterionnotapplicableproposed) | CriterionApplicability | 0.82 | 0.81 | 0.71 | 0.86 | 0.85 | 0.09 | 0.36 | uncertain; review manually |
| [CriterionNotApplicableWithdrawn](events/criterion-applicability.md#criterionnotapplicablewithdrawn) | CriterionApplicability | 0.78 | 0.78 | 0.62 | 0.85 | 0.85 | 0.08 | 0.33 | uncertain; review manually |
| [DataFlowRevisionRecorded](events/data-flow.md#dataflowrevisionrecorded) | DataFlow | 0.44 | 0.44 | 0.46 | 0.69 | 0.73 | 0.33 | 0.59 | uncertain; review manually |
| [ApplicationDeclared](events/declared-application.md#applicationdeclared) | DeclaredApplication | 0.72 | 0.70 | 0.64 | 0.83 | 0.85 | 0.12 | 0.46 | uncertain; review manually |
| [ApplicationImportEffectCanceled](events/declared-application.md#applicationimporteffectcanceled) | DeclaredApplication | 0.12 | 0.51 | 0.28 | 0.57 | 0.76 | 0.45 | 0.65 | uncertain; review manually |
| [ApplicationImportEffectPending](events/declared-application.md#applicationimporteffectpending) | DeclaredApplication | 0.16 | 0.65 | 0.24 | 0.53 | 0.78 | 0.39 | 0.58 | uncertain; review manually |
| [ApplicationImportTargetAuthorized](events/declared-application.md#applicationimporttargetauthorized) | DeclaredApplication | 0.30 | 0.61 | 0.22 | 0.47 | 0.82 | 0.40 | 0.54 | uncertain; review manually |
| [ApplicationImportTargetSettled](events/declared-application.md#applicationimporttargetsettled) | DeclaredApplication | 0.24 | 0.67 | 0.20 | 0.48 | 0.85 | 0.34 | 0.47 | high-confidence concern |
| [ApplicationRetired](events/declared-application.md#applicationretired) | DeclaredApplication | 0.83 | 0.83 | 0.83 | 0.90 | 0.85 | 0.06 | 0.38 | uncertain; review manually |
| [ApplicationRevised](events/declared-application.md#applicationrevised) | DeclaredApplication | 0.29 | 0.51 | 0.65 | 0.84 | 0.73 | 0.11 | 0.63 | uncertain; review manually |
| [SystemInstanceRegistered](events/declared-system-instance.md#systeminstanceregistered) | DeclaredSystemInstance | 0.82 | 0.81 | 0.53 | 0.81 | 0.84 | 0.17 | 0.42 | uncertain; review manually |
| [SystemInstanceRetired](events/declared-system-instance.md#systeminstanceretired) | DeclaredSystemInstance | 0.67 | 0.66 | 0.60 | 0.82 | 0.83 | 0.09 | 0.45 | uncertain; review manually |
| [SystemInstanceRevised](events/declared-system-instance.md#systeminstancerevised) | DeclaredSystemInstance | 0.05 | 0.39 | 0.45 | 0.67 | 0.70 | 0.16 | 0.68 | uncertain; review manually |
| [EmailAddressReserved](events/email-address.md#emailaddressreserved) | EmailAddress | 0.78 | 0.73 | 0.73 | 0.83 | 0.87 | 0.28 | 0.31 | uncertain; review manually |
| [EmailAddressVerified](events/email-address.md#emailaddressverified) | EmailAddress | 0.84 | 0.84 | 0.90 | 0.84 | 0.83 | 0.10 | 0.60 | uncertain; review manually |
| [EmailChallengeIssued](events/email-address.md#emailchallengeissued) | EmailAddress | 0.85 | 0.84 | 0.62 | 0.85 | 0.80 | 0.12 | 0.44 | uncertain; review manually |
| [IdentityRecoveryChallengeClaimed](events/email-address.md#identityrecoverychallengeclaimed) | EmailAddress | 0.27 | 0.67 | 0.44 | 0.70 | 0.71 | 0.15 | 0.66 | uncertain; review manually |
| [IdentityRecoveryChallengeCompleted](events/email-address.md#identityrecoverychallengecompleted) | EmailAddress | 0.34 | 0.61 | 0.55 | 0.70 | 0.70 | 0.14 | 0.68 | uncertain; review manually |
| [IdentityRecoveryChallengeIssued](events/email-address.md#identityrecoverychallengeissued) | EmailAddress | 0.82 | 0.81 | 0.68 | 0.86 | 0.68 | 0.11 | 0.50 | uncertain; review manually |
| [EngagementManagementAcknowledged](events/engagement-management-acknowledgement.md#engagementmanagementacknowledged) | EngagementManagementAcknowledgement | 0.57 | 0.54 | 0.55 | 0.76 | 0.81 | 0.13 | 0.55 | uncertain; review manually |
| [ArtifactDownloadAvailabilityUseSettled](events/evidence-artifact.md#artifactdownloadavailabilityusesettled) | EvidenceArtifact | 0.02 | 0.44 | 0.13 | 0.30 | 0.50 | 0.65 | 0.71 | high-confidence concern |
| [EvidenceArtifactDownloadRequested](events/evidence-artifact.md#evidenceartifactdownloadrequested) | EvidenceArtifact | 0.44 | 0.41 | 0.55 | 0.80 | 0.78 | 0.22 | 0.59 | uncertain; review manually |
| [EvidenceArtifactInspected](events/evidence-artifact.md#evidenceartifactinspected) | EvidenceArtifact | 0.31 | 0.60 | 0.62 | 0.84 | 0.77 | 0.11 | 0.73 | uncertain; review manually |
| [EvidenceArtifactQuarantineReleased](events/evidence-artifact.md#evidenceartifactquarantinereleased) | EvidenceArtifact | 0.61 | 0.58 | 0.52 | 0.87 | 0.85 | 0.18 | 0.43 | uncertain; review manually |
| [EvidenceArtifactRegistered](events/evidence-artifact.md#evidenceartifactregistered) | EvidenceArtifact | 0.70 | 0.68 | 0.47 | 0.86 | 0.87 | 0.19 | 0.38 | uncertain; review manually |
| [ArtifactHandlingDownloadUseAuthorized](events/evidence-handling-policy.md#artifacthandlingdownloaduseauthorized) | EvidenceHandlingPolicy | 0.26 | 0.46 | 0.31 | 0.59 | 0.73 | 0.37 | 0.58 | uncertain; review manually |
| [ArtifactHandlingDownloadUseSettled](events/evidence-handling-policy.md#artifacthandlingdownloadusesettled) | EvidenceHandlingPolicy | 0.09 | 0.54 | 0.26 | 0.55 | 0.68 | 0.24 | 0.61 | uncertain; review manually |
| [ArtifactHandlingPolicyAssigned](events/evidence-handling-policy.md#artifacthandlingpolicyassigned) | EvidenceHandlingPolicy | 0.35 | 0.61 | 0.67 | 0.74 | 0.68 | 0.10 | 0.65 | uncertain; review manually |
| [ArtifactHandlingPolicyRevised](events/evidence-handling-policy.md#artifacthandlingpolicyrevised) | EvidenceHandlingPolicy | 0.21 | 0.61 | 0.71 | 0.79 | 0.75 | 0.09 | 0.57 | uncertain; review manually |
| [EvidenceRedactionApprovedV2](events/evidence-redaction.md#evidenceredactionapprovedv2) | EvidenceRedaction | 0.48 | 0.29 | 0.58 | 0.74 | 0.81 | 0.18 | 0.50 | uncertain; review manually |
| [EvidenceRedactionPreparedV2](events/evidence-redaction.md#evidenceredactionpreparedv2) | EvidenceRedaction | 0.41 | 0.49 | 0.51 | 0.73 | 0.85 | 0.21 | 0.46 | uncertain; review manually |
| [EvidenceRequestActionCompletionUseAuthorized](events/evidence-request.md#evidencerequestactioncompletionuseauthorized) | EvidenceRequest | 0.15 | 0.64 | 0.18 | 0.51 | 0.71 | 0.36 | 0.63 | high-confidence concern |
| [EvidenceRequestActionCompletionUseSettled](events/evidence-request.md#evidencerequestactioncompletionusesettled) | EvidenceRequest | 0.24 | 0.64 | 0.17 | 0.52 | 0.75 | 0.32 | 0.57 | high-confidence concern |
| [EvidenceRequestCancelled](events/evidence-request.md#evidencerequestcancelled) | EvidenceRequest | 0.95 | 0.95 | 0.91 | 0.94 | 0.90 | 0.06 | 0.23 | uncertain; review manually |
| [EvidenceRequestFulfilled](events/evidence-request.md#evidencerequestfulfilled) | EvidenceRequest | 0.91 | 0.91 | 0.73 | 0.86 | 0.87 | 0.09 | 0.33 | uncertain; review manually |
| [EvidenceRequestOpened](events/evidence-request.md#evidencerequestopened) | EvidenceRequest | 0.92 | 0.92 | 0.84 | 0.89 | 0.89 | 0.08 | 0.24 | uncertain; review manually |
| [EvidenceSupportLinked](events/evidence-support.md#evidencesupportlinked) | EvidenceSupport | 0.16 | 0.68 | 0.34 | 0.44 | 0.52 | 0.23 | 0.78 | uncertain; review manually |
| [ExaminationEngagementOpened](events/examination-engagement.md#examinationengagementopened) | ExaminationEngagement | 0.86 | 0.85 | 0.73 | 0.88 | 0.89 | 0.08 | 0.30 | uncertain; review manually |
| [ExaminationEngagementRevised](events/examination-engagement.md#examinationengagementrevised) | ExaminationEngagement | 0.50 | 0.43 | 0.67 | 0.87 | 0.86 | 0.09 | 0.55 | uncertain; review manually |
| [TypeIBaselineAmended](events/examination-engagement.md#typeibaselineamended) | ExaminationEngagement | 0.63 | 0.60 | 0.21 | 0.64 | 0.83 | 0.10 | 0.43 | uncertain; review manually |
| [TypeIBaselineFrozen](events/examination-engagement.md#typeibaselinefrozen) | ExaminationEngagement | 0.69 | 0.68 | 0.21 | 0.71 | 0.85 | 0.12 | 0.32 | uncertain; review manually |
| [ExaminationSignedOff](events/examination-sign-off.md#examinationsignedoff) | ExaminationSignOff | 0.74 | 0.73 | 0.76 | 0.86 | 0.79 | 0.07 | 0.46 | uncertain; review manually |
| [CorrectiveActionAdmitted](events/finding-aggregate.md#correctiveactionadmitted) | FindingAggregate | 0.67 | 0.65 | 0.57 | 0.80 | 0.81 | 0.12 | 0.44 | uncertain; review manually |
| [FindingAcceptanceLinkedV2](events/finding-aggregate.md#findingacceptancelinkedv2) | FindingAggregate | 0.23 | 0.63 | 0.30 | 0.67 | 0.74 | 0.28 | 0.55 | uncertain; review manually |
| [FindingClosedV2](events/finding-aggregate.md#findingclosedv2) | FindingAggregate | 0.77 | 0.72 | 0.64 | 0.83 | 0.82 | 0.14 | 0.34 | uncertain; review manually |
| [FindingClosureAccepted](events/finding-aggregate.md#findingclosureaccepted) | FindingAggregate | 0.42 | 0.50 | 0.54 | 0.70 | 0.80 | 0.38 | 0.59 | uncertain; review manually |
| [FindingClosureCanceled](events/finding-aggregate.md#findingclosurecanceled) | FindingAggregate | 0.77 | 0.75 | 0.74 | 0.83 | 0.74 | 0.14 | 0.53 | uncertain; review manually |
| [FindingRaisedV2](events/finding-aggregate.md#findingraisedv2) | FindingAggregate | 0.88 | 0.87 | 0.63 | 0.85 | 0.85 | 0.10 | 0.31 | uncertain; review manually |
| [FindingReopenedV2](events/finding-aggregate.md#findingreopenedv2) | FindingAggregate | 0.94 | 0.94 | 0.76 | 0.89 | 0.88 | 0.09 | 0.30 | uncertain; review manually |
| [FindingRevisedV2](events/finding-aggregate.md#findingrevisedv2) | FindingAggregate | 0.34 | 0.52 | 0.60 | 0.84 | 0.82 | 0.10 | 0.53 | uncertain; review manually |
| [FirmProfessionalDutyDesignationEngagementUseAuthorized](events/firm-professional-duty-designation.md#firmprofessionaldutydesignationengagementuseauthorized) | FirmProfessionalDutyDesignation | 0.37 | 0.52 | 0.25 | 0.64 | 0.80 | 0.30 | 0.57 | uncertain; review manually |
| [FirmProfessionalDutyDesignationEngagementUseSettled](events/firm-professional-duty-designation.md#firmprofessionaldutydesignationengagementusesettled) | FirmProfessionalDutyDesignation | 0.30 | 0.60 | 0.20 | 0.57 | 0.82 | 0.39 | 0.60 | high-confidence concern |
| [FirmProfessionalDutyDesignationRecorded](events/firm-professional-duty-designation.md#firmprofessionaldutydesignationrecorded) | FirmProfessionalDutyDesignation | 0.50 | 0.45 | 0.48 | 0.82 | 0.83 | 0.24 | 0.56 | uncertain; review manually |
| [FirmProfessionalDutyDesignationRevoked](events/firm-professional-duty-designation.md#firmprofessionaldutydesignationrevoked) | FirmProfessionalDutyDesignation | 0.93 | 0.93 | 0.49 | 0.87 | 0.87 | 0.07 | 0.33 | uncertain; review manually |
| [ProfessionalDutyChangeAccepted](events/firm-professional-duty-designation.md#professionaldutychangeaccepted) | FirmProfessionalDutyDesignation | 0.19 | 0.61 | 0.48 | 0.64 | 0.70 | 0.25 | 0.62 | uncertain; review manually |
| [ProfessionalDutyChangeRejected](events/firm-professional-duty-designation.md#professionaldutychangerejected) | FirmProfessionalDutyDesignation | 0.86 | 0.85 | 0.66 | 0.83 | 0.77 | 0.11 | 0.47 | uncertain; review manually |
| [FirmStaffIdentityClaimed](events/firm-staff-identity-claim.md#firmstaffidentityclaimed) | FirmStaffIdentityClaim | 0.69 | 0.68 | 0.44 | 0.76 | 0.84 | 0.16 | 0.41 | uncertain; review manually |
| [FirmStaffChangeRecorded](events/firm-staff-record.md#firmstaffchangerecorded) | FirmStaffRecord | 0.15 | 0.42 | 0.60 | 0.73 | 0.66 | 0.38 | 0.70 | uncertain; review manually |
| [FirmStaffProfessionalDutyUseAuthorized](events/firm-staff-record.md#firmstaffprofessionaldutyuseauthorized) | FirmStaffRecord | 0.33 | 0.54 | 0.28 | 0.59 | 0.76 | 0.38 | 0.56 | uncertain; review manually |
| [FirmStaffProfessionalDutyUseSettled](events/firm-staff-record.md#firmstaffprofessionaldutyusesettled) | FirmStaffRecord | 0.28 | 0.56 | 0.21 | 0.50 | 0.80 | 0.45 | 0.55 | uncertain; review manually |
| [FirmStaffRecordEngagementUseAuthorized](events/firm-staff-record.md#firmstaffrecordengagementuseauthorized) | FirmStaffRecord | 0.23 | 0.46 | 0.24 | 0.53 | 0.75 | 0.46 | 0.59 | uncertain; review manually |
| [FirmStaffRecordEngagementUseSettled](events/firm-staff-record.md#firmstaffrecordengagementusesettled) | FirmStaffRecord | 0.30 | 0.52 | 0.22 | 0.47 | 0.80 | 0.44 | 0.53 | uncertain; review manually |
| [FirmStaffRegistrationAccepted](events/firm-staff-record.md#firmstaffregistrationaccepted) | FirmStaffRecord | 0.75 | 0.74 | 0.74 | 0.80 | 0.84 | 0.13 | 0.54 | uncertain; review manually |
| [FirmStaffRegistrationRejected](events/firm-staff-record.md#firmstaffregistrationrejected) | FirmStaffRecord | 0.59 | 0.58 | 0.78 | 0.85 | 0.86 | 0.08 | 0.42 | uncertain; review manually |
| [IdentityRecoveryAccepted](events/identity-recovery.md#identityrecoveryaccepted) | IdentityRecovery | 0.48 | 0.51 | 0.62 | 0.76 | 0.84 | 0.12 | 0.53 | uncertain; review manually |
| [IdentityRecoveryCompleted](events/identity-recovery.md#identityrecoverycompleted) | IdentityRecovery | 0.53 | 0.52 | 0.81 | 0.85 | 0.79 | 0.10 | 0.50 | uncertain; review manually |
| [IdentityRecoveryPreparationRejected](events/identity-recovery.md#identityrecoverypreparationrejected) | IdentityRecovery | 0.42 | 0.52 | 0.40 | 0.66 | 0.83 | 0.26 | 0.55 | uncertain; review manually |
| [IdentityRecoveryPrepared](events/identity-recovery.md#identityrecoveryprepared) | IdentityRecovery | 0.29 | 0.66 | 0.43 | 0.69 | 0.83 | 0.21 | 0.57 | uncertain; review manually |
| [SnapshotFrozen](events/immutable-snapshot.md#snapshotfrozen) | ImmutableSnapshot | 0.15 | 0.52 | 0.29 | 0.73 | 0.61 | 0.45 | 0.60 | uncertain; review manually |
| [ImportedOwnerResolutionRecorded](events/imported-owner-resolution.md#importedownerresolutionrecorded) | ImportedOwnerResolution | 0.21 | 0.57 | 0.29 | 0.68 | 0.74 | 0.30 | 0.55 | uncertain; review manually |
| [IndependenceRuleCatalogEngagementUseAuthorized](events/independence-rule-catalog.md#independencerulecatalogengagementuseauthorized) | IndependenceRuleCatalog | 0.31 | 0.59 | 0.27 | 0.62 | 0.80 | 0.29 | 0.60 | uncertain; review manually |
| [IndependenceRuleCatalogEngagementUseSettled](events/independence-rule-catalog.md#independencerulecatalogengagementusesettled) | IndependenceRuleCatalog | 0.34 | 0.61 | 0.27 | 0.68 | 0.84 | 0.23 | 0.54 | uncertain; review manually |
| [IndependenceRulesRevised](events/independence-rule-catalog.md#independencerulesrevised) | IndependenceRuleCatalog | 0.53 | 0.49 | 0.57 | 0.83 | 0.76 | 0.11 | 0.55 | uncertain; review manually |
| [IndependenceRuleRatificationCatalogEngagementUseAuthorized](events/independence-rule-ratification-catalog.md#independenceruleratificationcatalogengagementuseauthorized) | IndependenceRuleRatificationCatalog | 0.33 | 0.58 | 0.19 | 0.61 | 0.79 | 0.28 | 0.57 | high-confidence concern |
| [IndependenceRuleRatificationCatalogEngagementUseSettled](events/independence-rule-ratification-catalog.md#independenceruleratificationcatalogengagementusesettled) | IndependenceRuleRatificationCatalog | 0.32 | 0.59 | 0.21 | 0.64 | 0.81 | 0.24 | 0.52 | uncertain; review manually |
| [IndependenceRuleRatificationRecorded](events/independence-rule-ratification-catalog.md#independenceruleratificationrecorded) | IndependenceRuleRatificationCatalog | 0.49 | 0.45 | 0.46 | 0.81 | 0.83 | 0.19 | 0.45 | uncertain; review manually |
| [InformationAssetRevisionRecorded](events/information-asset.md#informationassetrevisionrecorded) | InformationAsset | 0.44 | 0.49 | 0.68 | 0.85 | 0.80 | 0.24 | 0.46 | uncertain; review manually |
| [LocationNameClaimed](events/location-name-claim.md#locationnameclaimed) | LocationNameClaim | 0.67 | 0.66 | 0.43 | 0.73 | 0.84 | 0.15 | 0.44 | uncertain; review manually |
| [LocationNameReleased](events/location-name-claim.md#locationnamereleased) | LocationNameClaim | 0.86 | 0.85 | 0.56 | 0.74 | 0.84 | 0.11 | 0.48 | uncertain; review manually |
| [LocationChangeAccepted](events/location.md#locationchangeaccepted) | Location | 0.21 | 0.63 | 0.43 | 0.57 | 0.64 | 0.37 | 0.68 | uncertain; review manually |
| [LocationChangeCanceled](events/location.md#locationchangecanceled) | Location | 0.66 | 0.65 | 0.74 | 0.83 | 0.82 | 0.14 | 0.44 | uncertain; review manually |
| [LocationRevisionRecorded](events/location.md#locationrevisionrecorded) | Location | 0.38 | 0.47 | 0.40 | 0.72 | 0.78 | 0.35 | 0.51 | uncertain; review manually |
| [ManagementAssertionApproved](events/management-assertion.md#managementassertionapproved) | ManagementAssertion | 0.87 | 0.87 | 0.72 | 0.85 | 0.85 | 0.09 | 0.51 | uncertain; review manually |
| [ManagementAssertionRecorded](events/management-assertion.md#managementassertionrecorded) | ManagementAssertion | 0.67 | 0.65 | 0.65 | 0.83 | 0.80 | 0.31 | 0.61 | uncertain; review manually |
| [ManagementAssertionRevised](events/management-assertion.md#managementassertionrevised) | ManagementAssertion | 0.56 | 0.51 | 0.54 | 0.85 | 0.85 | 0.14 | 0.60 | uncertain; review manually |
| [ManagementRepresentationLetterSigned](events/management-representation-letter.md#managementrepresentationlettersigned) | ManagementRepresentationLetter | 0.90 | 0.90 | 0.86 | 0.91 | 0.86 | 0.07 | 0.47 | uncertain; review manually |
| [RepresentationLetterRecorded](events/management-representation-letter.md#representationletterrecorded) | ManagementRepresentationLetter | 0.56 | 0.53 | 0.72 | 0.83 | 0.69 | 0.23 | 0.60 | uncertain; review manually |
| [MemberDeprovisionCleanupCompleted](events/member.md#memberdeprovisioncleanupcompleted) | Member | 0.31 | 0.37 | 0.29 | 0.69 | 0.75 | 0.41 | 0.56 | uncertain; review manually |
| [MemberDeprovisioned](events/member.md#memberdeprovisioned) | Member | 0.60 | 0.58 | 0.44 | 0.88 | 0.83 | 0.12 | 0.50 | uncertain; review manually |
| [MemberRegistered](events/member.md#memberregistered) | Member | 0.67 | 0.62 | 0.75 | 0.77 | 0.79 | 0.27 | 0.58 | uncertain; review manually |
| [MemberReinstated](events/member.md#memberreinstated) | Member | 0.77 | 0.76 | 0.78 | 0.89 | 0.85 | 0.07 | 0.48 | uncertain; review manually |
| [MemberSuspended](events/member.md#membersuspended) | Member | 0.94 | 0.94 | 0.87 | 0.92 | 0.84 | 0.07 | 0.32 | uncertain; review manually |
| [MethodologyTemplateContributionAccepted](events/methodology-template-contribution.md#methodologytemplatecontributionaccepted) | MethodologyTemplateContribution | 0.69 | 0.61 | 0.70 | 0.79 | 0.82 | 0.19 | 0.45 | uncertain; review manually |
| [MethodologyTemplateContributionProposed](events/methodology-template-contribution.md#methodologytemplatecontributionproposed) | MethodologyTemplateContribution | 0.85 | 0.83 | 0.70 | 0.82 | 0.88 | 0.11 | 0.41 | uncertain; review manually |
| [MethodologyTemplateContributionReviewed](events/methodology-template-contribution.md#methodologytemplatecontributionreviewed) | MethodologyTemplateContribution | 0.39 | 0.50 | 0.72 | 0.79 | 0.79 | 0.14 | 0.68 | uncertain; review manually |
| [MethodologyTemplateCreated](events/methodology-template.md#methodologytemplatecreated) | MethodologyTemplate | 0.71 | 0.71 | 0.81 | 0.88 | 0.88 | 0.09 | 0.44 | uncertain; review manually |
| [MethodologyTemplateDraftReviewed](events/methodology-template.md#methodologytemplatedraftreviewed) | MethodologyTemplate | 0.22 | 0.42 | 0.49 | 0.65 | 0.76 | 0.49 | 0.65 | uncertain; review manually |
| [MethodologyTemplateDraftRevised](events/methodology-template.md#methodologytemplatedraftrevised) | MethodologyTemplate | 0.25 | 0.39 | 0.45 | 0.70 | 0.83 | 0.39 | 0.50 | uncertain; review manually |
| [MethodologyTemplateRetired](events/methodology-template.md#methodologytemplateretired) | MethodologyTemplate | 0.73 | 0.72 | 0.72 | 0.89 | 0.89 | 0.07 | 0.38 | uncertain; review manually |
| [MethodologyTemplateVersionPublished](events/methodology-template.md#methodologytemplateversionpublished) | MethodologyTemplate | 0.54 | 0.39 | 0.80 | 0.83 | 0.83 | 0.15 | 0.51 | uncertain; review manually |
| [OperationalProcessNameClaimed](events/operational-process-name-claim.md#operationalprocessnameclaimed) | OperationalProcessNameClaim | 0.39 | 0.53 | 0.38 | 0.69 | 0.84 | 0.34 | 0.52 | uncertain; review manually |
| [OperationalProcessNameReleased](events/operational-process-name-claim.md#operationalprocessnamereleased) | OperationalProcessNameClaim | 0.71 | 0.69 | 0.49 | 0.74 | 0.84 | 0.20 | 0.40 | uncertain; review manually |
| [OperationalProcessChangeAccepted](events/operational-process.md#operationalprocesschangeaccepted) | OperationalProcess | 0.26 | 0.57 | 0.42 | 0.63 | 0.74 | 0.37 | 0.64 | uncertain; review manually |
| [OperationalProcessChangeCanceled](events/operational-process.md#operationalprocesschangecanceled) | OperationalProcess | 0.62 | 0.58 | 0.62 | 0.79 | 0.83 | 0.26 | 0.43 | uncertain; review manually |
| [OperationalProcessRetired](events/operational-process.md#operationalprocessretired) | OperationalProcess | 0.66 | 0.66 | 0.70 | 0.87 | 0.83 | 0.10 | 0.55 | uncertain; review manually |
| [OperationalProcessRevisionRecorded](events/operational-process.md#operationalprocessrevisionrecorded) | OperationalProcess | 0.19 | 0.48 | 0.36 | 0.74 | 0.80 | 0.47 | 0.48 | uncertain; review manually |
| [PartnerIndependenceEvaluationRecorded](events/partner-independence-decision.md#partnerindependenceevaluationrecorded) | PartnerIndependenceDecision | 0.26 | 0.61 | 0.53 | 0.78 | 0.82 | 0.25 | 0.49 | uncertain; review manually |
| [PersonMembershipCorrelated](events/person.md#personmembershipcorrelated) | Person | 0.01 | 0.51 | 0.12 | 0.31 | 0.31 | 0.61 | 0.83 | high-confidence concern |
| [PersonRecorded](events/person.md#personrecorded) | Person | 0.54 | 0.48 | 0.64 | 0.85 | 0.69 | 0.13 | 0.64 | uncertain; review manually |
| [PersonRevised](events/person.md#personrevised) | Person | 0.14 | 0.47 | 0.52 | 0.85 | 0.69 | 0.11 | 0.57 | uncertain; review manually |
| [PlatformOperatorGranted](events/platform-operator-roster.md#platformoperatorgranted) | PlatformOperatorRoster | 0.78 | 0.77 | 0.62 | 0.90 | 0.87 | 0.09 | 0.30 | uncertain; review manually |
| [PlatformOperatorRevoked](events/platform-operator-roster.md#platformoperatorrevoked) | PlatformOperatorRoster | 0.91 | 0.91 | 0.73 | 0.92 | 0.88 | 0.07 | 0.24 | uncertain; review manually |
| [PlatformOperatorRosterSeeded](events/platform-operator-roster.md#platformoperatorrosterseeded) | PlatformOperatorRoster | 0.25 | 0.37 | 0.24 | 0.72 | 0.88 | 0.51 | 0.39 | uncertain; review manually |
| [CampaignWaiverApproved](events/policy-campaign-participant.md#campaignwaiverapproved) | PolicyCampaignParticipant | 0.68 | 0.68 | 0.78 | 0.87 | 0.79 | 0.07 | 0.39 | uncertain; review manually |
| [PolicyAcknowledgementRecorded](events/policy-campaign-participant.md#policyacknowledgementrecorded) | PolicyCampaignParticipant | 0.81 | 0.81 | 0.84 | 0.91 | 0.80 | 0.17 | 0.34 | uncertain; review manually |
| [PolicyCampaignParticipantCampaignClosureUseAuthorized](events/policy-campaign-participant.md#policycampaignparticipantcampaignclosureuseauthorized) | PolicyCampaignParticipant | 0.07 | 0.32 | 0.15 | 0.42 | 0.76 | 0.58 | 0.62 | high-confidence concern |
| [PolicyCampaignParticipantCampaignClosureUseSettled](events/policy-campaign-participant.md#policycampaignparticipantcampaignclosureusesettled) | PolicyCampaignParticipant | 0.19 | 0.46 | 0.15 | 0.43 | 0.82 | 0.60 | 0.47 | high-confidence concern |
| [TrainingCompletionRecorded](events/policy-campaign-participant.md#trainingcompletionrecorded) | PolicyCampaignParticipant | 0.75 | 0.74 | 0.89 | 0.92 | 0.73 | 0.11 | 0.44 | uncertain; review manually |
| [PolicyCampaignAudienceAmended](events/policy-distribution-campaign.md#policycampaignaudienceamended) | PolicyDistributionCampaign | 0.36 | 0.57 | 0.57 | 0.83 | 0.84 | 0.12 | 0.60 | uncertain; review manually |
| [PolicyCampaignAudienceFrozen](events/policy-distribution-campaign.md#policycampaignaudiencefrozen) | PolicyDistributionCampaign | 0.66 | 0.65 | 0.31 | 0.78 | 0.89 | 0.17 | 0.34 | uncertain; review manually |
| [PolicyCampaignClosed](events/policy-distribution-campaign.md#policycampaignclosed) | PolicyDistributionCampaign | 0.62 | 0.61 | 0.78 | 0.81 | 0.79 | 0.09 | 0.48 | uncertain; review manually |
| [PolicyCampaignClosureAccepted](events/policy-distribution-campaign.md#policycampaignclosureaccepted) | PolicyDistributionCampaign | 0.36 | 0.53 | 0.40 | 0.68 | 0.81 | 0.45 | 0.57 | uncertain; review manually |
| [PolicyCampaignClosureCanceled](events/policy-distribution-campaign.md#policycampaignclosurecanceled) | PolicyDistributionCampaign | 0.71 | 0.68 | 0.71 | 0.82 | 0.79 | 0.23 | 0.57 | uncertain; review manually |
| [PolicyCampaignLaunched](events/policy-distribution-campaign.md#policycampaignlaunched) | PolicyDistributionCampaign | 0.78 | 0.78 | 0.78 | 0.88 | 0.88 | 0.07 | 0.38 | uncertain; review manually |
| [PolicyParticipantResultAdmitted](events/policy-distribution-campaign.md#policyparticipantresultadmitted) | PolicyDistributionCampaign | 0.54 | 0.52 | 0.37 | 0.74 | 0.83 | 0.22 | 0.50 | uncertain; review manually |
| [PolicyApproved](events/policy.md#policyapproved) | Policy | 0.52 | 0.51 | 0.85 | 0.90 | 0.78 | 0.05 | 0.53 | uncertain; review manually |
| [PolicyDraftCreated](events/policy.md#policydraftcreated) | Policy | 0.93 | 0.93 | 0.87 | 0.93 | 0.91 | 0.07 | 0.27 | uncertain; review manually |
| [PolicyDraftDiscarded](events/policy.md#policydraftdiscarded) | Policy | 0.92 | 0.91 | 0.85 | 0.93 | 0.88 | 0.08 | 0.39 | uncertain; review manually |
| [PolicyDraftRevised](events/policy.md#policydraftrevised) | Policy | 0.70 | 0.68 | 0.78 | 0.91 | 0.86 | 0.07 | 0.52 | uncertain; review manually |
| [PolicyPeriodicReviewConfirmed](events/policy.md#policyperiodicreviewconfirmed) | Policy | 0.75 | 0.73 | 0.83 | 0.91 | 0.87 | 0.07 | 0.33 | uncertain; review manually |
| [PolicyRetired](events/policy.md#policyretired) | Policy | 0.67 | 0.66 | 0.74 | 0.88 | 0.81 | 0.05 | 0.58 | uncertain; review manually |
| [PolicyRetirementProposed](events/policy.md#policyretirementproposed) | Policy | 0.82 | 0.81 | 0.66 | 0.92 | 0.88 | 0.06 | 0.37 | uncertain; review manually |
| [PolicyReviewed](events/policy.md#policyreviewed) | Policy | 0.48 | 0.52 | 0.84 | 0.91 | 0.80 | 0.05 | 0.52 | uncertain; review manually |
| [PopulationSnapshotChunkStored](events/population-snapshot-chunk.md#populationsnapshotchunkstored) | PopulationSnapshotChunk | 0.10 | 0.22 | 0.13 | 0.46 | 0.87 | 0.79 | 0.36 | high-confidence concern |
| [PopulationSnapshotFrozen](events/population-snapshot.md#populationsnapshotfrozen) | PopulationSnapshot | 0.65 | 0.63 | 0.23 | 0.79 | 0.88 | 0.24 | 0.34 | uncertain; review manually |
| [PracticeIndependenceCommitted](events/practice-independence-commitment.md#practiceindependencecommitted) | PracticeIndependenceCommitment | 0.34 | 0.61 | 0.29 | 0.76 | 0.82 | 0.14 | 0.50 | uncertain; review manually |
| [PracticeIndependenceReservationSettled](events/practice-independence-commitment.md#practiceindependencereservationsettled) | PracticeIndependenceCommitment | 0.17 | 0.69 | 0.18 | 0.55 | 0.80 | 0.27 | 0.69 | high-confidence concern |
| [PracticeIndependenceReserved](events/practice-independence-commitment.md#practiceindependencereserved) | PracticeIndependenceCommitment | 0.49 | 0.49 | 0.31 | 0.68 | 0.85 | 0.22 | 0.45 | uncertain; review manually |
| [ProfessionalAssertionIdentityClaimed](events/professional-assertion-identity-claim.md#professionalassertionidentityclaimed) | ProfessionalAssertionIdentityClaim | 0.15 | 0.62 | 0.16 | 0.59 | 0.79 | 0.18 | 0.57 | high-confidence concern |
| [ReadinessAssessmentAdopted](events/program-readiness.md#readinessassessmentadopted) | ProgramReadiness | 0.59 | 0.57 | 0.54 | 0.77 | 0.88 | 0.24 | 0.42 | uncertain; review manually |
| [ReadinessAssessmentRequested](events/program-readiness.md#readinessassessmentrequested) | ProgramReadiness | 0.50 | 0.43 | 0.66 | 0.76 | 0.85 | 0.23 | 0.50 | uncertain; review manually |
| [ReadinessDecisionAccepted](events/program-readiness.md#readinessdecisionaccepted) | ProgramReadiness | 0.36 | 0.52 | 0.60 | 0.72 | 0.65 | 0.22 | 0.63 | uncertain; review manually |
| [ReadinessDecisionCanceled](events/program-readiness.md#readinessdecisioncanceled) | ProgramReadiness | 0.69 | 0.66 | 0.75 | 0.81 | 0.82 | 0.17 | 0.57 | uncertain; review manually |
| [ReadinessDecisionRecorded](events/program-readiness.md#readinessdecisionrecorded) | ProgramReadiness | 0.64 | 0.60 | 0.69 | 0.76 | 0.80 | 0.34 | 0.52 | uncertain; review manually |
| [TypeIEntryDecisionAccepted](events/program-readiness.md#typeientrydecisionaccepted) | ProgramReadiness | 0.58 | 0.55 | 0.30 | 0.65 | 0.77 | 0.14 | 0.49 | uncertain; review manually |
| [TypeIEntryDecisionRecorded](events/program-readiness.md#typeientrydecisionrecorded) | ProgramReadiness | 0.23 | 0.66 | 0.19 | 0.40 | 0.74 | 0.28 | 0.56 | high-confidence concern |
| [ProgramNextPeriodCreated](events/program-rollover.md#programnextperiodcreated) | ProgramRollover | 0.46 | 0.49 | 0.52 | 0.75 | 0.64 | 0.13 | 0.62 | uncertain; review manually |
| [ProgramRolloverApproved](events/program-rollover.md#programrolloverapproved) | ProgramRollover | 0.49 | 0.46 | 0.55 | 0.76 | 0.85 | 0.07 | 0.68 | uncertain; review manually |
| [ProgramRolloverDecisionRecorded](events/program-rollover.md#programrolloverdecisionrecorded) | ProgramRollover | 0.03 | 0.83 | 0.43 | 0.66 | 0.54 | 0.29 | 0.83 | high-confidence concern |
| [ProgramRolloverExecutionClaimed](events/program-rollover.md#programrolloverexecutionclaimed) | ProgramRollover | 0.01 | 0.07 | 0.13 | 0.22 | 0.53 | 0.71 | 0.83 | high-confidence concern |
| [ProgramRolloverProposed](events/program-rollover.md#programrolloverproposed) | ProgramRollover | 0.48 | 0.49 | 0.53 | 0.81 | 0.88 | 0.08 | 0.52 | uncertain; review manually |
| [ProgramRolloverRevised](events/program-rollover.md#programrolloverrevised) | ProgramRollover | 0.31 | 0.60 | 0.49 | 0.81 | 0.84 | 0.09 | 0.57 | uncertain; review manually |
| [ProviderCoverageGapClosed](events/provider-assurance-gap.md#providercoveragegapclosed) | ProviderAssuranceGap | 0.82 | 0.81 | 0.71 | 0.87 | 0.87 | 0.07 | 0.44 | uncertain; review manually |
| [ProviderCoverageGapRecorded](events/provider-assurance-gap.md#providercoveragegaprecorded) | ProviderAssuranceGap | 0.73 | 0.71 | 0.59 | 0.81 | 0.87 | 0.20 | 0.50 | uncertain; review manually |
| [ProviderCoverageGapRiskAcceptanceAdmitted](events/provider-assurance-gap.md#providercoveragegapriskacceptanceadmitted) | ProviderAssuranceGap | 0.57 | 0.53 | 0.36 | 0.80 | 0.86 | 0.21 | 0.43 | uncertain; review manually |
| [AssuranceReportRecorded](events/provider-assurance-report.md#assurancereportrecorded) | ProviderAssuranceReport | 0.72 | 0.69 | 0.80 | 0.86 | 0.82 | 0.21 | 0.48 | uncertain; review manually |
| [AssuranceReportRevised](events/provider-assurance-report.md#assurancereportrevised) | ProviderAssuranceReport | 0.77 | 0.75 | 0.75 | 0.87 | 0.85 | 0.09 | 0.41 | uncertain; review manually |
| [ProviderAssuranceReportProviderReviewUseAuthorized](events/provider-assurance-report.md#providerassurancereportproviderreviewuseauthorized) | ProviderAssuranceReport | 0.34 | 0.44 | 0.22 | 0.54 | 0.76 | 0.43 | 0.56 | uncertain; review manually |
| [ProviderAssuranceReportProviderReviewUseSettled](events/provider-assurance-report.md#providerassurancereportproviderreviewusesettled) | ProviderAssuranceReport | 0.32 | 0.56 | 0.16 | 0.46 | 0.82 | 0.47 | 0.58 | high-confidence concern |
| [ProviderReviewCanceled](events/provider-assurance-review.md#providerreviewcanceled) | ProviderAssuranceReview | 0.52 | 0.51 | 0.77 | 0.86 | 0.83 | 0.08 | 0.59 | uncertain; review manually |
| [ProviderReviewRecorded](events/provider-assurance-review.md#providerreviewrecorded) | ProviderAssuranceReview | 0.37 | 0.55 | 0.81 | 0.75 | 0.76 | 0.26 | 0.59 | uncertain; review manually |
| [ProviderReviewRequested](events/provider-assurance-review.md#providerreviewrequested) | ProviderAssuranceReview | 0.24 | 0.59 | 0.59 | 0.74 | 0.70 | 0.15 | 0.71 | uncertain; review manually |
| [ProviderNameClaimed](events/provider-name-claim.md#providernameclaimed) | ProviderNameClaim | 0.66 | 0.65 | 0.51 | 0.76 | 0.84 | 0.14 | 0.50 | uncertain; review manually |
| [ProviderNameReleased](events/provider-name-claim.md#providernamereleased) | ProviderNameClaim | 0.79 | 0.78 | 0.66 | 0.75 | 0.83 | 0.11 | 0.48 | uncertain; review manually |
| [ProviderCoverageGapRiskAcceptanceLinked](events/provider-risk-acceptance.md#providercoveragegapriskacceptancelinked) | ProviderRiskAcceptance | 0.39 | 0.50 | 0.23 | 0.64 | 0.78 | 0.25 | 0.51 | uncertain; review manually |
| [ProviderChangeAccepted](events/provider.md#providerchangeaccepted) | Provider | 0.32 | 0.57 | 0.48 | 0.63 | 0.72 | 0.26 | 0.66 | uncertain; review manually |
| [ProviderChangeCanceled](events/provider.md#providerchangecanceled) | Provider | 0.70 | 0.68 | 0.81 | 0.84 | 0.82 | 0.12 | 0.42 | uncertain; review manually |
| [ProviderRecorded](events/provider.md#providerrecorded) | Provider | 0.31 | 0.55 | 0.54 | 0.65 | 0.55 | 0.32 | 0.71 | uncertain; review manually |
| [ProviderRetired](events/provider.md#providerretired) | Provider | 0.57 | 0.56 | 0.75 | 0.89 | 0.76 | 0.08 | 0.59 | uncertain; review manually |
| [ProviderRevised](events/provider.md#providerrevised) | Provider | 0.54 | 0.49 | 0.59 | 0.82 | 0.76 | 0.11 | 0.47 | uncertain; review manually |
| [ReadinessAssessmentRecorded](events/readiness-assessment.md#readinessassessmentrecorded) | ReadinessAssessment | 0.63 | 0.60 | 0.80 | 0.86 | 0.85 | 0.19 | 0.35 | uncertain; review manually |
| [ReadinessGapAnnotated](events/readiness-gap-plan.md#readinessgapannotated) | ReadinessGapPlan | 0.42 | 0.50 | 0.39 | 0.76 | 0.82 | 0.17 | 0.51 | uncertain; review manually |
| [ReadinessGapPlanReadinessDecisionUseAuthorized](events/readiness-gap-plan.md#readinessgapplanreadinessdecisionuseauthorized) | ReadinessGapPlan | 0.10 | 0.39 | 0.17 | 0.47 | 0.72 | 0.58 | 0.61 | high-confidence concern |
| [ReadinessGapPlanReadinessDecisionUseSettled](events/readiness-gap-plan.md#readinessgapplanreadinessdecisionusesettled) | ReadinessGapPlan | 0.11 | 0.51 | 0.16 | 0.49 | 0.76 | 0.50 | 0.52 | high-confidence concern |
| [ReadinessGapPlanned](events/readiness-gap-plan.md#readinessgapplanned) | ReadinessGapPlan | 0.71 | 0.68 | 0.55 | 0.78 | 0.84 | 0.13 | 0.39 | uncertain; review manually |
| [RiskDraftCreated](events/risk-draft.md#riskdraftcreated) | RiskDraft | 0.89 | 0.88 | 0.83 | 0.91 | 0.90 | 0.08 | 0.29 | uncertain; review manually |
| [RiskDraftRevised](events/risk-draft.md#riskdraftrevised) | RiskDraft | 0.79 | 0.76 | 0.77 | 0.90 | 0.86 | 0.09 | 0.35 | uncertain; review manually |
| [RiskAccepted](events/risk-evaluation.md#riskaccepted) | RiskEvaluation | 0.94 | 0.94 | 0.81 | 0.91 | 0.74 | 0.05 | 0.38 | uncertain; review manually |
| [RiskAssessmentRecorded](events/risk-evaluation.md#riskassessmentrecorded) | RiskEvaluation | 0.76 | 0.75 | 0.82 | 0.89 | 0.66 | 0.18 | 0.61 | uncertain; review manually |
| [RiskTreatmentChosen](events/risk-evaluation.md#risktreatmentchosen) | RiskEvaluation | 0.80 | 0.79 | 0.73 | 0.89 | 0.80 | 0.08 | 0.49 | uncertain; review manually |
| [RiskOwnerAssigned](events/risk-governance.md#riskownerassigned) | RiskGovernance | 0.94 | 0.94 | 0.91 | 0.94 | 0.88 | 0.06 | 0.28 | uncertain; review manually |
| [RiskReassessmentTriggered](events/risk-governance.md#riskreassessmenttriggered) | RiskGovernance | 0.75 | 0.73 | 0.63 | 0.84 | 0.82 | 0.21 | 0.49 | uncertain; review manually |
| [RiskMethodVersionPublished](events/risk-method.md#riskmethodversionpublished) | RiskMethod | 0.60 | 0.48 | 0.36 | 0.73 | 0.83 | 0.28 | 0.47 | uncertain; review manually |
| [RiskTreatmentActionAdded](events/risk-treatment-action.md#risktreatmentactionadded) | RiskTreatmentAction | 0.86 | 0.85 | 0.74 | 0.88 | 0.89 | 0.11 | 0.28 | uncertain; review manually |
| [RiskTreatmentActionCancelled](events/risk-treatment-action.md#risktreatmentactioncancelled) | RiskTreatmentAction | 0.92 | 0.92 | 0.76 | 0.92 | 0.89 | 0.08 | 0.29 | uncertain; review manually |
| [RiskTreatmentActionCompletionReviewAccepted](events/risk-treatment-action.md#risktreatmentactioncompletionreviewaccepted) | RiskTreatmentAction | 0.23 | 0.60 | 0.39 | 0.58 | 0.79 | 0.35 | 0.64 | uncertain; review manually |
| [RiskTreatmentActionCompletionReviewCanceled](events/risk-treatment-action.md#risktreatmentactioncompletionreviewcanceled) | RiskTreatmentAction | 0.69 | 0.67 | 0.57 | 0.76 | 0.84 | 0.14 | 0.49 | uncertain; review manually |
| [RiskTreatmentActionCompletionReviewed](events/risk-treatment-action.md#risktreatmentactioncompletionreviewed) | RiskTreatmentAction | 0.39 | 0.59 | 0.54 | 0.78 | 0.83 | 0.11 | 0.47 | uncertain; review manually |
| [RiskTreatmentActionCompletionSubmitted](events/risk-treatment-action.md#risktreatmentactioncompletionsubmitted) | RiskTreatmentAction | 0.68 | 0.67 | 0.58 | 0.79 | 0.85 | 0.13 | 0.49 | uncertain; review manually |
| [RiskTreatmentActionRevised](events/risk-treatment-action.md#risktreatmentactionrevised) | RiskTreatmentAction | 0.48 | 0.41 | 0.66 | 0.79 | 0.83 | 0.11 | 0.52 | uncertain; review manually |
| [RiskControlTreatmentProposed](events/risk-treatment-assertion.md#riskcontroltreatmentproposed) | RiskTreatmentAssertion | 0.69 | 0.67 | 0.53 | 0.83 | 0.85 | 0.07 | 0.39 | uncertain; review manually |
| [RiskControlTreatmentRetired](events/risk-treatment-assertion.md#riskcontroltreatmentretired) | RiskTreatmentAssertion | 0.39 | 0.49 | 0.37 | 0.76 | 0.82 | 0.13 | 0.53 | uncertain; review manually |
| [RiskControlTreatmentReviewed](events/risk-treatment-assertion.md#riskcontroltreatmentreviewed) | RiskTreatmentAssertion | 0.40 | 0.59 | 0.61 | 0.84 | 0.82 | 0.07 | 0.46 | uncertain; review manually |
| [RolePermissionAssigned](events/role-permission.md#rolepermissionassigned) | RolePermission | 0.77 | 0.76 | 0.83 | 0.92 | 0.85 | 0.08 | 0.34 | uncertain; review manually |
| [RolePermissionRemoved](events/role-permission.md#rolepermissionremoved) | RolePermission | 0.87 | 0.86 | 0.84 | 0.92 | 0.87 | 0.07 | 0.35 | uncertain; review manually |
| [RoleDefined](events/role.md#roledefined) | Role | 0.77 | 0.75 | 0.70 | 0.88 | 0.85 | 0.09 | 0.38 | uncertain; review manually |
| [RoleDeleted](events/role.md#roledeleted) | Role | 0.92 | 0.92 | 0.89 | 0.92 | 0.85 | 0.08 | 0.44 | uncertain; review manually |
| [RoleRenamed](events/role.md#rolerenamed) | Role | 0.97 | 0.97 | 0.89 | 0.94 | 0.88 | 0.06 | 0.25 | uncertain; review manually |
| [SeparationOfDutiesWaiverApproved](events/separation-of-duties-waiver.md#separationofdutieswaiverapproved) | SeparationOfDutiesWaiver | 0.89 | 0.89 | 0.74 | 0.91 | 0.88 | 0.05 | 0.31 | uncertain; review manually |
| [SeparationOfDutiesWaiverRecorded](events/separation-of-duties-waiver.md#separationofdutieswaiverrecorded) | SeparationOfDutiesWaiver | 0.45 | 0.48 | 0.60 | 0.84 | 0.82 | 0.28 | 0.54 | uncertain; review manually |
| [AssignmentIndependenceReevaluated](events/service-engagement-independence.md#assignmentindependencereevaluated) | ServiceEngagementIndependence | 0.14 | 0.70 | 0.39 | 0.78 | 0.77 | 0.21 | 0.61 | uncertain; review manually |
| [DirectoryIndependenceReevaluated](events/service-engagement-independence.md#directoryindependencereevaluated) | ServiceEngagementIndependence | 0.04 | 0.60 | 0.17 | 0.58 | 0.59 | 0.21 | 0.76 | high-confidence concern |
| [EngagementPersonalDecisionAdmitted](events/service-engagement-independence.md#engagementpersonaldecisionadmitted) | ServiceEngagementIndependence | 0.44 | 0.51 | 0.20 | 0.57 | 0.77 | 0.33 | 0.57 | high-confidence concern |
| [EngagementPersonalDecisionAuthorized](events/service-engagement-independence.md#engagementpersonaldecisionauthorized) | ServiceEngagementIndependence | 0.82 | 0.82 | 0.50 | 0.72 | 0.82 | 0.10 | 0.40 | uncertain; review manually |
| [EngagementPersonalDecisionCanceled](events/service-engagement-independence.md#engagementpersonaldecisioncanceled) | ServiceEngagementIndependence | 0.86 | 0.86 | 0.58 | 0.82 | 0.86 | 0.09 | 0.35 | uncertain; review manually |
| [EngagementPersonalDecisionMaterialized](events/service-engagement-independence.md#engagementpersonaldecisionmaterialized) | ServiceEngagementIndependence | 0.16 | 0.71 | 0.15 | 0.49 | 0.78 | 0.51 | 0.55 | high-confidence concern |
| [EngagementPracticeCommitmentsAuthorized](events/service-engagement-independence.md#engagementpracticecommitmentsauthorized) | ServiceEngagementIndependence | 0.15 | 0.66 | 0.25 | 0.57 | 0.73 | 0.25 | 0.65 | uncertain; review manually |
| [ServiceEngagementAcceptanceRecorded](events/service-engagement-independence.md#serviceengagementacceptancerecorded) | ServiceEngagementIndependence | 0.79 | 0.77 | 0.73 | 0.81 | 0.81 | 0.35 | 0.48 | uncertain; review manually |
| [ServiceEngagementAcceptanceRejected](events/service-engagement-independence.md#serviceengagementacceptancerejected) | ServiceEngagementIndependence | 0.90 | 0.90 | 0.77 | 0.86 | 0.85 | 0.08 | 0.43 | uncertain; review manually |
| [ServiceEngagementAcceptanceRequested](events/service-engagement-independence.md#serviceengagementacceptancerequested) | ServiceEngagementIndependence | 0.22 | 0.52 | 0.39 | 0.64 | 0.63 | 0.23 | 0.72 | uncertain; review manually |
| [ServiceEngagementAssignmentRevoked](events/service-engagement-independence.md#serviceengagementassignmentrevoked) | ServiceEngagementIndependence | 0.73 | 0.72 | 0.79 | 0.87 | 0.79 | 0.09 | 0.69 | uncertain; review manually |
| [ServiceEngagementMutationRecorded](events/service-engagement-independence.md#serviceengagementmutationrecorded) | ServiceEngagementIndependence | 0.03 | 0.55 | 0.23 | 0.54 | 0.48 | 0.69 | 0.72 | uncertain; review manually |
| [ServiceEngagementStaffAssigned](events/service-engagement-independence.md#serviceengagementstaffassigned) | ServiceEngagementIndependence | 0.80 | 0.80 | 0.83 | 0.87 | 0.83 | 0.09 | 0.40 | uncertain; review manually |
| [ServiceEngagementStaffAssignmentRejected](events/service-engagement-independence.md#serviceengagementstaffassignmentrejected) | ServiceEngagementIndependence | 0.84 | 0.83 | 0.77 | 0.85 | 0.82 | 0.09 | 0.44 | uncertain; review manually |
| [ServiceEngagementStaffAssignmentRequested](events/service-engagement-independence.md#serviceengagementstaffassignmentrequested) | ServiceEngagementIndependence | 0.63 | 0.59 | 0.62 | 0.81 | 0.80 | 0.11 | 0.52 | uncertain; review manually |
| [ServiceIndependenceReevaluated](events/service-engagement-independence.md#serviceindependencereevaluated) | ServiceEngagementIndependence | 0.17 | 0.75 | 0.47 | 0.76 | 0.71 | 0.12 | 0.61 | uncertain; review manually |
| [ServiceIdentityRecorded](events/service-identity.md#serviceidentityrecorded) | ServiceIdentity | 0.36 | 0.55 | 0.55 | 0.76 | 0.77 | 0.16 | 0.63 | uncertain; review manually |
| [ServiceIdentityRevised](events/service-identity.md#serviceidentityrevised) | ServiceIdentity | 0.50 | 0.36 | 0.53 | 0.79 | 0.83 | 0.12 | 0.43 | uncertain; review manually |
| [ImpactReviewWorkLinked](events/significant-change.md#impactreviewworklinked) | SignificantChange | 0.55 | 0.49 | 0.42 | 0.78 | 0.77 | 0.24 | 0.52 | uncertain; review manually |
| [SignificantChangeImpactAssessed](events/significant-change.md#significantchangeimpactassessed) | SignificantChange | 0.87 | 0.87 | 0.82 | 0.90 | 0.86 | 0.07 | 0.33 | uncertain; review manually |
| [SignificantChangeRecorded](events/significant-change.md#significantchangerecorded) | SignificantChange | 0.74 | 0.73 | 0.77 | 0.86 | 0.84 | 0.17 | 0.44 | uncertain; review manually |
| [SignificantChangeRevised](events/significant-change.md#significantchangerevised) | SignificantChange | 0.73 | 0.70 | 0.59 | 0.87 | 0.85 | 0.13 | 0.40 | uncertain; review manually |
| [SignificantChangeSystemDescriptionDecisionRecorded](events/significant-change.md#significantchangesystemdescriptiondecisionrecorded) | SignificantChange | 0.64 | 0.62 | 0.59 | 0.84 | 0.81 | 0.14 | 0.40 | uncertain; review manually |
| [BoundaryApproved](events/system-boundary.md#boundaryapproved) | SystemBoundary | 0.73 | 0.72 | 0.49 | 0.77 | 0.78 | 0.09 | 0.51 | uncertain; review manually |
| [BoundaryDraftCreated](events/system-boundary.md#boundarydraftcreated) | SystemBoundary | 0.90 | 0.90 | 0.64 | 0.84 | 0.86 | 0.11 | 0.28 | uncertain; review manually |
| [BoundaryDraftDiscarded](events/system-boundary.md#boundarydraftdiscarded) | SystemBoundary | 0.88 | 0.87 | 0.71 | 0.85 | 0.84 | 0.12 | 0.34 | uncertain; review manually |
| [BoundaryDraftRevised](events/system-boundary.md#boundarydraftrevised) | SystemBoundary | 0.77 | 0.75 | 0.67 | 0.85 | 0.84 | 0.09 | 0.31 | uncertain; review manually |
| [BoundaryReviewed](events/system-boundary.md#boundaryreviewed) | SystemBoundary | 0.27 | 0.72 | 0.67 | 0.81 | 0.74 | 0.07 | 0.60 | uncertain; review manually |
| [BoundarySuccessorProposed](events/system-boundary.md#boundarysuccessorproposed) | SystemBoundary | 0.42 | 0.50 | 0.26 | 0.68 | 0.81 | 0.17 | 0.42 | uncertain; review manually |
| [ResponsibilityAssigned](events/system-boundary.md#responsibilityassigned) | ControlDraft, CommitmentDraft, SystemBoundary | 0.70 | 0.70 | 0.80 | 0.83 | 0.54 | 0.08 | 0.51 | uncertain; review manually |
| [ResponsibilityRevoked](events/system-boundary.md#responsibilityrevoked) | ControlDraft, CommitmentDraft, SystemBoundary | 0.74 | 0.74 | 0.78 | 0.88 | 0.64 | 0.08 | 0.38 | uncertain; review manually |
| [SystemDescriptionApproved](events/system-description.md#systemdescriptionapproved) | SystemDescription | 0.95 | 0.95 | 0.76 | 0.86 | 0.87 | 0.09 | 0.34 | uncertain; review manually |
| [SystemDescriptionBound](events/system-description.md#systemdescriptionbound) | SystemDescription | 0.10 | 0.72 | 0.21 | 0.49 | 0.78 | 0.28 | 0.56 | uncertain; review manually |
| [SystemDescriptionDraftCreated](events/system-description.md#systemdescriptiondraftcreated) | SystemDescription | 0.88 | 0.87 | 0.69 | 0.84 | 0.88 | 0.12 | 0.29 | uncertain; review manually |
| [SystemDescriptionDraftRevised](events/system-description.md#systemdescriptiondraftrevised) | SystemDescription | 0.80 | 0.78 | 0.67 | 0.84 | 0.86 | 0.11 | 0.31 | uncertain; review manually |
| [SystemDescriptionNoChangeRecorded](events/system-description.md#systemdescriptionnochangerecorded) | SystemDescription | 0.51 | 0.41 | 0.62 | 0.79 | 0.78 | 0.20 | 0.50 | uncertain; review manually |
| [SystemDescriptionReviewed](events/system-description.md#systemdescriptionreviewed) | SystemDescription | 0.47 | 0.53 | 0.78 | 0.85 | 0.84 | 0.08 | 0.45 | uncertain; review manually |
| [SystemDescriptionSourcesBound](events/system-description.md#systemdescriptionsourcesbound) | SystemDescription | 0.08 | 0.66 | 0.17 | 0.47 | 0.76 | 0.41 | 0.63 | high-confidence concern |
| [SystemDescriptionSuccessorProposed](events/system-description.md#systemdescriptionsuccessorproposed) | SystemDescription | 0.49 | 0.42 | 0.30 | 0.73 | 0.83 | 0.18 | 0.36 | uncertain; review manually |
| [AccessReviewScopeDecided](events/system-instance-access-review-scope.md#accessreviewscopedecided) | SystemInstanceAccessReviewScope | 0.18 | 0.69 | 0.55 | 0.65 | 0.66 | 0.19 | 0.64 | uncertain; review manually |
| [TeamMemberAssigned](events/team-member.md#teammemberassigned) | TeamMember | 0.82 | 0.81 | 0.88 | 0.91 | 0.83 | 0.09 | 0.42 | uncertain; review manually |
| [TeamMemberRemoved](events/team-member.md#teammemberremoved) | TeamMember | 0.82 | 0.81 | 0.87 | 0.91 | 0.78 | 0.07 | 0.57 | uncertain; review manually |
| [TeamRoleAssigned](events/team-role.md#teamroleassigned) | TeamRole | 0.72 | 0.72 | 0.77 | 0.89 | 0.79 | 0.07 | 0.57 | uncertain; review manually |
| [TeamRoleRemoved](events/team-role.md#teamroleremoved) | TeamRole | 0.76 | 0.75 | 0.77 | 0.88 | 0.83 | 0.06 | 0.44 | uncertain; review manually |
| [TeamDefined](events/team.md#teamdefined) | Team | 0.32 | 0.45 | 0.54 | 0.84 | 0.77 | 0.16 | 0.60 | uncertain; review manually |
| [TeamDeleted](events/team.md#teamdeleted) | Team | 0.91 | 0.90 | 0.87 | 0.92 | 0.82 | 0.08 | 0.53 | uncertain; review manually |
| [TechnologyComponentRevisionRecorded](events/technology-component.md#technologycomponentrevisionrecorded) | TechnologyComponent | 0.27 | 0.45 | 0.40 | 0.75 | 0.80 | 0.34 | 0.50 | uncertain; review manually |
| [MethodologyTemplateApplied](events/template-application.md#methodologytemplateapplied) | TemplateApplication | 0.73 | 0.72 | 0.53 | 0.88 | 0.82 | 0.10 | 0.41 | uncertain; review manually |
| [TemplateApplicationDraftCreated](events/template-application.md#templateapplicationdraftcreated) | TemplateApplication | 0.39 | 0.00 | 0.51 | 0.77 | 0.84 | 0.30 | 0.51 | uncertain; review manually |
| [TemplateSuccessorAdoptionProposed](events/template-application.md#templatesuccessoradoptionproposed) | TemplateApplication | 0.45 | 0.48 | 0.34 | 0.83 | 0.81 | 0.14 | 0.42 | uncertain; review manually |
| [TemplateSuccessorDispositionRecorded](events/template-application.md#templatesuccessordispositionrecorded) | TemplateApplication | 0.49 | 0.43 | 0.29 | 0.78 | 0.82 | 0.29 | 0.43 | uncertain; review manually |
| [TenantIdentityProviderConfigured](events/tenant-identity-provider.md#tenantidentityproviderconfigured) | TenantIdentityProvider | 0.83 | 0.82 | 0.48 | 0.84 | 0.88 | 0.14 | 0.38 | uncertain; review manually |
| [TenantIdentityProviderCredentialsRotated](events/tenant-identity-provider.md#tenantidentityprovidercredentialsrotated) | TenantIdentityProvider | 0.85 | 0.84 | 0.59 | 0.87 | 0.88 | 0.17 | 0.42 | uncertain; review manually |
| [TenantIdentityProviderDisabled](events/tenant-identity-provider.md#tenantidentityproviderdisabled) | TenantIdentityProvider | 0.85 | 0.84 | 0.61 | 0.85 | 0.87 | 0.12 | 0.38 | uncertain; review manually |
| [TenantIdentityProviderTestRequested](events/tenant-identity-provider.md#tenantidentityprovidertestrequested) | TenantIdentityProvider | 0.73 | 0.62 | 0.48 | 0.87 | 0.86 | 0.18 | 0.36 | uncertain; review manually |
| [TenantIdentityProviderTested](events/tenant-identity-provider.md#tenantidentityprovidertested) | TenantIdentityProvider | 0.74 | 0.73 | 0.53 | 0.83 | 0.86 | 0.14 | 0.37 | uncertain; review manually |
| [TenantInvitationMailboxClaimed](events/tenant-invitation-mailbox-claim.md#tenantinvitationmailboxclaimed) | TenantInvitationMailboxClaim | 0.37 | 0.52 | 0.27 | 0.61 | 0.80 | 0.38 | 0.57 | uncertain; review manually |
| [TenantInvitationMailboxReleased](events/tenant-invitation-mailbox-claim.md#tenantinvitationmailboxreleased) | TenantInvitationMailboxClaim | 0.55 | 0.51 | 0.24 | 0.67 | 0.83 | 0.30 | 0.50 | uncertain; review manually |
| [TenantInvitationAccepted](events/tenant-invitation.md#tenantinvitationaccepted) | TenantInvitation | 0.79 | 0.78 | 0.88 | 0.88 | 0.84 | 0.09 | 0.45 | uncertain; review manually |
| [TenantMemberInvited](events/tenant-invitation.md#tenantmemberinvited) | TenantInvitation | 0.71 | 0.69 | 0.80 | 0.84 | 0.72 | 0.11 | 0.61 | uncertain; review manually |
| [TenantManagerRestorationAccepted](events/tenant-manager-guard.md#tenantmanagerrestorationaccepted) | TenantManagerGuard | 0.11 | 0.51 | 0.23 | 0.52 | 0.73 | 0.39 | 0.75 | uncertain; review manually |
| [TenantManagerRestorationCompleted](events/tenant-manager-guard.md#tenantmanagerrestorationcompleted) | TenantManagerGuard | 0.16 | 0.57 | 0.31 | 0.55 | 0.64 | 0.33 | 0.71 | uncertain; review manually |
| [TenantManagerRestored](events/tenant-manager-guard.md#tenantmanagerrestored) | TenantManagerGuard | 0.41 | 0.52 | 0.42 | 0.59 | 0.58 | 0.16 | 0.69 | uncertain; review manually |
| [TenantManagerSourceMutationAdmitted](events/tenant-manager-guard.md#tenantmanagersourcemutationadmitted) | TenantManagerGuard | 0.05 | 0.38 | 0.09 | 0.28 | 0.77 | 0.60 | 0.69 | high-confidence concern |
| [TenantManagerWithdrawalAccepted](events/tenant-manager-guard.md#tenantmanagerwithdrawalaccepted) | TenantManagerGuard | 0.23 | 0.55 | 0.25 | 0.53 | 0.80 | 0.41 | 0.62 | uncertain; review manually |
| [TenantManagerWithdrawalCompleted](events/tenant-manager-guard.md#tenantmanagerwithdrawalcompleted) | TenantManagerGuard | 0.31 | 0.51 | 0.37 | 0.63 | 0.73 | 0.30 | 0.57 | uncertain; review manually |
| [TenantSlugRegistered](events/tenant-slug.md#tenantslugregistered) | TenantSlug | 0.78 | 0.76 | 0.19 | 0.76 | 0.86 | 0.18 | 0.33 | high-confidence concern |
| [TenantSlugRegistrationRejected](events/tenant-slug.md#tenantslugregistrationrejected) | TenantSlug | 0.74 | 0.72 | 0.25 | 0.82 | 0.80 | 0.27 | 0.36 | uncertain; review manually |
| [TenantSlugSurrenderRejected](events/tenant-slug.md#tenantslugsurrenderrejected) | TenantSlug | 0.74 | 0.72 | 0.24 | 0.79 | 0.86 | 0.11 | 0.34 | uncertain; review manually |
| [TenantSlugSurrendered](events/tenant-slug.md#tenantslugsurrendered) | TenantSlug | 0.24 | 0.55 | 0.13 | 0.74 | 0.70 | 0.13 | 0.61 | high-confidence concern |
| [TenantActivatedV2](events/tenant.md#tenantactivatedv2) | Tenant | 0.47 | 0.19 | 0.57 | 0.80 | 0.66 | 0.16 | 0.51 | uncertain; review manually |
| [TenantOffboardingCompleted](events/tenant.md#tenantoffboardingcompleted) | Tenant | 0.29 | 0.59 | 0.69 | 0.85 | 0.71 | 0.10 | 0.68 | uncertain; review manually |
| [TenantOffboardingStartedV2](events/tenant.md#tenantoffboardingstartedv2) | Tenant | 0.78 | 0.74 | 0.57 | 0.89 | 0.86 | 0.11 | 0.37 | uncertain; review manually |
| [TenantReactivatedV2](events/tenant.md#tenantreactivatedv2) | Tenant | 0.81 | 0.78 | 0.74 | 0.90 | 0.87 | 0.10 | 0.31 | uncertain; review manually |
| [TenantRegistered](events/tenant.md#tenantregistered) | Tenant | 0.93 | 0.93 | 0.89 | 0.93 | 0.88 | 0.06 | 0.41 | uncertain; review manually |
| [TenantSlugChangeRejected](events/tenant.md#tenantslugchangerejected) | Tenant | 0.83 | 0.81 | 0.24 | 0.83 | 0.78 | 0.07 | 0.44 | uncertain; review manually |
| [TenantSlugChangeRequested](events/tenant.md#tenantslugchangerequested) | Tenant | 0.82 | 0.81 | 0.22 | 0.85 | 0.88 | 0.07 | 0.31 | uncertain; review manually |
| [TenantSlugChanged](events/tenant.md#tenantslugchanged) | Tenant | 0.41 | 0.52 | 0.24 | 0.71 | 0.69 | 0.07 | 0.65 | uncertain; review manually |
| [TenantSlugConfirmed](events/tenant.md#tenantslugconfirmed) | Tenant | 0.68 | 0.66 | 0.15 | 0.69 | 0.73 | 0.08 | 0.53 | high-confidence concern |
| [TenantSlugRejected](events/tenant.md#tenantslugrejected) | Tenant | 0.78 | 0.77 | 0.16 | 0.76 | 0.71 | 0.06 | 0.48 | high-confidence concern |
| [TenantSlugSurrenderConfirmed](events/tenant.md#tenantslugsurrenderconfirmed) | Tenant | 0.67 | 0.64 | 0.19 | 0.75 | 0.71 | 0.11 | 0.53 | high-confidence concern |
| [TenantSlugSurrenderFailed](events/tenant.md#tenantslugsurrenderfailed) | Tenant | 0.71 | 0.69 | 0.21 | 0.80 | 0.80 | 0.10 | 0.38 | uncertain; review manually |
| [TenantSlugSurrenderRequested](events/tenant.md#tenantslugsurrenderrequested) | Tenant | 0.69 | 0.67 | 0.17 | 0.78 | 0.84 | 0.08 | 0.39 | high-confidence concern |
| [TenantSuspendedV2](events/tenant.md#tenantsuspendedv2) | Tenant | 0.88 | 0.88 | 0.65 | 0.91 | 0.87 | 0.09 | 0.28 | uncertain; review manually |
| [TrainingRequirementDefined](events/training-requirement.md#trainingrequirementdefined) | TrainingRequirement | 0.71 | 0.69 | 0.64 | 0.87 | 0.90 | 0.16 | 0.38 | uncertain; review manually |
| [TypeIIPeriodRangeAuthorized](events/type-ii-observation-calendar.md#typeiiperiodrangeauthorized) | TypeIIObservationCalendar | 0.22 | 0.65 | 0.09 | 0.51 | 0.85 | 0.25 | 0.55 | high-confidence concern |
| [TypeIIPeriodRangeSettled](events/type-ii-observation-calendar.md#typeiiperiodrangesettled) | TypeIIObservationCalendar | 0.18 | 0.67 | 0.09 | 0.45 | 0.86 | 0.30 | 0.56 | high-confidence concern |
| [DueControlOccurrencesGenerated](events/type-ii-period.md#duecontroloccurrencesgenerated) | TypeIIPeriod | 0.14 | 0.64 | 0.26 | 0.58 | 0.56 | 0.48 | 0.59 | uncertain; review manually |
| [ObservationPeriodAmended](events/type-ii-period.md#observationperiodamended) | TypeIIPeriod | 0.75 | 0.72 | 0.74 | 0.84 | 0.78 | 0.10 | 0.49 | uncertain; review manually |
| [ObservationPeriodCloseFrozen](events/type-ii-period.md#observationperiodclosefrozen) | TypeIIPeriod | 0.28 | 0.54 | 0.33 | 0.63 | 0.77 | 0.53 | 0.50 | uncertain; review manually |
| [ObservationPeriodCloseProposed](events/type-ii-period.md#observationperiodcloseproposed) | TypeIIPeriod | 0.78 | 0.77 | 0.70 | 0.84 | 0.71 | 0.13 | 0.46 | uncertain; review manually |
| [ObservationPeriodStarted](events/type-ii-period.md#observationperiodstarted) | TypeIIPeriod | 0.93 | 0.93 | 0.81 | 0.88 | 0.79 | 0.09 | 0.34 | uncertain; review manually |
| [TypeIIPeriodAmendmentAccepted](events/type-ii-period.md#typeiiperiodamendmentaccepted) | TypeIIPeriod | 0.37 | 0.52 | 0.43 | 0.59 | 0.83 | 0.47 | 0.58 | uncertain; review manually |
| [TypeIIPeriodAmendmentCanceled](events/type-ii-period.md#typeiiperiodamendmentcanceled) | TypeIIPeriod | 0.77 | 0.76 | 0.60 | 0.76 | 0.85 | 0.23 | 0.38 | uncertain; review manually |
| [TypeIIPeriodCloseAmended](events/type-ii-period.md#typeiiperiodcloseamended) | TypeIIPeriod | 0.51 | 0.44 | 0.59 | 0.71 | 0.68 | 0.15 | 0.64 | uncertain; review manually |
| [TypeIIPeriodCloseApproved](events/type-ii-period.md#typeiiperiodcloseapproved) | TypeIIPeriod | 0.87 | 0.86 | 0.66 | 0.79 | 0.77 | 0.11 | 0.45 | uncertain; review manually |
| [TypeIIPeriodOpeningAccepted](events/type-ii-period.md#typeiiperiodopeningaccepted) | TypeIIPeriod | 0.54 | 0.48 | 0.29 | 0.56 | 0.85 | 0.49 | 0.44 | uncertain; review manually |
| [TypeIIPeriodOpeningRejected](events/type-ii-period.md#typeiiperiodopeningrejected) | TypeIIPeriod | 0.79 | 0.78 | 0.40 | 0.72 | 0.86 | 0.21 | 0.36 | uncertain; review manually |
| [TypeIIPlanApproved](events/type-ii-plan.md#typeiiplanapproved) | TypeIIPlan | 0.89 | 0.89 | 0.75 | 0.85 | 0.84 | 0.08 | 0.43 | uncertain; review manually |
| [TypeIIPlanProposed](events/type-ii-plan.md#typeiiplanproposed) | TypeIIPlan | 0.58 | 0.55 | 0.60 | 0.78 | 0.85 | 0.12 | 0.51 | uncertain; review manually |
| [TypeIIPlanReviewed](events/type-ii-plan.md#typeiiplanreviewed) | TypeIIPlan | 0.87 | 0.87 | 0.76 | 0.86 | 0.85 | 0.08 | 0.41 | uncertain; review manually |
| [TypeIIPlanRevised](events/type-ii-plan.md#typeiiplanrevised) | TypeIIPlan | 0.58 | 0.52 | 0.67 | 0.83 | 0.84 | 0.21 | 0.49 | uncertain; review manually |
| [RecoveryIdentityProtected](events/user-identity.md#recoveryidentityprotected) | UserIdentity | 0.23 | 0.69 | 0.38 | 0.66 | 0.71 | 0.21 | 0.63 | uncertain; review manually |
| [RecoveryIdentityProtectionSettled](events/user-identity.md#recoveryidentityprotectionsettled) | UserIdentity | 0.15 | 0.69 | 0.29 | 0.61 | 0.74 | 0.29 | 0.68 | uncertain; review manually |
| [UserIdentityAuthenticated](events/user-identity.md#useridentityauthenticated) | UserIdentity | 0.74 | 0.72 | 0.70 | 0.88 | 0.78 | 0.12 | 0.46 | uncertain; review manually |
| [UserIdentityProfileObserved](events/user-identity.md#useridentityprofileobserved) | UserIdentity | 0.23 | 0.49 | 0.26 | 0.61 | 0.75 | 0.26 | 0.65 | uncertain; review manually |
| [UserIdentityRegistered](events/user-identity.md#useridentityregistered) | UserIdentity | 0.70 | 0.70 | 0.71 | 0.82 | 0.85 | 0.10 | 0.51 | uncertain; review manually |
| [UserIdentityRevoked](events/user-identity.md#useridentityrevoked) | UserIdentity | 0.23 | 0.72 | 0.56 | 0.75 | 0.73 | 0.10 | 0.67 | uncertain; review manually |
| [WeeklyWorkDigestDeliveryRecorded](events/weekly-work-digest.md#weeklyworkdigestdeliveryrecorded) | WeeklyWorkDigest | 0.48 | 0.38 | 0.73 | 0.87 | 0.83 | 0.41 | 0.41 | uncertain; review manually |
| [WeeklyWorkDigestDeliveryRequested](events/weekly-work-digest.md#weeklyworkdigestdeliveryrequested) | WeeklyWorkDigest | 0.45 | 0.12 | 0.56 | 0.86 | 0.80 | 0.39 | 0.49 | uncertain; review manually |
| [WeeklyWorkDigestDeliverySuppressed](events/weekly-work-digest.md#weeklyworkdigestdeliverysuppressed) | WeeklyWorkDigest | 0.61 | 0.52 | 0.61 | 0.86 | 0.86 | 0.33 | 0.42 | uncertain; review manually |
| [WeeklyWorkDigestScheduled](events/weekly-work-digest.md#weeklyworkdigestscheduled) | WeeklyWorkDigest | 0.28 | 0.51 | 0.48 | 0.73 | 0.69 | 0.38 | 0.61 | uncertain; review manually |
| [WorkDigestDispatchAttemptStarted](events/work-digest-dispatch.md#workdigestdispatchattemptstarted) | WorkDigestDispatch | 0.03 | 0.36 | 0.33 | 0.59 | 0.68 | 0.74 | 0.62 | uncertain; review manually |
| [WorkDigestDispatchOutcomeRecorded](events/work-digest-dispatch.md#workdigestdispatchoutcomerecorded) | WorkDigestDispatch | 0.07 | 0.07 | 0.36 | 0.62 | 0.71 | 0.64 | 0.65 | uncertain; review manually |
| [WorkDigestDispatchScheduled](events/work-digest-dispatch.md#workdigestdispatchscheduled) | WorkDigestDispatch | 0.07 | 0.06 | 0.31 | 0.45 | 0.64 | 0.71 | 0.70 | uncertain; review manually |
| [WorkDigestUnknownRetryAuthorized](events/work-digest-dispatch.md#workdigestunknownretryauthorized) | WorkDigestDispatch | 0.05 | 0.32 | 0.11 | 0.33 | 0.72 | 0.81 | 0.71 | high-confidence concern |
| [WorkDigestPreferenceRecorded](events/work-digest-preference.md#workdigestpreferencerecorded) | WorkDigestPreference | 0.41 | 0.49 | 0.54 | 0.80 | 0.83 | 0.28 | 0.44 | uncertain; review manually |
| [WorkAssignmentLegacyHistoryImported](events/work-item-assignment.md#workassignmentlegacyhistoryimported) | WorkItemAssignment | 0.28 | 0.39 | 0.34 | 0.64 | 0.80 | 0.50 | 0.45 | uncertain; review manually |
| [WorkItemAssignmentEscalated](events/work-item-assignment.md#workitemassignmentescalated) | WorkItemAssignment | 0.47 | 0.51 | 0.64 | 0.83 | 0.84 | 0.09 | 0.57 | uncertain; review manually |
| [WorkItemAssignmentRecorded](events/work-item-assignment.md#workitemassignmentrecorded) | WorkItemAssignment | 0.32 | 0.61 | 0.74 | 0.86 | 0.76 | 0.25 | 0.55 | uncertain; review manually |
| [WorkRelationshipRecorded](events/work-relationship.md#workrelationshiprecorded) | WorkRelationship | 0.39 | 0.47 | 0.80 | 0.87 | 0.81 | 0.21 | 0.53 | uncertain; review manually |
| [WorkRelationshipRevised](events/work-relationship.md#workrelationshiprevised) | WorkRelationship | 0.57 | 0.52 | 0.74 | 0.89 | 0.85 | 0.09 | 0.45 | uncertain; review manually |
| [WorkforceObservationResolved](events/workforce-observation-resolution.md#workforceobservationresolved) | WorkforceObservationResolution | 0.36 | 0.53 | 0.47 | 0.76 | 0.77 | 0.13 | 0.56 | uncertain; review manually |
| [WorkforceSourceObserved](events/workforce-source-observation.md#workforcesourceobserved) | WorkforceSourceObservation | 0.24 | 0.62 | 0.22 | 0.70 | 0.83 | 0.22 | 0.53 | uncertain; review manually |
| [WorkforceSourceReconciled](events/workforce-source-observation.md#workforcesourcereconciled) | WorkforceSourceObservation | 0.17 | 0.68 | 0.19 | 0.71 | 0.82 | 0.20 | 0.50 | high-confidence concern |

## Source records and method

The request state included each name, its recorded owner/meaning, and the corresponding proposed Markdown section. `documentSectionSha256` and `sourceRecordSha256` on each item bind the judgment to the exact input; each batch also records its state/payload hash, model, usage, and item IDs. The generator is [review-names-jev.mjs](review-names-jev.mjs).

[Placeholder: cross-stakeholder review and any agreed replacement terms are not recorded yet.]
