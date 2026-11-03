# RiskMethod events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/risk-method.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| RiskMethodVersionPublished | RiskMethodVersionPublished | business | bdgrz.risk.method.version_published | 2 | [Placeholder: schema absent] | One immutable method publication, with its original operation and result. |

## Event details

### RiskMethodVersionPublished

| Canonical event detail | Value |
| --- | --- |
| Target event name | RiskMethodVersionPublished |
| Selected source event name | RiskMethodVersionPublished |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:RiskMethodVersionPublished |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RiskMethodVersionPublished |
| Adopted wire | `{"discriminator":"bdgrz.risk.method.version_published","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Risks/Events/RiskMethodVersionPublished.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | One immutable method publication, with its original operation and result. |
| Selected source meaning | One immutable method publication, with its original operation and result. |
| Meaning source | record summary in src/Compliance.Common/Features/Risks/Events/RiskMethodVersionPublished.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S036](../reactors/risk-method-reassessment-reactor.md#s036) | physical root, logical prerequisite | RiskMethod | RaiseRiskReassessmentTriggers | RiskGovernance |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
