# CorrectiveAction

- Classification: `business_aggregate`
- Source area: `Remediation` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped (findingId, actionId); the identity is established by the admitting owner or reactor named in the choreography, never by a caller request key; mutations bind CommandId to this exact identity.

**Invariant:** Independent corrective-action lifecycle, evidence and completion proof.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** Close only with at least one action, all complete, evidence/rationale present and independent closer unless exact waiver. Closed finding is immutable until reopen.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [CompleteCorrectiveAction](../commands/corrective-action.md#completecorrectiveaction) | http | CommandId |
| [MaterializeCorrectiveAction](../commands/corrective-action.md#materializecorrectiveaction) | receiver | CommandId |
| [AuthorizeCorrectiveActionFindingClosureUse](../commands/corrective-action.md#authorizecorrectiveactionfindingclosureuse) | receiver | CommandId |
| [SettleCorrectiveActionFindingClosureUse](../commands/corrective-action.md#settlecorrectiveactionfindingclosureuse) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [CorrectiveActionAddedV2](../events/corrective-action.md#correctiveactionaddedv2) | bdgrz.finding.corrective_action.added | 3 |
| [CorrectiveActionCompletedV2](../events/corrective-action.md#correctiveactioncompletedv2) | bdgrz.finding.corrective_action.completed | 3 |
| [CorrectiveActionFindingClosureUseAuthorized](../events/corrective-action.md#correctiveactionfindingclosureuseauthorized) | bdgrz.corrective-action.corrective-action-finding-closure-use-authorized | 1 |
| [CorrectiveActionFindingClosureUseSettled](../events/corrective-action.md#correctiveactionfindingclosureusesettled) | bdgrz.corrective-action.corrective-action-finding-closure-use-settled | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T036](../reactors/corrective-action-materialization-reactor.md#t036) | reaction_or_regular_process_stage | FindingAggregate | CorrectiveAction | MaterializeCorrectiveAction |
| [T063](../reactors/finding-closure-reactor.md#t063) | reaction_or_regular_process_stage | FindingAggregate | CorrectiveAction | AuthorizeCorrectiveActionFindingClosureUse |
| [T064](../reactors/finding-closure-reactor.md#t064) | reaction_or_regular_process_stage | FindingAggregate | CorrectiveAction | SettleCorrectiveActionFindingClosureUse |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [corrective-action-review-2026-10-29.md](../../product/definitive-aggregate-choreography/corrective-action-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/corrective-action.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
