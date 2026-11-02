# WeeklyWorkDigest

- Classification: `technical_process_or_storage_owner`
- Source area: `Work` (derived from source path; not a bounded-context ruling).
- Business aggregate: `false`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** [Placeholder: selected target identity is not recorded.]

**Invariant:** Technical delivery process invariant: exactly one schedule per member-week; only next deterministic attempt can run and callback must match its exact request; terminal outcomes/suppression stop further delivery.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** [Placeholder: replay rule not recorded.]

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

[Placeholder: no selected commands are listed for this owner.]

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [WeeklyWorkDigestDeliveryRecorded](../events/weekly-work-digest.md#weeklyworkdigestdeliveryrecorded) | bdgrz.work.weekly-digest.delivery-recorded | 2 |
| [WeeklyWorkDigestDeliveryRequested](../events/weekly-work-digest.md#weeklyworkdigestdeliveryrequested) | bdgrz.work.weekly-digest.delivery-requested | 2 |
| [WeeklyWorkDigestDeliverySuppressed](../events/weekly-work-digest.md#weeklyworkdigestdeliverysuppressed) | bdgrz.work.weekly-digest.delivery-suppressed | 2 |
| [WeeklyWorkDigestScheduled](../events/weekly-work-digest.md#weeklyworkdigestscheduled) | bdgrz.work.weekly-digest.scheduled | 1 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/weekly-work-digest.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `[Placeholder: implementation status not recorded]`.
