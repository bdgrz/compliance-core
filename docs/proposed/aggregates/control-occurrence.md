# ControlOccurrence

- Classification: `business_aggregate`
- Source area: `Operations` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** (tenantId, programId, controlId, businessWindow)

**Invariant:** One planned/attested/corrected/reviewed occurrence for a control performance window.

**Additional decision rule:** Refuses to record a management compliance review sign-off unless the occurrence holds a frozen evidence set binding the exact readiness assessment version, the monitoring measure versions, and the agenda or minutes artifact; later data changes never alter that set. The sign-off is one immutable attributed decision with outcome approved, action_requested, or deferred and a rationale; policy, risk, vendor, and management reviews are ordinary recurring-control occurrences with no dedicated review aggregate.

**Must preserve:** Occurrence facts bind exact frozen plan and evidence requirements; one business window has one identity across all generators.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AttestControlOccurrence](../commands/control-occurrence.md#attestcontroloccurrence) | http | CommandId |
| [CorrectControlAttestation](../commands/control-occurrence.md#correctcontrolattestation) | http | CommandId |
| [OpenControlOccurrence](../commands/control-occurrence.md#opencontroloccurrence) | http | CommandId |
| [ReviewControlOccurrence](../commands/control-occurrence.md#reviewcontroloccurrence) | http | CommandId |
| [OpenDueControlOccurrence](../commands/control-occurrence.md#openduecontroloccurrence) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ControlOccurrenceAttested](../events/control-occurrence.md#controloccurrenceattested) | bdgrz.control.occurrence.attested | 2 |
| [ControlOccurrenceOpened](../events/control-occurrence.md#controloccurrenceopened) | bdgrz.control.occurrence.opened | 2 |
| [ControlOccurrenceReviewed](../events/control-occurrence.md#controloccurrencereviewed) | bdgrz.control.occurrence.reviewed | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T040](../reactors/open-due-control-work-reactor.md#t040) | reaction_or_regular_process_stage | TypeIIPeriod | ControlOccurrence | OpenDueControlOccurrence |
| [S002](../reactors/control-occurrence-finding-reactor.md#s002) | business_receiver | ControlOccurrence | FindingAggregate | RaiseOccurrenceFinding |

## Product rules

- **D19-02** — A management compliance review's ControlOccurrence has a frozen evidence set binding the exact readiness assessment version, the monitoring measure versions, and the agenda or minutes artifact; later data changes never alter the frozen set; it replaces a separate ManagementReviewSnapshot aggregate. Decision: [m0-d19-periodic-reviews.md](../decisions/m0-d19-periodic-reviews.md).
- **D19-03** — Management's sign-off on the review occurrence is an immutable decision with outcome approved, action_requested, or deferred and a rationale. Decision: [m0-d19-periodic-reviews.md](../decisions/m0-d19-periodic-reviews.md).
- **D19-05** — Annual policy review is one occurrence per policy with evidence of the approved new or unchanged policy version; annual vendor review is one occurrence per material provider with evidence of the assurance-report review. Decision: [m0-d19-periodic-reviews.md](../decisions/m0-d19-periodic-reviews.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/control-occurrence.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
