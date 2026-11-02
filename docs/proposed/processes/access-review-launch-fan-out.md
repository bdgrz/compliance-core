# AccessReviewLaunchFanOut

- Root source events: AccessReviewCampaignLaunched
- Ordered stages: false

| Stage | Receivers | Rule |
| --- | --- | --- |
| materialize | MaterializeAccessReviewItem | One receiver command per item in the launch manifest; items are independent. |

Abort commands: None listed

Complete selected process definition: [JSON](../data/processes/access-review-launch-fan-out.json).

[Placeholder: timeout, scheduling, and operator recovery details not present in the selected process definition.]
