# RiskEvaluation events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/risk-evaluation.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| RiskAccepted | business | bdgrz.risk.accepted | 2 | [Placeholder: schema absent] | A named member accepted a risk as it currently stands without further treatment. |
| RiskAssessmentRecorded | business | bdgrz.risk.assessment.recorded | 2 | [Placeholder: schema absent] | An assessment of a risk's likelihood and impact was recorded by a named member as the risk's current assessment. |
| RiskTreatmentChosen | business | bdgrz.risk.treatment.chosen | 2 | [Placeholder: schema absent] | A treatment approach was chosen for a risk by a named member, recorded against the risk's current assessment. |

## Event details

### RiskAccepted

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:RiskAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RiskAccepted |
| Adopted wire | `{"discriminator":"bdgrz.risk.accepted","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Risks/Events/RiskAccepted.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A named member accepted a risk as it currently stands without further treatment. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Risks/Events/RiskAccepted.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [X009](../reactors/risk-evaluation-reactor.md#x009) | physical root, logical prerequisite | RiskEvaluation | RecordRiskReassessmentObligation | RiskGovernance |

### RiskAssessmentRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:RiskAssessmentRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RiskAssessmentRecorded |
| Adopted wire | `{"discriminator":"bdgrz.risk.assessment.recorded","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Risks/Events/RiskAssessmentRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | An assessment of a risk's likelihood and impact was recorded by a named member as the risk's current assessment. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Risks/Events/RiskAssessmentRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [X009](../reactors/risk-evaluation-reactor.md#x009) | physical root, logical prerequisite | RiskEvaluation | RecordRiskReassessmentObligation | RiskGovernance |

### RiskTreatmentChosen

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:RiskTreatmentChosen |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RiskTreatmentChosen |
| Adopted wire | `{"discriminator":"bdgrz.risk.treatment.chosen","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Risks/Events/RiskTreatmentChosen.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A treatment approach was chosen for a risk by a named member, recorded against the risk's current assessment. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Risks/Events/RiskTreatmentChosen.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
