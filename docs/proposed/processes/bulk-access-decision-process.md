# BulkAccessDecisionProcess

- Root source events: AccessReviewDecisionBatchAdmitted
- Ordered stages: true

| Stage | Receivers | Rule |
| --- | --- | --- |
| materialize | MaterializeAccessReviewDecisionBatch | [Placeholder: no stage-specific rule recorded] |
| reserve | ReserveAccessReviewItemDecision | All selected items in canonical order before any apply. |
| apply | ApplyAccessReviewItemDecision | All items; provisional and hidden. |
| publish | PublishAccessReviewDecisionBatch | One publication fact. |
| settle | SettleAccessReviewItemDecision | [Placeholder: no stage-specific rule recorded] |

Abort commands: CancelAccessReviewDecisionBatch

Complete selected process definition: [JSON](../data/processes/bulk-access-decision-process.json).

[Placeholder: timeout, scheduling, and operator recovery details not present in the selected process definition.]
