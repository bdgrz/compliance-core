# PolicyDistributionCampaign

- Classification: `business_aggregate`
- Source area: `PolicyDistribution` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped campaignId; `CampaignId` on `LaunchPolicyCampaign` is the caller-supplied immutable creation identity (exact immutable creation retry) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** Immutable policy/material version and frozen audience plus closure generation. The campaign admits every participant result against its frozen audience, so its stream grows with the audience for the campaign's bounded life; this is accepted (Jev keep_per_child_admission 1.00) and must not be reduced to a projection check

**Additional decision rule:** Audience is always workforce people from the roster, never platform members and never inferred from application access, team membership, or IdP. core_security is every in-scope employee and contractor with access to in-scope systems or data; role_targeted is people whose roster team is named on the version. Roster changes add attributed amendments (person, reason joiner/mover_in/mover_out/leaver, roster observation, time); launch audience plus amendments is the evidence population, joiners due within 30 days of start, and a leaver moves to removed in open campaigns keeping history.

**Must preserve:** Every result binds the accepted campaign version/hash and included participant; after close, no result accepted.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [CloseCampaign](../commands/policy-distribution-campaign.md#closecampaign) | http | CommandId |
| [LaunchPolicyCampaign](../commands/policy-distribution-campaign.md#launchpolicycampaign) | http | CampaignId |
| [LaunchTrainingCampaign](../commands/policy-distribution-campaign.md#launchtrainingcampaign) | http | CampaignId |
| [ReconcileCampaignAudience](../commands/policy-distribution-campaign.md#reconcilecampaignaudience) | http | CommandId |
| [AbortPolicyCampaignClosure](../commands/policy-distribution-campaign.md#abortpolicycampaignclosure) | abort | CommandId |
| [CompletePolicyCampaignClosure](../commands/policy-distribution-campaign.md#completepolicycampaignclosure) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [PolicyCampaignAudienceAmended](../events/policy-distribution-campaign.md#policycampaignaudienceamended) | bdgrz.policy_campaign.audience.amended | 2 |
| [PolicyCampaignAudienceFrozen](../events/policy-distribution-campaign.md#policycampaignaudiencefrozen) | bdgrz.policy_campaign.audience.frozen | 2 |
| [PolicyCampaignClosed](../events/policy-distribution-campaign.md#policycampaignclosed) | bdgrz.policy_campaign.closed | 2 |
| [PolicyCampaignClosureAccepted](../events/policy-distribution-campaign.md#policycampaignclosureaccepted) | bdgrz.policy-distribution-campaign.policy-campaign-closure-accepted | 1 |
| [PolicyCampaignClosureCanceled](../events/policy-distribution-campaign.md#policycampaignclosurecanceled) | bdgrz.policy-distribution-campaign.policy-campaign-closure-canceled | 1 |
| [PolicyCampaignLaunched](../events/policy-distribution-campaign.md#policycampaignlaunched) | bdgrz.policy_campaign.launched | 2 |
| [PolicyParticipantResultAdmitted](../events/policy-distribution-campaign.md#policyparticipantresultadmitted) | bdgrz.policy-distribution-campaign.policy-participant-result-admitted | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T038](../reactors/policy-campaign-closure-reactor.md#t038) | reaction_or_regular_process_stage | PolicyDistributionCampaign | PolicyDistributionCampaign | CompletePolicyCampaignClosure |
| [T067](../reactors/policy-campaign-closure-reactor.md#t067) | reaction_or_regular_process_stage | PolicyDistributionCampaign | PolicyCampaignParticipant | AuthorizePolicyCampaignParticipantCampaignClosureUse |
| [T068](../reactors/policy-campaign-closure-reactor.md#t068) | reaction_or_regular_process_stage | PolicyDistributionCampaign | PolicyCampaignParticipant | SettlePolicyCampaignParticipantCampaignClosureUse |
| [T076](../reactors/policy-participant-result-reactor.md#t076) | reaction_or_regular_process_stage | PolicyDistributionCampaign | PolicyCampaignParticipant | ApplyPolicyParticipantResult |
| [A010](../reactors/unassigned-architecture-rows.md#a010) | process_abort_binding | PolicyDistributionCampaign | PolicyDistributionCampaign | AbortPolicyCampaignClosure |

## Product rules

- **D12-01** — Policy audiences are always sets of workforce people from the roster, never platform members, and are never inferred from application access, platform team membership, or the IdP. Source: [m0-d12-policy-acknowledgement-training.md](../../product/decisions/m0-d12-policy-acknowledgement-training.md).
- **D12-03** — A core_security audience is every in-scope workforce person (employees and contractors with access to in-scope systems or data); a role_targeted audience is workforce people whose roster team is named on the policy version. Source: [m0-d12-policy-acknowledgement-training.md](../../product/decisions/m0-d12-policy-acknowledgement-training.md).
- **D12-04** — A PolicyDistributionCampaign freezes its launch audience by evaluating the audience rule against the roster at launch, and later roster changes never edit the frozen audience. Source: [m0-d12-policy-acknowledgement-training.md](../../product/decisions/m0-d12-policy-acknowledgement-training.md).
- **D12-05** — Roster changes during a campaign add attributed audience amendment entries (person, reason joiner/mover_in/mover_out/leaver, roster observation, time); the launch audience plus amendments is the evidence population. Source: [m0-d12-policy-acknowledgement-training.md](../../product/decisions/m0-d12-policy-acknowledgement-training.md).
- **D12-15** — A leaver moves to removed in open campaigns and keeps their history. Source: [m0-d12-policy-acknowledgement-training.md](../../product/decisions/m0-d12-policy-acknowledgement-training.md).
- **D12-14** — A mover is added by amendment to active role_targeted campaigns of the new team (mover_in) and removed from campaigns of the old team (mover_out), while earlier completions keep their history. Source: [m0-d12-policy-acknowledgement-training.md](../../product/decisions/m0-d12-policy-acknowledgement-training.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [policy-distribution-campaign-review-2026-10-29.md](../../product/definitive-aggregate-choreography/policy-distribution-campaign-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/policy-distribution-campaign.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
