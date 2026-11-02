# ProgramRolloverExecutionProcess

## Selected rollover contract

`ProgramRollover` retains the exact proposal and independent approval, then atomically claims the approved version before any successor mutation. The claim prevents revise/reject races and pins the actual approval tuple. `ProgramRolloverExecutionProcess` waits for a person to author a complete successor `TypeIIPlan` under normal source-validation rules, freezes that proposal and an exhaustive mapping of each rollover record and commitment to a plan destination or justified nonrepresentation, then waits for independent review and executive approval; `changes_requested` returns to human revision and a new complete mapping, while `reject` terminates as failed. It submits the exact `StartTypeIIObservationPeriod` command. Its 202 opening acceptance is pending. The process is complete only after it verifies the actual `ObservationPeriodStarted` chain against the immutable opening intent, exact plan proposal/review/approval facts and content hash, period/range, calendar authorization, operation root and result. Process state stores verified proof references and terminal outcome; the business aggregate does not emit `ProgramNextPeriodCreated`. Rejection/failure never implies the period started.

- Root source events: ProgramRolloverExecutionClaimed
- Ordered stages: true

| Stage | Receivers | Rule |
| --- | --- | --- |
| await_type_i_entry_approval | None listed | Require actual current TypeIEntryDecisionRecorded for this tenant/program with outcome approve or approve_with_exceptions before creating the successor Type I examination. This decision remains owned by ReadinessAssessment. |
| await_successor_examination | None listed | A personal authorized actor creates exact NextExaminationId through CreateExaminationEngagement under ExaminationEngagement rules. Verify actual ExaminationEngagementOpened, exact tenant/program/examination identity and approved next-period dates; examination owner retains its own lifecycle. |
| await_frozen_type_i_sources | None listed | Wait for actual TypeIBaselineFrozen on exact NextExaminationId; capture exact baseline ID/hash and complete source manifest. Require actual AuditOutcomeRecorded with status recorded, scope type_i, source kind auditor, same examination and exact baseline ID/hash. Require actual SystemDescriptionApproved and SystemDescriptionBound to same examination, type_i_baseline purpose, exact baseline ID and a system-description version present in the frozen baseline. These facts remain owned by their existing aggregates. |
| await_successor_plan_proposal | None listed | A personal authorized actor authors complete TypeIIPlanContent under TypeIIPlan source validation, with exact NextExaminationId, exact Type I baseline/outcome/system-description IDs/hashes, NextTypeIIPlanId and approved period dates. Freeze actual proposal version/content hash; do not invent missing sources, controls, findings or required changes. |
| validate_exhaustive_carry_forward_mapping | None listed | Every pinned rollover record and commitment has exactly one explicit plan destination or justified nonrepresentation consistent with approved disposition; preserve exact source/version/hash; reject omissions, duplicates, mismatches and unsupported conversions; freeze complete mapping manifest/hash privately. |
| await_plan_review | None listed | Wait for actual TypeIIPlanReviewed on exact current frozen version/hash. Reviewer differs from proposer. approve advances; changes_requested loops through human revision, target-owner source validation and a newly frozen exhaustive mapping. reject terminates as failed. |
| await_independent_executive_approval | None listed | Wait for actual TypeIIPlanApproved on same exact current version/hash whose review outcome is approve; approver differs from proposer and reviewer and satisfies owner rules. Approved plan is immutable. |
| accept_period_opening | StartTypeIIObservationPeriod | Only after actual approved plan proof, submit exact NextPeriodId, NextTypeIIPlanId, NextExaminationId and dates from the claimed rollover. Verify TypeIIPeriodOpeningAccepted and original 202; accepted is pending. |
| complete | None listed | Complete only on actual ObservationPeriodStarted chain linked to immutable opening intent, operation root/result, exact rollover claim and IDs, actual examination open fact, Type I entry decision, frozen baseline/hash/source manifest, auditor outcome/hash, approved+bound description fact/version, final plan proposal/revision/review/approval facts and hashes, exhaustive mapping manifest/hash, period/range and calendar authorization ID/generation. Store verified proof references privately; never emit ProgramNextPeriodCreated. |

Abort commands: RejectTypeIIPeriodOpening

Complete selected process definition: [JSON](../data/processes/program-rollover-execution-process.json).
R000 choreography row: [JSON](../data/processes/program-rollover-execution-row.json).

[Placeholder: timeout, scheduling, and operator recovery details not present in the selected process definition.]
