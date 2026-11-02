# EngagementPersonalDecisionProcess

- Root source events: EngagementPersonalDecisionAdmitted
- Ordered stages: true

| Stage | Receivers | Rule |
| --- | --- | --- |
| acquire | AuthorizeClientServicePersonalDecisionUse, AuthorizeFirmStaffRecordEngagementUse, AuthorizeFirmProfessionalDutyDesignationEngagementUse, AuthorizeIndependenceRuleCatalogEngagementUse, AuthorizeIndependenceRuleRatificationCatalogEngagementUse | Canonical key order from the frozen RequiredSourceUses manifest; management acknowledgement acquires only the uses its kind requires. |
| authorize | AuthorizeEngagementPersonalDecision | Irreversible; cancellation is only possible before this fact. |
| materialize | MaterializeEngagementManagementAcknowledgement, MaterializePartnerIndependenceDecision | [Placeholder: no stage-specific rule recorded] |
| settle | SettleClientServicePersonalDecisionUse, SettleFirmStaffRecordEngagementUse, SettleFirmProfessionalDutyDesignationEngagementUse, SettleIndependenceRuleCatalogEngagementUse, SettleIndependenceRuleRatificationCatalogEngagementUse, SettleEngagementPersonalDecision | [Placeholder: no stage-specific rule recorded] |

Abort commands: CancelEngagementPersonalDecision

Complete selected process definition: [JSON](../data/processes/engagement-personal-decision-process.json).

[Placeholder: timeout, scheduling, and operator recovery details not present in the selected process definition.]
