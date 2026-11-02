# IndependenceRuleRatificationCatalog

- Classification: `business_governor`
- Source area: `AccessControl` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Existing fixed CatalogId in its existing platform scope; selected CommandId binds exact ratification identity, rule version and expected catalog sequence.

**Invariant:** Only valid professional proof can ratify an exact immutable rule digest, and active ratified rule version advances monotonically.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RatifyIndependenceRuleVersion](../commands/independence-rule-ratification-catalog.md#ratifyindependenceruleversion) | http | CommandId |
| [AuthorizeIndependenceRuleRatificationCatalogEngagementUse](../commands/independence-rule-ratification-catalog.md#authorizeindependenceruleratificationcatalogengagementuse) | receiver | CommandId |
| [SettleIndependenceRuleRatificationCatalogEngagementUse](../commands/independence-rule-ratification-catalog.md#settleindependenceruleratificationcatalogengagementuse) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [IndependenceRuleRatificationCatalogEngagementUseAuthorized](../events/independence-rule-ratification-catalog.md#independenceruleratificationcatalogengagementuseauthorized) | bdgrz.independence-rule-ratification-catalog.independence-rule-ratification-catalog-engagement-use-authorized | 1 |
| [IndependenceRuleRatificationCatalogEngagementUseSettled](../events/independence-rule-ratification-catalog.md#independenceruleratificationcatalogengagementusesettled) | bdgrz.independence-rule-ratification-catalog.independence-rule-ratification-catalog-engagement-use-settled | 1 |
| [IndependenceRuleRatificationRecorded](../events/independence-rule-ratification-catalog.md#independenceruleratificationrecorded) | bdgrz.independence.rules.ratification-recorded | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T095](../reactors/engagement-acceptance-reactor.md#t095) | reaction_or_regular_process_stage | ServiceEngagementIndependence | IndependenceRuleRatificationCatalog | AuthorizeIndependenceRuleRatificationCatalogEngagementUse |
| [T096](../reactors/engagement-acceptance-reactor.md#t096) | reaction_or_regular_process_stage | ServiceEngagementIndependence | IndependenceRuleRatificationCatalog | SettleIndependenceRuleRatificationCatalogEngagementUse |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [independence-rule-ratification-catalog-review-2026-10-29.md](../../product/definitive-aggregate-choreography/independence-rule-ratification-catalog-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/independence-rule-ratification-catalog.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
