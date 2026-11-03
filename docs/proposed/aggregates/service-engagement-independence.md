# ServiceEngagementIndependence

- Classification: `business_aggregate`
- Source area: `AccessControl` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant-scoped tenant / engagementId; `EngagementId` on `CreateServiceEngagement` is the caller-supplied immutable creation identity (exact immutable creation retry) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** One engagement lifecycle, proposed and actual staff assignments, current content revision and accepted epoch bindings. Engagement revision and service epoch live in state; historical epoch streams cannot compete independently. An impaired independence evaluation result blocks acceptance of the attest engagement with no override (M0-D26 D26-07; Jev 0.81)

**Additional decision rule:** Advisor and Attest access exists only through assignment to an accepted ServiceEngagement for that client, and no person may hold both an Advisor and an Attest assignment for the same client ever, checked over their full assignment history. An impaired evaluation blocks acceptance with no override, and a firm ServiceEngagement and a client AuditEngagement are distinct records, each reference naming its kind and tenant.

**Must preserve:** Never reactivate a permanent independence commitment from reevaluation; acceptance binds exact service, draft, acknowledgement and ratified-rule proofs.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AcceptServiceEngagement](../commands/service-engagement-independence.md#acceptserviceengagement) | http | CommandId |
| [AmendServiceEngagement](../commands/service-engagement-independence.md#amendserviceengagement) | http | CommandId |
| [AssignServiceEngagementStaff](../commands/service-engagement-independence.md#assignserviceengagementstaff) | http | CommandId |
| [CloseServiceEngagement](../commands/service-engagement-independence.md#closeserviceengagement) | http | CommandId |
| [CreateServiceEngagement](../commands/service-engagement-independence.md#createserviceengagement) | http | EngagementId |
| [ProposeServiceEngagementStaff](../commands/service-engagement-independence.md#proposeserviceengagementstaff) | http | CommandId |
| [RecordDirectoryIndependenceReevaluation](../commands/service-engagement-independence.md#recorddirectoryindependencereevaluation) | command | CommandId |
| [RevokeServiceEngagementActualStaff](../commands/service-engagement-independence.md#revokeserviceengagementactualstaff) | http | CommandId |
| [WithdrawServiceEngagementStaffProposal](../commands/service-engagement-independence.md#withdrawserviceengagementstaffproposal) | http | CommandId |
| [RejectServiceEngagementAcceptance](../commands/service-engagement-independence.md#rejectserviceengagementacceptance) | abort | CommandId |
| [RejectServiceEngagementStaffAssignment](../commands/service-engagement-independence.md#rejectserviceengagementstaffassignment) | abort | CommandId |
| [CancelEngagementPersonalDecision](../commands/service-engagement-independence.md#cancelengagementpersonaldecision) | abort | CommandId |
| [CompleteServiceEngagementAcceptance](../commands/service-engagement-independence.md#completeserviceengagementacceptance) | receiver | CommandId |
| [CompleteServiceEngagementStaffAssignment](../commands/service-engagement-independence.md#completeserviceengagementstaffassignment) | receiver | CommandId |
| [AuthorizeEngagementPracticeCommitments](../commands/service-engagement-independence.md#authorizeengagementpracticecommitments) | receiver | CommandId |
| [RecordEngagementPersonalDecision](../commands/service-engagement-independence.md#recordengagementpersonaldecision) | receiver | CommandId |
| [AuthorizeEngagementPersonalDecision](../commands/service-engagement-independence.md#authorizeengagementpersonaldecision) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [AssignmentIndependenceReevaluated](../events/service-engagement-independence.md#assignmentindependencereevaluated) | bdgrz.independence.assignment-reevaluated | 1 |
| [DirectoryIndependenceReevaluated](../events/service-engagement-independence.md#directoryindependencereevaluated) | bdgrz.independence.directory-reevaluated | 1 |
| [EngagementPersonalDecisionAdmitted](../events/service-engagement-independence.md#engagementpersonaldecisionadmitted) | bdgrz.service-engagement-independence.engagement-personal-decision-admitted | 1 |
| [EngagementPersonalDecisionAuthorized](../events/service-engagement-independence.md#engagementpersonaldecisionauthorized) | bdgrz.service-engagement-independence.engagement-personal-decision-authorized | 1 |
| [EngagementPersonalDecisionCanceled](../events/service-engagement-independence.md#engagementpersonaldecisioncanceled) | bdgrz.service-engagement-independence.engagement-personal-decision-canceled | 1 |
| [EngagementPersonalDecisionRecorded](../events/service-engagement-independence.md#engagementpersonaldecisionrecorded) | bdgrz.service-engagement-independence.engagement-personal-decision-materialized | 1 |
| [EngagementPracticeCommitmentsAuthorized](../events/service-engagement-independence.md#engagementpracticecommitmentsauthorized) | bdgrz.service-engagement-independence.engagement-practice-commitments-authorized | 1 |
| [ServiceEngagementAcceptanceRecorded](../events/service-engagement-independence.md#serviceengagementacceptancerecorded) | bdgrz.service-engagement.acceptance-recorded | 1 |
| [ServiceEngagementAcceptanceRejected](../events/service-engagement-independence.md#serviceengagementacceptancerejected) | bdgrz.service-engagement-independence.service-engagement-acceptance-rejected | 1 |
| [ServiceEngagementAcceptanceRequested](../events/service-engagement-independence.md#serviceengagementacceptancerequested) | bdgrz.service-engagement-independence.service-engagement-acceptance-requested | 1 |
| [ServiceEngagementAssignmentRevoked](../events/service-engagement-independence.md#serviceengagementassignmentrevoked) | bdgrz.service-engagement.assignment-revoked | 1 |
| [ServiceEngagementMutationRecorded](../events/service-engagement-independence.md#serviceengagementmutationrecorded) | bdgrz.service-engagement.changed | 1 |
| [ServiceEngagementStaffAssigned](../events/service-engagement-independence.md#serviceengagementstaffassigned) | bdgrz.access-control.service-engagement.staff-assigned | 1 |
| [ServiceEngagementStaffAssignmentRejected](../events/service-engagement-independence.md#serviceengagementstaffassignmentrejected) | bdgrz.service-engagement-independence.service-engagement-staff-assignment-rejected | 1 |
| [ServiceEngagementStaffAssignmentRequested](../events/service-engagement-independence.md#serviceengagementstaffassignmentrequested) | bdgrz.service-engagement-independence.service-engagement-staff-assignment-requested | 1 |
| [ServiceIndependenceReevaluated](../events/service-engagement-independence.md#serviceindependencereevaluated) | bdgrz.independence.service-reevaluated | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T004](../reactors/engagement-acceptance-reactor.md#t004) | reaction_or_regular_process_stage | ServiceEngagementIndependence | ClientServiceIndependence | ReserveEngagementServiceHistory |
| [T005](../reactors/engagement-acceptance-reactor.md#t005) | reaction_or_regular_process_stage | ServiceEngagementIndependence | PracticeIndependenceCommitment | CommitPracticeIndependence |
| [T006](../reactors/engagement-acceptance-reactor.md#t006) | reaction_or_regular_process_stage | ServiceEngagementIndependence | ServiceEngagementIndependence | CompleteServiceEngagementAcceptance |
| [T007](../reactors/engagement-acceptance-reactor.md#t007) | reaction_or_regular_process_stage | ServiceEngagementIndependence | ClientServiceIndependence | ReleaseEngagementServiceHistoryReservation |
| [T082](../reactors/engagement-acceptance-reactor.md#t082) | reaction_or_regular_process_stage | ServiceEngagementIndependence | ServiceEngagementIndependence | CompleteServiceEngagementStaffAssignment |
| [T083](../reactors/engagement-acceptance-reactor.md#t083) | reaction_or_regular_process_stage | ServiceEngagementIndependence | PracticeIndependenceCommitment | ReservePracticeIndependence |
| [T084](../reactors/engagement-acceptance-reactor.md#t084) | reaction_or_regular_process_stage | ServiceEngagementIndependence | ServiceEngagementIndependence | AuthorizeEngagementPracticeCommitments |
| [T085](../reactors/engagement-acceptance-reactor.md#t085) | reaction_or_regular_process_stage | ServiceEngagementIndependence | PracticeIndependenceCommitment | ClosePracticeIndependenceReservation |
| [T089](../reactors/engagement-acceptance-reactor.md#t089) | reaction_or_regular_process_stage | ServiceEngagementIndependence | FirmStaffRecord | AuthorizeFirmStaffRecordEngagement |
| [T090](../reactors/engagement-acceptance-reactor.md#t090) | reaction_or_regular_process_stage | ServiceEngagementIndependence | FirmStaffRecord | CloseFirmStaffRecordEngagementAuthorization |
| [T091](../reactors/engagement-acceptance-reactor.md#t091) | reaction_or_regular_process_stage | ServiceEngagementIndependence | FirmProfessionalDutyDesignation | AuthorizeFirmProfessionalDutyDesignationEngagement |
| [T092](../reactors/engagement-acceptance-reactor.md#t092) | reaction_or_regular_process_stage | ServiceEngagementIndependence | FirmProfessionalDutyDesignation | CloseFirmProfessionalDutyDesignationEngagementAuthorization |
| [T093](../reactors/engagement-acceptance-reactor.md#t093) | reaction_or_regular_process_stage | ServiceEngagementIndependence | IndependenceRuleCatalog | AuthorizeIndependenceRuleCatalogEngagement |
| [T094](../reactors/engagement-acceptance-reactor.md#t094) | reaction_or_regular_process_stage | ServiceEngagementIndependence | IndependenceRuleCatalog | CloseIndependenceRuleCatalogEngagementAuthorization |
| [T095](../reactors/engagement-acceptance-reactor.md#t095) | reaction_or_regular_process_stage | ServiceEngagementIndependence | IndependenceRuleRatificationCatalog | AuthorizeIndependenceRuleRatificationCatalogEngagement |
| [T096](../reactors/engagement-acceptance-reactor.md#t096) | reaction_or_regular_process_stage | ServiceEngagementIndependence | IndependenceRuleRatificationCatalog | CloseIndependenceRuleRatificationCatalogEngagementAuthorization |
| [T107](../reactors/record-engagement-independence-decisions-reactor.md#t107) | reaction_or_regular_process_stage | ServiceEngagementIndependence | ClientServiceIndependence | AuthorizeClientServicePersonalDecision |
| [T108](../reactors/record-engagement-independence-decisions-reactor.md#t108) | reaction_or_regular_process_stage | ServiceEngagementIndependence | EngagementManagementAcknowledgement | RecordEngagementManagementAcknowledgement |
| [T109](../reactors/record-engagement-independence-decisions-reactor.md#t109) | reaction_or_regular_process_stage | ServiceEngagementIndependence | PartnerIndependenceDecision | RecordPartnerIndependenceEvaluation |
| [T110](../reactors/record-engagement-independence-decisions-reactor.md#t110) | reaction_or_regular_process_stage | EngagementManagementAcknowledgement\|PartnerIndependenceDecision | ServiceEngagementIndependence | RecordEngagementPersonalDecision |
| [T111](../reactors/record-engagement-independence-decisions-reactor.md#t111) | reaction_or_regular_process_stage | ServiceEngagementIndependence | ClientServiceIndependence | CloseClientServicePersonalDecisionAuthorization |
| [T112](../reactors/record-engagement-independence-decisions-reactor.md#t112) | reaction_or_regular_process_stage | ServiceEngagementIndependence | ServiceEngagementIndependence | AuthorizeEngagementPersonalDecision |
| [S017](../reactors/reassess-firm-staff-independence-after-changes-reactor.md#s017) | business_receiver | FirmStaffRecord | ServiceEngagementIndependence | RecordDirectoryIndependenceReevaluation |
| [S018](../reactors/reassess-staff-independence-after-changes-reactor.md#s018) | business_receiver | ServiceEngagementIndependence | ServiceEngagementIndependence | RecordDirectoryIndependenceReevaluation |
| [A002](../reactors/unassigned-domain-choreography.md#a002) | process_abort_binding | ServiceEngagementIndependence | ServiceEngagementIndependence | RejectServiceEngagementAcceptance |
| [A003](../reactors/unassigned-domain-choreography.md#a003) | process_abort_binding | ServiceEngagementIndependence | ServiceEngagementIndependence | RejectServiceEngagementStaffAssignment |
| [A004](../reactors/unassigned-domain-choreography.md#a004) | process_abort_binding | ServiceEngagementIndependence | ServiceEngagementIndependence | CancelEngagementPersonalDecision |

## Product rules

- **D03-04** — Advisor and Attest roles are granted only through assignment to an accepted ServiceEngagement and never as a standing organization-wide grant. Decision: [m0-d03-roles-and-separation-of-duties.md](../decisions/m0-d03-roles-and-separation-of-duties.md).
- **D23-12** — A firm ServiceEngagement and a client AuditEngagement are different records; references must name the kind and tenant and neither implies the other's authorization. Decision: [m0-d23-assurance-vocabulary.md](../decisions/m0-d23-assurance-vocabulary.md).
- **D26-01** — No person may hold both an Advisor and an Attest assignment for the same client organization, at the same time or ever; the check covers the person's full assignment history for that client. Decision: [m0-d26-independence.md](../decisions/m0-d26-independence.md).
- **D26-07** — An impaired IndependenceEvaluation result blocks acceptance of the attest engagement with no override. Decision: [m0-d26-independence.md](../decisions/m0-d26-independence.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/service-engagement-independence.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
