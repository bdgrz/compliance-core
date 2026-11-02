# WorkDigestDispatch

- Classification: `technical_process_or_storage_owner`
- Source area: `Work` (derived from source path; not a bounded-context ruling).
- Business aggregate: `false`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** [Placeholder: selected target identity is not recorded.]

**Invariant:** Technical dispatch invariant: one reservation/message per member-week, one in-flight send, retries only after definite transient rejection, and ambiguous external outcome freezes until authorized evidence-backed retry.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** [Placeholder: replay rule not recorded.]

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

[Placeholder: no selected commands are listed for this owner.]

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [WorkDigestDispatchAttemptStarted](../events/work-digest-dispatch.md#workdigestdispatchattemptstarted) | bdgrz.work.digest_dispatch.attempt_started | 1 |
| [WorkDigestDispatchOutcomeRecorded](../events/work-digest-dispatch.md#workdigestdispatchoutcomerecorded) | bdgrz.work.digest_dispatch.outcome_recorded | 1 |
| [WorkDigestDispatchScheduled](../events/work-digest-dispatch.md#workdigestdispatchscheduled) | bdgrz.work.digest_dispatch.scheduled | 1 |
| [WorkDigestUnknownRetryAuthorized](../events/work-digest-dispatch.md#workdigestunknownretryauthorized) | bdgrz.work.digest_dispatch.unknown_retry_authorized | 2 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/work-digest-dispatch.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `[Placeholder: implementation status not recorded]`.
