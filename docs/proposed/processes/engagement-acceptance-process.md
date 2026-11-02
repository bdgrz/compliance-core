# EngagementAcceptanceProcess

- Root source events: ServiceEngagementAcceptanceRequested, ServiceEngagementStaffAssignmentRequested
- Ordered stages: true

| Stage | Receivers | Rule |
| --- | --- | --- |
| acquire | AcquireEngagementServiceEpoch, AuthorizeFirmStaffRecordEngagementUse, AuthorizeFirmProfessionalDutyDesignationEngagementUse, AuthorizeIndependenceRuleCatalogEngagementUse, AuthorizeIndependenceRuleRatificationCatalogEngagementUse, ReservePracticeIndependence | One process acquires every key from the frozen manifest in canonical unsigned UTF8 stream-key order, in final mode, with no upgrades. The receiver list is not an acquisition order; the manifest is. |
| authorize | AuthorizeEngagementPracticeCommitments | Irreversible commit point; requires the complete exact grant set. |
| commit | CommitPracticeIndependence | Permanent walls; survive every later outcome. |
| complete | CompleteServiceEngagementAcceptance, CompleteServiceEngagementStaffAssignment | [Placeholder: no stage-specific rule recorded] |
| settle | ReleaseEngagementServiceEpoch, SettleFirmStaffRecordEngagementUse, SettleFirmProfessionalDutyDesignationEngagementUse, SettleIndependenceRuleCatalogEngagementUse, SettleIndependenceRuleRatificationCatalogEngagementUse, SettlePracticeIndependenceReservation | Temporary grants only; permanent commitments remain. |

Abort commands: RejectServiceEngagementAcceptance, RejectServiceEngagementStaffAssignment

Complete selected process definition: [JSON](../data/processes/engagement-acceptance-process.json).

[Placeholder: timeout, scheduling, and operator recovery details not present in the selected process definition.]
