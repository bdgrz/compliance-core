# IndependenceRuleCatalog

- Classification: `business_governor`
- Source area: `AccessControl` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Existing fixed CatalogId in its existing platform scope; selected CommandId binds exact rule version and expected catalog sequence.

**Invariant:** Authored independence rules advance consecutively and cannot falsely claim professional ratification. A rule change publishes a new consecutive rule-set version; evaluations keep the rule-set version they were made under and are never rewritten (M0-D26 D26-05; Jev 0.97)

**Additional decision rule:** Each rule change publishes a new consecutive rule-set version and past evaluations keep the version they were made under, never rewritten.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ReviseIndependenceRules](../commands/independence-rule-catalog.md#reviseindependencerules) | command | CommandId |
| [AuthorizeIndependenceRuleCatalogEngagement](../commands/independence-rule-catalog.md#authorizeindependencerulecatalogengagement) | receiver | CommandId |
| [CloseIndependenceRuleCatalogEngagementAuthorization](../commands/independence-rule-catalog.md#closeindependencerulecatalogengagementauthorization) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [IndependenceRuleCatalogEngagementAuthorized](../events/independence-rule-catalog.md#independencerulecatalogengagementauthorized) | bdgrz.independence-rule-catalog.independence-rule-catalog-engagement-use-authorized | 1 |
| [IndependenceRuleCatalogEngagementAuthorizationClosed](../events/independence-rule-catalog.md#independencerulecatalogengagementauthorizationclosed) | bdgrz.independence-rule-catalog.independence-rule-catalog-engagement-use-settled | 1 |
| [IndependenceRulesRevised](../events/independence-rule-catalog.md#independencerulesrevised) | bdgrz.independence.rules.revised | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T093](../reactors/engagement-acceptance-reactor.md#t093) | reaction_or_regular_process_stage | ServiceEngagementIndependence | IndependenceRuleCatalog | AuthorizeIndependenceRuleCatalogEngagement |
| [T094](../reactors/engagement-acceptance-reactor.md#t094) | reaction_or_regular_process_stage | ServiceEngagementIndependence | IndependenceRuleCatalog | CloseIndependenceRuleCatalogEngagementAuthorization |

## Product rules

- **D26-05** — The IndependenceRuleSet is versioned; rule changes create new versions and never rewrite past evaluations. Source: [m0-d26-independence.md](../../product/decisions/m0-d26-independence.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [independence-rule-catalog-review-2026-10-29.md](../../product/definitive-aggregate-choreography/independence-rule-catalog-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/independence-rule-catalog.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
