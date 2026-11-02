# Policy

- Classification: `business_aggregate`
- Source area: `Policies` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant / policies / PolicyId; supplied PolicyId must equal the unchanged tenant/program/normalized-identifier UUIDv5. Selected mutations use CommandId and exact policy/content/version/review bindings.

**Invariant:** A policy has one pending revision/retirement; immutable approved version intervals form one successor line, activation needs the latest accepted independent review and independent approval, and retired/approved content cannot be edited.

**Additional decision rule:** Each approved version carries exactly one audience kind (core_security or role_targeted); a role_targeted version names a non-empty list of roster teams.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ApprovePolicy](../commands/policy.md#approvepolicy) | http | CommandId |
| [ApprovePolicyRetirement](../commands/policy.md#approvepolicyretirement) | http | CommandId |
| [ConfirmPolicyReview](../commands/policy.md#confirmpolicyreview) | http | CommandId |
| [CreatePolicyDraft](../commands/policy.md#createpolicydraft) | http | PolicyId |
| [DiscardPolicyDraft](../commands/policy.md#discardpolicydraft) | http | CommandId |
| [ProposePolicyRetirement](../commands/policy.md#proposepolicyretirement) | http | CommandId |
| [ProposePolicySuccessor](../commands/policy.md#proposepolicysuccessor) | http | CommandId |
| [ReviewPolicyDraft](../commands/policy.md#reviewpolicydraft) | http | CommandId |
| [RevisePolicyDraft](../commands/policy.md#revisepolicydraft) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [PolicyApproved](../events/policy.md#policyapproved) | bdgrz.policy.approved | 2 |
| [PolicyDraftCreated](../events/policy.md#policydraftcreated) | bdgrz.policy.draft.created | 2 |
| [PolicyDraftDiscarded](../events/policy.md#policydraftdiscarded) | bdgrz.policy.draft.discarded | 2 |
| [PolicyDraftRevised](../events/policy.md#policydraftrevised) | bdgrz.policy.draft.revised | 2 |
| [PolicyPeriodicReviewConfirmed](../events/policy.md#policyperiodicreviewconfirmed) | bdgrz.policy.periodic_review.confirmed | 2 |
| [PolicyRetired](../events/policy.md#policyretired) | bdgrz.policy.retired | 2 |
| [PolicyRetirementProposed](../events/policy.md#policyretirementproposed) | bdgrz.policy.retirement.proposed | 2 |
| [PolicyReviewed](../events/policy.md#policyreviewed) | bdgrz.policy.reviewed | 2 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D12-02** — Each approved policy version carries exactly one audience kind (core_security or role_targeted); a role_targeted policy version names a non-empty list of roster teams. Source: [m0-d12-policy-acknowledgement-training.md](../../product/decisions/m0-d12-policy-acknowledgement-training.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [policy-review-2026-10-29.md](../../product/definitive-aggregate-choreography/policy-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/policy.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
