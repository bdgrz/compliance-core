# FirmStaffRecord

- Classification: `business_aggregate`
- Source area: `AccessControl` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant-scoped platform / staffMemberId; `StaffMemberId` on `RegisterFirmStaff` is the caller-supplied immutable creation identity (exact immutable creation retry) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** Immutable canonical user, practice and registration source, plus one staff record active/status revision.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** Canonical user maps to exactly one immutable staff/practice identity; status proofs remain versioned and authoritative.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RegisterFirmStaff](../commands/firm-staff-record.md#registerfirmstaff) | http | StaffMemberId |
| [SetFirmStaffStatus](../commands/firm-staff-record.md#setfirmstaffstatus) | http | CommandId |
| [RejectFirmStaffRegistration](../commands/firm-staff-record.md#rejectfirmstaffregistration) | abort | CommandId |
| [CompleteFirmStaffRegistration](../commands/firm-staff-record.md#completefirmstaffregistration) | receiver | CommandId |
| [AuthorizeFirmStaffRecordEngagement](../commands/firm-staff-record.md#authorizefirmstaffrecordengagement) | receiver | CommandId |
| [CloseFirmStaffRecordEngagementAuthorization](../commands/firm-staff-record.md#closefirmstaffrecordengagementauthorization) | receiver | CommandId |
| [AuthorizeFirmStaffProfessionalDuty](../commands/firm-staff-record.md#authorizefirmstaffprofessionalduty) | receiver | CommandId |
| [CloseFirmStaffProfessionalDutyAuthorization](../commands/firm-staff-record.md#closefirmstaffprofessionaldutyauthorization) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [FirmStaffChangeRecorded](../events/firm-staff-record.md#firmstaffchangerecorded) | bdgrz.firm-staff.changed | 1 |
| [FirmStaffProfessionalDutyAuthorized](../events/firm-staff-record.md#firmstaffprofessionaldutyauthorized) | bdgrz.firm-staff-record.firm-staff-professional-duty-use-authorized | 1 |
| [FirmStaffProfessionalDutyAuthorizationClosed](../events/firm-staff-record.md#firmstaffprofessionaldutyauthorizationclosed) | bdgrz.firm-staff-record.firm-staff-professional-duty-use-settled | 1 |
| [FirmStaffRecordEngagementAuthorized](../events/firm-staff-record.md#firmstaffrecordengagementauthorized) | bdgrz.firm-staff-record.firm-staff-record-engagement-use-authorized | 1 |
| [FirmStaffRecordEngagementAuthorizationClosed](../events/firm-staff-record.md#firmstaffrecordengagementauthorizationclosed) | bdgrz.firm-staff-record.firm-staff-record-engagement-use-settled | 1 |
| [FirmStaffRegistrationAccepted](../events/firm-staff-record.md#firmstaffregistrationaccepted) | bdgrz.firm-staff-record.firm-staff-registration-accepted | 1 |
| [FirmStaffRegistrationRejected](../events/firm-staff-record.md#firmstaffregistrationrejected) | bdgrz.firm-staff-record.firm-staff-registration-rejected | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T002](../reactors/firm-staff-registration-reactor.md#t002) | reaction_or_regular_process_stage | FirmStaffRecord | FirmStaffIdentityClaim | ClaimFirmStaffIdentity |
| [T003](../reactors/firm-staff-registration-reactor.md#t003) | reaction_or_regular_process_stage | FirmStaffIdentityClaim | FirmStaffRecord | CompleteFirmStaffRegistration |
| [T089](../reactors/engagement-acceptance-reactor.md#t089) | reaction_or_regular_process_stage | ServiceEngagementIndependence | FirmStaffRecord | AuthorizeFirmStaffRecordEngagement |
| [T090](../reactors/engagement-acceptance-reactor.md#t090) | reaction_or_regular_process_stage | ServiceEngagementIndependence | FirmStaffRecord | CloseFirmStaffRecordEngagementAuthorization |
| [T113](../reactors/apply-or-revoke-professional-duty-designation-reactor.md#t113) | reaction_or_regular_process_stage | FirmProfessionalDutyDesignation | FirmStaffRecord | AuthorizeFirmStaffProfessionalDuty |
| [T114](../reactors/apply-or-revoke-professional-duty-designation-reactor.md#t114) | reaction_or_regular_process_stage | FirmProfessionalDutyDesignation | FirmStaffRecord | CloseFirmStaffProfessionalDutyAuthorization |
| [S017](../reactors/reassess-firm-staff-independence-after-changes-reactor.md#s017) | business_receiver | FirmStaffRecord | ServiceEngagementIndependence | RecordDirectoryIndependenceReevaluation |
| [A001](../reactors/unassigned-domain-choreography.md#a001) | process_abort_binding | FirmStaffRecord | FirmStaffRecord | RejectFirmStaffRegistration |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/firm-staff-record.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
