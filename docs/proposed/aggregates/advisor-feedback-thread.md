# AdvisorFeedbackThread

- Classification: `business_aggregate`
- Source area: `AdvisorCollaboration` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped (handoffId, feedbackId); the thread is opened by AdvisorFeedbackAdmitted on the handoff; `CorrectionId` identifies each correction operation; mutations bind CommandId to this exact identity.

**Invariant:** Immutable contribution with its correction/response lifecycle.

**Additional decision rule:** Feedback is of kind comment, change_request or validated and records author, time, target record version and kind at admission; a posted entry is never edited, a correction is a new entry linking back to the original, and revoking access never rewrites past attribution. Validated feedback is attributed to an exact record version, never changes that record's approval state, and never satisfies a separation-of-duties, approval or independent-review requirement.

**Must preserve:** Feedback records exact selected source version and admitted generation; later selection makes it historical.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [CorrectAdvisorFeedback](../commands/advisor-feedback-thread.md#correctadvisorfeedback) | http | CorrectionId |
| [ResolveAdvisorFeedback](../commands/advisor-feedback-thread.md#resolveadvisorfeedback) | http | CommandId |
| [RespondToAdvisorFeedback](../commands/advisor-feedback-thread.md#respondtoadvisorfeedback) | http | CommandId |
| [MaterializeAdvisorFeedback](../commands/advisor-feedback-thread.md#materializeadvisorfeedback) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [AdvisorFeedbackCorrected](../events/advisor-feedback-thread.md#advisorfeedbackcorrected) | bdgrz.advisor-feedback.corrected | 2 |
| [AdvisorFeedbackRecorded](../events/advisor-feedback-thread.md#advisorfeedbackrecorded) | bdgrz.advisor-feedback.recorded | 2 |
| [AdvisorFeedbackResolved](../events/advisor-feedback-thread.md#advisorfeedbackresolved) | bdgrz.advisor-feedback.resolved | 1 |
| [AdvisorFeedbackResponded](../events/advisor-feedback-thread.md#advisorfeedbackresponded) | bdgrz.advisor-feedback.responded | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T013](../reactors/advisor-feedback-materialization-reactor.md#t013) | reaction_or_regular_process_stage | AdvisorHandoff | AdvisorFeedbackThread | MaterializeAdvisorFeedback |

## Product rules

- **D14-08** — AdvisorFeedback is of kind comment, change_request, or validated and records author, time, target record version, and kind. Source: [m0-d14-advisor-collaboration.md](../../product/decisions/m0-d14-advisor-collaboration.md).
- **D14-09** — A validated feedback is an attributed review of an exact record version; it never changes the record's approval state and never satisfies a separation-of-duties or approval requirement. Source: [m0-d14-advisor-collaboration.md](../../product/decisions/m0-d14-advisor-collaboration.md).
- **D14-11** — Advisor feedback is immutable once posted; a correction is a new entry linking back, and revocation removes future access but never past attribution. Source: [m0-d14-advisor-collaboration.md](../../product/decisions/m0-d14-advisor-collaboration.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [advisor-feedback-thread-review-2026-10-29.md](../../product/definitive-aggregate-choreography/advisor-feedback-thread-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/advisor-feedback-thread.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
