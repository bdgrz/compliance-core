# AccessReviewItem

- Classification: `business_aggregate`
- Source area: `AccessReviews` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant-scoped (campaignId, itemId); the identity is established by the admitting owner or reactor named in the choreography, never by a caller request key; mutations bind CommandId to this exact identity.

**Invariant:** One reviewer decision, reassignment, remediation verification, exception and history for one frozen item. A privileged entitlement item accepts only an individual named reviewer decision; bulk or delegated decisions are refused for privileged access (M0-D07 D07-09; source AccessReviewRules.cs)

**Additional decision rule:** Remediation is verified complete only against a later accepted population snapshot that shows the entitlement removed or changed; a ticket or assertion alone is refused as verification.

**Must preserve:** Keep reviewer and remediation owner distinct per item unless exact waiver; no stale projected summary may satisfy completion.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ExemptAccessRemediation](../commands/access-review-item.md#exemptaccessremediation) | http | CommandId |
| [ReassignAccessReviewResponsibility](../commands/access-review-item.md#reassignaccessreviewresponsibility) | http | CommandId |
| [RecordAccessDecision](../commands/access-review-item.md#recordaccessdecision) | http | CommandId |
| [RecordAccessRemediationChange](../commands/access-review-item.md#recordaccessremediationchange) | http | CommandId |
| [VerifyAccessRemediation](../commands/access-review-item.md#verifyaccessremediation) | http | CommandId |
| [OpenAccessReviewItem](../commands/access-review-item.md#openaccessreviewitem) | receiver | CommandId |
| [ReserveAccessReviewItemDecision](../commands/access-review-item.md#reserveaccessreviewitemdecision) | receiver | CommandId |
| [ApplyAccessReviewItemDecision](../commands/access-review-item.md#applyaccessreviewitemdecision) | receiver | CommandId |
| [CancelAccessReviewItemDecision](../commands/access-review-item.md#cancelaccessreviewitemdecision) | receiver | CommandId |
| [AuthorizeAccessReviewItemCampaignCompletion](../commands/access-review-item.md#authorizeaccessreviewitemcampaigncompletion) | receiver | CommandId |
| [CloseAccessReviewItemCampaignCompletionAuthorization](../commands/access-review-item.md#closeaccessreviewitemcampaigncompletionauthorization) | receiver | CommandId |
| [CloseAccessReviewItemDecisionAuthorization](../commands/access-review-item.md#closeaccessreviewitemdecisionauthorization) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [AccessDecisionsRecorded](../events/access-review-item.md#accessdecisionsrecorded) | bdgrz.access_review.decisions.recorded | 2 |
| [AccessRemediationChangeRecorded](../events/access-review-item.md#accessremediationchangerecorded) | bdgrz.access_review.remediation.change_recorded | 2 |
| [AccessRemediationExceptionRecorded](../events/access-review-item.md#accessremediationexceptionrecorded) | bdgrz.access_review.remediation.exception_recorded | 2 |
| [AccessRemediationVerified](../events/access-review-item.md#accessremediationverified) | bdgrz.access_review.remediation.verified | 2 |
| [AccessReviewItemCampaignCompletionAuthorized](../events/access-review-item.md#accessreviewitemcampaigncompletionauthorized) | bdgrz.access-review-item.access-review-item-campaign-completion-use-authorized | 1 |
| [AccessReviewItemCampaignCompletionAuthorizationClosed](../events/access-review-item.md#accessreviewitemcampaigncompletionauthorizationclosed) | bdgrz.access-review-item.access-review-item-campaign-completion-use-settled | 1 |
| [AccessReviewItemDecisionAuthorized](../events/access-review-item.md#accessreviewitemdecisionauthorized) | bdgrz.access-review-item.access-review-item-decision-authorized | 1 |
| [AccessReviewItemDecisionCanceled](../events/access-review-item.md#accessreviewitemdecisioncanceled) | bdgrz.access-review-item.access-review-item-decision-canceled | 1 |
| [AccessReviewItemDecisionAuthorizationClosed](../events/access-review-item.md#accessreviewitemdecisionauthorizationclosed) | bdgrz.access-review-item.access-review-item-decision-settled | 1 |
| [AccessReviewItemOpened](../events/access-review-item.md#accessreviewitemopened) | bdgrz.access-review-item.access-review-item-opened | 1 |
| [AccessReviewResponsibilityReassigned](../events/access-review-item.md#accessreviewresponsibilityreassigned) | bdgrz.access_review.responsibility.reassigned | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T008](../reactors/open-access-review-work-reactor.md#t008) | reaction_or_regular_process_stage | AccessReviewCampaign | AccessReviewItem | OpenAccessReviewItem |
| [T009](../reactors/publish-approved-access-review-decision-reactor.md#t009) | reaction_or_regular_process_stage | AccessReviewDecisionBatch | AccessReviewItem | ReserveAccessReviewItemDecision |
| [T010](../reactors/publish-approved-access-review-decision-reactor.md#t010) | reaction_or_regular_process_stage | AccessReviewDecisionBatch | AccessReviewItem | ApplyAccessReviewItemDecision |
| [T012](../reactors/cancel-unpublished-access-review-decisions-reactor.md#t012) | reaction_or_regular_process_stage | AccessReviewDecisionBatch | AccessReviewItem | CancelAccessReviewItemDecision |
| [T059](../reactors/access-review-completion-reactor.md#t059) | reaction_or_regular_process_stage | AccessReviewCampaign | AccessReviewItem | AuthorizeAccessReviewItemCampaignCompletion |
| [T060](../reactors/access-review-completion-reactor.md#t060) | reaction_or_regular_process_stage | AccessReviewCampaign | AccessReviewItem | CloseAccessReviewItemCampaignCompletionAuthorization |
| [T080](../reactors/publish-approved-access-review-decision-reactor.md#t080) | reaction_or_regular_process_stage | AccessReviewDecisionBatch | AccessReviewItem | CloseAccessReviewItemDecisionAuthorization |

## Product rules

- **D07-09** — Privileged entitlements require named reviewer approval in each review campaign. Decision: [m0-d07-access-review-population.md](../decisions/m0-d07-access-review-population.md).
- **D07-11** — Access remediation is complete only when a later population snapshot shows the entitlement removed or changed; a ticket alone is not verification. Decision: [m0-d07-access-review-population.md](../decisions/m0-d07-access-review-population.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/access-review-item.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
