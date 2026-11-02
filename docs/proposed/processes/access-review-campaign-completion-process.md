# AccessReviewCampaignCompletionProcess

- Root source events: AccessReviewCampaignCompletionAccepted
- Ordered stages: true

| Stage | Receivers | Rule |
| --- | --- | --- |
| acquire | AuthorizeAccessReviewItemCampaignCompletionUse | Canonical order over the drained item set. |
| complete | CompleteAccessReviewCampaignClosure | [Placeholder: no stage-specific rule recorded] |
| settle | SettleAccessReviewItemCampaignCompletionUse | Installs the parent-generation closure fence. |

Abort commands: AbortAccessReviewCampaignCompletion

Complete selected process definition: [JSON](../data/processes/access-review-campaign-completion-process.json).

[Placeholder: timeout, scheduling, and operator recovery details not present in the selected process definition.]
