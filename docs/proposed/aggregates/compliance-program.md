# ComplianceProgram

- Classification: `business_aggregate`
- Source area: `Programs` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant + caller ProgramId; creation uses that identity and later mutations use caller CommandId.

**Invariant:** Program target dates must form a coherent chronological plan; one program identity has immutable creation intent. Criteria selection separately chooses current edition.

**Additional decision rule:** Target dates are optional and each revision carries author attribution; the plan requires readiness <= Type I as-of <= Type II start <= Type II end when present. Revising the plan appends rather than overwrites earlier target dates and authors, a target date is never promoted to auditor-confirmed by time, display or imported note, and R1, R2 and T1 milestone dates stay undated until an agreed plan exists. A program holds one exact criteria edition id, existing programs keep it, and moving to a newer edition is an explicit attributed SelectProgramCriteriaEdition at the expected revision.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [CreateProgram](../commands/compliance-program.md#createprogram) | http | ProgramId |
| [ReviseProgram](../commands/compliance-program.md#reviseprogram) | command | CommandId |
| [SelectProgramCriteriaEdition](../commands/compliance-program.md#selectprogramcriteriaedition) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ProgramCreated](../events/compliance-program.md#programcreated) | bdgrz.program.created | 2 |
| [ProgramCriteriaEditionSelected](../events/compliance-program.md#programcriteriaeditionselected) | bdgrz.program.criteria.selected | 2 |
| [ProgramRevised](../events/compliance-program.md#programrevised) | bdgrz.program.revised | 2 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D01-02** — Program target dates are optional (nullable) values that carry author and revision attribution; a target date is never promoted to an auditor-confirmed date by passage of time, status display, or an imported note. Source: [m0-d01-program-targets.md](../../product/decisions/m0-d01-program-targets.md).
- **D01-04** — Revising a program plan preserves earlier target dates and their author attribution rather than overwriting them. Source: [m0-d01-program-targets.md](../../product/decisions/m0-d01-program-targets.md).
- **D02-02** — A program references a criteria catalog edition by exact edition identifier, and existing programs and engagements keep the edition they selected; moving to a newer edition is an explicit, attributed remapping. Source: [m0-d02-criteria-content.md](../../product/decisions/m0-d02-criteria-content.md).
- **D01-01** — A program keeps one identity through its readiness, Type I, and Type II stages; the stages are planned and do not assert that an examination or operating period occurred, and an engagement and its outcome are separate records from the program. Source: [m0-d01-program-targets.md](../../product/decisions/m0-d01-program-targets.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/compliance-program.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
