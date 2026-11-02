# FirmProfessionalDutyDesignation

- Classification: `business_aggregate`
- Source area: `AccessControl` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** platform / (canonicalUserId, duty, nullable tenantScope)

**Invariant:** One active signed duty designation and monotonic signed authority revision per exact subject/duty/scope; retain continuity proof. The platform scope is explicit and is not an empty tenant UUID.

**Additional decision rule:** A firm staff member holds Advisor or Attest only per engagement assignment, never as a standing tenant role, and independence walls constrain any designation; client roles for firm staff require a separate explicit membership grant.

**Must preserve:** Exact predecessor, monotonically increasing authority revision, stable signed subject, valid staff source proof.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordFirmProfessionalDutyDesignation](../commands/firm-professional-duty-designation.md#recordfirmprofessionaldutydesignation) | http | AssertionId |
| [RevokeFirmProfessionalDutyDesignation](../commands/firm-professional-duty-designation.md#revokefirmprofessionaldutydesignation) | http | AssertionId |
| [RejectProfessionalDutyChange](../commands/firm-professional-duty-designation.md#rejectprofessionaldutychange) | abort | CommandId |
| [ApplyProfessionalDutyChange](../commands/firm-professional-duty-designation.md#applyprofessionaldutychange) | receiver | CommandId |
| [AuthorizeFirmProfessionalDutyDesignationEngagementUse](../commands/firm-professional-duty-designation.md#authorizefirmprofessionaldutydesignationengagementuse) | receiver | CommandId |
| [SettleFirmProfessionalDutyDesignationEngagementUse](../commands/firm-professional-duty-designation.md#settlefirmprofessionaldutydesignationengagementuse) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [FirmProfessionalDutyDesignationEngagementUseAuthorized](../events/firm-professional-duty-designation.md#firmprofessionaldutydesignationengagementuseauthorized) | bdgrz.firm-professional-duty-designation.firm-professional-duty-designation-engagement-use-authorized | 1 |
| [FirmProfessionalDutyDesignationEngagementUseSettled](../events/firm-professional-duty-designation.md#firmprofessionaldutydesignationengagementusesettled) | bdgrz.firm-professional-duty-designation.firm-professional-duty-designation-engagement-use-settled | 1 |
| [FirmProfessionalDutyDesignationRecorded](../events/firm-professional-duty-designation.md#firmprofessionaldutydesignationrecorded) | bdgrz.firm.professional-duty.designation-recorded | 1 |
| [FirmProfessionalDutyDesignationRevoked](../events/firm-professional-duty-designation.md#firmprofessionaldutydesignationrevoked) | bdgrz.firm.professional-duty.designation-revoked | 1 |
| [ProfessionalDutyChangeAccepted](../events/firm-professional-duty-designation.md#professionaldutychangeaccepted) | bdgrz.firm-professional-duty-designation.professional-duty-change-accepted | 1 |
| [ProfessionalDutyChangeRejected](../events/firm-professional-duty-designation.md#professionaldutychangerejected) | bdgrz.firm-professional-duty-designation.professional-duty-change-rejected | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T000](../reactors/professional-duty-change-reactor.md#t000) | reaction_or_regular_process_stage | FirmProfessionalDutyDesignation | ProfessionalAssertionIdentityClaim | ClaimProfessionalAssertionIdentity |
| [T001](../reactors/professional-duty-change-reactor.md#t001) | reaction_or_regular_process_stage | ProfessionalAssertionIdentityClaim | FirmProfessionalDutyDesignation | ApplyProfessionalDutyChange |
| [T091](../reactors/engagement-acceptance-reactor.md#t091) | reaction_or_regular_process_stage | ServiceEngagementIndependence | FirmProfessionalDutyDesignation | AuthorizeFirmProfessionalDutyDesignationEngagementUse |
| [T092](../reactors/engagement-acceptance-reactor.md#t092) | reaction_or_regular_process_stage | ServiceEngagementIndependence | FirmProfessionalDutyDesignation | SettleFirmProfessionalDutyDesignationEngagementUse |
| [T113](../reactors/professional-duty-change-reactor.md#t113) | reaction_or_regular_process_stage | FirmProfessionalDutyDesignation | FirmStaffRecord | AuthorizeFirmStaffProfessionalDutyUse |
| [T114](../reactors/professional-duty-change-reactor.md#t114) | reaction_or_regular_process_stage | FirmProfessionalDutyDesignation | FirmStaffRecord | SettleFirmStaffProfessionalDutyUse |
| [A000](../reactors/unassigned-architecture-rows.md#a000) | process_abort_binding | FirmProfessionalDutyDesignation | FirmProfessionalDutyDesignation | RejectProfessionalDutyChange |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [firm-professional-duty-designation-review-2026-10-29.md](../../product/definitive-aggregate-choreography/firm-professional-duty-designation-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/firm-professional-duty-designation.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
