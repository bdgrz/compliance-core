# ControlEvaluationPlan

- Classification: `business_aggregate`
- Source area: `Evaluations` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant / control-evaluation-plans / ControlId, with immutable ProgramId. PlanVersionId identifies the selected immutable definition operation and its complete procedure/author/source intent.

**Invariant:** A plan version has a stable identity and immutable procedure; only a successor to the current plan version can be appended and exact duplicate identity must have the same content.

**Additional decision rule:** A plan version names the exact approved control version it evaluates, and each procedure step pins its exact inspected record version or artifact content reference. A version is frozen once an evaluation starts and a change appends a successor version without rewriting results already recorded.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [DefineControlEvaluationPlan](../commands/control-evaluation-plan.md#definecontrolevaluationplan) | http | PlanVersionId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ControlEvaluationPlanVersionDefined](../events/control-evaluation-plan.md#controlevaluationplanversiondefined) | bdgrz.control.evaluation.plan.version-defined | 2 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D13-01** — A ControlEvaluationPlan version names the exact approved control version it evaluates, and each procedure step keeps its exact inspected record version or artifact content reference so starting an evaluation never resolves to newer versions. Source: [m0-d13-control-evaluation.md](../../product/decisions/m0-d13-control-evaluation.md).
- **D13-03** — A control evaluation plan version is frozen when evaluation starts; a changed plan is a new version and never rewrites results already recorded. Source: [m0-d13-control-evaluation.md](../../product/decisions/m0-d13-control-evaluation.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [control-evaluation-plan-review-2026-10-29.md](../../product/definitive-aggregate-choreography/control-evaluation-plan-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/control-evaluation-plan.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
