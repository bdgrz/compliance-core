# SystemInstanceAccessReviewScope

- Classification: `business_aggregate`
- Source area: `Applications` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant + SystemInstanceId in its existing access-review-scope address, with exact current instance version. Selected CommandId binds the decision and its exact scope/version/effective terms.

**Invariant:** Scope decisions for one instance advance a monotonic effective date and expected decision sequence with independent approver or exact-scope waiver.

**Additional decision rule:** A scope decision is included or excluded, an exclusion requires a reason, and it references the exact system instance version, supersedes the prior decision without editing it, and is approved by the Compliance Lead who is not the instance's access owner.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [DecideAccessReviewScope](../commands/system-instance-access-review-scope.md#decideaccessreviewscope) | http | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [AccessReviewScopeDecided](../events/system-instance-access-review-scope.md#accessreviewscopedecided) | bdgrz.system_instance.access_review_scope.decided | 2 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D05-07** — An access-review scope decision (included or excluded) must be approved by the Compliance Lead and the approver may not be the access owner of that system instance. Source: [m0-d05-application-inventory.md](../../product/decisions/m0-d05-application-inventory.md).
- **D05-08** — An access-review scope decision is effective-dated and references an exact system instance version; a new decision supersedes the previous one and never edits it. Source: [m0-d05-application-inventory.md](../../product/decisions/m0-d05-application-inventory.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/system-instance-access-review-scope.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
