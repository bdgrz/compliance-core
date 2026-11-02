# ReadinessDecisionProcess

- Root source events: ReadinessDecisionAccepted, TypeIEntryDecisionAccepted
- Ordered stages: true

| Stage | Receivers | Rule |
| --- | --- | --- |
| acquire | AuthorizeReadinessGapPlanReadinessDecisionUse | [Placeholder: no stage-specific rule recorded] |
| complete | CompleteReadinessDecision, CompleteTypeIEntryDecision | One of the two, by decision kind. |
| settle | SettleReadinessGapPlanReadinessDecisionUse | [Placeholder: no stage-specific rule recorded] |

Abort commands: AbortReadinessDecision

Complete selected process definition: [JSON](../data/processes/readiness-decision-process.json).

[Placeholder: timeout, scheduling, and operator recovery details not present in the selected process definition.]
