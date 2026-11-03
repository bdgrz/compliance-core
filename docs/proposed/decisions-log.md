# Architecture and product decisions

Architecture decisions: **35**. Complete selected records: [decision JSON](data/decisions.json).

| ID | Status | Choice | Rationale |
| --- | --- | --- | --- |
| OWNER_ApplicationSourceClaim | SELECTED_FOR_TARGET_DEFINITION | absorb_in_source_authority | Source-wide exact revision/generation and published-batch ordering already own mutable replace/remove mappings. Explicitly absorb the earlier per-record-root role into ApplicationImportSource; preserve full verified mapping authority, immutable plans and publication gates. |
| OWNER_PlatformOperatorGrant | SELECTED_FOR_TARGET_DEFINITION | one_roster_authority | Keep exact active operator identities/assignment episodes and acting-operator/last-operator checks in one atomic roster decision. Explicitly supersede the earlier per-user split; never reduce authority to a count or cap current membership. |
| OWNER_AuditPopulationRowDecision | SELECTED_FOR_TARGET_DEFINITION | parent_owns_current_reconciliation | Classification, reconciliation invalidation, complete-source proof and freeze seal are one conflict set on AuditPopulation. Immutable row/version bodies are values, not separate owners. Explicitly supersede the earlier row-root proposal; remove lifetime history coupling without splitting the seal. |
| OWNER_AccessExpectationScopeGuard | SELECTED_FOR_TARGET_DEFINITION | line_authority_explicit_scope_override | Required exclusion is predecessor/successor competition within the same expectation lineage. Different lines may coexist, including different policies on one system. Explicitly supersede the older unspecified cross-line governor; adding a future cross-line ban is a separately reviewed product/design change with a precise predicate. |
| EV_D01 | SELECTED_FOR_TARGET_DEFINITION | adopt_recommended | A professional assertion identity binding is permanent and non-recyclable. Exclude the stale release label. |
| EV_D02 | SELECTED_FOR_TARGET_DEFINITION | adopt_recommended | Canonical package delivery is manifest Admitted then independent child Delivered. Exclude redundant Authorized; add no automated send authority. |
| EV_D03 | SELECTED_FOR_TARGET_DEFINITION | adopt_recommended | The selected directed source/type/target UUID identity is the relationship uniqueness boundary. Exclude separate identity claim. |
| EV_D04 | SELECTED_FOR_TARGET_DEFINITION | adopt_recommended | Relationship removal records lifecycle, never releases/recycles canonical tuple identity. Exclude separate identity release. |
| EV_D05 | SELECTED_FOR_TARGET_DEFINITION | adopt_recommended | Recorded is the canonical authored preference decision, including accepted NoChange. ChangedAt advances only for actual value changes. Old Changed v1 remains read-only compatibility, with no replay conversion. |
| ARTIFACT_DOWNLOAD | SELECTED_FOR_TARGET_DEFINITION | artifact_first_202_qualified_grant_v2 | Ratify the selected artifact-first Pending202 protocol: stable caller CommandId, atomic local available-artifact reservation in Requested, artifact-qualified derived receiving grant, ordered source-use acquisition, encrypted original ticket before native issuance, terminal settlement/cancellation fencing, current original-actor scoped read, unchanged original expiry, and receiving-issued v2 preserving old artifact-issued v1 decoder. |
| PROOF_CARRIERS | SELECTED_FOR_TARGET_DEFINITION | admission_reference_and_verified_stage_proofs | Registered root subscription, logical prerequisite fact and continuation carrier are separate. Canonical continuation retains original admission reference and root-derived identity, but receiver must materialize/verify every required typed intermediate/terminal proof and exact immutable intent before effect or early replay where binding does not cover terms. A bare NativeFactReference never grants authority. Selected typed-source tables are corrected to this normative admission-plus-prerequisite model; installed carrier presence is not proof of conformance. |
| EFFECT_IDENTITIES | SELECTED_FOR_TARGET_DEFINITION | retain_occupied_v1_version_new_algorithm | Retain exact occupied v1 effect UUID byte algorithm and each process frozen target-key/episode selection. Do not silently replace IDs. Explicitly version and domain-separate any new length-prefixed algorithm; new or ambiguous target/episode domains require a selected injective encoding and compatibility/fencing before use. |
| PROGRAM_ROLLOVER_EXECUTION | SELECTED_FOR_TARGET_DEFINITION | atomic_claim_private_human_governed_process | Atomically claim the exact approved version and actual approval tuple, including pinned NextExaminationId/NextTypeIIPlanId/NextPeriodId and dates. The private process coordinates existing readiness, examination, Type I baseline/auditor outcome/system description, TypeIIPlan review/approval and TypeIIPeriod start owners. It never synthesizes or owns their facts. A human-authored complete plan exhaustively maps every rollover item. Opening acceptance remains pending; actual ObservationPeriodStarted is terminal. Retire ProgramNextPeriodCreated from selected business emissions. |
| OWNER_ApplicationNameClaim | SELECTED_FOR_TARGET_DEFINITION | add_owner | Tenant-wide application name/alias uniqueness needs an authoritative claim owner like the other inventories; the per-application owner and the directory projection cannot decide it. |
| OWNER_WorkDigestPreference_Technical | SELECTED_FOR_TARGET_DEFINITION | reclassify_technical | A settings holder with no multi-part business invariant; moved to the technical state owners. Emission retained. |
| EV-D06 | SELECTED_FOR_TARGET_DEFINITION | reservation_fact_on_owner_stream | Use-authorized/settled and similar hold facts stay on the owner stream because the reducer must refuse conflicting commands while the hold exists; they are labelled a distinct reservation kind, not business decisions. |
| EV-D07 | SELECTED_FOR_TARGET_DEFINITION | add_planned_retirement_facts | Provider and OperationalProcess retirement are distinct terminal decisions that consumers must recognise without reading content; planned ProviderRetired and OperationalProcessRetired. |
| EV-D08 | SELECTED_FOR_TARGET_DEFINITION | keep_per_child_admission | PolicyDistributionCampaign keeps per-participant result admission on its stream; growth is bounded by the campaign audience and life and is accepted. |
| EV-D09 | SELECTED_FOR_TARGET_DEFINITION | refusals_retained_as_facts | The thirteen Rejected/Failed events remain business facts (eleven at 0.78 or higher). TenantSlugRegistrationRejected (0.61) and TenantSlugSurrenderRejected (0.58) are retained but flagged for review. |
| RULE_D26_OWNERS | SELECTED_FOR_TARGET_DEFINITION | assign_independence_rules | D26-05 to IndependenceRuleCatalog, D26-07 to ServiceEngagementIndependence, D26-03 to ClientServiceIndependence (moderate), D26-06 to ClientIndependenceDecision (moderate); D26-04 remains open. |
| OPEN_ResponseReceiptsInPayload | OPEN | null (recorded) | Whether FirstResponse/OperationResult belong inside the business event or in an operation receipt keyed by command identity is unsettled (0.57 vs 0.39, confidence 0.35). Revision-in-payload as metadata 0.68. No contract change adopted. |
| OPEN_CriteriaEditionOwner | OPEN | null (recorded) | Whether a platform CriteriaEdition owner is needed for D02-01 is unsettled (0.62 at confidence 0.43). |
| OPEN_LifecycleFactsOtherInventories | OPEN | null (recorded) | Distinct lifecycle facts for ServiceIdentity (0.67), Person (0.65), WorkRelationship, Location, DataFlow (0.60), TechnologyComponent (0.56), InformationAsset (0.54) are unsettled; generic revision facts stay. |
| OPEN_ClientExportClassification | OPEN | null (recorded) | ClientExport reads as governor 0.50 / technical 0.38; classification unchanged. |
| OWNER_CommitmentControlLink | SELECTED_FOR_TARGET_DEFINITION | add_owner | Versioned many-to-many commitment/control links need their own owner, mirroring RiskTreatmentAssertion. |
| OWNER_ImportedOwnerResolution | SELECTED_FOR_TARGET_DEFINITION | add_owner | Exact-email match at import and attributed manual resolution are one write decision with no existing owner. |
| RULE_D26-04 | SELECTED_FOR_TARGET_DEFINITION | two_step | ClientIndependenceDecision classifies conditionally_compatible; ClientServiceIndependence refuses acceptance admission without a referencing partner evaluation. |
| RULE_D16-15 | SELECTED_FOR_TARGET_DEFINITION | ArtifactEngagementHold | Active holds block disposition at offboarding; the hold owner decides it. |
| OPEN_CampaignNaturalKey | OPEN | null (recorded) | Whether a second open PolicyDistributionCampaign for the same policy version and roster snapshot is an error is leaning yes (0.65); if so Jev prefers an active-campaign claim owner (0.72). Access review campaign overlap is unsettled (0.49); duplicate legal holds leaning error (0.63). No owner added. |
| CMD_ConcurrencyGuards | SELECTED_FOR_TARGET_DEFINITION | per_command | 41 CommandId-identified mutations without ExpectedRevision were classified: adopted at confidence 0.7 or higher ({'creation_key_only': 5, 'terminal_idempotent_no_guard': 5, 'require_expected_revision': 4}); 27 remain unresolved and keep their current fields. |
| CHOREO_PlannedEdges | SELECTED_FOR_TARGET_DEFINITION | add_planned_edges | Ten planned reaction rows X000-X009 for the planned owners and facts and for the pass F reactions Jev rated above 0.6 with a confident counterpart; receiver guards from the October 23 reactor contracts lifted onto 113 edges as their prerequisite rule. |
| CHOREO_RecoveryContractCases | SELECTED_FOR_TARGET_DEFINITION | state_four_cases | The one-sentence recovery contract on every row is stated as four named cases (lost acknowledgement, retry after partial progress, concurrency conflict, abort of the admitted root) with unchanged meaning; calibration on five rows moved Jev recovery_complete from 0.14-0.41 to 0.94-0.98. 121 rows still have no edge-specific stage proof set and are marked stageProofStatus=to_be_specified. |
| TENANT_Lifecycle | SELECTED_FOR_TARGET_DEFINITION | explicit_state_machine | The Tenant owner decides suspension, reactivation and offboarding (0.89); the invariant now states the legal transitions (0.93). |
| CHOREO_X008_ProofRead | SELECTED_FOR_TARGET_DEFINITION | proof_read_at_signoff | The signed letter is a stage proof the SignOffExamination command verifies; no reactor or availability fact. |
| CMD_AuditObservationPlanned | SELECTED_FOR_TARGET_DEFINITION | planned_from_aggregate_methods | Four planned command contracts derived from the existing aggregate methods so the owner is no longer command-less. |

The source catalog retains some older OPEN labels. Their proposed write-model disposition is recorded in [Write model domain rules](write-model-domain-rules.md).

Product decision records remain the individual authority:

- [m0-d01-program-targets.md](../product/decisions/m0-d01-program-targets.md)
- [m0-d02-criteria-content.md](../product/decisions/m0-d02-criteria-content.md)
- [m0-d03-roles-and-separation-of-duties.md](../product/decisions/m0-d03-roles-and-separation-of-duties.md)
- [m0-d04-readiness-material.md](../product/decisions/m0-d04-readiness-material.md)
- [m0-d05-application-inventory.md](../product/decisions/m0-d05-application-inventory.md)
- [m0-d06-workforce-source.md](../product/decisions/m0-d06-workforce-source.md)
- [m0-d07-access-review-population.md](../product/decisions/m0-d07-access-review-population.md)
- [m0-d08-technology-inventory.md](../product/decisions/m0-d08-technology-inventory.md)
- [m0-d09-commitments.md](../product/decisions/m0-d09-commitments.md)
- [m0-d10-risk-method.md](../product/decisions/m0-d10-risk-method.md)
- [m0-d11-vendors.md](../product/decisions/m0-d11-vendors.md)
- [m0-d12-policy-acknowledgement-training.md](../product/decisions/m0-d12-policy-acknowledgement-training.md)
- [m0-d13-control-evaluation.md](../product/decisions/m0-d13-control-evaluation.md)
- [m0-d14-advisor-collaboration.md](../product/decisions/m0-d14-advisor-collaboration.md)
- [m0-d15-work-queue.md](../product/decisions/m0-d15-work-queue.md)
- [m0-d16-evidence-handling.md](../product/decisions/m0-d16-evidence-handling.md)
- [m0-d17-audit-deliverables.md](../product/decisions/m0-d17-audit-deliverables.md)
- [m0-d18-success-measures.md](../product/decisions/m0-d18-success-measures.md)
- [m0-d19-periodic-reviews.md](../product/decisions/m0-d19-periodic-reviews.md)
- [m0-d20-integration-sources.md](../product/decisions/m0-d20-integration-sources.md)
- [m0-d21-significant-change.md](../product/decisions/m0-d21-significant-change.md)
- [m0-d22-canonical-ownership.md](../product/decisions/m0-d22-canonical-ownership.md)
- [m0-d23-assurance-vocabulary.md](../product/decisions/m0-d23-assurance-vocabulary.md)
- [m0-d24-accessibility-and-browsers.md](../product/decisions/m0-d24-accessibility-and-browsers.md)
- [m0-d25-client-tenancy.md](../product/decisions/m0-d25-client-tenancy.md)
- [m0-d26-independence.md](../product/decisions/m0-d26-independence.md)
- [m0-d27-attest-scope.md](../product/decisions/m0-d27-attest-scope.md)
- [member-deprovisioning.md](../product/decisions/member-deprovisioning.md)
- [r2-access-review-campaign-routing.md](../product/decisions/r2-access-review-campaign-routing.md)
- [r2-weekly-work-digest-delivery.md](../product/decisions/r2-weekly-work-digest-delivery.md)
- [restricted-application-visibility.md](../product/decisions/restricted-application-visibility.md)
- [restricted-technology-inventory-visibility.md](../product/decisions/restricted-technology-inventory-visibility.md)
