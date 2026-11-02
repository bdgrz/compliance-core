# ControlEvaluationPlan events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/control-evaluation-plan.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| ControlEvaluationPlanVersionDefined | business | bdgrz.control.evaluation.plan.version-defined | 2 | [Placeholder: schema absent] | One complete immutable plan version accepted on its control's plan line. |

## Event details

### ControlEvaluationPlanVersionDefined

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ControlEvaluationPlanVersionDefined |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlEvaluationPlanVersionDefined |
| Adopted wire | `{"discriminator":"bdgrz.control.evaluation.plan.version-defined","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Evaluations/Events/ControlEvaluationPlanVersionDefined.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | One complete immutable plan version accepted on its control's plan line. |
| Meaning source | record summary in src/Compliance.Common/Features/Evaluations/Events/ControlEvaluationPlanVersionDefined.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
