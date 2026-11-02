# ProgramRollover

- Classification: `business_aggregate`
- Source area: `Programs` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant / program-rollovers / RolloverId, with exact source program/version and successor proof. RolloverId identifies creation; selected mutations use CommandId.

**Invariant:** One exact carry-forward proposal version, including the exact successor examination, plan and period identities, requires independent approval and an atomic execution claim before successor writes; the claim pins that version against revision. The recoverable private process coordinates existing source owners and stores exact completion proof.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ApproveProgramRollover](../commands/program-rollover.md#approveprogramrollover) | http | CommandId |
| [ProposeProgramRollover](../commands/program-rollover.md#proposeprogramrollover) | http | RolloverId |
| [ClaimProgramRolloverExecution](../commands/program-rollover.md#claimprogramrolloverexecution) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ProgramNextPeriodCreated](../events/program-rollover.md#programnextperiodcreated) | bdgrz.programs.program-next-period-created | 1 |
| [ProgramRolloverApproved](../events/program-rollover.md#programrolloverapproved) | bdgrz.programs.program-rollover-approved | 2 |
| [ProgramRolloverDecisionRecorded](../events/program-rollover.md#programrolloverdecisionrecorded) | bdgrz.programs.program-rollover-decision-recorded | 1 |
| [ProgramRolloverExecutionClaimed](../events/program-rollover.md#programrolloverexecutionclaimed) | bdgrz.programs.program-rollover-execution-claimed | 1 |
| [ProgramRolloverProposed](../events/program-rollover.md#programrolloverproposed) | bdgrz.programs.program-rollover-proposed | 2 |
| [ProgramRolloverRevised](../events/program-rollover.md#programrolloverrevised) | bdgrz.programs.program-rollover-revised | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [R000](../reactors/one-new-committed-execution-claim-starter.md#r000) | reaction_or_regular_process_stage | ProgramRollover | ProgramRolloverExecutionProcess | StartProgramRolloverExecutionProcess |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [program-rollover-review-2026-10-29.md](../../product/definitive-aggregate-choreography/program-rollover-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/program-rollover.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
