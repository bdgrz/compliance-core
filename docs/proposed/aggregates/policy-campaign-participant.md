# PolicyCampaignParticipant

- Classification: `business_aggregate`
- Source area: `PolicyDistribution` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped (campaignId, personId); the identity is established by the admitting owner or reactor named in the choreography, never by a caller request key; mutations bind CommandId to this exact identity.

**Invariant:** Independent acknowledgement, completion, exception and delivery status.

**Additional decision rule:** An acknowledgement records the exact policy version by version and content hash and the exact text shown (the organization's text if an Org Admin replaced the default). Item state is exactly one of pending, acknowledged, overdue, excepted, or removed. New joiners are due within 30 days of start date; others re-acknowledge annually and on each new major version. A mover_out or leaver moves to removed with earlier completions retained.

**Must preserve:** Every result binds the accepted campaign version/hash and included participant; after close, no result accepted.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AcknowledgePolicy](../commands/policy-campaign-participant.md#acknowledgepolicy) | http | CommandId |
| [ApproveCampaignWaiver](../commands/policy-campaign-participant.md#approvecampaignwaiver) | http | CommandId |
| [RecordTrainingCompletion](../commands/policy-campaign-participant.md#recordtrainingcompletion) | http | CommandId |
| [ApplyPolicyParticipantResult](../commands/policy-campaign-participant.md#applypolicyparticipantresult) | receiver | CommandId |
| [AuthorizePolicyCampaignParticipantCampaignClosureUse](../commands/policy-campaign-participant.md#authorizepolicycampaignparticipantcampaignclosureuse) | receiver | CommandId |
| [SettlePolicyCampaignParticipantCampaignClosureUse](../commands/policy-campaign-participant.md#settlepolicycampaignparticipantcampaignclosureuse) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [CampaignWaiverApproved](../events/policy-campaign-participant.md#campaignwaiverapproved) | bdgrz.policy_campaign.waiver.approved | 2 |
| [PolicyAcknowledgementRecorded](../events/policy-campaign-participant.md#policyacknowledgementrecorded) | bdgrz.policy.acknowledgement.recorded | 2 |
| [PolicyCampaignParticipantCampaignClosureUseAuthorized](../events/policy-campaign-participant.md#policycampaignparticipantcampaignclosureuseauthorized) | bdgrz.policy-campaign-participant.policy-campaign-participant-campaign-closure-use-authorized | 1 |
| [PolicyCampaignParticipantCampaignClosureUseSettled](../events/policy-campaign-participant.md#policycampaignparticipantcampaignclosureusesettled) | bdgrz.policy-campaign-participant.policy-campaign-participant-campaign-closure-use-settled | 1 |
| [TrainingCompletionRecorded](../events/policy-campaign-participant.md#trainingcompletionrecorded) | bdgrz.training_completion.recorded | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T067](../reactors/policy-campaign-closure-reactor.md#t067) | reaction_or_regular_process_stage | PolicyDistributionCampaign | PolicyCampaignParticipant | AuthorizePolicyCampaignParticipantCampaignClosureUse |
| [T068](../reactors/policy-campaign-closure-reactor.md#t068) | reaction_or_regular_process_stage | PolicyDistributionCampaign | PolicyCampaignParticipant | SettlePolicyCampaignParticipantCampaignClosureUse |
| [T076](../reactors/policy-participant-result-reactor.md#t076) | reaction_or_regular_process_stage | PolicyDistributionCampaign | PolicyCampaignParticipant | ApplyPolicyParticipantResult |

## Product rules

- **D12-06** — An acknowledgement records that the person read, understood, and agrees to comply with the exact policy version identified by version and content hash, and stores the exact text shown (the organization's text if an Org Admin replaced the default). Source: [m0-d12-policy-acknowledgement-training.md](../../product/decisions/m0-d12-policy-acknowledgement-training.md).
- **D12-08** — An acknowledgement item is in exactly one state of pending, acknowledged, overdue, excepted, or removed. Source: [m0-d12-policy-acknowledgement-training.md](../../product/decisions/m0-d12-policy-acknowledgement-training.md).
- **D12-07** — New joiners must acknowledge within 30 days of their start date; everyone else re-acknowledges annually and whenever a new major policy version is approved. Source: [m0-d12-policy-acknowledgement-training.md](../../product/decisions/m0-d12-policy-acknowledgement-training.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [policy-campaign-participant-review-2026-10-29.md](../../product/definitive-aggregate-choreography/policy-campaign-participant-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/policy-campaign-participant.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
