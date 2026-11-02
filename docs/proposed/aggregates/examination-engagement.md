# ExaminationEngagement

- Classification: `business_aggregate`
- Source area: `Examinations` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant / examinations / ExaminationId, with immutable ProgramId and exact baseline/amendment identities. ExaminationId identifies creation; selected mutations use CommandId and exact source-version bindings.

**Invariant:** Freeze locks Type I setup to exact approved boundary and complete source coverage; amendments preserve prior baseline and enumerate changed sources.

**Additional decision rule:** An auditor-confirmed engagement date is a distinct attributed value requiring an evidence reference and an engagement-owned decision or auditor communication, never a status flag on a target date. A missing audit firm, date, or service is recorded as unknown, never as a placeholder value.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AmendTypeIBaseline](../commands/examination-engagement.md#amendtypeibaseline) | http | CommandId |
| [CreateExaminationEngagement](../commands/examination-engagement.md#createexaminationengagement) | creation | ExaminationId |
| [FreezeTypeIBaseline](../commands/examination-engagement.md#freezetypeibaseline) | http | CommandId |
| [ReviseExaminationEngagement](../commands/examination-engagement.md#reviseexaminationengagement) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ExaminationEngagementOpened](../events/examination-engagement.md#examinationengagementopened) | bdgrz.examinations.examination-engagement-opened | 2 |
| [ExaminationEngagementRevised](../events/examination-engagement.md#examinationengagementrevised) | bdgrz.examinations.examination-engagement-revised | 2 |
| [TypeIBaselineAmended](../events/examination-engagement.md#typeibaselineamended) | bdgrz.examinations.type-i-baseline-amended | 2 |
| [TypeIBaselineFrozen](../events/examination-engagement.md#typeibaselinefrozen) | bdgrz.examinations.type-i-baseline-frozen | 2 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D01-03** — An auditor-confirmed engagement date is a distinct attributed value that requires an evidence reference and an attributable engagement-owned decision or auditor communication; it is never represented as a status flag on a target date. Source: [m0-d01-program-targets.md](../../product/decisions/m0-d01-program-targets.md).
- **D01-11** — A missing audit firm, date, or service is recorded as unknown, never as a placeholder value. Source: [m0-d01-program-targets.md](../../product/decisions/m0-d01-program-targets.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [examination-engagement-review-2026-10-29.md](../../product/definitive-aggregate-choreography/examination-engagement-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/examination-engagement.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
