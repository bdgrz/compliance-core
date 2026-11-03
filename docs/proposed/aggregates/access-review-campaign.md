# AccessReviewCampaign

- Classification: `business_governor`
- Source area: `AccessReviews` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant-scoped campaignId; `CampaignId` on `LaunchAccessReviewCampaign` is the caller-supplied immutable creation identity (exact immutable creation retry) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** Frozen campaign snapshot, generation, and compact completion gate.

**Additional decision rule:** Each campaign has its own CampaignId. A second campaign may use the same population snapshot; no rule prohibits overlapping campaigns.

**Must preserve:** Keep reviewer and remediation owner distinct per item unless exact waiver; no stale projected summary may satisfy completion.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** Each campaign keeps its own saved population, completion decision, and closure. Completing one campaign does not close or change another.





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [CompleteAccessReviewCampaign](../commands/access-review-campaign.md#completeaccessreviewcampaign) | http | CommandId |
| [LaunchAccessReviewCampaign](../commands/access-review-campaign.md#launchaccessreviewcampaign) | http | CampaignId |
| [RecordBulkAccessDecision](../commands/access-review-campaign.md#recordbulkaccessdecision) | http | DecisionBatchId |
| [AbortAccessReviewCampaignCompletion](../commands/access-review-campaign.md#abortaccessreviewcampaigncompletion) | abort | CommandId |
| [CompleteAccessReviewCampaignClosure](../commands/access-review-campaign.md#completeaccessreviewcampaignclosure) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [AccessReviewCampaignCompleted](../events/access-review-campaign.md#accessreviewcampaigncompleted) | bdgrz.access_review.campaign.completed | 2 |
| [AccessReviewCampaignCompletionAccepted](../events/access-review-campaign.md#accessreviewcampaigncompletionaccepted) | bdgrz.access-review-campaign.access-review-campaign-completion-accepted | 1 |
| [AccessReviewCampaignCompletionCanceled](../events/access-review-campaign.md#accessreviewcampaigncompletioncanceled) | bdgrz.access-review-campaign.access-review-campaign-completion-canceled | 1 |
| [AccessReviewCampaignLaunched](../events/access-review-campaign.md#accessreviewcampaignlaunched) | bdgrz.access_review.campaign.launched | 2 |
| [AccessReviewDecisionBatchAdmitted](../events/access-review-campaign.md#accessreviewdecisionbatchadmitted) | bdgrz.access-review-campaign.access-review-decision-batch-admitted | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T008](../reactors/open-access-review-work-reactor.md#t008) | reaction_or_regular_process_stage | AccessReviewCampaign | AccessReviewItem | OpenAccessReviewItem |
| [T059](../reactors/access-review-completion-reactor.md#t059) | reaction_or_regular_process_stage | AccessReviewCampaign | AccessReviewItem | AuthorizeAccessReviewItemCampaignCompletion |
| [T060](../reactors/access-review-completion-reactor.md#t060) | reaction_or_regular_process_stage | AccessReviewCampaign | AccessReviewItem | CloseAccessReviewItemCampaignCompletionAuthorization |
| [T073](../reactors/access-review-completion-reactor.md#t073) | reaction_or_regular_process_stage | AccessReviewCampaign | AccessReviewCampaign | CompleteAccessReviewCampaignClosure |
| [T086](../reactors/publish-approved-access-review-decision-reactor.md#t086) | reaction_or_regular_process_stage | AccessReviewCampaign | AccessReviewDecisionBatch | RecordAccessReviewDecisionBatch |
| [A005](../reactors/unassigned-domain-choreography.md#a005) | process_abort_binding | AccessReviewCampaign | AccessReviewDecisionBatch | CancelAccessReviewDecisionBatch |
| [A006](../reactors/unassigned-domain-choreography.md#a006) | process_abort_binding | AccessReviewCampaign | AccessReviewCampaign | AbortAccessReviewCampaignCompletion |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/access-review-campaign.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
