# FindingMigrationAggregate

- Classification: `technical_process_or_storage_owner`
- Source area: `Remediation` (derived from source path; not a bounded-context ruling).
- Business aggregate: `false`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** [Placeholder: selected target identity is not recorded.]

**Invariant:** Technical process invariant: migration has one exact immutable source fence, advances one contiguous physical source offset after target import, and completes only with exact parity count/digest at the fence.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** [Placeholder: replay rule not recorded.]

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

[Placeholder: no selected commands are listed for this owner.]

## Events

[Placeholder: no canonical events are listed for this owner.]

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/finding-migration-aggregate.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `[Placeholder: implementation status not recorded]`.
